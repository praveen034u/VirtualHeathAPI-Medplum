using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using InfluxDB.Client;
using InfluxDB.Client.Core;
using VirtualHealthAPI;

// If your MedPlumService lives in a namespace, adjust this using accordingly:
// using VirtualHealthAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// ── ADD CORE SERVICES ──────────────────────────────────────────────────────────

// MVC controllers
builder.Services.AddControllers();

// HTTP client & your existing MedPlum service
builder.Services.AddHttpClient();
builder.Services.AddSingleton<VirtualHealthAPI.MedplumService>();

// ── INFLUXDB CLIENT (Vitals) ───────────────────────────────────────────────────

// Pull these four settings from appsettings.json under “Influx”
var influxUrl = builder.Configuration["Influx:Url"]!;
var influxToken = builder.Configuration["Influx:Token"]!;
var influxOrg = builder.Configuration["Influx:Org"]!;
var influxBucket = builder.Configuration["Influx:Bucket"]!;

// Register InfluxDB client
var influxOptions = new InfluxDBClientOptions.Builder()
    .Url(influxUrl)
    .AuthenticateToken(influxToken)
    .Org(influxOrg)
    .Build();
builder.Services.AddSingleton(_ => new InfluxDBClient(influxOptions));

// ── SWAGGER / OPENAPI ──────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VirtualHealth API",
        Version = "v1",
        Description = "All existing controllers + Vitals minimal endpoint"
    });
});

// ── CORS ────────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(
            "https://localhost:5002",  // UI (Blazor) origin(s)
            "https://localhost:5003",
            "http://localhost:5003",
            "https://localhost:7236"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
    );
});

var app = builder.Build();

// ── MIDDLEWARE PIPELINE ─────────────────────────────────────────────────────────

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "VirtualHealth API v1");
        c.RoutePrefix = string.Empty;     // Serve Swagger UI at root
    });
}

app.UseCors();               // enable CORS everywhere
app.UseHttpsRedirection();
app.UseAuthorization();

// Map your existing MVC controllers
app.MapControllers();

// ── VITALS MINIMAL API ENDPOINT ────────────────────────────────────────────────

// GET /api/vitals/{patientId}?durationMinutes=30
app.MapGet("/api/vitals/{patientId}", async (
    string patientId,
    int durationMinutes,
    InfluxDBClient client
) =>
{
    var flux = $@"
      from(bucket: ""{influxBucket}"")
        |> range(start: -{durationMinutes}m)
        |> filter(fn: (r) =>
             r._measurement == ""vitals"" and
             r.patientId      == ""{patientId}"" and
             (r._field == ""heartRate"" or r._field == ""spo2"")
           )
        |> pivot(
             rowKey:     [""_time""],
             columnKey:  [""_field""],
             valueColumn:""_value""
           )
        |> sort(columns: [""_time""])
    ";

    var tables = await client.GetQueryApi().QueryAsync(flux, influxOrg);

    var data = tables
      .SelectMany(t => t.Records)
      .Select(r => new
      {
          time = r.GetTimeInDateTime()?.ToLocalTime(),
          heartRate = r.GetValueByKey("heartRate"),
          spo2 = r.GetValueByKey("spo2")
      })
      .ToList();

    return Results.Json(data);
})
.WithName("GetVitals");

app.Run();
