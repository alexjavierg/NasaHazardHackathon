using NasaHazard.Api.Contracts.Internal;
using NasaHazard.Api.Contracts.Responses;

namespace NasaHazard.Api.Algorithms;

public class HuaicoRiskEngine
{
    public HazardSimulationResponse CalculateRiskAndSimulation(
        SatelliteRawDataDto raw,
        double? simulatedRainMm)
    {
        double lat = raw.Latitude;
        double lon = raw.Longitude;
        double offsetDeg = raw.RadiusKm / 111.32;

        // 1. ESTADÍSTICA REAL DE LOS 10 AÑOS (NASA POWER)
        var validHistory = raw.Historical10YearsDaily ?? new List<HistoricalDayRecordDto>();
        var wetDays = validHistory.Where(d => d.RainMm >= 1.0).OrderBy(d => d.RainMm).ToList();

        double p95Threshold = 12.0; // Umbral base para zonas áridas/quebradas secas
        if (wetDays.Count >= 5)
        {
            int p95Index = (int)Math.Floor(wetDays.Count * 0.95);
            p95Threshold = Math.Max(6.0, wetDays[Math.Clamp(p95Index, 0, wetDays.Count - 1)].RainMm);
        }

        var maxEvent = validHistory.OrderByDescending(d => d.RainMm).FirstOrDefault()
                       ?? new HistoricalDayRecordDto("2017-03-15", 0.0, 0.4);

        string currentMonthPrefix = $"-{DateTime.UtcNow:MM}-";
        var monthRecords = validHistory.Where(d => d.Date.Contains(currentMonthPrefix)).ToList();
        double avgMonthMoisture = monthRecords.Count > 0
            ? Math.Round(monthRecords.Average(d => d.SoilMoisture), 2)
            : 0.35;

        // 2. TOPOGRAFÍA 3D REAL Y PENDIENTE (GRILLA 5 KM)
        var grid = raw.TerrainGrid5Km.Count > 0
            ? raw.TerrainGrid5Km
            : new List<GeoPoint3DDto> { new(lat, lon, 500) };

        var centerPoint = grid
            .OrderBy(p => CalculateDistanceMeters(lat, lon, p.Lat, p.Lon))
            .First();

        var highestPoint = grid.OrderByDescending(p => p.ElevationMeters).First();
        var lowestPoint = grid.OrderBy(p => p.ElevationMeters).First();

        double distHighToLowMeters = Math.Max(250.0, CalculateDistanceMeters(
            highestPoint.Lat, highestPoint.Lon, lowestPoint.Lat, lowestPoint.Lon));
        double elevationDropMeters = Math.Max(1.0, highestPoint.ElevationMeters - lowestPoint.ElevationMeters);

        // Factor de amplificación de micro-quebrada (grilla 5km suaviza laderas locales)
        double rawSlopeRad = Math.Atan((elevationDropMeters * 2.8) / distHighToLowMeters);
        double maxSlopeDegrees = Math.Round(Math.Clamp(rawSlopeRad * (180.0 / Math.PI), 1.5, 52.0), 1);

        // 3. ALGORITMO DE RIESGO (LLUVIA + MEMORIA DEL SUELO + PENDIENTE)
        double effectiveRain = simulatedRainMm ?? raw.CurrentRainMm;
        double rainRatio = effectiveRain / Math.Max(1.0, p95Threshold);

        double rainScore = Math.Clamp(rainRatio * 42.0, 0, 48);
        double moistureScore = Math.Clamp(raw.SurfaceSoilMoisture * 32.0, 0, 32);
        double slopeScore = Math.Clamp((maxSlopeDegrees / 35.0) * 20.0, 0, 20);

        int overallRiskScore = (int)Math.Round(Math.Clamp(rainScore + moistureScore + slopeScore, 5, 100));
        string alertLevel = GetAlertLevel(overallRiskScore);

        // Si hay pendiente fuerte (>12°), el riesgo se transforma en Huaico; si es plano, en Inundación
        double slopeFactor = Math.Clamp(maxSlopeDegrees / 28.0, 0.15, 1.0);
        double huaicoProb = Math.Round(Math.Clamp(overallRiskScore * slopeFactor * 1.05, 2.0, 99.0), 1);
        double floodProb = Math.Round(Math.Clamp(overallRiskScore * (1.15 - (slopeFactor * 0.45)), 5.0, 99.0), 1);

        string modeTag = simulatedRainMm.HasValue ? "[MODO SIMULACIÓN] " : "";
        string triggerReason = $"{modeTag}Lluvia analizada ({effectiveRain:F1} mm vs umbral P95 de {p95Threshold:F1} mm), " +
                               $"saturación superficial del suelo al {(raw.SurfaceSoilMoisture * 100):F0}% y " +
                               $"desnivel topográfico de {elevationDropMeters:F0} m ({maxSlopeDegrees:F1}°).";

        // 4. PROYECCIÓN A 7 DÍAS CON MEMORIA DE HUMEDAD DE SUELO
        var forecastList = new List<DailyForecastRiskDto>();
        double rollingMoisture = raw.SurfaceSoilMoisture;

        foreach (var day in raw.RawForecastNext7Days)
        {
            double moistureGain = day.PredictedRainMm / Math.Max(15.0, p95Threshold * 2.2);
            rollingMoisture = Math.Round(Math.Clamp((rollingMoisture * 0.91) + moistureGain, 0.08, 0.98), 2);

            double dayRainScore = Math.Clamp((day.PredictedRainMm / Math.Max(1.0, p95Threshold)) * 42.0, 0, 48);
            int dayScore = (int)Math.Round(Math.Clamp(dayRainScore + (rollingMoisture * 32.0) + slopeScore, 5, 100));

            forecastList.Add(new DailyForecastRiskDto(
                Date: day.Date,
                PredictedRainMm: day.PredictedRainMm,
                ProjectedSoilSaturation: rollingMoisture,
                DayRiskScore: dayScore,
                AlertLevel: GetAlertLevel(dayScore)
            ));
        }

        // 5. VECTORES 3D DE CAUCE Y ZONA DE EVACUACIÓN
        var sortedByElevation = grid.OrderByDescending(p => p.ElevationMeters).ToList();
        var flowCoordinates = new List<GeoPoint3DDto>
        {
            sortedByElevation.First(),
            sortedByElevation[sortedByElevation.Count / 4],
            centerPoint,
            sortedByElevation[(sortedByElevation.Count * 3) / 4],
            sortedByElevation.Last()
        };

        double velocityMs = Math.Round(
            Math.Clamp(1.2 + (maxSlopeDegrees * 0.14) + (effectiveRain * 0.08), 0.8, 12.5), 1);

        var lowestBasinPoints = grid.OrderBy(p => p.ElevationMeters).Take(4).ToList();

        // Buscar loma segura: más alta que el centro, pero sin irse hasta la cumbre extrema
        var safeCandidate = grid
            .Where(p => p.ElevationMeters > centerPoint.ElevationMeters + 10)
            .OrderBy(p => CalculateDistanceMeters(lat, lon, p.Lat, p.Lon))
            .FirstOrDefault() ?? highestPoint;

        int safeDistMeters = (int)Math.Round(
            CalculateDistanceMeters(lat, lon, safeCandidate.Lat, safeCandidate.Lon));

        return new HazardSimulationResponse(
            Location: new LocationMetadataDto(
                Latitude: lat,
                Longitude: lon,
                RadiusKm: raw.RadiusKm,
                CenterElevationMeters: centerPoint.ElevationMeters,
                MaxSlopeDegrees: maxSlopeDegrees
            ),
            CurrentSatelliteMetrics: new CurrentConditionsDto(
                PrecipitationLast24hMm: effectiveRain,
                SurfaceSoilMoistureIndex: raw.SurfaceSoilMoisture,
                DeepSoilMoistureIndex: raw.DeepSoilMoisture,
                MaxTemperatureCelsius: raw.MaxTempCelsius,
                DataSource: simulatedRainMm.HasValue
                    ? "NASA POWER (10Y Baseline) + Open-Meteo DEM + Rain Override"
                    : "NASA POWER (GPM/MERRA-2) + Open-Meteo 3D DEM (Real-Time)"
            ),
            Historical10YearBaseline: new HistoricalBaselineDto(
                YearsAnalyzed: 10,
                P95ExtremeRainThresholdMm: Math.Round(p95Threshold, 2),
                MaxHistoricalRainDayMm: maxEvent.RainMm,
                MaxHistoricalEventDate: maxEvent.Date,
                AverageSoilMoistureForMonth: avgMonthMoisture
            ),
            RiskAssessment: new RiskAssessmentDto(
                OverallRiskScore: overallRiskScore,
                AlertLevel: alertLevel,
                HuaicoProbabilityPercent: huaicoProb,
                FloodProbabilityPercent: floodProb,
                TriggerReason: triggerReason
            ),
            ForecastNext7Days: forecastList,
            Simulation3D: new Terrain3DSimulationDto(
                BoundingBox5Km: new BoundingBoxDto(
                    North: Math.Round(lat + offsetDeg, 5),
                    South: Math.Round(lat - offsetDeg, 5),
                    East: Math.Round(lon + offsetDeg, 5),
                    West: Math.Round(lon - offsetDeg, 5)
                ),
                DebrisFlowPaths: new List<FlowPathVectorDto>
                {
                    new(
                        PathId: "quebrada-principal-01",
                        Severity: alertLevel,
                        EstimatedVelocityMetersPerSec: velocityMs,
                        Coordinates: flowCoordinates
                    )
                },
                InundationPolygon: lowestBasinPoints,
                RecommendedEvacuationZone: new SafeZoneDto(
                    Name: "Ladera Elevada Segura (Fuera de Cauce)",
                    Lat: safeCandidate.Lat,
                    Lon: safeCandidate.Lon,
                    ElevationMeters: safeCandidate.ElevationMeters,
                    DistanceFromCenterMeters: Math.Max(120, safeDistMeters)
                )
            )
        );
    }

    private static string GetAlertLevel(int score) => score switch
    {
        >= 80 => "CRITICAL",
        >= 60 => "HIGH",
        >= 35 => "MODERATE",
        _ => "LOW"
    };

    private static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Radio terrestre en metros
        double dLat = (lat2 - lat1) * (Math.PI / 180.0);
        double dLon = (lon2 - lon1) * (Math.PI / 180.0);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * (Math.PI / 180.0)) * Math.Cos(lat2 * (Math.PI / 180.0)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}