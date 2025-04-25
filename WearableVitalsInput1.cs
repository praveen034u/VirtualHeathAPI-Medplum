namespace VirtualHealthAPI
{
    public class WearableVitalsInput
    {
        public string PatientId { get; set; } = default!;
        public int? HeartRate { get; set; }
        public int? Systolic { get; set; }
        public int? Diastolic { get; set; }
        public int? Spo2 { get; set; }
        public double? Temperature { get; set; }
        public int? Steps { get; set; }
    }
}