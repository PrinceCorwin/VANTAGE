namespace VANTAGE.Interfaces
{
    // Interface for ViewModels to provide context-aware help navigation
    public interface IHelpAware
    {
        // Anchor ID for HTML help navigation (e.g., "progress-module", "schedule-module")
        string HelpAnchor { get; }

        // Display name shown in sidebar header (e.g., "Progress Module", "Schedule")
        string ModuleDisplayName { get; }
    }
}