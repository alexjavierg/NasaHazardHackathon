using NasaHazard.Api.Contracts.Internal;
using NasaHazard.Api.Contracts.Responses;
using NasaHazard.Api.Services.Interfaces;

namespace NasaHazard.Api.Services;

public class MockSatelliteDataService : ISatelliteDataService
{
    public Task<SatelliteRawDataDto> GetNormalizedSatelliteDataAsync(
        double lat, double lon, double radiusKm, CancellationToken ct = default)
    {
        var history = new List<HistoricalDayRecordDto>
        {
            new("2017-03-15", 42.6, 0.92),
            new("2023-03-10", 28.4, 0.85),
            new("2024-02-18", 16.0, 0.72),
            new("2025-01-20", 14.5, 0.68)
        };

        for (int i = 1; i <= 96; i++)
        {
            history.Add(new($"2025-05-{(i % 28) + 1:D2}", Math.Round((i % 12) * 1.1, 1), 0.38));
        }

        var forecast = new List<RawForecastDayDto>
        {
            new("2026-11-15", 12.5),
            new("2026-11-16", 24.0),
            new("2026-11-17", 8.2),
            new("2026-11-18", 2.1),
            new("2026-11-19", 0.0),
            new("2026-11-20", 4.5),
            new("2026-11-21", 15.8)
        };

        var grid = new List<GeoPoint3DDto>
        {
            new(lat + 0.0245, lon - 0.0098, 1420.0),
            new(lat + 0.0150, lon - 0.0063, 1185.0),
            new(lat + 0.0075, lon - 0.0028, 990.0),
            new(lat, lon, 862.5),
            new(lat - 0.0055, lon + 0.0022, 835.0),
            new(lat + 0.0017, lon + 0.0047, 918.0)
        };

        var rawData = new SatelliteRawDataDto(
            Latitude: lat,
            Longitude: lon,
            RadiusKm: radiusKm,
            CurrentRainMm: 18.4,
            SurfaceSoilMoisture: 0.84,
            DeepSoilMoisture: 0.79,
            MaxTempCelsius: 26.3,
            Historical10YearsDaily: history,
            RawForecastNext7Days: forecast,
            TerrainGrid5Km: grid
        );

        return Task.FromResult(rawData);
    }
}