namespace VANTAGE.Utilities
{
    // Single source of truth for which Activities-grid columns the Manage UDF Names
    // dialog is allowed to relabel. v1 = UDFs only; required-metadata fields stay
    // non-renameable until a centralized error-message helper exists.
    public static class ProgressRenameableColumns
    {
        public static readonly string[] UDFs =
        {
            "UDF1", "UDF2", "UDF3", "UDF4", "UDF5", "UDF6", "UDF7", "UDF8", "UDF9",
            "UDF10", "UDF11", "UDF12", "UDF13", "UDF14", "UDF15", "UDF16", "UDF17", "UDF20"
        };
    }
}
