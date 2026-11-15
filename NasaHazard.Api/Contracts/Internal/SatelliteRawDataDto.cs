using NasaHazard.Api.Contracts.Responses;

namespace NasaHazard.Api.Contracts.Internal;

public record SatelliteRawDataDto(
    double Latitude,
    double Longitude,
    double RadiusKm,
    // 1. Estado actual del satélite (NASA POWER)
    double CurrentRainMm,
    double SurfaceSoilMoisture,      // GWETTOP (0.0 a 1.0)
    double DeepSoilMoisture,         // GWETPROF (0.0 a 1.0)
    double MaxTempCelsius,
    // 2. Data histórica limpia de 10 años (sin los -999 de la NASA)
    List<HistoricalDayRecordDto> Historical10YearsDaily,
    // 3. Pronóstico bruto de lluvia para los próximos 7 días
    List<RawForecastDayDto> RawForecastNext7Days,
    // 4. Grilla topográfica de puntos dentro de los 5 km (con su altura en metros)
    List<GeoPoint3DDto> TerrainGrid5Km
);

public record HistoricalDayRecordDto(string Date, double RainMm, double SoilMoisture);

public record RawForecastDayDto(string Date, double PredictedRainMm);