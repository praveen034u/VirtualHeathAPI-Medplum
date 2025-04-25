using VirtualHealthAPI;

public class PatientProfileInput
{
    public string PatientId { get; set; } = default!;
    public string PatientName { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Gender { get; set; } = default!;
    public string BirthDate { get; set; } = default!;
    public List<ConditionInput> Conditions { get; set; } = new();
    public PractitionerInput Pcp { get; set; } = new();
    public List<VitalSignsInput> Vitals { get; set; } = new();
}

public class ConditionInput
{
    public string Code { get; set; } = default!;
    public string Display { get; set; } = default!;
}

public class VitalSignsInput
{
    public string Type { get; set; } = default!;
    public double Value { get; set; }
    public string Unit { get; set; } = default!;
    public DateTime Timestamp { get; set; }
}