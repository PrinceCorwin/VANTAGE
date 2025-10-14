namespace VANTAGE.Utilities
{
    public static class ThemeManager
    {
        // === Font Settings ===
        public static string FontFamilyPrimary { get; set; } = "Segoe UI";
        public static string FontFamilySecondary { get; set; } = "Segoe UI Semibold";
        public static double FontSizeSmall { get; set; } = 12;
        public static double FontSizeNormal { get; set; } = 14;
        public static double FontSizeLarge { get; set; } = 18;

        // === Color Palette ===
        public static string BackgroundColor { get; set; } = "#FF1E1E1E";
        public static string ForegroundColor { get; set; } = "#FFFFFFFF";
        public static string TextColorPrimary { get; set; } = "#FFFFFFFF";
        public static string TextColorSecondary { get; set; } = "#FFB0B0B0";
        public static string AccentColor { get; set; } = "#FF0078D7";
        public static string BorderColor { get; set; } = "#FF3C3C3C";
        public static string DisabledColor { get; set; } = "#FF6A6A6A";
        public static string HoverColor { get; set; } = "#220078D7";
        public static string SelectedColor { get; set; } = "#330078D7";

        // === Window Elements ===
        public static string WindowBackground { get; set; } = "#FF1C1C1C";
        public static string ControlBackground { get; set; } = "#FF2A2A2A";
        public static string ControlForeground { get; set; } = "#FFFFFFFF";
        public static string ControlBorder { get; set; } = "#FF3A3A3A";
        public static string ControlHoverBackground { get; set; } = "#FF333333";

        // === DataGrid Colors ===
        public static string GridAlternatingRowBackground { get; set; } = "#FF252525";
        public static string GridHeaderBackground { get; set; } = "#FF2A2A2A";
        public static string GridHeaderForeground { get; set; } = "#FFFFFFFF";
        public static string GridSelectedRowBackground { get; set; } = "#330078D7";
        public static string GridGridLineColor { get; set; } = "#FF3C3C3C";

        // === Status Colors ===
        public static string StatusGreen { get; set; } = "#FF27AE60";      // My records
        public static string StatusGray { get; set; } = "#FF7F8C8D";       // Others' records
        public static string StatusYellow { get; set; } = "#FFF39C12";     // Modified/unsaved
        public static string StatusRed { get; set; } = "#FFE74C3C";        // Errors
        public static string StatusComplete { get; set; } = "#FF27AE60";   // 100% complete
        public static string StatusInProgress { get; set; } = "#FF3498DB"; // 0-99% complete
        public static string StatusNotStarted { get; set; } = "#FF95A5A6"; // 0% complete

        // === Toolbar Colors ===
        public static string ToolbarBackground { get; set; } = "#FF252525";
        public static string ToolbarButtonHover { get; set; } = "#FF333333";
        public static string ToolbarButtonActive { get; set; } = "#FF0078D7";

        // === StatusBar Colors ===
        public static string StatusBarBackground { get; set; } = "#FF1C1C1C";
        public static string StatusBarForeground { get; set; } = "#FFB0B0B0";

        // === Shadows / Elevation ===
        public static double ShadowOpacity { get; set; } = 0.25;
        public static double ShadowDepth { get; set; } = 4.0;

        // === Corner Radius / Layout ===
        public static double CornerRadiusSmall { get; set; } = 4.0;
        public static double CornerRadiusLarge { get; set; } = 12.0;
        public static double SpacingSmall { get; set; } = 4.0;
        public static double SpacingNormal { get; set; } = 8.0;
        public static double SpacingLarge { get; set; } = 16.0;

        // === Border Thickness ===
        public static double BorderThickness { get; set; } = 1.0;
        public static double BorderThicknessAccent { get; set; } = 2.0;
    }
}