using Microsoft.AspNetCore.Mvc;
using VirtualHealthAPI;

[ApiController]
[Route("api/[controller]")]
public class MedplumController : ControllerBase
{
    private readonly MedplumService _medplum;

    public MedplumController(MedplumService medplum)
    {
        _medplum = medplum;
    }

    [HttpPost("create-profile-with-pcp-and-vitals")]
    public async Task<IActionResult> CreateWithVitals([FromBody] PatientProfileInput input)
    {
        var result = await _medplum.CreatePatientWithPcpAndVitalsAsync(input);
        return Ok(new { message = result });
    }

    [HttpPost("ingest-wearable-observations")]
    public async Task<IActionResult> IngestVitals([FromBody] WearableVitalsInput input)
    {
        var result = await _medplum.IngestWearableObservationsAsync(input);
        return Ok(new { message = result });
    }

    [HttpGet("current-wearable-observations/{patientId}")]
    public async Task<IActionResult> GetCurrentVitals(string patientId)
    {
        var results = await _medplum.GetPatientObservationsAsync(patientId);
        return Ok(results);
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

    [HttpGet("provider-reported-observations/{patientId}")]
    public async Task<IActionResult> GetProviderReportedObservations(string patientId)
    {
        var result = await _medplum.GetProviderReportedObservationsByCategoryAsync(patientId);
        return Ok(result);
    }

    [HttpGet("patient-lab-results/{patientId}")]
    public async Task<IActionResult> GetLabResults(string patientId)
    {
        var result = await _medplum.GetPatientLabResultsAsync(patientId);
        return Ok(result);
    }
}
