namespace NasaHazard.Api.Contracts.Responses;

public record HazardSimulationResponse(
    LocationMetadataDto Location,
    CurrentConditionsDto CurrentSatelliteMetrics,
    HistoricalBaselineDto Historical10YearBaseline,
    RiskAssessmentDto RiskAssessment,
    HumanLifeSavingPlanDto HumanLifeSavingPlan,
    List<DailyForecastRiskDto> ForecastNext7Days,
    Terrain3DSimulationDto Simulation3D
);

public record LocationMetadataDto(
    double Latitude,
    double Longitude,
    double RadiusKm,
    double CenterElevationMeters,
    double MaxSlopeDegrees
);

public record CurrentConditionsDto(
    double PrecipitationLast24hMm,
    double SurfaceSoilMoistureIndex,
    double DeepSoilMoistureIndex,
    double MaxTemperatureCelsius,
    string DataSource
);

public record HistoricalBaselineDto(
    int YearsAnalyzed,
    double P95ExtremeRainThresholdMm,
    double MaxHistoricalRainDayMm,
    string MaxHistoricalEventDate,
    double AverageSoilMoistureForMonth
);

public record RiskAssessmentDto(
    int OverallRiskScore,
    string AlertLevel,
    double HuaicoProbabilityPercent,
    double FloodProbabilityPercent,
    double EstimatedMudflowVolumeM3PerSec,
    string TriggerReason
);

public record HumanLifeSavingPlanDto(
    int EstimatedImpactLeadTimeMinutes,
    string EvacuationCompassDirection,
    int WalkingTimeAdultMinutes,
    int WalkingTimeVulnerableMinutes,
    string SmsCommunityAlertText,
    List<string> ImmediateActionChecklist
);

public record DailyForecastRiskDto(
    string Date,
    double PredictedRainMm,
    double ProjectedSoilSaturation,
    int DayRiskScore,
    string AlertLevel
);

public record Terrain3DSimulationDto(
    BoundingBoxDto BoundingBox5Km,
    List<FlowPathVectorDto> DebrisFlowPaths,
    List<GeoPoint3DDto> InundationPolygon,
    SafeZoneDto RecommendedEvacuationZone
);

public record BoundingBoxDto(double North, double South, double East, double West);

public record FlowPathVectorDto(
    string PathId,
    string Severity,
    double EstimatedVelocityMetersPerSec,
    List<GeoPoint3DDto> Coordinates
);

public record GeoPoint3DDto(double Lat, double Lon, double ElevationMeters);

public record SafeZoneDto(
    string Name,
    double Lat,
    double Lon,
    double ElevationMeters,
    int DistanceFromCenterMeters
);