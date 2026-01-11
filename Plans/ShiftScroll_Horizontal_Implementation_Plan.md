# Shift+Scroll Horizontal Scrolling - Implementation Plan

## Overview

Implement Shift+Scroll wheel functionality to enable horizontal scrolling throughout the MILESTONE application. This is a standard UX pattern used by Chrome, Firefox, Excel, VS Code, and most modern applications.

**Behavior:** When the user holds Shift and scrolls the mouse wheel, the scroll direction changes from vertical to horizontal.

---

## Scope

### Primary Targets (Syncfusion Grids)

| View | Control | x:Name |
|------|---------|--------|
| ProgressView.xaml | SfDataGrid | `sfActivities` |
| ScheduleView.xaml | SfDataGrid | `sfSchedule` (or similar) |
| ScheduleView.xaml | SfDataGrid | Any detail/snapshot grids |
| AdminSnapshotsDialog.xaml | ListView | `lvSnapshots` |
| ManageSnapshotsDialog.xaml | ListView | `lvWeeks` |
| Any other dialogs | ListView/DataGrid | As applicable |

**Note:** WorkPackageView uses ListBoxes (not SfDataGrid), which typically don't have horizontal scrollbars. Evaluate during implementation if any WP controls need this behavior.

### Secondary Targets (Standard WPF ScrollViewers)

Any control that manifests a horizontal scrollbar should support Shift+Scroll:
- ListViews
- ScrollViewers
- DataGrids (non-Syncfusion)
- TreeViews with horizontal content
- Any custom panels wrapped in ScrollViewer

---

## Implementation Approach

### Step 0: Check Syncfusion Built-in Support (FIRST)

Before implementing a custom solution, verify whether Syncfusion SfDataGrid already supports Shift+Scroll:

1. **Check Documentation:** Search Syncfusion docs for "horizontal scroll" + "shift" or "mouse wheel"
2. **Test Current Behavior:** In the app, try Shift+Scroll on an SfDataGrid to see if it already works
3. **Check Properties:** Look for properties like `AllowHorizontalScrollWheel`, `HorizontalScrollMode`, etc.

If Syncfusion provides built-in support, enable it via properties and skip the custom behavior for SfDataGrid controls. Only implement custom behavior for standard WPF controls (ListView, ScrollViewer) that lack this feature.

### Recommended: Attached Behavior (Per-Control Application)

Create a reusable attached behavior that can be applied declaratively in XAML to individual controls.

**Advantages:**
- Single implementation, applied per-control via XAML
- No code-behind modifications needed per view
- Easy to enable/disable per control
- Follows WPF best practices
- Explicit control over which controls get the behavior

**Application Method:** Per-control in XAML (not global styles) for explicit control and easier debugging.

### File to Create

```
Behaviors/HorizontalScrollBehavior.cs
```

---

## Technical Implementation

### Step 1: Create the Attached Behavior

Create `Behaviors/HorizontalScrollBehavior.cs` with:

1. **Attached Property:** `IsEnabled` (bool) - enables/disables the behavior
2. **Event Handling:** Hook into `PreviewMouseWheel` when attached
3. **Modifier Check:** Only act when `Keyboard.Modifiers == ModifierKeys.Shift`
4. **ScrollViewer Discovery:** Walk visual tree to find the ScrollViewer
5. **Horizontal Scroll:** Call `ScrollViewer.ScrollToHorizontalOffset()` or use `LineLeft()`/`LineRight()`

### Step 2: Visual Tree Helper with Caching

SfDataGrid and other complex controls bury their ScrollViewer in the visual tree. Need a helper method to find it:

```
FindVisualChild<ScrollViewer>(DependencyObject parent)
```

This recursively searches the visual tree for the first ScrollViewer descendant.

**Performance Optimization:** Cache the ScrollViewer reference to avoid visual tree traversal on every scroll event. Use a `ConditionalWeakTable<UIElement, ScrollViewer>` or store the reference in the first successful lookup. The cache should be cleared when the control is unloaded.

### Step 3: Scroll Calculation

When Shift+Scroll is detected:
1. Get `e.Delta` (positive = scroll up/right, negative = scroll down/left)
2. Calculate horizontal offset: `currentOffset - (e.Delta * multiplier)`
3. Multiplier should match feel of vertical scroll (start with 1.0, adjust if needed)
4. Clamp to valid range: `0` to `ScrollableWidth`
5. Call `ScrollToHorizontalOffset(newOffset)`
6. Set `e.Handled = true` to prevent vertical scroll

### Step 4: XAML Application

Apply to controls via attached property:

```xml
xmlns:behaviors="clr-namespace:VANTAGE.Behaviors"

<syncfusion:SfDataGrid 
    behaviors:HorizontalScrollBehavior.IsEnabled="True"
    ... />
```

Or apply globally via implicit style in App.xaml:

```xml
<Style TargetType="syncfusion:SfDataGrid">
    <Setter Property="behaviors:HorizontalScrollBehavior.IsEnabled" Value="True"/>
</Style>
```

---

## Detailed Behavior Specification

### Input Handling

| Input | Action |
|-------|--------|
| Scroll wheel (no modifier) | Normal vertical scroll (no change) |
| Shift + Scroll wheel up | Scroll left |
| Shift + Scroll wheel down | Scroll right |
| Ctrl + Scroll wheel | Reserved for zoom (no action) |
| Alt + Scroll wheel | No action |

### Edge Cases

