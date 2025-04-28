using VirtualHealthAPI;

public class PatientProfileInput
{
    public string PatientId { get; set; } =  string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string BirthDate { get; set; } = default!; // mandatory 

    public string Email { get; set; } = string.Empty; // optional 
    public string PhoneNumber { get; set; } = string.Empty;


    public List<ConditionInput> PastConditions { get; set; } = new();
    public PractitionerInput Pcp { get; set; } = new();

    public string EmergencyContactFirstName { get; set; } = default!;
    public string EmergencyContactLastName { get; set; } = default!;

    public string EmergencyContactPhone { get; set; } = string.Empty;

    // Immunization History
    public List<ImmunizationInput> Immunizations { get; set; } = new();

    // Mental Health (Category = survey or exam PHQ9)
    public List<MentalHealthSurveyInput> MentalHealthAssessments { get; set; } = new();

    public PatientAddressInput PatientAddress  { get; set; }  = new PatientAddressInput();
    public List<SocialHistoryInput> SocialHistories { get; set; } = new();
    public List<LifestyleInput> LifestyleHistories { get; set; } = new();
    public List<VitalSignsInput> VitalSigns { get; set; } = new();
}

public class PatientAddressInput 
    {
    public string AddressLine1 { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
public class SocialHistoryInput
{
    public string BehaviorCode { get; set; } // LOINC code for Smoking Status
    public string BehaviorName { get; set; }
    public string StatusCode { get; set; }     // SNOMED code for Former smoker
    public string StatusDisplay { get; set; }
}

public class LifestyleInput
{
    public string LifestyleCode { get; set; }    // LOINC/SNOMED Code
    public string LifestyleName { get; set; }    // Exercise Frequency, Diet Habit
    public string Detail { get; set; }            // e.g., Exercises 3 times/week
}

public class ImmunizationInput
{
    /// <summary>
    /// CVX Code for the vaccine (e.g., "207" for COVID-19 Moderna vaccine, "140" for Influenza).
    /// </summary>
    public string VaccineCode { get; set; }

    /// <summary>
    /// Human-readable vaccine name (e.g., "COVID-19 Moderna", "Influenza 2023-24").
    /// </summary>
    public string Display { get; set; }

    /// <summary>
    /// Date when the vaccine was administered.
    /// </summary>
    public DateTime DateGiven { get; set; }

    /// <summary>
    /// Immunization status code: completed, entered-in-error, or not-done.
    /// </summary>
    public ImmunizationStatusCodes immunizationStatusCodes= ImmunizationStatusCodes.NotDone;
}

public enum ImmunizationStatusCodes
{
    Completed,
    EnteredInError,
    NotDone
}

public class ConditionInput
{
    public string Code { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
}

public class MentalHealthSurveyInput
{
    public string SurveyQuestionCode { get; set; }  // PHQ-9 question code or similar
    public string QuestionText { get; set; }
    public int Score { get; set; }                   // Score (e.g., 0-3 for PHQ-9 items)
}

public class ObservationResult
{
    /// <summary>
    /// Type of observation (e.g., "Heart Rate", "Blood Pressure", "HbA1c", "Smoking Status").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Observed value (e.g., "120 bpm", "6.8 %", "Former smoker").
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the observation was recorded.
    /// </summary>
    public string Timestamp { get; set; } = string.Empty;
}

public class VitalSignsInput
{
    public string Type { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public enum ObservationFilterType
{
    All,
    WearableVitals,
    SocialHistory,
    Lifestyle
}

public class PatientLabResultsOutput
{
    public List<ObservationSummary> GeneralLabs { get; set; } = new();
    public List<ObservationSummary> ImagingResults { get; set; } = new();
}

public class ObservationSummary
{
    public string CodeDisplay { get; set; } = string.Empty;  // Example: Heart rate
    public string CodeSystem { get; set; } = string.Empty;   // Example: http://loinc.org
    public string CodeValue { get; set; } = string.Empty;    // Example: 8867-4
    public string Categories { get; set; } = string.Empty;   // vital-signs, social-history etc.
    public string Value { get; set; } = string.Empty;         // 78 beats/min, 120 mmHg etc.
    public string EffectiveDateTime { get; set; } = string.Empty;  // 2025-04-27T10:00:00Z
}


public class VitalTrendResult
{
    public DateTime Timestamp { get; set; }
    public string Type { get; set; }
    public double Value { get; set; }
}

public class LabResultsInput
{
    public string PatientId { get; set; } = default!;
    public double? Hba1c { get; set; }
    public double? TotalCholesterol { get; set; }
    public double? Hdl { get; set; }
    public double? Ldl { get; set; }
    public double? Triglycerides { get; set; }
    public double? Hemoglobin { get; set; }
    public double? Wbc { get; set; }
    public DateTime? CollectedDateTime { get; set; }  // Optional timestamp
}


public class ImagingResultInput
{
    public string PatientId { get; set; } = default!;
    public string? ImagingType { get; set; }  // Example: "Chest X-ray"
    public string? LoincCode { get; set; }    // Example: "18748-4" (Chest X-ray Study)
    public string ResultSummary { get; set; } = default!;  // Example: "No signs of pneumonia."
    public DateTime? CollectedDateTime { get; set; }       // Optional
}
