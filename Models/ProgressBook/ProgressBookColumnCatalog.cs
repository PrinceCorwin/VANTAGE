using System.Collections.Generic;

namespace VANTAGE.Models.ProgressBook
{
    // Single source of truth for Progress Book column metadata.
    // Maps an Activity FieldName (or synthetic key) to:
    //   - SourceKind: how the renderer resolves the cell value
    //                 (Direct   = Activity property by reflection,
    //                  Computed = derived value handled in the PDF generator,
    //                  EntryBox = handwriting cell — renders just the % glyph)
    //   - DisplayHeader: friendly label shown in both the UI column list and the PDF header row.
    //
    // Fields not present here resolve to (Direct, FieldName-as-is). No more .ToUpper()
    // ugliness in PDF headers for uncatalogued fields.
    //
    // Add an entry here when you want a new column to have a short label or a non-Direct
    // source kind. Both ProgressBooksView (UI column list, Add dropdown, layout migration)
    // and ProgressBookPdfGenerator (header text + value dispatch) read from this catalog.
    public static class ProgressBookColumnCatalog
    {
        // Synthetic FieldName for the Computed REM MH column. Not an Activity property —
        // the PDF generator resolves it as BudgetMHs - EarnMHsCalc.
        public const string RemainingMHsFieldName = "RemainingMHs";

        // Synthetic FieldName for the un-removable handwriting column. Not an Activity
        // property — the PDF generator renders it as a bold % glyph at the cell's left edge.
        public const string EntryBoxFieldName = "% ENTRY";

        private static readonly Dictionary<string, (ColumnSourceKind Kind, string DisplayHeader)> _entries
            = new()
        {
            // IDs
            ["ActivityID"]            = (ColumnSourceKind.Direct,   "Act ID"),
            // UniqueID intentionally absent — falls back to "UniqueID"

            // Tags / categories
            ["ROCStep"]               = (ColumnSourceKind.Direct,   "ROC"),
            ["Description"]           = (ColumnSourceKind.Direct,   "DESC"),
            ["PhaseCategory"]         = (ColumnSourceKind.Direct,   "PhaseCat"),
            ["SecondDwgNO"]           = (ColumnSourceKind.Direct,   "2ndDwgNo"),
            ["SecondActno"]           = (ColumnSourceKind.Direct,   "2ndActNo"),
            ["SchedActNO"]            = (ColumnSourceKind.Direct,   "ActNo"),
            ["WorkPackage"]           = (ColumnSourceKind.Direct,   "WP"),
            ["RespParty"]             = (ColumnSourceKind.Direct,   "RP"),

            // Progress values
            ["BudgetMHs"]             = (ColumnSourceKind.Direct,   "MHs"),
            ["Quantity"]              = (ColumnSourceKind.Direct,   "QTY"),
            [RemainingMHsFieldName]   = (ColumnSourceKind.Computed, "REM MH"),
            ["EarnMHsCalc"]           = (ColumnSourceKind.Direct,   "ERN MH"),
            ["EarnedQtyCalc"]         = (ColumnSourceKind.Direct,   "ERN QTY"),
            ["EarnQtyEntry"]          = (ColumnSourceKind.Direct,   "Qty Entry"),
            ["PercentCompleteCalc"]   = (ColumnSourceKind.Direct,   "% Comp"),
            // PercentEntry intentionally shares the "% Comp" label with PercentCompleteCalc:
            // they're the same value for Progress Book purposes, and showing the duplicate
            // label nudges a user who adds both to delete one.
            ["PercentEntry"]          = (ColumnSourceKind.Direct,   "% Comp"),

            // Handwriting cell — un-removable in the layout builder
            [EntryBoxFieldName]       = (ColumnSourceKind.EntryBox, "% ENTRY"),
        };

        // True when this FieldName has a catalog entry.
        public static bool Contains(string fieldName) => _entries.ContainsKey(fieldName);

        // SourceKind for a column. Falls back to Direct when uncatalogued.
        public static ColumnSourceKind GetSourceKind(string fieldName) =>
            _entries.TryGetValue(fieldName, out var meta) ? meta.Kind : ColumnSourceKind.Direct;

        // Display label for the UI column list and the PDF header row. Falls back to the
        // FieldName itself when uncatalogued — never .ToUpper() it.
        public static string GetDisplayHeader(string fieldName) =>
            _entries.TryGetValue(fieldName, out var meta) ? meta.DisplayHeader : fieldName;
    }
}
