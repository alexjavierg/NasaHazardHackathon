using NasaHazard.Api.Contracts.Internal;

namespace NasaHazard.Api.Services.Interfaces;

public interface ISatelliteDataService
{
    Task<SatelliteRawDataDto> GetNormalizedSatelliteDataAsync(
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken ct = default);
}