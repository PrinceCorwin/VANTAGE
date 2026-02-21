# Theme System Refactor PRD

## Problem Statement
The current theme system has ~83 keys per theme, many overloaded across unrelated UI elements. Changing `AccentColor` affects button highlights, progress bars, spinners, and toggle states simultaneously. `ControlBackground` is used for grid cells, buttons, dialog bodies, and form controls. This makes per-element tweaking impossible and theme creation error-prone. Theme changes require an app restart.

## Goals
1. **Live theme switching** — themes apply instantly without restart (all refs use DynamicResource)
2. **Targeted token splits** — only split keys that are overloaded across unrelated UI regions; keep existing well-scoped keys as-is
3. **On-demand variable creation** — new per-object keys are created when actually needed, not pre-allocated
4. **User-created themes** — well-documented token system with a theme creation guide
5. **Phased rollout** — each phase testable independently, no big-bang risk

## Variable Philosophy
Most current keys are already well-scoped to a single UI region (`GridHeaderBackground`, `SidebarBackground`, `ToolbarBackground`, `ButtonSuccessBackground`, etc.). The problem is limited to a handful of keys that are shared across unrelated elements — `AccentColor`, `ControlBackground`, `ForegroundColor`, `DisabledColor`.

**Approach:** Split only those overloaded keys into region-specific variants. Do NOT mass-rename working keys for cosmetic consistency. New variables are created on-demand when you actually need to differentiate an element's color from its group — not pre-allocated with placeholder slots.

