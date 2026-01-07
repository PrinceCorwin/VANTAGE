using System.Windows.Controls;
using VANTAGE.Interfaces;

namespace VANTAGE.Views
{
    public partial class ProgressBooksView : UserControl, IHelpAware
    {
        public ProgressBooksView()
        {
            InitializeComponent();
        }

        // IHelpAware implementation
        public string HelpAnchor => "progress-books";
        public string ModuleDisplayName => "Progress Books";
    }
}