1. **No horizontal scrollbar visible:** Behavior should do nothing (check `ScrollableWidth > 0`)
2. **At scroll boundary:** Clamp offset, don't wrap around
3. **Nested ScrollViewers:** Behavior should apply to the first ScrollViewer found in the control
4. **Modal dialogs:** Should work the same as main window

### Scroll Speed

Match vertical scroll behavior:
- `e.Delta` is typically ±120 per notch
- Standard multiplier: `e.Delta / 120 * LineWidth` where LineWidth ≈ 48 pixels
- Alternative: Use `ScrollViewer.LineRight()` / `LineLeft()` called multiple times based on delta

---

## Files to Modify

| File | Change |
|------|--------|
| `Behaviors/HorizontalScrollBehavior.cs` | **CREATE** - New attached behavior with ScrollViewer caching |
| `ProgressView.xaml` | Add behavior to sfActivities grid (if Syncfusion lacks built-in support) |
| `ScheduleView.xaml` | Add behavior to grids (if Syncfusion lacks built-in support) |
| `AdminSnapshotsDialog.xaml` | Add behavior to ListView |
| `ManageSnapshotsDialog.xaml` | Add behavior to ListView |

**Approach:** Per-control application in XAML for explicit control. Do NOT use global App.xaml styles.

---

## Alternative: Global Application via App.xaml (NOT RECOMMENDED)

**Decision:** User chose per-control XAML application for explicit control and easier debugging.

For reference only - instead of adding the behavior to each control individually, one could apply globally:

```xml
<!-- In App.xaml Resources -->
<Style TargetType="syncfusion:SfDataGrid" BasedOn="{StaticResource {x:Type syncfusion:SfDataGrid}}">
    <Setter Property="behaviors:HorizontalScrollBehavior.IsEnabled" Value="True"/>
</Style>

<Style TargetType="ListView">
    <Setter Property="behaviors:HorizontalScrollBehavior.IsEnabled" Value="True"/>
</Style>

<Style TargetType="ScrollViewer">
    <Setter Property="behaviors:HorizontalScrollBehavior.IsEnabled" Value="True"/>
</Style>
```

**Note:** Test this approach - Syncfusion controls may require the behavior on the control itself rather than inherited via style.

---

## Code Structure

```csharp
// Behaviors/HorizontalScrollBehavior.cs

namespace VANTAGE.Behaviors
{
    public static class HorizontalScrollBehavior
    {
        // Cache for ScrollViewer references (avoids visual tree search on every scroll)
        private static readonly ConditionalWeakTable<UIElement, ScrollViewer> _scrollViewerCache = new();

        // Attached property: IsEnabled
        public static readonly DependencyProperty IsEnabledProperty = ...

        // Property changed callback - hooks/unhooks events, clears cache on disable
        private static void OnIsEnabledChanged(...) { }

        // PreviewMouseWheel handler - uses cached ScrollViewer
        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e) { }

        // Visual tree helper to find ScrollViewer (caches result)
        private static ScrollViewer? GetOrFindScrollViewer(UIElement element) { }

        // Recursive visual tree search
        private static ScrollViewer? FindScrollViewerInVisualTree(DependencyObject parent) { }
    }
}
```

---

## Testing Checklist

### Functionality Tests

- [ ] Shift+Scroll scrolls horizontally in ProgressView grid
- [ ] Shift+Scroll scrolls horizontally in ScheduleView grid
- [ ] Shift+Scroll scrolls horizontally in dialogs with ListViews
- [ ] Normal scroll (no Shift) still scrolls vertically
- [ ] Ctrl+Scroll does NOT trigger horizontal scroll
- [ ] Scroll direction matches expectation (Shift+Up = Left, Shift+Down = Right)

### Edge Case Tests

- [ ] No horizontal scrollbar: Shift+Scroll does nothing
- [ ] At left edge: Cannot scroll further left
- [ ] At right edge: Cannot scroll further right
- [ ] Scroll speed feels natural and matches vertical scroll
- [ ] Works in modal dialogs
- [ ] Works when grid has focus
- [ ] Works when grid does not have focus (mouse hover)

### Performance Tests

- [ ] No lag or stutter during Shift+Scroll
- [ ] Visual tree search is cached or fast enough
- [ ] Large grids (200k+ records) scroll smoothly

---

## Code Conventions Reminder

- Use `//` comments only, never `/// <summary>` XML docs
- Handle nullability properly (`ScrollViewer?` return type)
- No Debug.WriteLine in final code
- Log errors with `AppLogger.Error(ex, "HorizontalScrollBehavior.MethodName")`

---

## Optional Future Enhancement: Tilt Wheel Support

If users have mice with horizontal tilt wheels, native tilt support requires Win32 interop:

1. Hook into `WM_MOUSEHWHEEL` (0x020E) via `HwndSource.AddHook()`
2. Extract horizontal delta from message
3. Apply same scroll logic

This is more complex and can be deferred unless users specifically request it.

---

## Summary

| Item | Value |
|------|-------|
| First step | Check if Syncfusion has built-in Shift+Scroll support |
| New files | 1 (`Behaviors/HorizontalScrollBehavior.cs`) |
| Modified files | 4-5 (ProgressView, ScheduleView, AdminSnapshotsDialog, ManageSnapshotsDialog) |
| Application method | Per-control in XAML (not global styles) |
| Performance | ScrollViewer caching to avoid repeated visual tree traversal |
| Complexity | Low-Medium |
| Risk | Low (additive feature, no breaking changes) |
| User benefit | Standard UX pattern, faster navigation in wide grids |