## Current State
- 3 themes: Dark, Light, Orchid (`Themes/DarkTheme.xaml`, `LightTheme.xaml`, `OrchidTheme.xaml`)
- ~83 keys per theme file (61 brushes + styles + Colors + Doubles)
- ~1,290 StaticResource refs across 42 XAML files, ~101 DynamicResource refs in 3 files
- `ThemeManager.cs` handles load/swap via `Application.Current.Resources.MergedDictionaries`
- `ThemeManagerDialog` saves preference, requires restart
- Local styles duplicated in ProgressView.xaml and ScheduleView.xaml
- Hardcoded colors in ~6 files (overlays, progress tracks, splitters)
- 4 missing keys referenced in FindReplaceDialog (`ContentBackground`, `ContentForeground`, `PrimaryBackground`, `PrimaryForeground`)
- SfSkinManager.SetTheme() called in 28 dialog/view constructors (one-time, doesn't update on theme change)

## Key Files
| File | Role |
|------|------|
| `Utilities/ThemeManager.cs` | Theme load/save/apply engine |
| `Themes/DarkTheme.xaml` | Dark theme (default) |
| `Themes/LightTheme.xaml` | Light theme |
| `Themes/OrchidTheme.xaml` | Orchid theme |
| `Dialogs/ThemeManagerDialog.xaml(.cs)` | Theme picker dialog |
| `App.xaml` | Default merged dictionaries |
| `Views/ProgressView.xaml` | Largest consumer (187 Static + 70 Dynamic refs), local styles |
| `Views/ScheduleView.xaml` | Second largest (136 Static + 18 Dynamic refs), local styles |

## Technical Constraints
- `Storyboard`/`ColorAnimation` targets require StaticResource (WPF limitation) — affects FilterToggleButton icon animation
- `SfLinearProgressBar.TrackColor` may not support DynamicResource — handle via code-behind
- `LoadingSplashWindow` displays before theme loads — must stay hardcoded (already done)
- Styles defined inside theme files (default Button, ToolbarButtonStyle) get replaced wholesale on dictionary swap, so their internal StaticResource refs work correctly
- Styles defined in local view files (ProgressView, ScheduleView) do NOT get replaced — their StaticResource trigger refs won't update on live theme switch

---

## Phase 1: Live Theme Switching Engine
**Goal:** Enable instant theme switching from the dialog. No visual changes to the app.

**Changes:**
- `ThemeManager.cs`:
  - Make `ApplyTheme()` public
  - Add `public static event Action<string>? ThemeChanged`
  - Fire event after applying theme
  - Walk `Application.Current.Windows` and call `SfSkinManager.SetTheme()` on each
- `ThemeManagerDialog.xaml`:
  - Change all StaticResource to DynamicResource
  - Remove "Theme will be applied when the application restarts" text
- `ThemeManagerDialog.xaml.cs`:
  - Call `ThemeManager.ApplyTheme()` live on radio button change (not just SaveTheme)
  - Re-apply SfSkinManager to self after swap
- All 3 theme files:
  - Add missing keys: `ContentBackground`, `ContentForeground`, `PrimaryBackground`, `PrimaryForeground`, `ProgressBarTrackColor`, `GridAlternatingRowBackground`

**Test:** Open ThemeManagerDialog, switch between all 3 themes — dialog updates instantly. Close dialog, verify main window background changed.

---

## Phase 2: StaticResource to DynamicResource Migration
**Goal:** Convert all ~1,290 StaticResource theme refs to DynamicResource. Mechanical find-replace, file-by-file.

**Order:** Dialogs (27 files) → MainWindow → Views (7 files)

**Leave as StaticResource:**
- Converters: `{StaticResource PercentToDecimalConverter}`, `{StaticResource BoolToVisibilityConverter}`, etc.
- Style BasedOn type refs: `{StaticResource {x:Type syncfusion:GridCell}}`
- Storyboard animation targets: `FilterIconColor`, `FilterIconActiveColor` in ProgressView
- Style references themselves: Will be handled in Phase 4 when styles move to SharedStyles.xaml

**Test each batch:** Switch themes via dialog, verify all colors update. Check VS Output for binding errors.

---

## Phase 3: Hardcoded Color Elimination
**Goal:** Replace hardcoded hex colors with theme resources.

| File | Hardcoded | Replace With |
|------|-----------|-------------|
| `Dialogs/AdminSnapshotsDialog.xaml` | `#CC1E1E1E` overlay | `OverlayBackground` (exists) |
| `Dialogs/ManageProgressLogDialog.xaml` | `#CC1E1E1E` overlay | `OverlayBackground` |
| `Dialogs/SyncDialog.xaml` | `#333333` track | `ProgressBarTrackColor` (from Phase 1) |
| `MainWindow.xaml` | `#333333` track | `ProgressBarTrackColor` |
| `Views/SidePanelView.xaml` | `#666666` foreground | `AppForegroundSecondary` |
| `Views/AnalysisView.xaml` | Splitter hex fills | `SplitterDots` (existing) |

**Skip intentionally:** LoadingSplashWindow, ThemeManagerDialog swatches, installer, Excel/email HTML colors.

---

## Phase 4: Extract Local Styles to SharedStyles.xaml
**Goal:** Centralize duplicate view-local styles. Ensure template triggers use DynamicResource for live switching.

**Create `Themes/SharedStyles.xaml`** containing styles extracted from:

**From ProgressView.xaml:**
- `RoundedButtonStyle` (CornerRadius="15")
- `SidebarButtonStyle` (CornerRadius="3")
- 7 required-field cell styles (CompTypeRequired, PhaseCodeRequired, ROCStepRequired, DescRequired, etc.)
- Base GridCell style (BorderThickness, Padding)
- GridHeaderCellControl style

**From ScheduleView.xaml:**
- `FilterToggleButtonStyle`
- `ActionFilterToggleStyle`
- `HorizontalGridSplitterStyle`
- Required-field cell styles, base GridCell style

**Register** in `App.xaml` MergedDictionaries AFTER the theme dictionary. `ThemeManager.ApplyTheme()` already only removes dictionaries matching `Syncfusion.Themes.` and `Themes/*Theme.xaml` — SharedStyles.xaml is preserved.

**Test:** All buttons, toggles, splitters render identically to before. Switch themes — hover/pressed trigger states update correctly.

---

## Phase 5: Syncfusion Live Switching for Grids/Controls
**Goal:** Syncfusion components (SfDataGrid, SfBusyIndicator, SfLinearProgressBar) update on theme change.

**Approach:**
- `ThemeManager.ApplyTheme()` already walks windows (from Phase 1)
- Views with Syncfusion grids subscribe to `ThemeManager.ThemeChanged` to re-apply skin on their specific grid controls

**Files:** ThemeManager.cs + all view code-behind files (ProgressView, ScheduleView, AnalysisView, WorkPackageView, ProgressBooksView, DeletedRecordsView)

**Test:** Open ProgressView → switch theme → grid headers, scrollbars, row colors all update live. Same for ScheduleView dual grids.

---

## Phase 6: Targeted Overloaded Token Splits
**Goal:** Split only the keys that are shared across unrelated UI regions, so changing one region doesn't break another. No mass renames.

**Strategy:** Add the new region-specific key to all 3 theme files (same color value as the original), update consumers for that region to use the new key, build and test. The original key stays for its remaining consumers.

**Split `AccentColor` into:**
| Key | Used for |
|-----|----------|
| `AccentColor` | keep — primary accent, links, highlights |
| `ProgressBarAccent` | progress bar fill color |
| `ToggleCheckedBackground` | checked toggle/switch state |

**Split `ControlBackground` into:**
| Key | Used for |
|-----|----------|
| `ControlBackground` | keep — form controls, combo boxes, text inputs |
| `DialogBackground` | dialog body backgrounds |
| `GridCellBackground` | data grid cell backgrounds |

**Split `ForegroundColor` into:**
| Key | Used for |
|-----|----------|
| `ForegroundColor` | keep — primary text, labels |
| `DialogForeground` | dialog text |
| `GridCellForeground` | grid cell text |

**Split `DisabledColor` into:**
| Key | Used for |
|-----|----------|
| `DisabledColor` | keep — disabled control backgrounds/borders |
| `DisabledForeground` | disabled text (currently `DisabledText` already exists, just verify it's used consistently) |

All new keys start with the same color value as the original — visually nothing changes. This adds ~6-8 new keys, bringing the total to ~90 per theme file.

**Test:** Switch themes, verify no visual differences. Then intentionally change one new key's value in a single theme to confirm it only affects the intended region.

---

## Phase 7: Theme Guide
**Goal:** Document the token system for future theme creation.

**Create `Themes/THEME_GUIDE.md`:**
- Full token reference with descriptions and which UI elements each key controls
- Categorized sections (Core, Window, Control, Grid, Status, Button, Overlay, Shadow, etc.)
- Color type guide (SolidColorBrush vs Color vs sys:Double)
- Step-by-step: how to create a new theme by copying an existing theme file
- Syncfusion base theme mapping (FluentDark vs FluentLight)
- Guidance on when to create a new variable vs. reuse an existing one

**Future variable creation policy:** When you need to differentiate an element's color from its group, create a new key at that time. Add it to all theme files with the same value as the key it's splitting from, update the specific consumers, and document it in the theme guide. No pre-allocation of unused keys.

---

## Risks & Mitigations
| Risk | Mitigation |
|------|-----------|
| Storyboard animations require StaticResource | Leave FilterToggleButton ColorAnimation as StaticResource (minor visual, template reloads on swap anyway) |
| SfLinearProgressBar TrackColor may not support DynamicResource | Set via code-behind in ThemeChanged handler |
| Phase 6 split misses a consumer | New keys start with same color as original — worst case, an element still uses the old shared key and looks correct, just isn't independently tunable yet |
| Syncfusion visual glitch during live switch | Apply SfSkinManager immediately after dictionary swap in ThemeManager |
| Performance overhead from ~1,290 DynamicResource refs | Negligible in WPF; Syncfusion grids virtualize cells |

## Verification (per phase)
- `dotnet build` — zero errors
- Switch between all 3 themes via dialog
- Visually verify: Progress, Schedule, Analysis, WorkPackage, ProgressBooks, DeletedRecords views
- Open 2-3 dialogs and verify colors
- Check VS Output window for binding errors
- Test hover/pressed/disabled states on buttons and toggles
