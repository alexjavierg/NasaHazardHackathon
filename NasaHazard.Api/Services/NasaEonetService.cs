using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using NasaHazard.Api.Contracts.Responses;
using NasaHazard.Api.Services.Interfaces;

namespace NasaHazard.Api.Services;

public sealed class NasaEonetService : INasaEonetService
{
    private const string CacheKey = "nasa-eonet-open-events-v3";
    private const string EventsUrl =
        "https://eonet.gsfc.nasa.gov/api/v3/events?category=severeStorms%2Cfloods%2Clandslides&status=open&limit=15";
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public NasaEonetService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NasaHazardHackathon/1.0");
        _cache = cache;
    }

    public async Task<IReadOnlyList<NasaLiveEventDto>> GetOpenEventsAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<NasaLiveEventDto>? cached) && cached is not null)
        {
            return cached;
        }

        using var response = await _httpClient.GetAsync(EventsUrl, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var events = new List<NasaLiveEventDto>();

        if (document.RootElement.TryGetProperty("events", out var eventArray) && eventArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in eventArray.EnumerateArray())
            {
                var geometry = GetLatestGeometry(item);
                var coordinates = geometry is { } geo ? ReadCoordinatePairs(geo) : [];
                var categories = ReadCategories(item);

                events.Add(new NasaLiveEventDto(
                    Id: ReadString(item, "id") ?? string.Empty,
                    Title: ReadString(item, "title") ?? "Evento natural sin título",
                    Categories: categories,
                    Date: geometry is { } latest ? ReadString(latest, "date") : null,
                    Latitude: coordinates.Count == 0 ? null : coordinates.Average(point => point.Latitude),
                    Longitude: coordinates.Count == 0 ? null : coordinates.Average(point => point.Longitude),
                    GeometryType: geometry is { } geoElement && geoElement.TryGetProperty("type", out var typeElement)
                        ? typeElement.GetString()
                        : null,
                    Geometry: geometry is { } geometryElement ? geometryElement.Clone() : null,
                    Link: ReadString(item, "link") ?? "https://eonet.gsfc.nasa.gov/",
                    IsOpen: item.TryGetProperty("closed", out var closed) && closed.ValueKind == JsonValueKind.Null
                ));
            }
        }

        IReadOnlyList<NasaLiveEventDto> result = events;
        _cache.Set(CacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    private static JsonElement? GetLatestGeometry(JsonElement item)
    {
        if (!item.TryGetProperty("geometry", out var geometries) || geometries.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? latest = null;
        string latestDate = string.Empty;
        foreach (var geometry in geometries.EnumerateArray())
        {
            var date = ReadString(geometry, "date") ?? string.Empty;
            if (latest is null || string.CompareOrdinal(date, latestDate) > 0)
            {
                latest = geometry;
                latestDate = date;
            }
        }

        return latest;
    }

    private static List<(double Latitude, double Longitude)> ReadCoordinatePairs(JsonElement geometry)
    {
        var points = new List<(double Latitude, double Longitude)>();
        if (geometry.TryGetProperty("coordinates", out var coordinates))
        {
            CollectCoordinatePairs(coordinates, points);
        }
        return points;
    }

    private static void CollectCoordinatePairs(JsonElement element, List<(double Latitude, double Longitude)> points)
    {
        if (element.ValueKind != JsonValueKind.Array) return;

        if (element.GetArrayLength() >= 2 &&
            element[0].ValueKind == JsonValueKind.Number &&
            element[1].ValueKind == JsonValueKind.Number)
        {
            // GeoJSON uses [longitude, latitude].
            var longitude = element[0].GetDouble();
            var latitude = element[1].GetDouble();
            if (latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180)
            {
                points.Add((latitude, longitude));
            }
            return;
        }

        foreach (var child in element.EnumerateArray())
        {
            CollectCoordinatePairs(child, points);
        }
    }

    private static IReadOnlyList<string> ReadCategories(JsonElement item)
    {
        if (!item.TryGetProperty("categories", out var categories) || categories.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return categories.EnumerateArray()
            .Select(category => ReadString(category, "title") ?? ReadString(category, "id"))
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Select(title => title!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ReadString(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
