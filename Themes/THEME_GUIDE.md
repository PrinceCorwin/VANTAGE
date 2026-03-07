# VANTAGE Theme System Guide

Reference for creating and maintaining themes. Each theme is a single ResourceDictionary XAML file in `Themes/` that defines the same set of keys with theme-appropriate color values.

## Architecture Overview

- **Theme files:** `Themes/DarkTheme.xaml`, `LightTheme.xaml`, `OrchidTheme.xaml`, `DarkForestTheme.xaml`
- **Shared styles:** `Themes/SharedStyles.xaml` — cross-theme styles (RoundedButtonStyle, PrimaryButtonStyle). NOT swapped on theme change.
- **Engine:** `Utilities/ThemeManager.cs` — swaps ResourceDictionaries at runtime, fires `ThemeChanged` event
- **Generator:** `Scripts/Generate-Theme.ps1` — generates a theme XAML from 4 hex colors + dark/light base
- **Syncfusion base themes:** FluentDark (for dark backgrounds), FluentLight (for light backgrounds)
- **Live switching:** Themes apply instantly. No restart required.

## How to Create a New Theme

Use the `/create-theme` skill or follow these manual steps:

1. **Run the generator script** (preferred) or copy an existing theme file:
   ```
   powershell -ExecutionPolicy Bypass -File "Scripts/Generate-Theme.ps1" -ThemeName "<Name>" -Base "<Dark|Light>" -PrimaryHex "<#hex>" -AccentHex "<#hex>" -SecondaryHex "<#hex>" -SurfaceHex "<#hex>"
   ```
   - **Primary** — background tone
   - **Accent** — interactive highlights (buttons, links, toggles)
   - **Secondary** — structural chrome (grid headers, toolbar)
   - **Surface** — controls, cards, dialogs, inputs

2. **Register in ThemeManager.cs:**
   - Add to `ThemeMap`: `{ "Ocean", "FluentDark" }` (or `"FluentLight"` for light themes)
   - Add to `AvailableThemes` array

3. **Add radio button in ThemeManagerDialog.xaml/.cs** — Copy an existing radio button block, update name/label/row, update code-behind selection logic.

4. **Update Help/manual.html** — Add the new theme to the Themes section.

5. **Manual tuning** — After generating, review and adjust these keys that often need per-theme attention:
   - `ScanButtonForeground`, `SummaryBudgetForeground` — may need a brighter color if accent is muted
   - `SidebarButtonHoverBorder`, `SidebarButtonHoverBackground` — hover visibility
   - `GridCellBackground`, `GridAlternatingRowBackground` — data row colors

6. **Build and test** — Switch to the new theme. Verify all views, dialogs, hover/pressed/disabled states.

## Syncfusion Base Theme Mapping

| Your theme background | Use Syncfusion base | Why |
|-----------------------|--------------------|-----|
| Dark backgrounds | `FluentDark` | Grid chrome, scrollbars, headers render for dark bg |
| Light backgrounds | `FluentLight` | Grid chrome, scrollbars, headers render for light bg |

The Syncfusion base theme controls SfDataGrid headers, scrollbars, cell selection, and other Syncfusion-specific UI that our theme keys don't reach. Choose the base that matches your background luminance.

## Important Rules

### Status Buttons Are Locked Per Base Type
The Complete, In Progress, and Not Started quick filter buttons must look **identical** across all themes of the same base type (dark or light). These are functional/semantic colors, not decorative. **Never derive them from the accent color or any palette color.**

All dark themes must match `DarkTheme.xaml` values. All light themes must match `LightTheme.xaml` values. The generator script hardcodes these automatically.

Locked keys: `StatusGreen`, `StatusGreenBgBtn`, `StatusGreenFgBtn`, `StatusYellow`, `StatusYellowBg`, `StatusYellowFg`, `StatusRed`, `StatusRedBg`, `StatusRedBgBtn`, `StatusRedFgBtn`, `StatusGoldBg`, `StatusInProgress`, `StatusInProgressBgBtn`, `StatusInProgressFgBtn`, `StatusNotStarted`

### Grid Row Backgrounds
`GridCellBackground` controls the primary data row background and `GridAlternatingRowBackground` controls alternating rows. Both are applied via `RecordOwnershipRowStyleSelector` in code-behind. The Syncfusion default row background is NOT used — these keys fully control row colors.

## Token Reference

All keys are `SolidColorBrush` unless noted otherwise.

### Font Settings
| Key | Type | Description |
|-----|------|-------------|
| `FontFamilyPrimary` | FontFamily | App-wide font face (Segoe UI) |
| `FontSizeNormal` | sys:Double | Default font size (14) |

### Core Palette
| Key | Description |
|-----|-------------|
| `BackgroundColor` | Primary app background (main content areas) |
| `DarkBackgroundColor` | Darker background variant |
| `DarkestBackgroundColor` | Darkest background (main window top toolbar) |
| `ForegroundColor` | Primary text color — used everywhere for labels, headers, inputs |
| `TextColorSecondary` | Muted/secondary text |
| `AccentColor` | Primary accent — links, active highlights, accent text |
| `BorderColor` | Default control borders |
| `DisabledColor` | Disabled control foreground (used in button style IsEnabled=False triggers) |

