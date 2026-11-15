namespace NasaHazard.Api.Contracts.Requests;

public record AnalyzeLocationRequest(
    double Latitude = -11.9365,
    double Longitude = -76.6912,
    double RadiusKm = 5.0,
    double? SimulatedRainMm = null
);