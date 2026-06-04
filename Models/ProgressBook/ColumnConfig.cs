using System.Text.Json.Serialization;

namespace VANTAGE.Models.ProgressBook
{
    // How the renderer should source the value for a given column.
    // Direct = pull straight from an Activity property by FieldName
    // Computed = derived value (e.g. REM MH = BudgetMHs - EarnMHsCalc)
    // EntryBox = handwritten field in the PDF (renders only the % glyph)
    public enum ColumnSourceKind
    {
        Direct = 0,
        Computed = 1,
        EntryBox = 2
    }

    // Configuration for a single column in the progress book layout.
    // Columns are rendered as a single ordered list (no zones) per the
    // 2026-06 refactor; the renderer reads SourceKind to decide how to
    // resolve the cell value.
    public class ColumnConfig
    {
        [JsonPropertyName("fieldName")]
        public string FieldName { get; set; } = string.Empty;

        // DEPRECATED: Width is now auto-calculated from content
        // Kept for backwards compatibility with saved layouts
        [JsonPropertyName("width")]
        public int Width { get; set; } = 10;

        // Display order in the layout (0-based)
        [JsonPropertyName("displayOrder")]
        public int DisplayOrder { get; set; }

        // How the renderer resolves this column's value. Defaults to Direct
        // so legacy saved layouts (which never wrote this field) keep
        // their existing Direct semantics on deserialize.
        [JsonPropertyName("sourceKind")]
        public ColumnSourceKind SourceKind { get; set; } = ColumnSourceKind.Direct;

        // Header text shown in the PDF column header row. Optional —
        // when null/empty the renderer falls back to FieldName. Lets us
        // render "REM MH" instead of "RemainingMHs", "CUR %" instead of
        // "PercentEntry", etc., without renaming underlying activity props.
        [JsonPropertyName("displayHeader")]
        public string? DisplayHeader { get; set; }
    }
}
