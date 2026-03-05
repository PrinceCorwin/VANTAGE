using System.Threading.Tasks;

namespace VANTAGE.Services.Plugins
{
    // Interface that all VANTAGE plugins must implement
    public interface IVantagePlugin
    {
        // Unique plugin identifier (must match manifest id)
        string Id { get; }

        // Display name
        string Name { get; }

        // Called when the plugin is loaded - set up UI elements, register handlers
        void Initialize(IPluginHost host);

        // Called when the plugin is being unloaded - clean up resources
        void Shutdown();
    }
}
