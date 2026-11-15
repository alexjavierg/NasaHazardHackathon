using NasaHazard.Api.Algorithms;
using NasaHazard.Api.Services;
using NasaHazard.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

builder.Services.AddScoped<ISatelliteDataService, RealSatelliteDataService>();
builder.Services.AddHttpClient<INasaEonetService, NasaEonetService>();
builder.Services.AddHttpClient<IEmergencyInfrastructureService, OverpassEmergencyInfrastructureService>();
builder.Services.AddScoped<HuaicoRiskEngine>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("HackathonCors", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NASA Hazard API v1");
    c.RoutePrefix = string.Empty;
});

app.UseCors("HackathonCors");
app.UseAuthorization();
app.MapControllers();

_ = Task.Run(async () =>
{
    var presets = new (double Lat, double Lon)[]
    {
        (-11.9365, -76.6912),
        (-12.3314, -76.8231),
        (-5.1945, -80.6328),
        (-16.3989, -71.5350),
        (-22.5049, -43.1786),
        (1.1478, -76.6481)
    };

    using var scope = app.Services.CreateScope();
    var satService = scope.ServiceProvider.GetRequiredService<ISatelliteDataService>();
    foreach (var (lat, lon) in presets)
    {
        try { await satService.GetNormalizedSatelliteDataAsync(lat, lon, 5.0); }
        catch { }
    }
});

app.Run();