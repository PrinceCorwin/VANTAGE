namespace VANTAGE.Models
{
    public class UserSettings
    {
        public int UserSettingID { get; set; }
        public int UserID { get; set; }
        public string SettingName { get; set; }
        public string SettingValue { get; set; }
        public string DataType { get; set; }
    }
}