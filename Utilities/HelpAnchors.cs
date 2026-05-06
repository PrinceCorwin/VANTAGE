namespace VANTAGE.Utilities
{
    // Central registry of help-manual anchor IDs.
    // Each constant maps to an id="..." on an element in Help/manual.html.
    // Info icons reference these constants instead of raw strings so that renames
    // are caught at compile time and a self-test can verify every anchor exists.
    public static class HelpAnchors
    {
        public const string WPNamePattern = "wp-name-pattern";
    }
}
