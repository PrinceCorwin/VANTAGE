using System.Reflection;
using VANTAGE.Models;
using VANTAGE.Data;

namespace VANTAGE.Utilities;

// Tracks a single field change on one Activity record
public class CellChange
{
    public string UniqueID { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
    // Derived fields that were recalculated (PercentEntry→EarnQtyEntry, EarnMHsCalc, etc.)
    public Dictionary<string, object?>? DerivedOldValues { get; set; }
}

// One user operation (single edit, multi-row paste, cell clear, etc.)
public class EditAction
{
    public string Description { get; set; } = string.Empty;
    public List<CellChange> Changes { get; set; } = new();
}

// Multi-level undo/redo for Progress module edits
public class UndoManager
{
    private readonly Stack<EditAction> _undoStack = new();
    private readonly Stack<EditAction> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int UndoCount => _undoStack.Count;

    // Record an action that was just performed
    public void RecordAction(EditAction action)
    {
        if (action.Changes.Count == 0) return;

        _undoStack.Push(action);
        _redoStack.Clear(); // New edit invalidates redo history
    }

    // Undo the last action - restores old values on Activity objects and saves to DB
    public async Task<(bool success, string description)> UndoAsync(
        Func<string, Activity?> findActivity)
    {
        if (!CanUndo)
            return (false, string.Empty);

        var action = _undoStack.Pop();

        try
        {
            await ApplyChanges(action, isUndo: true, findActivity);
            _redoStack.Push(action);
            return (true, action.Description);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "UndoManager.UndoAsync");
            // Push it back since we failed
            _undoStack.Push(action);
            return (false, $"Undo failed: {ex.Message}");
        }
    }

    // Redo the last undone action
    public async Task<(bool success, string description)> RedoAsync(
        Func<string, Activity?> findActivity)
    {
        if (!CanRedo)
            return (false, string.Empty);

        var action = _redoStack.Pop();

        try
        {
            await ApplyChanges(action, isUndo: false, findActivity);
            _undoStack.Push(action);
            return (true, action.Description);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "UndoManager.RedoAsync");
            _redoStack.Push(action);
            return (false, $"Redo failed: {ex.Message}");
        }
    }

    // Apply changes to Activity objects and save to database
    private async Task ApplyChanges(EditAction action, bool isUndo,
        Func<string, Activity?> findActivity)
    {
        var modifiedActivities = new List<Activity>();

        foreach (var change in action.Changes)
        {
            var activity = findActivity(change.UniqueID);
            if (activity == null) continue;

            var property = typeof(Activity).GetProperty(change.ColumnName);
            if (property == null) continue;

            // Set the value and let the Activity property setter handle recalculation
            object? valueToApply = isUndo ? change.OldValue : change.NewValue;
            property.SetValue(activity, valueToApply);

            // Restore ActStart/ActFin explicitly since they aren't recalculated by setters
            // (PercentEntry validation in CurrentCellEndEdit clears them based on % value)
            if (isUndo && change.DerivedOldValues != null)
            {
                if (change.DerivedOldValues.TryGetValue(nameof(Activity.ActStart), out var oldStart))
                    activity.ActStart = oldStart as DateTime?;
                if (change.DerivedOldValues.TryGetValue(nameof(Activity.ActFin), out var oldFin))
                    activity.ActFin = oldFin as DateTime?;
            }

            // Mark dirty for sync
            activity.UpdatedBy = App.CurrentUser?.Username ?? "Unknown";
            activity.UpdatedUtcDate = DateTime.UtcNow;
            activity.LocalDirty = 1;

            if (!modifiedActivities.Contains(activity))
                modifiedActivities.Add(activity);
        }

        // Save all modified activities to database
        foreach (var activity in modifiedActivities)
        {
            await ActivityRepository.UpdateActivityInDatabase(activity);
        }
    }

    // Clear all history (called on sync, data reload, module switch)
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    // Helper: capture derived field values before an edit to a progress-related column
    public static Dictionary<string, object?>? CaptureDerivedFields(Activity activity, string columnName)
    {
        // Only capture derived fields for columns that trigger recalculation
        if (columnName != nameof(Activity.PercentEntry) &&
            columnName != nameof(Activity.EarnQtyEntry) &&
            columnName != nameof(Activity.Quantity) &&
            columnName != nameof(Activity.BudgetMHs))
        {
            return null;
        }

        return new Dictionary<string, object?>
        {
            [nameof(Activity.PercentEntry)] = activity.PercentEntry,
            [nameof(Activity.EarnQtyEntry)] = activity.EarnQtyEntry,
            [nameof(Activity.EarnMHsCalc)] = activity.EarnMHsCalc,
            [nameof(Activity.EarnedQtyCalc)] = activity.EarnedQtyCalc,
            [nameof(Activity.ActStart)] = activity.ActStart,
            [nameof(Activity.ActFin)] = activity.ActFin,
        };
    }
}
