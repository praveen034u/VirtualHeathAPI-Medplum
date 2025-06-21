namespace VirtualHealthAPI
{
    public class AlarmNotification
    {
        public string PatientId { get; set; }
        public string AlarmType { get; set; }

        public TriggeredItem TriggeredBy { get; set; } // Optional, who triggered the alarm

        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public AlarmNotification(string patientId, string alarmType, TriggeredItem triggeredBy, DateTime timestamp, string message)
        {
            PatientId = patientId;
            AlarmType = alarmType;
            TriggeredBy = triggeredBy;
            Timestamp = timestamp;
            Message = message;
        }
        public override string ToString()
        {
            return $"PatientId: {PatientId}, AlarmType: {AlarmType}, Timestamp: {Timestamp}, Message: {Message}";
        }
    }

    public class TriggeredItem
    {
        public int HeartRate { get; set; }
        public int Spo2 { get; set; }
        public int RespiratoryRate { get; set; }
        public int BloodGlucose { get; set; }
    }
}
