using InfluxDB.Client;

var builder = WebApplication.CreateBuilder(args);

// influx db
var influxUrl = builder.Configuration["Influx:Url"]!;
var influxToken = builder.Configuration["Influx:Token"]!;
var influxOrg = builder.Configuration["Influx:Org"]!;
var influxBucket = builder.Configuration["Influx:Bucket"]!;

var influxOptions = new InfluxDBClientOptions.Builder()
    .Url(influxUrl)
    .AuthenticateToken(influxToken)
    .Org(influxOrg)
    .Build();
builder.Services.AddSingleton(_ => new InfluxDBClient(influxOptions));

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<VirtualHealthAPI.MedplumService>();

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("http://localhost:8000")
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseCors(x => x
    .AllowAnyMethod()
    .AllowAnyHeader()
    .SetIsOriginAllowed(origin => true)
    .AllowCredentials());

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
