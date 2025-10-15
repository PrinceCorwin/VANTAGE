using System.ComponentModel;

namespace VANTAGE.Models
{
    public class User : INotifyPropertyChanged
    {
        private int _userId;
        private string _username;
        private string _fullName;
        private string _email;
        private string _phoneNumber;
        private bool _isAdmin;
        private string _adminToken;

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

        public string PhoneNumber
        {
            get => _phoneNumber;
            set { _phoneNumber = value; OnPropertyChanged(nameof(PhoneNumber)); }
        }

        public bool IsAdmin
        {
            get => _isAdmin;
            set { _isAdmin = value; OnPropertyChanged(nameof(IsAdmin)); }
        }

        public string AdminToken
        {
            get => _adminToken;
            set { _adminToken = value; OnPropertyChanged(nameof(AdminToken)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}