using NasaHazard.Api.Services.Models;

namespace NasaHazard.Api.Services.Interfaces;

public interface IEmergencyInfrastructureService
{
    Task<IReadOnlyList<EmergencyFacilityCandidate>> FindNearbyFacilitiesAsync(
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken ct = default);
}
