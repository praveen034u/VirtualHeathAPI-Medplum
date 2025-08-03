using System.Text.Json.Serialization;

public class AlarmNotification
{
    public string AlertId { get; set; }
    public string PatientId { get; set; }
    public string DeviceId { get; set; }
    public DateTime Timestamp { get; set; }
    public string AlertType { get; set; }
    public TriggeredBy TriggeredBy { get; set; }
    public Thresholds Thresholds { get; set; }
    public string Message { get; set; }
    public Notified Notified { get; set; }
    public RawVitals RawVitals { get; set; }
}

public class TriggeredBy
{
    public int HeartRate { get; set; }
    public int Spo2 { get; set; }
    public int RespiratoryRate { get; set; }
    public int BloodGlucose { get; set; }
}

public class Thresholds
{
    public MinMax HeartRate { get; set; }
    public MinMax Spo2 { get; set; }
    public MinMax RespiratoryRate { get; set; }
    public MinMax BloodGlucose { get; set; }
}

public class MinMax
{
    public double Min { get; set; }
    public double? Max { get; set; }
}

public class Notified
{
    public Contact Pcp { get; set; }
    public Contact EmergencyContact { get; set; }
}

public class Contact
{
    public string Name { get; set; }
    public string Email { get; set; } // For PCP
    public string Phone { get; set; } // For emergency contact
    public string Status { get; set; }
}

public class RawVitals
{
    public int HeartRate { get; set; }
    public int Spo2 { get; set; }
    public int RespiratoryRate { get; set; }
    public int BloodGlucose { get; set; }
    public double? Temperature { get; set; }
    public DateTime Timestamp { get; set; }
}

public class AlarmResponseWrapper
{
    [JsonPropertyName("message")]
    public List<AlarmNotification> Message { get; set; }
}