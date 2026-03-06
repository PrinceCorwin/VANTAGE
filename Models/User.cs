using System.ComponentModel;

namespace VANTAGE.Models
{
    public class User : INotifyPropertyChanged
    {
        private int _userId;
        private string _username = string.Empty;
        private string _fullName = string.Empty;
        private string _email = string.Empty;
        private bool _isAdmin;
        private bool _isEstimator;
        private bool _isManager;

        public int UserID
        {
            get => _userId;
            set { _userId = value; OnPropertyChanged(nameof(UserID)); }
        }

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(nameof(Username)); }
        }

        public string FullName
        {
            get => _fullName;
            set { _fullName = value; OnPropertyChanged(nameof(FullName)); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(nameof(Email)); }
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            set { _isAdmin = value; OnPropertyChanged(nameof(IsAdmin)); }
        }

        public bool IsEstimator
        {
            get => _isEstimator;
            set { _isEstimator = value; OnPropertyChanged(nameof(IsEstimator)); }
        }

        public bool IsManager
        {
            get => _isManager;
            set { _isManager = value; OnPropertyChanged(nameof(IsManager)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}