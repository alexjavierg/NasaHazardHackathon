using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using NasaHazard.Api.Services.Interfaces;
using NasaHazard.Api.Services.Models;

namespace NasaHazard.Api.Services;

public sealed class OverpassEmergencyInfrastructureService : IEmergencyInfrastructureService
{
    private const string OverpassEndpoint = "https://overpass-api.de/api/interpreter";
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public OverpassEmergencyInfrastructureService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(35);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NasaHazardHackathon/1.0");
        _cache = cache;
    }

    public async Task<IReadOnlyList<EmergencyFacilityCandidate>> FindNearbyFacilitiesAsync(
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken ct = default)
    {
        var cacheKey = string.Create(CultureInfo.InvariantCulture,
            $"overpass-facilities:{latitude:R}:{longitude:R}:{radiusKm:R}");
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<EmergencyFacilityCandidate>? cached) && cached is not null)
        {
            return cached;
        }

        var radiusMeters = radiusKm * 1000.0;
        var lat = latitude.ToString("F6", CultureInfo.InvariantCulture);
        var lon = longitude.ToString("F6", CultureInfo.InvariantCulture);
        var radius = radiusMeters.ToString("F0", CultureInfo.InvariantCulture);
        var query = $"[out:json][timeout:25];(nwr(around:{radius},{lat},{lon})[\"amenity\"~\"^(hospital|clinic|doctors|police|school|shelter|fire_station)$\"];nwr(around:{radius},{lat},{lon})[\"healthcare\"~\"^(hospital|clinic|doctor)$\"];nwr(around:{radius},{lat},{lon})[\"emergency\"=\"fire_station\"];);out center tags;";

        using var request = new HttpRequestMessage(HttpMethod.Post, OverpassEndpoint)
        {
            Content = new StringContent($"data={Uri.EscapeDataString(query)}", Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var facilities = ParseFacilities(document.RootElement, latitude, longitude, radiusKm);
        IReadOnlyList<EmergencyFacilityCandidate> result = facilities;
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
        return result;
    }

    private static List<EmergencyFacilityCandidate> ParseFacilities(
        JsonElement root,
        double centerLatitude,
        double centerLongitude,
        double radiusKm)
    {
        var facilities = new List<EmergencyFacilityCandidate>();
        if (!root.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
        {
            return facilities;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in elements.EnumerateArray())
        {
            var osmType = GetString(element, "type");
            if (!element.TryGetProperty("id", out var idElement) || !idElement.TryGetInt64(out var osmId)) continue;
            if (string.IsNullOrWhiteSpace(osmType)) continue;

            var dedupeKey = $"{osmType}/{osmId}";
            if (!seen.Add(dedupeKey)) continue;

            if (!TryGetCoordinates(element, out var latitude, out var longitude)) continue;
            var distanceKm = DistanceKm(centerLatitude, centerLongitude, latitude, longitude);
            if (distanceKm > radiusKm + 0.02) continue;

            var tags = ReadRelevantTags(element);
            var category = Classify(tags);
            if (category is null) continue;

            facilities.Add(new EmergencyFacilityCandidate(
                OsmType: osmType,
                OsmId: osmId,
                Category: category,
                Name: tags.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : "Sin nombre registrado en OpenStreetMap",
                Latitude: latitude,
                Longitude: longitude,
                DistanceKm: Math.Round(distanceKm, 3),
                Tags: tags
            ));
        }

        return facilities
            .OrderBy(facility => facility.DistanceKm)
            .ToList();
    }

    private static bool TryGetCoordinates(JsonElement element, out double latitude, out double longitude)
    {
        if (element.TryGetProperty("lat", out var lat) && element.TryGetProperty("lon", out var lon) &&
            lat.TryGetDouble(out latitude) && lon.TryGetDouble(out longitude))
        {
            return true;
        }

        if (element.TryGetProperty("center", out var center) &&
            center.TryGetProperty("lat", out lat) && center.TryGetProperty("lon", out lon) &&
            lat.TryGetDouble(out latitude) && lon.TryGetDouble(out longitude))
        {
            return true;
        }

        latitude = longitude = 0;
        return false;
    }

    private static Dictionary<string, string> ReadRelevantTags(JsonElement element)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!element.TryGetProperty("tags", out var tagObject) || tagObject.ValueKind != JsonValueKind.Object)
        {
            return tags;
        }

        string[] allowed = ["name", "amenity", "healthcare", "emergency", "operator", "website", "phone"];
        foreach (var property in tagObject.EnumerateObject())
        {
            if (allowed.Contains(property.Name, StringComparer.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
            {
                tags[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }
        return tags;
    }

    private static string? Classify(IReadOnlyDictionary<string, string> tags)
    {
        tags.TryGetValue("amenity", out var amenity);
        tags.TryGetValue("healthcare", out var healthcare);
        tags.TryGetValue("emergency", out var emergency);

        if (amenity is "hospital" || healthcare is "hospital") return "Hospital";
        if (amenity is "clinic" or "doctors" || healthcare is "clinic" or "doctor") return "Centro de salud / posta";
        if (amenity is "police") return "Comisaría";
        if (amenity is "school") return "Colegio (refugio potencial; no confirmado)";
        if (amenity is "shelter") return "Refugio registrado en OSM (disponibilidad no confirmada)";
        if (amenity is "fire_station" || emergency is "fire_station") return "Estación de bomberos";
        return null;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0088;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
