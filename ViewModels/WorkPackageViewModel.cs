using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using VANTAGE.Models;
using VANTAGE.Repositories;
using VANTAGE.Interfaces;

namespace VANTAGE.ViewModels
{
    // ViewModel for WorkPackageView - handles data loading and state management
    // Complex editor logic remains in code-behind for now
    public class WorkPackageViewModel : INotifyPropertyChanged, IHelpAware
    {
        private List<FormTemplate> _formTemplates = new();
        private List<WPTemplate> _wpTemplates = new();
        private List<ProjectItem> _projects = new();
        private List<User> _users = new();
        private ObservableCollection<FormTemplate> _wpFormsList = new();

        private FormTemplate? _selectedFormTemplate;
        private WPTemplate? _selectedWPTemplate;
        private ProjectItem? _selectedProject;
        private UserItem? _selectedPkgManager;
        private UserItem? _selectedScheduler;
        private string _statusText = "Ready";
        private bool _hasUnsavedChanges;

        public event PropertyChangedEventHandler? PropertyChanged;

        // IHelpAware implementation
        public string HelpAnchor => "work-packages";
        public string ModuleDisplayName => "Work Packages";

        // Data collections
        public List<FormTemplate> FormTemplates
        {
            get => _formTemplates;
            set { _formTemplates = value; OnPropertyChanged(); }
        }

        public List<WPTemplate> WPTemplates
        {
            get => _wpTemplates;
            set { _wpTemplates = value; OnPropertyChanged(); }
        }

        public List<ProjectItem> Projects
        {
            get => _projects;
            set { _projects = value; OnPropertyChanged(); }
        }

        public List<User> Users
        {
            get => _users;
            set { _users = value; OnPropertyChanged(); }
        }

        public ObservableCollection<FormTemplate> WPFormsList
        {
            get => _wpFormsList;
            set { _wpFormsList = value; OnPropertyChanged(); }
        }

        // Selected items
        public FormTemplate? SelectedFormTemplate
        {
            get => _selectedFormTemplate;
            set { _selectedFormTemplate = value; OnPropertyChanged(); }
        }

        public WPTemplate? SelectedWPTemplate
        {
            get => _selectedWPTemplate;
            set { _selectedWPTemplate = value; OnPropertyChanged(); }
        }

        public ProjectItem? SelectedProject
        {
            get => _selectedProject;
            set { _selectedProject = value; OnPropertyChanged(); }
        }

        public UserItem? SelectedPkgManager
        {
            get => _selectedPkgManager;
            set { _selectedPkgManager = value; OnPropertyChanged(); }
        }

        public UserItem? SelectedScheduler
        {
            get => _selectedScheduler;
            set { _selectedScheduler = value; OnPropertyChanged(); }
        }

        // State
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set { _hasUnsavedChanges = value; OnPropertyChanged(); }
        }

        // Load templates from repository
        public async Task LoadTemplatesAsync()
        {
            FormTemplates = await TemplateRepository.GetAllFormTemplatesAsync();
            WPTemplates = await TemplateRepository.GetAllWPTemplatesAsync();
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
