# Manage My Snapshots Dialog - Implementation Plan

**Status:** IMPLEMENTED - January 9, 2026

## Overview

Rename and enhance the existing `DeleteSnapshotsDialog` to `ManageSnapshotsDialog`, adding the ability to revert activity records to a previous snapshot state.

---

## File Operations

### Files to Rename

| Original Path | New Path |
|---------------|----------|
| `Dialogs/DeleteSnapshotsDialog.xaml` | `Dialogs/ManageSnapshotsDialog.xaml` |
| `Dialogs/DeleteSnapshotsDialog.xaml.cs` | `Dialogs/ManageSnapshotsDialog.xaml.cs` |

### Files to Modify

| File | Changes Required |
|------|------------------|
| `MainWindow.xaml` | Update menu item Header text |
| `MainWindow.xaml.cs` | Update dialog class reference |

### Files to Create

| File | Purpose |
|------|---------|
| `Dialogs/SkippedRecordsDialog.xaml` | Shows skipped UniqueIDs with reasons |
| `Dialogs/SkippedRecordsDialog.xaml.cs` | Code-behind with copy to clipboard |

---

## Phase 1: Rename Files and Update References

### Step 1.1: Update ManageSnapshotsDialog.xaml

Change the following in the renamed XAML file:

```xml
<!-- OLD -->
x:Class="VANTAGE.Dialogs.DeleteSnapshotsDialog"
Title="Delete My Progress Snapshots"

<!-- NEW -->
x:Class="VANTAGE.Dialogs.ManageSnapshotsDialog"
Title="Manage My Snapshots"
```

Update header text:
```xml
<!-- OLD -->
<TextBlock Text="Delete Progress Snapshots" .../>
<TextBlock Text="Select weeks to delete your submitted progress snapshots." .../>

<!-- NEW -->
<TextBlock Text="Manage My Snapshots" .../>
<TextBlock Text="Select a snapshot to delete or revert to." .../>
```

### Step 1.2: Update ManageSnapshotsDialog.xaml.cs

Change class declaration:
```csharp
// OLD
public partial class DeleteSnapshotsDialog : Window

// NEW
public partial class ManageSnapshotsDialog : Window
```

Update constructor:
```csharp
// OLD
public DeleteSnapshotsDialog()

// NEW
public ManageSnapshotsDialog()
```

Update Loaded event handler name:
```csharp
// OLD
Loaded += DeleteSnapshotsDialog_Loaded;
private async void DeleteSnapshotsDialog_Loaded(...)

// NEW
Loaded += ManageSnapshotsDialog_Loaded;
private async void ManageSnapshotsDialog_Loaded(...)
```

Update all AppLogger context strings from "DeleteSnapshotsDialog.*" to "ManageSnapshotsDialog.*"

### Step 1.3: Update MainWindow.xaml

```xml
<!-- OLD -->
<syncfusion:DropDownMenuItem Header="Delete My Snapshots" Click="MenuDeleteSnapshots_Click"/>

<!-- NEW -->
<syncfusion:DropDownMenuItem Header="Manage My Snapshots" Click="MenuManageSnapshots_Click"/>
```

### Step 1.4: Update MainWindow.xaml.cs

Rename method and update dialog reference:
```csharp
// OLD
private void MenuDeleteSnapshots_Click(object sender, RoutedEventArgs e)
{
    ...
    var dialog = new Dialogs.DeleteSnapshotsDialog();
    ...
}

// NEW
private void MenuManageSnapshots_Click(object sender, RoutedEventArgs e)
{
    ...
    var dialog = new Dialogs.ManageSnapshotsDialog();
    ...
}
```

---

## Phase 2: Update Dialog UI

### Step 2.1: Add Revert Button to XAML

Update the buttons StackPanel in ManageSnapshotsDialog.xaml:

```xml
<!-- Buttons - replace existing StackPanel -->
<StackPanel Grid.Row="7" 
            Orientation="Horizontal" 
            HorizontalAlignment="Right">
    <Button x:Name="btnCancel"
            Content="Cancel"
            Width="100"
            Height="35"
            Margin="0,0,10,0"
            Background="{StaticResource ControlBackground}"
            Foreground="{StaticResource ForegroundColor}"
            BorderBrush="{StaticResource ControlBorder}"
            BorderThickness="1"
            Click="BtnCancel_Click"/>
    <Button x:Name="btnDelete"
            Content="Delete Selected"
            Width="120"
            Height="35"
            Margin="0,0,10,0"
            Background="#B33A3A"
            Foreground="{StaticResource ForegroundColor}"
            BorderBrush="#B33A3A"
            BorderThickness="1"
            IsEnabled="False"
            Click="BtnDelete_Click"/>
    <Button x:Name="btnRevert"
            Content="Revert To Selected"
            Width="140"
            Height="35"
            Background="{StaticResource AccentColor}"
            Foreground="{StaticResource ForegroundColor}"
            BorderBrush="{StaticResource AccentColor}"
            BorderThickness="1"
            IsEnabled="False"
            Click="BtnRevert_Click"/>
</StackPanel>
```

### Step 2.2: Update Selection Summary Logic

In ManageSnapshotsDialog.xaml.cs, update `UpdateSelectionSummary()`:

```csharp
private void UpdateSelectionSummary()
{
    var selectedWeeks = _weeks.Where(w => w.IsSelected).ToList();
    int weekCount = selectedWeeks.Count;
    int snapshotCount = selectedWeeks.Sum(w => w.SnapshotCount);

    txtSelectionSummary.Text = $"{weekCount} week(s) selected ({snapshotCount:N0} snapshots)";
    
    // Delete enabled when 1 or more selected
    btnDelete.IsEnabled = weekCount > 0;
    
    // Revert enabled when EXACTLY 1 selected
    btnRevert.IsEnabled = weekCount == 1;
}
```

---

## Phase 3: Create SkippedRecordsDialog

### Step 3.1: Create SkippedRecordsDialog.xaml

