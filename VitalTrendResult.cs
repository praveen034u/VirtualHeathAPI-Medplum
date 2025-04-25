namespace VirtualHealthAPI
{
    public class VitalTrendResult
    {
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = default!;
        public double Value { get; set; }
    }

}
