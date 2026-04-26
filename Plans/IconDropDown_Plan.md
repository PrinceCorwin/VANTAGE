# Plan: Custom IconDropDown Control

## Context

VANTAGE has multiple sidebar/toolbar dropdowns: Actions (Progress sidebar), USER filters (Progress sidebar), File / Tools / Admin (MainWindow toolbar). The Tools/Admin path uses Syncfusion `DropDownButtonAdv` + `DropDownMenuItem`, which themes cleanly via `SfSkinManager` but does not natively render emoji/SVG icons next to item labels in a layout we control.

The Progress Actions menu started as a WPF `Button + ContextMenu`. Adding icons via `<MenuItem.Icon>` worked, but customizing the visual to match Tools/Admin required overriding `ControlTemplate`, which dragged in opaque WPF chrome (the persistent white bar above the icon column we couldn't isolate). Switching to `DropDownButtonAdv` cleaned the visual but cost icon support and several iterations to re-theme.

The right answer: stop fighting WPF MenuItem chrome and build a small, dedicated dropdown control we own end-to-end. It supports icons, themes via the same `DynamicResource` keys as the rest of the app, and behaves the way users have requested (Select All keeps menu open, hover-out auto-close, submenus). Replace btnActions first; once it's solid, migrate the USER filter button. Tools/Admin in MainWindow stay on `DropDownButtonAdv` (icon-less, working fine there).

## Design

### Files to create

| File | Purpose |
|---|---|
| `Controls/IconDropDown.xaml` + `.xaml.cs` | UserControl: trigger button + popup hosting items |
| `Controls/IconDropDownItem.cs` | Plain CLR model for one menu item (Icon, Label, Click handler, optional submenu, IsDestructive flag) |
| `Controls/IconDropDownItemTemplate.xaml` | `ResourceDictionary` with the item visual template, themed via `DynamicResource` |

### Item model (`IconDropDownItem.cs`)

```csharp
public class IconDropDownItem
{
    public string Icon { get; set; } = "";              // Unicode/emoji string; empty hides icon column for that row
    public string Label { get; set; } = "";
    public string? ToolTip { get; set; }
    public Action? Click { get; set; }                   // simpler than ICommand for our use; no DataContext to reason about
    public bool IsDestructive { get; set; }              // red foreground (Delete Row(s))
    public bool IsSeparator { get; set; }                // render as a thin themed line, no click
    public bool KeepOpenOnClick { get; set; }            // for Select All
    public ObservableCollection<IconDropDownItem>? SubItems { get; set; }
}
```

### IconDropDown UserControl

XAML structure:
- `Button` trigger (matches `SidebarButtonStyle` so it fits in the sidebar)
  - StackPanel content: `Label` text + ` ▼` chevron
- `Popup` triggered open by `IsDropDownOpen` dependency property
  - Themed `Border` (Background = `ControlBackground`, BorderBrush = `ControlBorder`, BorderThickness = 1)
  - `ItemsControl` bound to `Items` dependency property
    - `ItemTemplate` is a Grid: 3 columns (Icon Auto, Label *, SubmenuArrow Auto)
    - Item rendered as a `Button` styled flat (no border, transparent), with hover background = `ControlHoverBackground`
    - When `IsSeparator = true`, item template switches to a thin `Border` (themed)
    - When `SubItems != null`, render `▶` and host a child `IconDropDown` as submenu (recursive)

DependencyProperties on `IconDropDown`:
- `Label` (string)
- `Items` (`ObservableCollection<IconDropDownItem>`)
- `IsDropDownOpen` (bool)
- `ToolTipText` (string?) — for the trigger button

Behaviors implemented in code-behind:
- Click on trigger → toggle `IsDropDownOpen`
- Click on an item → invoke `item.Click`, then `IsDropDownOpen = false` unless `item.KeepOpenOnClick = true` (handles Select All staying open)
- `Popup.Opened` → start a 150ms `DispatcherTimer` polling `IsMouseOver` on the popup `Border`. If `IsMouseOver = false` for ≥400ms cumulative, set `IsDropDownOpen = false` (handles hover-out auto-close)
- `Popup.Closed` → stop the timer
- Submenu: nested `IconDropDown` activated on parent item hover, opens to the right (Placement="Right")

### Theming

Every brush comes from `DynamicResource` keys defined in all four themes (`ControlBackground`, `ForegroundColor`, `ControlBorder`, `ControlHoverBackground`, `DisabledText`, `ErrorText`). No `SfSkinManager.SetTheme` call needed — the control isn't a Syncfusion control, so it themes through the same path as the rest of the sidebar. Theme change works automatically because all bindings are dynamic.

### Rollout

**Phase 1 (this work):** Build the control + replace btnActions in ProgressView. Verify across all four themes, including all 7 actions (Select All, Delete, Copy Visible, Copy All, Duplicate, Add Blank, Export).

**Phase 2 (follow-up):** Migrate the USER filter button in the same sidebar. It already uses Button + ContextMenu and would benefit from the same hover-out auto-close.

**Phase 3 (defer until/unless asked):** MainWindow Tools/Admin — only worth migrating if you want hover-out auto-close on those too. They look fine on `DropDownButtonAdv` because MainWindow has SfSkinManager scoped to the window root.

## Critical files

- New: `Controls/IconDropDown.xaml`, `Controls/IconDropDown.xaml.cs`, `Controls/IconDropDownItem.cs`
- Modified: `Views/ProgressView.xaml` — replace the `btnActions` `DropDownButtonAdv` block (lines ~498–528) with a single `<controls:IconDropDown>` element bound to a list built in code-behind
- Modified: `Views/ProgressView.xaml.cs` — build the `Items` collection once in the constructor (or as a property), routing each item's `Click` to the existing handlers (`MenuSelectAll_Click`, `DeleteSelectedActivities_Click`, etc.). Drop the `SfSkinManager.SetTheme(btnActions, ...)` calls (no longer needed)
- New (likely needed): namespace alias `xmlns:controls="clr-namespace:VANTAGE.Controls"` in `ProgressView.xaml`

## Verification

1. Theme cycle through Dark / Light / Orchid / DarkForest — items, hover state, submenu, separator, and disabled state all retheme cleanly.
2. Click each of the 7 actions — same behavior as today (Select All multi-row fix from earlier session still upstream and unaffected).
3. Open menu, click Select All → menu stays open with rows now selected.
4. Open menu, hover Copy Row(s) → submenu opens to the right with Visible / All options.
5. Open menu, move cursor away → menu closes after ~400ms (the deferred behavior we wanted earlier).
6. Open menu, click outside → menu closes immediately (standard).
7. Multi-theme: build doesn't regress; visually consistent.

## Estimated scope

- Control XAML: ~150 lines
- Control code-behind: ~80 lines (DependencyProperties, click routing, hover-poll timer, submenu show/hide)
- Item model: ~25 lines
- ProgressView wiring (build Items list, replace btnActions): ~40 lines

Roughly half a day of focused work, plus iteration to nail the visual polish.

## Open questions

1. **Icon source format** — emoji Unicode strings (current) keeps things simple and theme-agnostic. Alternative is SVG via SharpVectors (already a project dependency) or PNG resources. Recommend stay with emoji until/unless the team wants vector icons.
2. **Trigger button style** — match `SidebarButtonStyle` exactly (drop-shadow, border, sizing) so Actions sits visually alongside Columns/Clear Filters/Assign? Yes by default, but worth confirming.
3. **Icon column width** — fixed (e.g., always 24px so labels align across items) or auto (collapses when no icon)? Recommend fixed 24px for visual consistency, with the option to set `Icon = ""` to leave the cell empty but keep alignment.
4. **Submenu behavior** — open on hover or only on click? Tools menu opens on hover; recommend matching that.
5. **Keyboard nav** — required (Tab/arrow keys, Enter to invoke, Esc to close)? Standard accessibility but adds ~30 lines. Defer unless you need it.

Once these are answered I can execute Phase 1 directly.
