using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using InfluxDB.Client;
using InfluxDB.Client.Core;
using VirtualHealthAPI;
using Amazon.S3;
using Amazon;

// If your MedPlumService lives in a namespace, adjust this using accordingly:
// using VirtualHealthAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// ── ADD CORE SERVICES ──────────────────────────────────────────────────────────

// MVC controllers
builder.Services.AddControllers();

// HTTP client & your existing MedPlum service
builder.Services.AddHttpClient();
builder.Services.AddSingleton<VirtualHealthAPI.MedplumService>();
builder.Services.AddSingleton<TwilioService>();
builder.Services.AddSingleton<S3AlarmReader>();


builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:8000") 
});

// Read AWS settings from GitHub secret environment variables
var awsSettings = new AWSSettings
{
    AccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
    SecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"),
    Region = Environment.GetEnvironmentVariable("AWS_REGION")
};

var s3Config = new AmazonS3Config
{
    RegionEndpoint = RegionEndpoint.GetBySystemName(awsSettings?.Region)
};
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddSingleton<PromptLibraryService>();

var s3Client = new AmazonS3Client(awsSettings?.AccessKey, awsSettings?.SecretKey, s3Config);

// Register IAmazonS3 instance
builder.Services.AddSingleton<IAmazonS3>(s3Client);

// Listen on PORT from Cloud Run environment.
// comment for local development and uncomment for sever
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    serverOptions.ListenAnyIP(Int32.Parse(port));
});

// ── INFLUXDB CLIENT (Vitals) ───────────────────────────────────────────────────

// Pull these four settings from appsettings.json under “Influx”
var influxUrl = builder.Configuration["Influx:Url"]!;
var influxToken = Environment.GetEnvironmentVariable("INFLUX_TOKEN");
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
builder.Services.AddSwaggerGen();


// ── CORS ────────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(
            "https://myapp.example:7236",  // UI (Blazor) origin(s)
            "https://virtualhealth.ai4magic.com",
            "https://localhost:7236"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
    );
});

var app = builder.Build();

// ── MIDDLEWARE PIPELINE ─────────────────────────────────────────────────────────
// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
    app.UseSwagger();
    app.UseSwaggerUI();
//}

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
             (r._field == ""heartRate"" or r._field == ""spo2"" or r._field == ""bloodGlucose"" or r._field == ""respiratoryRate"")
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
          spo2 = r.GetValueByKey("spo2"),
          bloodGlucose = r.GetValueByKey("bloodGlucose"),
          respiratoryRate = r.GetValueByKey("respiratoryRate")
      })
      .ToList();

    return Results.Json(data);
})
.WithName("GetVitals");

app.Run();
