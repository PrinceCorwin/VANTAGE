# Create Theme

Generate a new VANTAGE theme from 4 hex colors and a dark/light base.

## Trigger
When the user says "create theme", "new theme", or "generate theme".

## Instructions

1. **Gather inputs** — Ask the user for (if not already provided):
   - **Theme name** — e.g., "Ocean", "Forest", "Sunset" (PascalCase, no spaces)
   - **Dark or Light** base
   - **Primary color** (hex) — the background tone, the overall feel of the app
   - **Accent color** (hex) — the pop/highlight color for interactive elements
   - **Secondary color** (hex) — structural chrome: grid headers, toolbar tint
   - **Surface color** (hex) — controls, cards, dialogs, text inputs, panels

2. **Run the generator script:**
   ```
   powershell -ExecutionPolicy Bypass -File "Scripts/Generate-Theme.ps1" -ThemeName "<Name>" -Base "<Dark|Light>" -PrimaryHex "<#hex>" -AccentHex "<#hex>" -SecondaryHex "<#hex>" -SurfaceHex "<#hex>"
   ```
   Run from the repository root directory.

3. **Register the theme** — After the script generates the XAML file:

   a. Read `Utilities/ThemeManager.cs` and add the new theme:
      - Add entry to `ThemeMap` dictionary: `{ "<Name>", "FluentDark" }` (or `"FluentLight"` for light themes)
      - Add `"<Name>"` to the `AvailableThemes` array

   b. Read `Dialogs/ThemeManagerDialog.xaml` and `Dialogs/ThemeManagerDialog.xaml.cs`:
      - Add a new RowDefinition in the Grid
      - Add a new RadioButton (copy existing, update x:Name, Grid.Row, Background swatch, BorderBrush, Text, ToolTip)
      - In code-behind: add `else if` for the new theme in both the constructor (initial state) and `RbTheme_Checked` (selection logic)
      - Bump Grid.Row numbers for Info text and Close button

   c. Update `Help/manual.html` — add the new theme to the Themes section.

4. **Post-generation manual adjustments** — The script generates reasonable defaults, but these keys almost always need manual tuning after the user tests:

   ### Status Buttons (DO NOT CHANGE from base theme)
   The Complete, In Progress, and Not Started buttons must look identical across all themes of the same base type. The script hardcodes these from Dark/Light theme values. **Never derive these from the accent or any other palette color.**

   Keys that are locked to base theme:
   - `StatusGreen`, `StatusGreenBgBtn`, `StatusGreenFgBtn`
   - `StatusYellow`, `StatusYellowBg`, `StatusYellowFg`
   - `StatusRed`, `StatusRedBg`, `StatusRedBgBtn`, `StatusRedFgBtn`
   - `StatusInProgress`, `StatusInProgressBgBtn`, `StatusInProgressFgBtn`
   - `StatusNotStarted`, `StatusGoldBg`

   ### Independent Highlight Keys (likely need per-theme tuning)
   These keys are intentionally decoupled from AccentColor so they can be set to colors that pop against the theme's background:
   - `ScanButtonForeground` — SCAN button text. If accent is too muted, use a bright contrasting color (e.g., `#FF009AFC` bright blue works well on dark themes).
   - `SummaryBudgetForeground` — Budget stat value. Same consideration as scan button.
   - `SummaryEarnedForeground` — Earned stat value (defaults to green `#FF27AE60`).
   - `SummaryPercentForeground` — % Complete stat value (defaults to amber `#FFFFB400` for dark, red `#b63434` for light).
   - `SidebarButtonHoverBorder` — Sidebar button border on hover. If accent is too close to the background, pick a brighter color.
   - `SidebarButtonHoverBackground` — Sidebar button background on hover.

   ### Grid Row Backgrounds
   - `GridCellBackground` — Primary data row background (wired up via `RecordOwnershipRowStyleSelector`)
   - `GridAlternatingRowBackground` — Alternate data row background
   - These default to Surface-derived colors but often look better when matched to other structural colors in the theme (e.g., toolbar backgrounds) for visual consistency.

5. **Build and verify:**
   ```
   dotnet build
   ```
   Fix any build errors.

6. **Tell the user** to test by switching to the new theme in the app. The user will run the app from Visual Studio — do NOT run it from Claude Code.

7. **Iterate** — If the user wants color adjustments:
   - Edit specific keys directly in `Themes/<Name>Theme.xaml` (preferred for tweaks)
   - Or re-run the script to regenerate from scratch (NOTE: this overwrites ALL manual edits — re-apply them after)

## Color Tips for Users
- **Primary:** The hue that defines the app's background feel. For dark themes, pick any hue — the script darkens it. For light themes, it lightens it. Example: `#1B3A5C` for dark navy, `#4A90D9` for light blue.
- **Accent:** The pop color for interactive elements (buttons, links, toggles, progress bars). Pick something that contrasts with primary — complementary or analogous hues work well. Note: if accent is too muted, the Scan button, Budget stat, and sidebar hover effects will be hard to see — those can be overridden independently.
- **Secondary:** Structural chrome — grid headers, toolbar. A deeper shade of primary works, or a contrasting hue for visual interest.
- **Surface:** The "card" color — text inputs, dialogs, combo boxes, panels. For dark themes, typically a lighter shade than primary. For light themes, often white or a very light tint. This is what users see most when interacting with controls.

## File Reference
- **Generator script:** `Scripts/Generate-Theme.ps1`
- **Theme files:** `Themes/<Name>Theme.xaml`
- **Theme registration:** `Utilities/ThemeManager.cs`
- **Theme dialog:** `Dialogs/ThemeManagerDialog.xaml` + `.xaml.cs`
- **Row style selector:** `Styles/RecordOwnershipRowStyleSelector.cs` (uses `GridCellBackground` and `GridAlternatingRowBackground`)
- **Token reference:** `Themes/THEME_GUIDE.md`