### Window & Control Elements
| Key | Description |
|-----|-------------|
| `WindowBackground` | Main window background |
| `ControlBackground` | Form controls, text inputs, combo boxes, dialog sections, context menus |
| `ControlBackgroundGreen` | Green-tinted control background (sync success indicators) |
| `ControlBackgroundRed` | Red-tinted control background (error indicators) |
| `ControlForeground` | Control text foreground |
| `ControlBorder` | Control border color |
| `ControlHoverBackground` | Control hover state background |
| `ControlPressedBackground` | Control pressed state background |

### Content/Dialog Keys
| Key | Description |
|-----|-------------|
| `ContentBackground` | FindReplaceDialog and similar content panel backgrounds |
| `ContentForeground` | FindReplaceDialog text |
| `PrimaryBackground` | Primary action background (FindReplaceDialog buttons) |
| `PrimaryForeground` | Primary action text (white) |

### Progress & Grid Keys
| Key | Description |
|-----|-------------|
| `ProgressBarTrackColor` | SfLinearProgressBar track (the unfilled portion) |
| `GridAlternatingRowBackground` | Alternating data row background (applied via RowStyleSelector) |

### Split Token Keys (for independent per-region tuning)
| Key | Derives from | Description |
|-----|-------------|-------------|
| `ProgressBarAccent` | AccentColor | SfLinearProgressBar fill color (ProgressColor) |
| `ToggleCheckedBackground` | AccentColor | Toggle button IsChecked=True background |
| `DialogBackground` | ControlBackground | Reserved — dialog body backgrounds |
| `GridCellBackground` | ControlBackground | Primary data row background (applied via RowStyleSelector) |
| `DialogForeground` | ForegroundColor | Reserved — dialog text |
| `GridCellForeground` | ForegroundColor | Reserved — grid cell text |

These start with the same values as their parent. Change them to independently tune a specific region without affecting the parent key's other consumers.

### DataGrid
| Key | Description |
|-----|-------------|
| `GridHeaderBackground` | Grid column header background |
| `GridHeaderForeground` | Grid column header text |

### Status Colors (LOCKED per base type — see Important Rules)
| Key | Description |
|-----|-------------|
| `StatusGreen` | Complete status indicator |
| `StatusGreenBgBtn` | Complete status button background |
| `StatusGreenFgBtn` | Complete status button text |
| `StatusYellow` | Warning status |
| `StatusYellowBg` | Warning status background |
| `StatusYellowFg` | Warning status text |
| `StatusRed` | Error/overdue status |
| `StatusRedBg` | Error status background (LinearGradientBrush in Dark theme) |
| `StatusRedBgBtn` | Error status button background |
| `StatusRedFgBtn` | Error status button text |
| `StatusGoldBg` | Gold status background |
| `StatusInProgress` | In-progress status indicator |
| `StatusInProgressBgBtn` | In-progress button background |
| `StatusInProgressFgBtn` | In-progress button text |
| `StatusNotStarted` | Not-started status (gray) |

### Filter
| Key | Description |
|-----|-------------|
| `ActiveFilterBorderColor` | Green border shown on active filter buttons |

### Toolbar
| Key | Description |
|-----|-------------|
| `ToolbarBackground` | ProgressView toolbar background (search/buttons bar) |
| `ToolbarForeground` | Toolbar text/icon color (light — must contrast dark toolbar) |
| `ToolbarHoverBackground` | Toolbar button hover |
| `ToolbarHoverForeground` | Toolbar button hover text |

### Status Bar
| Key | Description |
|-----|-------------|
| `StatusBarBackground` | Bottom status bar background |
| `StatusBarForeground` | Status bar text |

### Non-Owned Records
| Key | Description |
|-----|-------------|
| `NotOwnedRowBackground` | Background for records not assigned to current user |
| `NotOwnedRowForeground` | Dimmed text for non-owned records |

### Action Buttons
| Key | Description |
|-----|-------------|
| `ButtonSuccessBackground` | Green action button (Add, Save, Restore) |
| `ButtonSuccessBorder` | Green button border |
| `ButtonSuccessHover` | Green button hover |
| `ButtonDangerBackground` | Red action button (Delete, Remove) |
| `ButtonDangerBorder` | Red button border |
| `ButtonDangerHover` | Red button hover |
| `ButtonPrimaryBackground` | Blue/accent action button |
| `ButtonPrimaryBorder` | Primary button border |
| `ButtonPrimaryHover` | Primary button hover |

### Overlay
| Key | Description |
|-----|-------------|
| `OverlayBackground` | Modal overlay (semi-transparent, dark or light) |
| `OverlayText` | Overlay primary text |
| `OverlayTextSecondary` | Overlay secondary text |

### Error/Warning
| Key | Description |
|-----|-------------|
| `ErrorText` | Error message text (red) |
| `WarningText` | Warning message text (amber) |
| `WarningHighlight` | Warning highlight (red accent) |
| `WarningBoxBg` | Warning box background |
| `WarningBoxBorder` | Warning box border |
| `WarningHighlightBackground` | Semi-transparent warning highlight |

