namespace VANTAGE.Models
{
    public class AppSettings
    {
        public int SettingID { get; set; }
        public string SettingName { get; set; } = null!;
        public string SettingValue { get; set; } = null!;
        public string DataType { get; set; } = null!;
    }
}