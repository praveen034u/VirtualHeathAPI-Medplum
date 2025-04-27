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

    [HttpGet("current-observations/{patientId}")]
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

    [HttpGet("patient-full-profile/{patientId}")]
    public async Task<IActionResult> GetFullProfile(string patientId)
    {
        var result = await _medplum.GetPatientFullProfileAsync(patientId);
        return Ok(result);
    }
}
