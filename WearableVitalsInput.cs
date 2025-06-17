namespace VirtualHealthAPI
{
    public class WearableVitalsInput
    {
        public string PatientId { get; set; } = default!;
        public string DeviceId { get; set; } = default!;
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
        public double? Vo2Max { get; set; }//  40590-2
        public double? SkinTemperature { get; set; }
        public double? SleepDuration { get; set; }
        public double? SleepRestlessnessIndex { get; set; }
        public double? StepsGoalCompletion { get; set; }
        public int? OxygenDesaturationEvents { get; set; }
        public DateTime? CollectedDateTime { get; set; }
    }

   public class ProvidersReportedObservationsInput
   {
        public string PatientId { get; set; } = default!;
        public string ProviderId { get; set; } = default!;
        public int? Phq9Score { get; set; }
        public string? PhysicalExamFinding { get; set; }
        public string? SmokingStatus { get; set; }
        public string? AlcoholUse { get; set; }
        public string? StressLevel { get; set; }
        public string? Occupation { get; set; }
        public string? ExerciseFrequency { get; set; }
        public string? DietHabits { get; set; }
        public DateTime? CollectedDateTime { get; set; }
   }
        public enum NotificationPreference
        {
            Email,
            SMS,
            PushNotification,
            VoiceCall   
        }

        public class NotificationRequest
        {
            public string PatientId { get; set; } = default!;
            public string Message { get; set; } = default!;
            public string NotificationType { get; set; } = default!;
            public string emailId { get; set; } = default!; // Optional, if you want to send email notifications
            public string PhoneNumber { get; set; } = default!; // Optional, if you want to send SMS notifications
            public string ProviderId { get; set; } = default!; // Optional, if you want to notify a specific provider
            public string DeviceId { get; set; } = default!; // Optional, if you want to send push notifications to a specific device
            public NotificationPreference NotificationPreference { get; set; } = NotificationPreference.SMS; // Default preference
            public DateTime? NotificationDateTime { get; set; } // Optional, if you want to schedule the notification
        }
}