### Analysis Module
| Key | Description |
|-----|-------------|
| `AnalysisRedBg` | 0-25% complete range |
| `AnalysisOrangeBg` | >25-50% complete range |
| `AnalysisYellowBg` | >50-75% complete range |
| `AnalysisGreenBg` | >75-100% complete range |

### UI Elements
| Key | Description |
|-----|-------------|
| `SidebarBackground` | Side panel background |
| `SidebarBorder` | Side panel border |
| `DividerColor` | Section divider lines |
| `SplitterDots` | GridSplitter dot indicators |
| `DisabledText` | Disabled text (used by ProgressView filter button disabled state) |

### Action/Filter Button Foregrounds
| Key | Description |
|-----|-------------|
| `ActionButtonForeground` | Action button text on dark toolbar |
| `ActionFilterForeground` | Filter toggle button text |

### Sidebar Button Hover
| Key | Description |
|-----|-------------|
| `SidebarButtonHoverBorder` | Sidebar button border color on hover (ProgressView side panel) |
| `SidebarButtonHoverBackground` | Sidebar button background on hover |

### Independent Highlight Keys
These are decoupled from AccentColor so they can be tuned per theme for visibility:

| Key | Description |
|-----|-------------|
| `ScanButtonForeground` | SCAN button text color (often needs a bright color that pops) |
| `SummaryBudgetForeground` | Summary Budget stat value color |
| `SummaryEarnedForeground` | Summary Earned stat value color |
| `SummaryPercentForeground` | Summary % Complete stat value color |

### Elevation/Shadow
| Key | Type | Description |
|-----|------|-------------|
| `SidebarButtonBorder` | SolidColorBrush | Sidebar button border |
| `ButtonHoverForeground` | SolidColorBrush | Button text on hover |
| `ButtonShadowColor` | Color | Drop shadow color |
| `ButtonShadowOpacity` | sys:Double | Drop shadow opacity (0.85 dark, 0.15 light) |
| `TextShadowDepth` | sys:Double | Text shadow depth |
| `TextShadowBlurRadius` | sys:Double | Text shadow blur |
| `TextShadowColor` | Color | Text shadow color |
| `SummaryLabelColor` | SolidColorBrush | Summary section labels |
| `TextShadowOpacity` | sys:Double | Text shadow opacity |

### Filter Icon Colors
| Key | Type | Description |
|-----|------|-------------|
| `FilterIconColor` | Color | Grid filter icon default color (must be visible on GridHeaderBackground) |
| `FilterIconActiveColor` | Color | Grid filter icon when filter is active (red) |

These are `Color` not `SolidColorBrush` because they're used in Storyboard ColorAnimations (which require StaticResource).

### Styles (defined in theme files)
Each theme file includes two Button styles that get swapped with the theme:
- **Default Button style** (implicit, no x:Key) — used by all `<Button>` elements
- **ToolbarButtonStyle** (x:Key) — transparent background, for toolbar buttons

These use `StaticResource` internally (fine because the entire style is replaced on theme swap).

## Technical Constraints

### StaticResource vs DynamicResource
- **All consumer XAML files use DynamicResource** — enables live theme switching
- **Styles inside theme files use StaticResource** — the entire style is replaced on swap, so internal refs resolve correctly
- **SharedStyles.xaml uses DynamicResource** — it's NOT swapped, so its refs must be dynamic
- **Storyboard/ColorAnimation targets MUST use StaticResource** — WPF limitation. Affects `FilterIconColor` and `FilterIconActiveColor` in ProgressView and ScheduleView.

### SfSkinManager and Live Switching
- Views with Syncfusion grids subscribe to `ThemeManager.ThemeChanged` to re-apply `SfSkinManager.SetTheme()` on their grid controls
- Subscription must use `Loaded`/`Unloaded` pattern (not constructor), because `SfSkinManager.SetTheme()` triggers visual tree rebuilds that fire `Unloaded`
- ProgressView additionally toggles `RowStyleSelector` (null then restore) after SfSkinManager to force `RecordOwnershipRowStyleSelector` to re-read updated theme resources
- The grid is briefly hidden (`Opacity = 0`) during the transition to prevent flash of stale row colors

### SfLinearProgressBar.TrackColor
`TrackColor` on SfLinearProgressBar doesn't reliably accept DynamicResource binding in all contexts. Use `ProgressBarTrackColor` where it works; fall back to code-behind if needed.

### Line Endings
All generated theme files must use CRLF line endings. The generator script handles this via `[System.IO.File]::WriteAllText()` with CRLF-normalized content. Do NOT use `Out-File` which can produce mixed line endings.

## When to Create a New Key

Create a new key when you need to differentiate an element's color from the key it currently shares. Process:

1. Add the new key to ALL theme files with the same value as the key it's splitting from
2. Update the specific consumers to reference the new key
3. Build and verify — visually nothing should change
4. Now you can independently tune the new key per theme
5. Document the new key in this guide

Do NOT pre-allocate unused keys. Create them on demand when you actually need the split.
