# Plan: Add Disable Tooltips Setting

## Overview
Add a toggle setting in the MainWindow settings dropdown to enable/disable tooltips application-wide.

## Implementation Steps

### 1. Add Setting to SettingsManager.cs
Add helper methods for the DisableTooltips user setting:
```csharp
public static bool GetDisableTooltips(int userId)
{
    string value = GetUserSetting(userId, "DisableTooltips", "false");
    return value == "true";
}

public static void SetDisableTooltips(int userId, bool disabled)
{
    SetUserSetting(userId, "DisableTooltips", disabled ? "true" : "false", "string");
}
```

### 2. Add Toggle to Settings Popup (MainWindow.xaml)
Add a CheckBox to the `popupSettings` StackPanel after existing buttons:
```xaml
<CheckBox x:Name="chkDisableTooltips"
          Content="Disable Tooltips"
          Foreground="{StaticResource ForegroundColor}"
          Margin="12,8"
          Checked="ChkDisableTooltips_Changed"
          Unchecked="ChkDisableTooltips_Changed"/>
```

### 3. Add Event Handler (MainWindow.xaml.cs)
```csharp
private void ChkDisableTooltips_Changed(object sender, RoutedEventArgs e)
{
    bool disabled = chkDisableTooltips.IsChecked == true;
    SettingsManager.SetDisableTooltips(App.CurrentUser!.UserID, disabled);
    ApplyTooltipSetting(disabled);
}

private void ApplyTooltipSetting(bool disabled)
{
    ToolTipService.SetIsEnabled(this, !disabled);
}
```

### 4. Load Setting on Startup
In MainWindow constructor (after InitializeComponent):
```csharp
// Load tooltip setting
bool tooltipsDisabled = SettingsManager.GetDisableTooltips(App.CurrentUser!.UserID);
chkDisableTooltips.IsChecked = tooltipsDisabled;
ApplyTooltipSetting(tooltipsDisabled);
```

## Files to Modify
1. `Utilities/SettingsManager.cs` - Add Get/Set methods
2. `MainWindow.xaml` - Add CheckBox to settings popup
3. `MainWindow.xaml.cs` - Add handler and apply logic

## Notes
- Uses `ToolTipService.SetIsEnabled()` on MainWindow - this inherits to all child controls
- Setting persists per-user via UserSettings table
- Default is `false` (tooltips enabled)
