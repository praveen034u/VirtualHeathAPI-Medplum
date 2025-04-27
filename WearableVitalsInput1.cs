namespace VirtualHealthAPI
{
    public class WearableVitalsInput
    {
        public string PatientId { get; set; } = default!;
        public double? HeartRate { get; set; }
        public double? Systolic { get; set; }
        public double? Diastolic { get; set; }
        public double? Spo2 { get; set; }
        public double? Temperature { get; set; }
        public double? Steps { get; set; }
        public double? RespiratoryRate { get; set; }
        public double? BloodGlucose { get; set; }
        public double? CaloriesBurned { get; set; }
        public double? HeartRateVariability { get; set; }
        public double? Vo2Max { get; set; }
        public double? SkinTemperature { get; set; }
        public double? SleepDuration { get; set; }
        public double? SleepRestlessnessIndex { get; set; }
        public string? StressLevel { get; set; }
        public double? StepsGoalCompletion { get; set; }
        public int? OxygenDesaturationEvents { get; set; }
        public DateTime? CollectedDateTime { get; set; }
    }
}