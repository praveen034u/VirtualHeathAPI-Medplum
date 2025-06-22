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

        // Group systolic and diastolic BP readings (latest 7 each)
        var systolicReadings = vitals
            .Where(v => v.Type == "SystolicBP")
            .OrderByDescending(v => v.Timestamp)
            .Take(7)
            .Select(v => v.Value)
            .ToList();

        var diastolicReadings = vitals
            .Where(v => v.Type == "DiastolicBP")
            .OrderByDescending(v => v.Timestamp)
            .Take(7)
            .Select(v => v.Value)
            .ToList();

        var finalPrimed = promptTemplate
            .Replace("{bp-sys-readings}", string.Join(", ", systolicReadings))
            .Replace("{bp-dist-readings}", string.Join(", ", diastolicReadings))
            .Replace("{age}", patientProfile.Age.ToString())
            .Replace("{gender}", patientProfile.Gender)
            .Replace("{prehypertension-flag}", patientProfile.HasPrehypertension ? "a" : "no");

        var aiResponse = await _geminiService.GetInsightFromPrompt(finalPrimed);

        return Ok(new { htmlResponse = aiResponse });
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



}
public class InsightRequest
{
    public string Prompt { get; set; }
    public string PatientId { get; set; }
}
public class PromptEntry
{
    public string UserPrompt { get; set; }
    public string Primed { get; set; }
}
public class VitalReading
{
    public string Type { get; set; }           // e.g., "SystolicBP"
    public int Value { get; set; }             // e.g., 120
    public DateTime Timestamp { get; set; }    // e.g., 2025-06-22T...
}

