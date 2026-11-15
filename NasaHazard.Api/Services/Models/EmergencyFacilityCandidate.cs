namespace NasaHazard.Api.Services.Models;

public sealed record EmergencyFacilityCandidate(
    string OsmType,
    long OsmId,
    string Category,
    string Name,
    double Latitude,
    double Longitude,
    double DistanceKm,
    IReadOnlyDictionary<string, string> Tags
);
