namespace VANTAGE.Models
{
    // Simple label/value pair for chart visualizations in the Analysis module
    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }
}
