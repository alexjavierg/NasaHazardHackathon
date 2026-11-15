using NasaHazard.Api.Algorithms;
using NasaHazard.Api.Services;
using NasaHazard.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

builder.Services.AddScoped<ISatelliteDataService, RealSatelliteDataService>();
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

app.Run();