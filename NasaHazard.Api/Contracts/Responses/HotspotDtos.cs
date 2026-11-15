using System.Text.Json;

namespace NasaHazard.Api.Contracts.Responses;

public sealed record HotspotPresetsResponse(
    string Notice,
    IReadOnlyList<HotspotPresetDto> Presets
);

public sealed record HotspotPresetDto(
    string Id,
    string Name,
    string Country,
    double Latitude,
    double Longitude,
    double RadiusKm,
    bool IsValidatedRiskZone,
    string Description
);

public sealed record NasaLiveEventsResponse(
    string Source,
    string Status,
    DateTimeOffset RetrievedAtUtc,
    string Notice,
    IReadOnlyList<NasaLiveEventDto> Events
);

public sealed record NasaLiveEventDto(
    string Id,
    string Title,
    IReadOnlyList<string> Categories,
    string? Date,
    double? Latitude,
    double? Longitude,
    string? GeometryType,
    JsonElement? Geometry,
    string Link,
    bool IsOpen
);