```xml
<Window x:Class="VANTAGE.Dialogs.SkippedRecordsDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:skinManager="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
        skinManager:SfSkinManager.Theme="{skinManager:SkinManagerExtension ThemeName=FluentDark}"
        Title="Skipped Records"
        Height="450"
        Width="600"
        WindowStartupLocation="CenterOwner"
        Background="{StaticResource BackgroundColor}"
        ResizeMode="CanResize"
        MinHeight="300"
        MinWidth="400"
        WindowStyle="SingleBorderWindow">

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="10"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="15"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <StackPanel Grid.Row="0">
            <TextBlock Text="Skipped Records" 
                       FontWeight="SemiBold" 
                       FontSize="16" 
                       Foreground="{StaticResource ForegroundColor}"/>
            <TextBlock x:Name="txtSummary"
                       Text="The following records could not be restored:" 
                       Foreground="{StaticResource ForegroundColor}" 
                       Opacity="0.7"
                       FontSize="12"
                       Margin="0,5,0,0"/>
        </StackPanel>

        <!-- Records List -->
        <Border Grid.Row="2" 
                BorderBrush="{StaticResource ControlBorder}" 
                BorderThickness="1" 
                Background="{StaticResource ControlBackground}">
            <ListView x:Name="lvSkippedRecords"
                      Background="Transparent"
                      BorderThickness="0"
                      Foreground="{StaticResource ControlForeground}">
                <ListView.View>
                    <GridView>
                        <GridViewColumn Header="UniqueID" Width="280" DisplayMemberBinding="{Binding UniqueID}"/>
                        <GridViewColumn Header="Reason" Width="280" DisplayMemberBinding="{Binding Reason}"/>
                    </GridView>
                </ListView.View>
            </ListView>
        </Border>

        <!-- Buttons -->
        <StackPanel Grid.Row="4" 
                    Orientation="Horizontal" 
                    HorizontalAlignment="Right">
            <Button x:Name="btnCopyToClipboard"
                    Content="Copy to Clipboard"
                    Width="130"
                    Height="35"
                    Margin="0,0,10,0"
                    Background="{StaticResource ControlBackground}"
                    Foreground="{StaticResource ForegroundColor}"
                    BorderBrush="{StaticResource ControlBorder}"
                    BorderThickness="1"
                    Click="BtnCopyToClipboard_Click"/>
            <Button x:Name="btnClose"
                    Content="Close"
                    Width="100"
                    Height="35"
                    Background="{StaticResource AccentColor}"
                    Foreground="{StaticResource ForegroundColor}"
                    BorderBrush="{StaticResource AccentColor}"
                    BorderThickness="1"
                    Click="BtnClose_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

### Step 3.2: Create SkippedRecordsDialog.xaml.cs

```csharp
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace VANTAGE.Dialogs
{
    public partial class SkippedRecordsDialog : Window
    {
        private readonly List<SkippedRecordItem> _records;

        public SkippedRecordsDialog(List<SkippedRecordItem> records)
        {
            InitializeComponent();
            _records = records;
            
            lvSkippedRecords.ItemsSource = _records;
            txtSummary.Text = $"{_records.Count} record(s) could not be restored:";
        }

        private void BtnCopyToClipboard_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("UniqueID\tReason");
            
            foreach (var record in _records)
            {
                sb.AppendLine($"{record.UniqueID}\t{record.Reason}");
            }

            Clipboard.SetText(sb.ToString());
            
            MessageBox.Show(
                $"Copied {_records.Count} records to clipboard.",
                "Copied",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // Model for skipped record display
    public class SkippedRecordItem
    {
        public string UniqueID { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
```

---

## Phase 4: Implement Revert Logic

### Step 4.1: Add Required Using Statements

Add to ManageSnapshotsDialog.xaml.cs:
```csharp
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;
using VANTAGE.Models;
```

### Step 4.2: Add BtnRevert_Click Handler

```csharp
private async void BtnRevert_Click(object sender, RoutedEventArgs e)
{
    // Step 1: Validate single selection (defensive check - button should already be disabled if not exactly 1)
    var selectedWeeks = _weeks.Where(w => w.IsSelected).ToList();
    
    if (selectedWeeks.Count == 0)
    {
        MessageBox.Show("Please select a snapshot to revert to.", "No Selection",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }
    
    if (selectedWeeks.Count > 1)
    {
        MessageBox.Show("Please select only one snapshot to revert to.", "Multiple Selection",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }

    var selectedWeek = selectedWeeks[0];
    string currentUser = App.CurrentUser!.Username;

    // Step 2: Show warning dialog with backup option
    var warningResult = ShowRevertWarningDialog(selectedWeek);
    
    if (warningResult == RevertWarningResult.Cancel)
        return;

    // Disable buttons during operation
    btnDelete.IsEnabled = false;
    btnRevert.IsEnabled = false;
    btnCancel.IsEnabled = false;

    try
    {
        var busyDialog = new BusyDialog(this, "Preparing revert...");
        busyDialog.Show();

        // Step 3: Create backup snapshot if requested
        if (warningResult == RevertWarningResult.CreateBackupFirst)
        {
            busyDialog.UpdateStatus("Creating backup snapshot...");
            var backupResult = await CreateBackupSnapshotAsync(currentUser);
            
            if (!backupResult.Success)
            {
                busyDialog.Close();
                MessageBox.Show($"Failed to create backup snapshot:\n{backupResult.ErrorMessage}\n\nRevert cancelled.",
                    "Backup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            busyDialog.UpdateStatus($"Backup created ({backupResult.Count} records)...");
        }

        // Step 4: Sync to ensure local data is current
        busyDialog.UpdateStatus("Syncing current records...");
        
        if (!AzureDbManager.CheckConnection(out string connError))
        {
            busyDialog.Close();
            MessageBox.Show($"Cannot connect to Azure:\n{connError}\n\nRevert cancelled.",
                "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Get user's projects for sync
        var userProjects = GetUserProjects(currentUser);
        if (userProjects.Count > 0)
        {
            await SyncManager.PullRecordsAsync(userProjects);
            await SyncManager.PushRecordsAsync(userProjects);
        }

        // Step 5: Execute revert
        busyDialog.UpdateStatus("Loading snapshot data...");
        var revertResult = await ExecuteRevertAsync(selectedWeek.WeekEndDateStr, currentUser, 
            status => busyDialog.UpdateStatus(status));

        if (!revertResult.Success)
        {
            busyDialog.Close();
            MessageBox.Show($"Revert failed:\n{revertResult.ErrorMessage}",
                "Revert Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Step 6: Push reverted records to Azure
        busyDialog.UpdateStatus("Syncing restored records to Azure...");
        if (userProjects.Count > 0)
        {
            await SyncManager.PushRecordsAsync(userProjects);
        }

        busyDialog.Close();

        // Step 7: Show results
        ShowRevertResultsDialog(revertResult);

        // Step 8: Refresh ProgressView if loaded
        RefreshProgressViewIfLoaded();

        // Close the manage snapshots dialog
        DialogResult = true;
        Close();
    }
    catch (Exception ex)
    {
        AppLogger.Error(ex, "ManageSnapshotsDialog.BtnRevert_Click");
        MessageBox.Show($"Error during revert:\n{ex.Message}", "Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
    finally
    {
        btnDelete.IsEnabled = true;
        btnRevert.IsEnabled = true;
        btnCancel.IsEnabled = true;
        UpdateSelectionSummary();
    }
}
```

### Step 4.3: Add RevertWarningResult Enum

Add inside the namespace but outside the class:
```csharp
public enum RevertWarningResult
{
    Cancel,
    CreateBackupFirst,
    SkipBackup
}
```

### Step 4.4: Add ShowRevertWarningDialog Method

```csharp
private RevertWarningResult ShowRevertWarningDialog(SnapshotWeekItem selectedWeek)
{
    var result = RevertWarningResult.Cancel;

    var dialog = new Window
    {
        Title = "Revert to Snapshot",
        Width = 500,
        Height = 280,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = this,
        ResizeMode = ResizeMode.NoResize,
        WindowStyle = WindowStyle.SingleBorderWindow,
        Background = (Brush)Application.Current.Resources["BackgroundColor"]
    };

    Syncfusion.SfSkinManager.SfSkinManager.SetTheme(dialog, 
        new Syncfusion.SfSkinManager.Theme("FluentDark"));

    var grid = new Grid { Margin = new Thickness(20) };
    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

    // Warning icon and title
    var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
    headerPanel.Children.Add(new TextBlock 
    { 
        Text = "⚠️", 
        FontSize = 24, 
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 10, 0)
    });
    headerPanel.Children.Add(new TextBlock 
    { 
        Text = "REVERT TO SNAPSHOT", 
        FontSize = 16, 
        FontWeight = FontWeights.SemiBold,
        Foreground = (Brush)Application.Current.Resources["ForegroundColor"],
        VerticalAlignment = VerticalAlignment.Center
    });
    Grid.SetRow(headerPanel, 0);
    grid.Children.Add(headerPanel);

    // Message
    var messageText = $"This will replace your current activity records with data from:\n\n" +
                      $"    Week ending: {selectedWeek.WeekEndDate:MM/dd/yyyy}\n" +
                      $"    Records: {selectedWeek.SnapshotCount:N0} snapshots\n\n" +
                      $"Your current progress will be OVERWRITTEN.\n" +
                      $"Records you no longer own will be skipped.\n\n" +
                      $"Would you like to create a backup snapshot first?";

    var messageBlock = new TextBlock
    {
        Text = messageText,
        Foreground = (Brush)Application.Current.Resources["ForegroundColor"],
        TextWrapping = TextWrapping.Wrap,
        FontSize = 13
    };
    Grid.SetRow(messageBlock, 1);
    grid.Children.Add(messageBlock);

    // Buttons
    var buttonPanel = new StackPanel 
    { 
        Orientation = Orientation.Horizontal, 
        HorizontalAlignment = HorizontalAlignment.Right,
        Margin = new Thickness(0, 15, 0, 0)
    };

    var btnCancel = new Button
    {
        Content = "Cancel",
        Width = 100,
        Height = 35,
        Margin = new Thickness(0, 0, 10, 0),
        Background = (Brush)Application.Current.Resources["ControlBackground"],
        Foreground = (Brush)Application.Current.Resources["ForegroundColor"],
        BorderBrush = (Brush)Application.Current.Resources["ControlBorder"]
    };
    btnCancel.Click += (s, e) => { result = RevertWarningResult.Cancel; dialog.Close(); };

    var btnSkip = new Button
    {
        Content = "Skip Backup",
        Width = 100,
        Height = 35,
        Margin = new Thickness(0, 0, 10, 0),
        Background = (Brush)Application.Current.Resources["ControlBackground"],
        Foreground = (Brush)Application.Current.Resources["ForegroundColor"],
        BorderBrush = (Brush)Application.Current.Resources["ControlBorder"]
    };
    btnSkip.Click += (s, e) => { result = RevertWarningResult.SkipBackup; dialog.Close(); };

    var btnBackup = new Button
    {
        Content = "Create Backup First",
        Width = 140,
        Height = 35,
        Background = (Brush)Application.Current.Resources["AccentColor"],
        Foreground = (Brush)Application.Current.Resources["ForegroundColor"],
        BorderBrush = (Brush)Application.Current.Resources["AccentColor"]
    };
    btnBackup.Click += (s, e) => { result = RevertWarningResult.CreateBackupFirst; dialog.Close(); };

    buttonPanel.Children.Add(btnCancel);
    buttonPanel.Children.Add(btnSkip);
    buttonPanel.Children.Add(btnBackup);
    Grid.SetRow(buttonPanel, 2);
    grid.Children.Add(buttonPanel);

    dialog.Content = grid;
    dialog.ShowDialog();

    return result;
}
```

### Step 4.5: Add CreateBackupSnapshotAsync Method

```csharp
private async Task<(bool Success, int Count, string? ErrorMessage)> CreateBackupSnapshotAsync(string username)
{
    return await Task.Run(() =>
    {
        try
        {
            // Use today's date for backup (differentiates from regular Friday submissions)
            string backupWeekEndDate = DateTime.Today.ToString("yyyy-MM-dd");

            using var azureConn = AzureDbManager.GetConnection();
            azureConn.Open();

            // Insert current Activities as backup snapshots
            var cmd = azureConn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ProgressSnapshots (
                    UniqueID, WeekEndDate, Area, AssignedTo, AzureUploadUtcDate,
                    Aux1, Aux2, Aux3, BaseUnit, BudgetHoursGroup, BudgetHoursROC, BudgetMHs,
                    ChgOrdNO, ClientBudget, ClientCustom3, ClientEquivQty, CompType, CreatedBy,
                    DateTrigger, Description, DwgNO, EarnQtyEntry, EarnedMHsRoc, EqmtNO,
                    EquivQTY, EquivUOM, Estimator, HexNO, HtTrace, InsulType, LineNumber,
                    MtrlSpec, Notes, PaintCode, PercentEntry, PhaseCategory, PhaseCode,
                    PipeGrade, PipeSize1, PipeSize2, PrevEarnMHs, PrevEarnQTY, ProgDate,
                    ProjectID, Quantity, RevNO, RFINO, ROCBudgetQTY, ROCID, ROCPercent,
                    ROCStep, SchedActNO, SchFinish, SchStart, SecondActno, SecondDwgNO,
                    Service, ShopField, ShtNO, SubArea, PjtSystem, SystemNO, TagNO,
                    UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9, UDF10,
                    UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, UDF18, UDF20,
                    UpdatedBy, UpdatedUtcDate, UOM, WorkPackage, XRay
                )
                SELECT 
                    UniqueID, @weekEndDate, Area, AssignedTo, AzureUploadUtcDate,
                    Aux1, Aux2, Aux3, BaseUnit, BudgetHoursGroup, BudgetHoursROC, BudgetMHs,
                    ChgOrdNO, ClientBudget, ClientCustom3, ClientEquivQty, CompType, CreatedBy,
                    DateTrigger, Description, DwgNO, EarnQtyEntry, EarnedMHsRoc, EqmtNO,
                    EquivQTY, EquivUOM, Estimator, HexNO, HtTrace, InsulType, LineNumber,
                    MtrlSpec, Notes, PaintCode, PercentEntry, PhaseCategory, PhaseCode,
                    PipeGrade, PipeSize1, PipeSize2, PrevEarnMHs, PrevEarnQTY, ProgDate,
                    ProjectID, Quantity, RevNO, RFINO, ROCBudgetQTY, ROCID, ROCPercent,
                    ROCStep, SchedActNO, SchFinish, SchStart, SecondActno, SecondDwgNO,
                    Service, ShopField, ShtNO, SubArea, PjtSystem, SystemNO, TagNO,
                    UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9, UDF10,
                    UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, UDF18, UDF20,
                    @updatedBy, @updatedUtcDate, UOM, WorkPackage, XRay
                FROM Activities
                WHERE AssignedTo = @username
                  AND IsDeleted = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM ProgressSnapshots ps 
                      WHERE ps.UniqueID = Activities.UniqueID 
                        AND ps.WeekEndDate = @weekEndDate
                  )";

            cmd.Parameters.AddWithValue("@weekEndDate", backupWeekEndDate);
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@updatedBy", username);
            cmd.Parameters.AddWithValue("@updatedUtcDate", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));

            int count = cmd.ExecuteNonQuery();

            AppLogger.Info($"Created backup snapshot with {count} records for {backupWeekEndDate}",
                "ManageSnapshotsDialog.CreateBackupSnapshotAsync", username);

            return (true, count, null);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "ManageSnapshotsDialog.CreateBackupSnapshotAsync");
            return (false, 0, ex.Message);
        }
    });
}
```

### Step 4.6: Add GetUserProjects Method

```csharp
private List<string> GetUserProjects(string username)
{
    var projects = new List<string>();

    try
    {
        using var conn = DatabaseSetup.GetConnection();
        conn.Open();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT DISTINCT ProjectID 
            FROM Activities 
            WHERE AssignedTo = @username 
              AND ProjectID IS NOT NULL 
              AND ProjectID != ''";
        cmd.Parameters.AddWithValue("@username", username);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            projects.Add(reader.GetString(0));
        }
    }
    catch (Exception ex)
    {
        AppLogger.Error(ex, "ManageSnapshotsDialog.GetUserProjects");
    }

    return projects;
}
```

### Step 4.7: Add RevertResult Class

Add inside the namespace but outside the class:
```csharp
public class RevertResult
{
    public bool Success { get; set; }
    public int RestoredCount { get; set; }
    public List<SkippedRecordItem> SkippedRecords { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
```

### Step 4.8: Add ExecuteRevertAsync Method

```csharp
private async Task<RevertResult> ExecuteRevertAsync(string weekEndDate, string username, Action<string> updateStatus)
{
    var result = new RevertResult();

    try
    {
        // Step 1: Load all snapshot records from Azure
        updateStatus("Loading snapshot records from Azure...");
        var snapshots = await LoadSnapshotsFromAzureAsync(weekEndDate, username);

        if (snapshots.Count == 0)
        {
            result.ErrorMessage = "No snapshot records found for the selected week.";
            return result;
        }

        updateStatus($"Found {snapshots.Count:N0} snapshot records...");

        // Step 2: Get current state of these UniqueIDs from local database
        updateStatus("Checking current record ownership...");
        var currentState = await GetCurrentActivityStateAsync(snapshots.Select(s => s.UniqueID).ToList());

        // Step 3: Categorize records
        var toRestore = new List<SnapshotRecord>();
        
        foreach (var snapshot in snapshots)
        {
            if (!currentState.TryGetValue(snapshot.UniqueID, out var currentOwner))
            {
                // Record doesn't exist locally
                result.SkippedRecords.Add(new SkippedRecordItem
                {
                    UniqueID = snapshot.UniqueID,
                    Reason = "Record no longer exists"
                });
            }
            else if (!string.Equals(currentOwner, username, StringComparison.OrdinalIgnoreCase))
            {
                // Ownership changed
                result.SkippedRecords.Add(new SkippedRecordItem
                {
                    UniqueID = snapshot.UniqueID,
                    Reason = "No longer owned by you"
                });
            }
            else
            {
                // Can restore
                toRestore.Add(snapshot);
            }
        }

        if (toRestore.Count == 0)
        {
            result.ErrorMessage = "No records can be restored. All records have either changed ownership or no longer exist.";
            return result;
        }

        // Step 4: Update local Activities with snapshot values
        updateStatus($"Restoring {toRestore.Count:N0} records...");
        int restored = await RestoreActivitiesAsync(toRestore, username, updateStatus);

        result.RestoredCount = restored;
        result.Success = true;

        AppLogger.Info(
            $"Reverted to snapshot {weekEndDate}: {restored} restored, {result.SkippedRecords.Count} skipped",
            "ManageSnapshotsDialog.ExecuteRevertAsync", username);
    }
    catch (Exception ex)
    {
        AppLogger.Error(ex, "ManageSnapshotsDialog.ExecuteRevertAsync");
        result.ErrorMessage = ex.Message;
    }

    return result;
}
```

### Step 4.9: Add SnapshotRecord Class

Add inside the namespace but outside the class. This holds ALL non-calculated Activity fields:
```csharp
public class SnapshotRecord
{
    public string UniqueID { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string? AzureUploadUtcDate { get; set; }
    public string Aux1 { get; set; } = string.Empty;
    public string Aux2 { get; set; } = string.Empty;
    public string Aux3 { get; set; } = string.Empty;
    public double BaseUnit { get; set; }
    public double BudgetHoursGroup { get; set; }
    public double BudgetHoursROC { get; set; }
    public double BudgetMHs { get; set; }
    public string ChgOrdNO { get; set; } = string.Empty;
    public double ClientBudget { get; set; }
    public double ClientCustom3 { get; set; }
    public double ClientEquivQty { get; set; }
    public string CompType { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public int DateTrigger { get; set; }
    public string Description { get; set; } = string.Empty;
    public string DwgNO { get; set; } = string.Empty;
    public double EarnQtyEntry { get; set; }
    public double EarnedMHsRoc { get; set; }
    public string EqmtNO { get; set; } = string.Empty;
    public string EquivQTY { get; set; } = string.Empty;
    public string EquivUOM { get; set; } = string.Empty;
    public string Estimator { get; set; } = string.Empty;
    public int HexNO { get; set; }
    public string HtTrace { get; set; } = string.Empty;
    public string InsulType { get; set; } = string.Empty;
    public string LineNumber { get; set; } = string.Empty;
    public string MtrlSpec { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string PaintCode { get; set; } = string.Empty;
    public double PercentEntry { get; set; }
    public string PhaseCategory { get; set; } = string.Empty;
    public string PhaseCode { get; set; } = string.Empty;
    public string PipeGrade { get; set; } = string.Empty;
    public double PipeSize1 { get; set; }
    public double PipeSize2 { get; set; }
    public double PrevEarnMHs { get; set; }
    public double PrevEarnQTY { get; set; }
    public string? ProgDate { get; set; }
    public string ProjectID { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string RevNO { get; set; } = string.Empty;
    public string RFINO { get; set; } = string.Empty;
    public double ROCBudgetQTY { get; set; }
    public string ROCID { get; set; } = string.Empty;
    public double ROCPercent { get; set; }
    public string ROCStep { get; set; } = string.Empty;
    public string SchedActNO { get; set; } = string.Empty;
    public string? SchFinish { get; set; }
    public string? SchStart { get; set; }
    public string SecondActno { get; set; } = string.Empty;
    public string SecondDwgNO { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string ShopField { get; set; } = string.Empty;
    public string ShtNO { get; set; } = string.Empty;
    public string SubArea { get; set; } = string.Empty;
    public string PjtSystem { get; set; } = string.Empty;
    public string SystemNO { get; set; } = string.Empty;
    public string TagNO { get; set; } = string.Empty;
    public string UDF1 { get; set; } = string.Empty;
    public string UDF2 { get; set; } = string.Empty;
    public string UDF3 { get; set; } = string.Empty;
    public string UDF4 { get; set; } = string.Empty;
    public string UDF5 { get; set; } = string.Empty;
    public string UDF6 { get; set; } = string.Empty;
    public string UDF7 { get; set; } = string.Empty;
    public string UDF8 { get; set; } = string.Empty;
    public string UDF9 { get; set; } = string.Empty;
    public string UDF10 { get; set; } = string.Empty;
    public string UDF11 { get; set; } = string.Empty;
    public string UDF12 { get; set; } = string.Empty;
    public string UDF13 { get; set; } = string.Empty;
    public string UDF14 { get; set; } = string.Empty;
    public string UDF15 { get; set; } = string.Empty;
    public string UDF16 { get; set; } = string.Empty;
    public string UDF17 { get; set; } = string.Empty;
    public string UDF18 { get; set; } = string.Empty;
    public string UDF20 { get; set; } = string.Empty;
    public string UOM { get; set; } = string.Empty;
    public string WorkPackage { get; set; } = string.Empty;
    public string XRay { get; set; } = string.Empty;
}
```

### Step 4.10: Add LoadSnapshotsFromAzureAsync Method

```csharp
private async Task<List<SnapshotRecord>> LoadSnapshotsFromAzureAsync(string weekEndDate, string username)
{
    return await Task.Run(() =>
    {
        var snapshots = new List<SnapshotRecord>();

        using var azureConn = AzureDbManager.GetConnection();
        azureConn.Open();

        var cmd = azureConn.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                UniqueID, Area, AssignedTo, AzureUploadUtcDate,
                Aux1, Aux2, Aux3, BaseUnit, BudgetHoursGroup, BudgetHoursROC, BudgetMHs,
                ChgOrdNO, ClientBudget, ClientCustom3, ClientEquivQty, CompType, CreatedBy,
                DateTrigger, Description, DwgNO, EarnQtyEntry, EarnedMHsRoc, EqmtNO,
                EquivQTY, EquivUOM, Estimator, HexNO, HtTrace, InsulType, LineNumber,
                MtrlSpec, Notes, PaintCode, PercentEntry, PhaseCategory, PhaseCode,
                PipeGrade, PipeSize1, PipeSize2, PrevEarnMHs, PrevEarnQTY, ProgDate,
                ProjectID, Quantity, RevNO, RFINO, ROCBudgetQTY, ROCID, ROCPercent,
                ROCStep, SchedActNO, SchFinish, SchStart, SecondActno, SecondDwgNO,
                Service, ShopField, ShtNO, SubArea, PjtSystem, SystemNO, TagNO,
                UDF1, UDF2, UDF3, UDF4, UDF5, UDF6, UDF7, UDF8, UDF9, UDF10,
                UDF11, UDF12, UDF13, UDF14, UDF15, UDF16, UDF17, UDF18, UDF20,
                UOM, WorkPackage, XRay
            FROM ProgressSnapshots
            WHERE AssignedTo = @username
              AND WeekEndDate = @weekEndDate";
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@weekEndDate", weekEndDate);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            snapshots.Add(MapReaderToSnapshotRecord(reader));
        }

        return snapshots;
    });
}

private static SnapshotRecord MapReaderToSnapshotRecord(SqlDataReader reader)
{
    return new SnapshotRecord
    {
        UniqueID = reader.GetString(0),
        Area = GetStringOrEmpty(reader, 1),
        AssignedTo = GetStringOrEmpty(reader, 2),
        AzureUploadUtcDate = reader.IsDBNull(3) ? null : reader.GetString(3),
        Aux1 = GetStringOrEmpty(reader, 4),
        Aux2 = GetStringOrEmpty(reader, 5),
        Aux3 = GetStringOrEmpty(reader, 6),
        BaseUnit = GetDoubleOrZero(reader, 7),
        BudgetHoursGroup = GetDoubleOrZero(reader, 8),
        BudgetHoursROC = GetDoubleOrZero(reader, 9),
        BudgetMHs = GetDoubleOrZero(reader, 10),
        ChgOrdNO = GetStringOrEmpty(reader, 11),
        ClientBudget = GetDoubleOrZero(reader, 12),
        ClientCustom3 = GetDoubleOrZero(reader, 13),
        ClientEquivQty = GetDoubleOrZero(reader, 14),
        CompType = GetStringOrEmpty(reader, 15),
        CreatedBy = GetStringOrEmpty(reader, 16),
        DateTrigger = GetIntOrZero(reader, 17),
        Description = GetStringOrEmpty(reader, 18),
        DwgNO = GetStringOrEmpty(reader, 19),
        EarnQtyEntry = GetDoubleOrZero(reader, 20),
        EarnedMHsRoc = GetDoubleOrZero(reader, 21),
        EqmtNO = GetStringOrEmpty(reader, 22),
        EquivQTY = GetStringOrEmpty(reader, 23),
        EquivUOM = GetStringOrEmpty(reader, 24),
        Estimator = GetStringOrEmpty(reader, 25),
        HexNO = GetIntOrZero(reader, 26),
        HtTrace = GetStringOrEmpty(reader, 27),
        InsulType = GetStringOrEmpty(reader, 28),
        LineNumber = GetStringOrEmpty(reader, 29),
        MtrlSpec = GetStringOrEmpty(reader, 30),
        Notes = GetStringOrEmpty(reader, 31),
        PaintCode = GetStringOrEmpty(reader, 32),
        PercentEntry = GetDoubleOrZero(reader, 33),
        PhaseCategory = GetStringOrEmpty(reader, 34),
        PhaseCode = GetStringOrEmpty(reader, 35),
        PipeGrade = GetStringOrEmpty(reader, 36),
        PipeSize1 = GetDoubleOrZero(reader, 37),
        PipeSize2 = GetDoubleOrZero(reader, 38),
        PrevEarnMHs = GetDoubleOrZero(reader, 39),
        PrevEarnQTY = GetDoubleOrZero(reader, 40),
        ProgDate = reader.IsDBNull(41) ? null : reader.GetString(41),
        ProjectID = GetStringOrEmpty(reader, 42),
        Quantity = GetDoubleOrZero(reader, 43),
        RevNO = GetStringOrEmpty(reader, 44),
        RFINO = GetStringOrEmpty(reader, 45),
        ROCBudgetQTY = GetDoubleOrZero(reader, 46),
        ROCID = GetStringOrEmpty(reader, 47),
        ROCPercent = GetDoubleOrZero(reader, 48),
        ROCStep = GetStringOrEmpty(reader, 49),
        SchedActNO = GetStringOrEmpty(reader, 50),
        SchFinish = reader.IsDBNull(51) ? null : reader.GetString(51),
        SchStart = reader.IsDBNull(52) ? null : reader.GetString(52),
        SecondActno = GetStringOrEmpty(reader, 53),
        SecondDwgNO = GetStringOrEmpty(reader, 54),
        Service = GetStringOrEmpty(reader, 55),
        ShopField = GetStringOrEmpty(reader, 56),
        ShtNO = GetStringOrEmpty(reader, 57),
        SubArea = GetStringOrEmpty(reader, 58),
        PjtSystem = GetStringOrEmpty(reader, 59),
        SystemNO = GetStringOrEmpty(reader, 60),
        TagNO = GetStringOrEmpty(reader, 61),
        UDF1 = GetStringOrEmpty(reader, 62),
        UDF2 = GetStringOrEmpty(reader, 63),
        UDF3 = GetStringOrEmpty(reader, 64),
        UDF4 = GetStringOrEmpty(reader, 65),
        UDF5 = GetStringOrEmpty(reader, 66),
        UDF6 = GetStringOrEmpty(reader, 67),
        UDF7 = GetStringOrEmpty(reader, 68),
        UDF8 = GetStringOrEmpty(reader, 69),
        UDF9 = GetStringOrEmpty(reader, 70),
        UDF10 = GetStringOrEmpty(reader, 71),
        UDF11 = GetStringOrEmpty(reader, 72),
        UDF12 = GetStringOrEmpty(reader, 73),
        UDF13 = GetStringOrEmpty(reader, 74),
        UDF14 = GetStringOrEmpty(reader, 75),
        UDF15 = GetStringOrEmpty(reader, 76),
        UDF16 = GetStringOrEmpty(reader, 77),
        UDF17 = GetStringOrEmpty(reader, 78),
        UDF18 = GetStringOrEmpty(reader, 79),
        UDF20 = GetStringOrEmpty(reader, 80),
        UOM = GetStringOrEmpty(reader, 81),
        WorkPackage = GetStringOrEmpty(reader, 82),
        XRay = GetStringOrEmpty(reader, 83)
    };
}

// Helper methods for safe reader access
private static string GetStringOrEmpty(SqlDataReader reader, int ordinal)
{
    return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
}

private static double GetDoubleOrZero(SqlDataReader reader, int ordinal)
{
    if (reader.IsDBNull(ordinal)) return 0;
    try { return reader.GetDouble(ordinal); }
    catch { return 0; }
}

private static int GetIntOrZero(SqlDataReader reader, int ordinal)
{
    if (reader.IsDBNull(ordinal)) return 0;
    try { return reader.GetInt32(ordinal); }
    catch { return 0; }
}
```

### Step 4.11: Add GetCurrentActivityStateAsync Method

```csharp
private async Task<Dictionary<string, string>> GetCurrentActivityStateAsync(List<string> uniqueIds)
{
    return await Task.Run(() =>
    {
        var ownership = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var conn = DatabaseSetup.GetConnection();
        conn.Open();

        // Process in batches to avoid parameter limits
        const int batchSize = 500;
        for (int i = 0; i < uniqueIds.Count; i += batchSize)
        {
            var batch = uniqueIds.Skip(i).Take(batchSize).ToList();
            var placeholders = string.Join(",", batch.Select((_, idx) => $"@id{idx}"));

            var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT UniqueID, AssignedTo 
                FROM Activities 
                WHERE UniqueID IN ({placeholders})";

            for (int j = 0; j < batch.Count; j++)
            {
                cmd.Parameters.AddWithValue($"@id{j}", batch[j]);
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string uniqueId = reader.GetString(0);
                string assignedTo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                ownership[uniqueId] = assignedTo;
            }
        }

        return ownership;
    });
}
```

### Step 4.12: Add RestoreActivitiesAsync Method

```csharp
private async Task<int> RestoreActivitiesAsync(List<SnapshotRecord> snapshots, string username, Action<string> updateStatus)
{
    return await Task.Run(() =>
    {
        int restored = 0;
        string updatedUtcDate = DateTime.UtcNow.ToString("o");

        using var conn = DatabaseSetup.GetConnection();
        conn.Open();

        using var transaction = conn.BeginTransaction();

        try
        {
            int processed = 0;
            foreach (var snapshot in snapshots)
            {
                var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = BuildRestoreUpdateSql();
                AddRestoreParameters(cmd, snapshot, username, updatedUtcDate);

                int rows = cmd.ExecuteNonQuery();
                if (rows > 0) restored++;

                processed++;
                if (processed % 100 == 0)
                {
                    updateStatus($"Restored {processed:N0} of {snapshots.Count:N0} records...");
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return restored;
    });
}

private static string BuildRestoreUpdateSql()
{
    // Update ALL non-calculated fields from snapshot
    // Excludes: Status, EarnMHsCalc, EarnedQtyCalc, PercentCompleteCalc, ROCLookupID (calculated)
    // Sets fresh: UpdatedBy, UpdatedUtcDate, LocalDirty
    return @"
        UPDATE Activities SET
            Area = @area,
            AssignedTo = @assignedTo,
            Aux1 = @aux1,
            Aux2 = @aux2,
            Aux3 = @aux3,
            BaseUnit = @baseUnit,
            BudgetHoursGroup = @budgetHoursGroup,
            BudgetHoursROC = @budgetHoursROC,
            BudgetMHs = @budgetMHs,
            ChgOrdNO = @chgOrdNO,
            ClientBudget = @clientBudget,
            ClientCustom3 = @clientCustom3,
            ClientEquivQty = @clientEquivQty,
            CompType = @compType,
            DateTrigger = @dateTrigger,
            Description = @description,
            DwgNO = @dwgNO,
            EarnQtyEntry = @earnQtyEntry,
            EarnedMHsRoc = @earnedMHsRoc,
            EqmtNO = @eqmtNO,
            EquivQTY = @equivQTY,
            EquivUOM = @equivUOM,
            Estimator = @estimator,
            HexNO = @hexNO,
            HtTrace = @htTrace,
            InsulType = @insulType,
            LineNumber = @lineNumber,
            MtrlSpec = @mtrlSpec,
            Notes = @notes,
            PaintCode = @paintCode,
            PercentEntry = @percentEntry,
            PhaseCategory = @phaseCategory,
            PhaseCode = @phaseCode,
            PipeGrade = @pipeGrade,
            PipeSize1 = @pipeSize1,
            PipeSize2 = @pipeSize2,
            PrevEarnMHs = @prevEarnMHs,
            PrevEarnQTY = @prevEarnQTY,
            ProgDate = @progDate,
            ProjectID = @projectID,
            Quantity = @quantity,
            RevNO = @revNO,
            RFINO = @rfiNO,
            ROCBudgetQTY = @rocBudgetQTY,
            ROCID = @rocID,
            ROCPercent = @rocPercent,
            ROCStep = @rocStep,
            SchedActNO = @schedActNO,
            SchFinish = @schFinish,
            SchStart = @schStart,
            SecondActno = @secondActno,
            SecondDwgNO = @secondDwgNO,
            Service = @service,
            ShopField = @shopField,
            ShtNO = @shtNO,
            SubArea = @subArea,
            PjtSystem = @pjtSystem,
            SystemNO = @systemNO,
            TagNO = @tagNO,
            UDF1 = @udf1,
            UDF2 = @udf2,
            UDF3 = @udf3,
            UDF4 = @udf4,
            UDF5 = @udf5,
            UDF6 = @udf6,
            UDF7 = @udf7,
            UDF8 = @udf8,
            UDF9 = @udf9,
            UDF10 = @udf10,
            UDF11 = @udf11,
            UDF12 = @udf12,
            UDF13 = @udf13,
            UDF14 = @udf14,
            UDF15 = @udf15,
            UDF16 = @udf16,
            UDF17 = @udf17,
            UDF18 = @udf18,
            UDF20 = @udf20,
            UOM = @uom,
            WorkPackage = @workPackage,
            XRay = @xRay,
            UpdatedBy = @updatedBy,
            UpdatedUtcDate = @updatedUtcDate,
            LocalDirty = 1
        WHERE UniqueID = @uniqueId";
}

private static void AddRestoreParameters(SqliteCommand cmd, SnapshotRecord snapshot, string username, string updatedUtcDate)
{
    cmd.Parameters.AddWithValue("@uniqueId", snapshot.UniqueID);
    cmd.Parameters.AddWithValue("@area", snapshot.Area);
    cmd.Parameters.AddWithValue("@assignedTo", snapshot.AssignedTo);
    cmd.Parameters.AddWithValue("@aux1", snapshot.Aux1);
    cmd.Parameters.AddWithValue("@aux2", snapshot.Aux2);
    cmd.Parameters.AddWithValue("@aux3", snapshot.Aux3);
    cmd.Parameters.AddWithValue("@baseUnit", snapshot.BaseUnit);
    cmd.Parameters.AddWithValue("@budgetHoursGroup", snapshot.BudgetHoursGroup);
    cmd.Parameters.AddWithValue("@budgetHoursROC", snapshot.BudgetHoursROC);
    cmd.Parameters.AddWithValue("@budgetMHs", snapshot.BudgetMHs);
    cmd.Parameters.AddWithValue("@chgOrdNO", snapshot.ChgOrdNO);
    cmd.Parameters.AddWithValue("@clientBudget", snapshot.ClientBudget);
    cmd.Parameters.AddWithValue("@clientCustom3", snapshot.ClientCustom3);
    cmd.Parameters.AddWithValue("@clientEquivQty", snapshot.ClientEquivQty);
    cmd.Parameters.AddWithValue("@compType", snapshot.CompType);
    cmd.Parameters.AddWithValue("@dateTrigger", snapshot.DateTrigger);
    cmd.Parameters.AddWithValue("@description", snapshot.Description);
    cmd.Parameters.AddWithValue("@dwgNO", snapshot.DwgNO);
    cmd.Parameters.AddWithValue("@earnQtyEntry", snapshot.EarnQtyEntry);
    cmd.Parameters.AddWithValue("@earnedMHsRoc", snapshot.EarnedMHsRoc);
    cmd.Parameters.AddWithValue("@eqmtNO", snapshot.EqmtNO);
    cmd.Parameters.AddWithValue("@equivQTY", snapshot.EquivQTY);
    cmd.Parameters.AddWithValue("@equivUOM", snapshot.EquivUOM);
    cmd.Parameters.AddWithValue("@estimator", snapshot.Estimator);
    cmd.Parameters.AddWithValue("@hexNO", snapshot.HexNO);
    cmd.Parameters.AddWithValue("@htTrace", snapshot.HtTrace);
    cmd.Parameters.AddWithValue("@insulType", snapshot.InsulType);
    cmd.Parameters.AddWithValue("@lineNumber", snapshot.LineNumber);
    cmd.Parameters.AddWithValue("@mtrlSpec", snapshot.MtrlSpec);
    cmd.Parameters.AddWithValue("@notes", snapshot.Notes);
    cmd.Parameters.AddWithValue("@paintCode", snapshot.PaintCode);
    cmd.Parameters.AddWithValue("@percentEntry", snapshot.PercentEntry);
    cmd.Parameters.AddWithValue("@phaseCategory", snapshot.PhaseCategory);
    cmd.Parameters.AddWithValue("@phaseCode", snapshot.PhaseCode);
    cmd.Parameters.AddWithValue("@pipeGrade", snapshot.PipeGrade);
    cmd.Parameters.AddWithValue("@pipeSize1", snapshot.PipeSize1);
    cmd.Parameters.AddWithValue("@pipeSize2", snapshot.PipeSize2);
    cmd.Parameters.AddWithValue("@prevEarnMHs", snapshot.PrevEarnMHs);
    cmd.Parameters.AddWithValue("@prevEarnQTY", snapshot.PrevEarnQTY);
    cmd.Parameters.AddWithValue("@progDate", snapshot.ProgDate ?? (object)DBNull.Value);
    cmd.Parameters.AddWithValue("@projectID", snapshot.ProjectID);
    cmd.Parameters.AddWithValue("@quantity", snapshot.Quantity);
    cmd.Parameters.AddWithValue("@revNO", snapshot.RevNO);
    cmd.Parameters.AddWithValue("@rfiNO", snapshot.RFINO);
    cmd.Parameters.AddWithValue("@rocBudgetQTY", snapshot.ROCBudgetQTY);
    cmd.Parameters.AddWithValue("@rocID", snapshot.ROCID);
    cmd.Parameters.AddWithValue("@rocPercent", snapshot.ROCPercent);
    cmd.Parameters.AddWithValue("@rocStep", snapshot.ROCStep);
    cmd.Parameters.AddWithValue("@schedActNO", snapshot.SchedActNO);
    cmd.Parameters.AddWithValue("@schFinish", snapshot.SchFinish ?? (object)DBNull.Value);
    cmd.Parameters.AddWithValue("@schStart", snapshot.SchStart ?? (object)DBNull.Value);
    cmd.Parameters.AddWithValue("@secondActno", snapshot.SecondActno);
    cmd.Parameters.AddWithValue("@secondDwgNO", snapshot.SecondDwgNO);
    cmd.Parameters.AddWithValue("@service", snapshot.Service);
    cmd.Parameters.AddWithValue("@shopField", snapshot.ShopField);
    cmd.Parameters.AddWithValue("@shtNO", snapshot.ShtNO);
    cmd.Parameters.AddWithValue("@subArea", snapshot.SubArea);
    cmd.Parameters.AddWithValue("@pjtSystem", snapshot.PjtSystem);
    cmd.Parameters.AddWithValue("@systemNO", snapshot.SystemNO);
    cmd.Parameters.AddWithValue("@tagNO", snapshot.TagNO);
    cmd.Parameters.AddWithValue("@udf1", snapshot.UDF1);
    cmd.Parameters.AddWithValue("@udf2", snapshot.UDF2);
    cmd.Parameters.AddWithValue("@udf3", snapshot.UDF3);
    cmd.Parameters.AddWithValue("@udf4", snapshot.UDF4);
    cmd.Parameters.AddWithValue("@udf5", snapshot.UDF5);
    cmd.Parameters.AddWithValue("@udf6", snapshot.UDF6);
    cmd.Parameters.AddWithValue("@udf7", snapshot.UDF7);
    cmd.Parameters.AddWithValue("@udf8", snapshot.UDF8);
    cmd.Parameters.AddWithValue("@udf9", snapshot.UDF9);
    cmd.Parameters.AddWithValue("@udf10", snapshot.UDF10);
    cmd.Parameters.AddWithValue("@udf11", snapshot.UDF11);
    cmd.Parameters.AddWithValue("@udf12", snapshot.UDF12);
    cmd.Parameters.AddWithValue("@udf13", snapshot.UDF13);
    cmd.Parameters.AddWithValue("@udf14", snapshot.UDF14);
    cmd.Parameters.AddWithValue("@udf15", snapshot.UDF15);
    cmd.Parameters.AddWithValue("@udf16", snapshot.UDF16);
    cmd.Parameters.AddWithValue("@udf17", snapshot.UDF17);
    cmd.Parameters.AddWithValue("@udf18", snapshot.UDF18);
    cmd.Parameters.AddWithValue("@udf20", snapshot.UDF20);
    cmd.Parameters.AddWithValue("@uom", snapshot.UOM);
    cmd.Parameters.AddWithValue("@workPackage", snapshot.WorkPackage);
    cmd.Parameters.AddWithValue("@xRay", snapshot.XRay);
    cmd.Parameters.AddWithValue("@updatedBy", username);
    cmd.Parameters.AddWithValue("@updatedUtcDate", updatedUtcDate);
}
```

### Step 4.13: Add ShowRevertResultsDialog Method

```csharp
private void ShowRevertResultsDialog(RevertResult result)
{
    string message = $"Revert Complete\n\n" +
                     $"✓ Restored: {result.RestoredCount:N0} records";

    if (result.SkippedRecords.Count > 0)
    {
        int ownershipSkipped = result.SkippedRecords.Count(r => r.Reason == "No longer owned by you");
        int notFoundSkipped = result.SkippedRecords.Count(r => r.Reason == "Record no longer exists");

        if (ownershipSkipped > 0)
            message += $"\n⊘ Skipped (ownership changed): {ownershipSkipped:N0}";
        if (notFoundSkipped > 0)
            message += $"\n⊘ Skipped (no longer exists): {notFoundSkipped:N0}";

        message += "\n\nWould you like to view the skipped records?";

        var viewResult = MessageBox.Show(message, "Revert Complete",
            MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (viewResult == MessageBoxResult.Yes)
        {
            var skippedDialog = new SkippedRecordsDialog(result.SkippedRecords);
            skippedDialog.Owner = this;
            skippedDialog.ShowDialog();
        }
    }
    else
    {
        MessageBox.Show(message, "Revert Complete",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
```

### Step 4.14: Add RefreshProgressViewIfLoaded Method

```csharp
private void RefreshProgressViewIfLoaded()
{
    try
    {
        // Find MainWindow and check if ProgressView is loaded
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow?.ContentArea.Content is Views.ProgressView progressView)
        {
            _ = progressView.RefreshData();
        }
    }
    catch (Exception ex)
    {
        AppLogger.Error(ex, "ManageSnapshotsDialog.RefreshProgressViewIfLoaded");
        // Non-critical - don't show error to user
    }
}
```

---

## Phase 5: Testing Checklist

After implementation, verify:

1. **Dialog Opens**: File menu → "Manage My Snapshots" opens dialog
2. **Snapshots Load**: User's snapshots display correctly
3. **Button Enable States**:
   - 0 selected: Both Delete and Revert disabled
   - 1 selected: Both enabled
   - 2+ selected: Delete enabled, Revert disabled
4. **Delete Flow**: Same as before (multiple selection allowed)
5. **Revert Validation**: Error message if somehow clicked with 0 or 2+ selections
6. **Warning Dialog**: Shows snapshot info and backup options
7. **Backup Creation**: Creates snapshot with today's date
8. **Sync Before Revert**: Runs push/pull
9. **Revert Execution**: Updates local Activities
10. **Sync After Revert**: Pushes changes to Azure
11. **Results Dialog**: Shows restored count and skipped summary
12. **Skipped Details**: Dialog shows UniqueIDs and reasons
13. **Copy to Clipboard**: Copies tab-separated list
14. **Grid Refresh**: ProgressView updates if visible

---

## Code Conventions Reminder

- Use `//` comments only, never `/// <summary>`
- Handle nullability properly (string? for nullable, string for required)
- Log errors with `AppLogger.Error(ex, "ClassName.MethodName")`
- Log user actions with `AppLogger.Info(message, "ClassName.MethodName", username)`
- Use `(object)DBNull.Value` for null SQL parameters
- Use `StringComparison.OrdinalIgnoreCase` for username comparisons

---

## Files Summary

| File | Action |
|------|--------|
| `Dialogs/ManageSnapshotsDialog.xaml` | Rename from DeleteSnapshotsDialog.xaml, update UI |
| `Dialogs/ManageSnapshotsDialog.xaml.cs` | Rename from DeleteSnapshotsDialog.xaml.cs, add revert logic |
| `Dialogs/SkippedRecordsDialog.xaml` | Create new |
| `Dialogs/SkippedRecordsDialog.xaml.cs` | Create new |
| `MainWindow.xaml` | Update menu item text and click handler name |
| `MainWindow.xaml.cs` | Rename method, update dialog reference |
