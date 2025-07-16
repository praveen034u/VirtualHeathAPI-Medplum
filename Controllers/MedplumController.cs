using Microsoft.AspNetCore.Mvc;
using VirtualHealthAPI;

[ApiController]
[Route("api/[controller]")]
public class MedplumController : ControllerBase
{
    private readonly MedplumService _medplum;
    private readonly PromptLibraryService _promptLibrary;
    private readonly GeminiService _geminiService;

    public MedplumController(MedplumService medplum, PromptLibraryService promptLibrary, GeminiService geminiService)
    {
        _medplum = medplum;
        _promptLibrary = promptLibrary;
        _geminiService = geminiService;
    }

    [HttpPost("create-profile-with-pcp-and-vitals")]
    public async Task<IActionResult> CreateWithVitals([FromBody] PatientProfileInput input)
    {
        var result = await _medplum.CreatePatientWithPcpAndVitalsAsync(input);
        return Ok(new { message = result });
    }

    [HttpPost("create-patient-profile")]
    public async Task<IActionResult> CreateProfile([FromBody] PatientProfileInput input)
    {
        var result = await _medplum.UpsertPatientProfileAsync(input);
        return Ok(new { message = result });
    }

    [HttpPost("update-patient-profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] PatientProfileInput input)
    {
        var result = await _medplum.UpsertPatientProfileAsync(input);
        return Ok(new { message = result });
    }

    [HttpPost("ingest-wearable-observations-hourly-ehr")]
    public async Task<IActionResult> IngestVitalsHourly([FromBody] WearableVitalsInput input)
    {
        var result = await _medplum.IngestWearableObservationsEHRSystemAsync(input);
        return Ok(new { message = result });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok("API is up and running!");
    }

    [HttpGet("current-observations/{patientId}")]
    public async Task<IActionResult> GetCurrentVitals(string patientId)
    {
        var results = await _medplum.GetPatientObservationsAsync(patientId);
        return Ok(results);
    }

    [HttpGet("health-prediction-by-observations/{patientId}")]
    public async Task<IActionResult> GetPredictionUsingAI(string patientId)
    {
        var results = await _medplum.GetPredictionUsingAIAsync(patientId);
        return Ok(results);
    }

    [HttpPost("ingest-wearable-observations-realtime-datastore")]
    public async Task<IActionResult> IngestVitalsRealtime([FromBody] WearableVitalsInput input)
    {
        var result = await _medplum.IngestWearableObservationsDataStoreAsync(input);
        return Ok(new { message = result });
    }

    [HttpGet("vitals-trend/{patientId}")]
    public async Task<IActionResult> GetVitalsTrend(string patientId)
    {
        var results = await _medplum.GetVitalsTrendAsync(patientId);
        return Ok(results);
    }

    [HttpGet("patient-full-profile/{emailId}")]
    public async Task<IActionResult> GetFullProfile(string emailId)
    {
        var result = await _medplum.GetPatientFullProfileByEmailAsync(emailId);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpGet("patient-full-profileByPatientId/{patientId}")]
    public async Task<IActionResult> GetFullProfileByPatientId(string patientId)
    {
        var result = await _medplum.GetPatientFullProfileByPatientIdAsync(patientId);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost("ingest-general-lab-results-observations")]
    public async Task<IActionResult> IngestGeneralLabResults([FromBody] LabResultsInput input)
    {
        var result = await _medplum.SaveLabResultsAsync(input);
        return Ok(new { message = result });
    }

    [HttpPost("ingest-imaging-lab-results-observations")]
    public async Task<IActionResult> IngestImagingLabResults([FromBody] ImagingResultInput input)
    {
        var result = await _medplum.SaveImagingResultAsync(input);
        return Ok(new { message = result });
    }

    [HttpPost("ingest-provider-reported-observations")]
    public async Task<IActionResult> IngestProviderReportedObservations([FromBody] ProvidersReportedObservationsInput input)
    {
        var result = await _medplum.SaveProvidersReportedObservationsAsync(input);
        return Ok(result);
    }

    //[HttpGet("provider-reported-observations/{patientId}")]
    //public async Task<IActionResult> GetProviderReportedObservations(string patientId)
    //{
    //    var result = await _medplum.GetProviderReportedObservationsByCategoryAsync(patientId);
    //    return Ok(result);
    //}

    [HttpGet("patient-lab-results/{patientId}")]
    public async Task<IActionResult> GetLabResults(string patientId)
    {
        var result = await _medplum.GetPatientLabResultsAsync(patientId);
        return Ok(result);
    }

    [HttpGet("alarm-notification/{patientId}")]
    public async Task<IActionResult> GetAlarmNotification(string patientId)
    {
        var result = await _medplum.GetAlarmNotification(patientId);
        return Ok(new { message = result });

    }
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateHealthInsight([FromBody] InsightRequest request)
    {
        var promptTemplate = _promptLibrary.GetPrimedPrompt(request.Prompt?.Trim());
        if (string.IsNullOrEmpty(promptTemplate))
            return BadRequest("Invalid prompt.");

        var patientProfileInput = await _medplum.GetPatientFullProfileByPatientIdAsync(request.PatientId);

        var patientProfile = new PatientProfile
        {
            Age = CalculateAge(patientProfileInput.BirthDate),
            Gender = patientProfileInput.Gender?.ToString()?.ToLower() ?? "unknown",
            HasPrehypertension = patientProfileInput.PastConditions != null &&
                                 patientProfileInput.PastConditions.Any(c =>
                                     c.Display.Contains("prehypertension", StringComparison.OrdinalIgnoreCase))
        };

        var vitals = await _medplum.GetVitalsTrendAsync(request.PatientId);

        // Extract various vitals as needed
        var systolicReadings = GetLatestValues(vitals, "SystolicBP", 7);
        var diastolicReadings = GetLatestValues(vitals, "DiastolicBP", 7);
        var sleepDurations = GetLatestValues(vitals, "SleepDuration", 7);
        var sleepRestlessness = GetLatestValues(vitals, "SleepRestlessness", 7);
        var stressReadings = GetLatestValues(vitals, "Stress", 7);
        var restingHRs = GetLatestValues(vitals, "RestingHeartRate", 7);
        var maxHRs = GetLatestValues(vitals, "MaxHeartRate", 7);
        var stepsReadings = GetLatestValues(vitals, "Steps", 7);

        // Replace placeholders if they exist
        var finalPrimed = promptTemplate
            .Replace("{bp-sys-readings}", string.Join(", ", systolicReadings))
            .Replace("{bp-dist-readings}", string.Join(", ", diastolicReadings))
            .Replace("{sleep-duration-readings}", string.Join(", ", sleepDurations))
            .Replace("{sleep-restlessness-indexes}", string.Join(", ", sleepRestlessness))
            .Replace("{stress-readings}", string.Join(", ", stressReadings))
            .Replace("{resting-heart-rate-readings}", string.Join(", ", restingHRs))
            .Replace("{max-heart-rate-readings}", string.Join(", ", maxHRs))
            .Replace("{steps-readings}", string.Join(", ", stepsReadings))
            .Replace("{steps-goal-completion}", "85") // hardcoded or fetched separately
            .Replace("{activity-level}", "moderately active") // same
            .Replace("{hrv-value}", "62") // default HRV
            .Replace("{average-bedtime}", "10:30 PM")
            .Replace("{average-wake-up-time}", "6:30 AM")
            .Replace("{stress-level-description}", "moderate")
            .Replace("{cardiac-history-flag}", "no")
            .Replace("{age}", patientProfile.Age.ToString())
            .Replace("{gender}", patientProfile.Gender)
            .Replace("{prehypertension-flag}", patientProfile.HasPrehypertension ? "a" : "no");

        var aiResponse = await _geminiService.GetInsightFromPrompt(finalPrimed, request.UserId);

        return Ok(new { htmlResponse = aiResponse });
    }

    private List<int> GetLatestValues(List<VirtualHealthAPI.VitalTrendResult> vitals, string type, int count)
    {
        return vitals
            .Where(v => v.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(v => v.Timestamp)
            .Take(count)
            .Select(v => (int)v.Value) // Cast Value to int as VitalTrendResult.Value is double
            .ToList();
    }

    private int CalculateAge(string birthDateString)
    {
        if (string.IsNullOrEmpty(birthDateString)) return 0;

        if (!DateTime.TryParse(birthDateString, out var birthDate)) return 0;

        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--;
        return age;
    }

    /* [HttpPost("create-prescription")]
     public async Task<IActionResult> CreatePrescription([FromBody] Prescription prescription)
     {

         try
         {
             if (prescription == null)
                 return BadRequest("Prescription cannot be null.");
             prescription = GetPrescriptionSampleData(); // For testing, replace with actual data from request
             var result = await _medplum.CreatePrescriptionAsync(prescription);
             return Ok(new { message = result });
         }
         catch (Exception ex)
         {
             // Log error
             return StatusCode(500, new { Error = ex.Message });
         }
     } */
    // POST: api/prescription
    [HttpPost("create-prescription")]
    public async Task<IActionResult> CreatePrescription([FromBody] Prescription prescription)
    {
        // For testing, get sample data 
        // prescription = GetPrescriptionSampleData();
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        if (prescription == null || prescription.Medications == null || prescription.Medications.Count == 0)
            throw new ArgumentException("Prescription must include at least one medication.");
        
         var message = await _medplum.CreatePatientPrescriptionAsync(prescription);
        return Ok(new { message });
    }
    // GET: api/prescription/patient/{patientId}
    [HttpGet("get-prescription/{patientId}")]
    public async Task<IActionResult> GetPrescriptionsByPatientId(string patientId)
    {
        var prescriptions = await _medplum.GetPatientPrescriptionsAsync(patientId);
        if (prescriptions.Count == 0)
            return NotFound($"No prescriptions found for Patient/{patientId}.");

        return Ok(prescriptions);
    }
    private Prescription GetPrescriptionSampleData()
    {
        var prescription = new Prescription
        {
            //PrescriptionId = "RX20250703001",
            //DateWritten = DateTime.Parse("2025-07-03"),
            Patient = new Patient
            {
                PatientId = "01978609-4506-72a9-a00e-8083bbf66207",// "PAT1001",
                FirstName = "Darshan",
                LastName = "Singh",
                //DateOfBirth = "1980-02-10",
                //Address = "456 Elm Street, Springfield",
                //Phone = "(555) 222-3344"
            },
            Prescriber = new Prescriber
            {
                ////Id = "PROV9001",
                Name = "Dr. Maria Singh, MD",
                LicenseNumber = "MD-123456",
                //Clinic = "City Health Clinic",
                //Address = "123 Main Street, Springfield",
                //Phone = "(555) 123-4567"
            },
            Medications = new List<MedicationOrder>
            {
                new MedicationOrder
                    {
                        Medication = new Medication
                        {
                            Name = "Amoxicillin",
                            Strength = "500 mg",
                            Form = "Capsule",
                            Route = "Oral",
                           // Manufacturer = "Generic Pharma Inc."
                        },
                        Directions = "Take 1 capsule by mouth every 8 hours",
                        Duration = "7 days",
                        Quantity = new Quantity
                        {
                            Amount = 21,
                            Unit = "capsules"
                        },
                        Refills = 0
                    },
                    new MedicationOrder
                    {
                        Medication = new Medication
                        {
                            Name = "Ibuprofen",
                            Strength = "200 mg",
                            Form = "Tablet",
                            Route = "Oral",
                           // Manufacturer = "PainRelief Inc."
                        },
                        Directions = "Take 1 tablet every 6 hours as needed for pain",
                        Duration = "5 days",
                        Quantity = new Quantity
                        {
                            Amount = 20,
                            Unit = "tablets"
                        },
                        Refills = 1
                    }
            },
            Pharmacy = new Pharmacy
            {
                Name = "City Pharmacy",
                Address = "789 Maple Avenue, Springfield",
                Phone = "(555) 987-6543"
            },
            PharmacyInstructions = new List<string>
            {
                "Finish all medication unless otherwise directed",
                "Take with food if stomach upset occurs"
            },
            Warnings = new List<string>
            {
                "Do not skip doses",
                "Consult your doctor if symptoms persist"
            }
        };
        return prescription;
    }

}
public class InsightRequest
{
    public string Prompt { get; set; }
    public string PatientId { get; set; }

    public string UserId { get; set; }
}

public class VitalReading
{
    public string Type { get; set; }
    public int Value { get; set; }
    public DateTime Timestamp { get; set; }
}

