using NasaHazard.Api.Contracts.Responses;

namespace NasaHazard.Api.Services.Interfaces;

public interface INasaEonetService
{
    Task<IReadOnlyList<NasaLiveEventDto>> GetOpenEventsAsync(CancellationToken ct = default);
}
