using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using NasaHazard.Api.Contracts.Internal;
using NasaHazard.Api.Contracts.Responses;
using NasaHazard.Api.Services.Interfaces;

namespace NasaHazard.Api.Services;

public class RealSatelliteDataService : ISatelliteDataService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public RealSatelliteDataService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(25);
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NasaHazardHackathon/1.0 (Contact: dev@nasahackathon.org)");
        }
        _cache = cache;
    }

    public async Task<SatelliteRawDataDto> GetNormalizedSatelliteDataAsync(
        double lat, double lon, double radiusKm, CancellationToken ct = default)
    {
        string cacheKey = FormattableString.Invariant($"sat_data_{lat:F2}_{lon:F2}_{radiusKm:F1}");
        if (_cache.TryGetValue(cacheKey, out SatelliteRawDataDto? cachedData) && cachedData is not null)
        {
            return cachedData;
        }

        // Ejecutamos las 3 consultas satelitales/topográficas en paralelo
        var nasaTask = FetchNasaPower10YearsAsync(lat, lon, ct);
        var forecastTask = FetchOpenMeteoForecastAsync(lat, lon, ct);
        var terrainTask = FetchTerrainGrid5KmAsync(lat, lon, radiusKm, ct);

        await Task.WhenAll(nasaTask, forecastTask, terrainTask);

        var nasaResult = await nasaTask;
        var forecastResult = await forecastTask;
        var terrainResult = await terrainTask;

        var finalDto = new SatelliteRawDataDto(
            Latitude: lat,
            Longitude: lon,
            RadiusKm: radiusKm,
            CurrentRainMm: nasaResult.LatestRain,
            SurfaceSoilMoisture: nasaResult.LatestSurfaceMoisture,
            DeepSoilMoisture: nasaResult.LatestDeepMoisture,
            MaxTempCelsius: nasaResult.LatestMaxTemp,
            Historical10YearsDaily: nasaResult.History,
            RawForecastNext7Days: forecastResult,
            TerrainGrid5Km: terrainResult
        );

        _cache.Set(cacheKey, finalDto, TimeSpan.FromMinutes(30));
        return finalDto;
    }

    private async Task<(double LatestRain, double LatestSurfaceMoisture, double LatestDeepMoisture, double LatestMaxTemp, List<HistoricalDayRecordDto> History)>
        FetchNasaPower10YearsAsync(double lat, double lon, CancellationToken ct)
    {
        string endDate = DateTime.UtcNow.ToString("yyyyMMdd");
        string startDate = DateTime.UtcNow.AddYears(-10).ToString("yyyyMMdd");

        string url = FormattableString.Invariant(
            $"https://power.larc.nasa.gov/api/temporal/daily/point?parameters=PRECTOTCORR,GWETTOP,GWETPROF,T2M_MAX&community=RE&longitude={lon:F4}&latitude={lat:F4}&start={startDate}&end={endDate}&format=JSON");

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var paramNode = doc.RootElement.GetProperty("properties").GetProperty("parameter");

        var rainDict = paramNode.GetProperty("PRECTOTCORR");
        var gwetTopDict = paramNode.GetProperty("GWETTOP");
        var gwetProfDict = paramNode.GetProperty("GWETPROF");
        var tempMaxDict = paramNode.GetProperty("T2M_MAX");

        var history = new List<HistoricalDayRecordDto>(3650);
        double latestRain = 0, latestTop = 0.4, latestProf = 0.4, latestTemp = 22.0;

        foreach (var dayProp in rainDict.EnumerateObject())
        {
            string rawDate = dayProp.Name; // Formato YYYYMMDD
            double rainVal = dayProp.Value.GetDouble();
            double topVal = gwetTopDict.TryGetProperty(rawDate, out var tEl) ? tEl.GetDouble() : -999;
            double profVal = gwetProfDict.TryGetProperty(rawDate, out var pEl) ? pEl.GetDouble() : -999;
            double tempVal = tempMaxDict.TryGetProperty(rawDate, out var tmEl) ? tmEl.GetDouble() : -999;

            // Descartar valores vacíos (-999) de los sensores de la NASA
            if (rainVal < 0) continue;

            double cleanTop = topVal >= 0 ? topVal : latestTop;
            string formattedDate = $"{rawDate[..4]}-{rawDate[4..6]}-{rawDate[6..8]}";

            history.Add(new HistoricalDayRecordDto(formattedDate, Math.Round(rainVal, 2), Math.Round(cleanTop, 3)));

            latestRain = Math.Round(rainVal, 2);
            if (topVal >= 0) latestTop = Math.Round(topVal, 3);
            if (profVal >= 0) latestProf = Math.Round(profVal, 3);
            if (tempVal > -100) latestTemp = Math.Round(tempVal, 1);
        }

        return (latestRain, latestTop, latestProf, latestTemp, history);
    }

    private async Task<List<RawForecastDayDto>> FetchOpenMeteoForecastAsync(double lat, double lon, CancellationToken ct)
    {
        string url = FormattableString.Invariant(
            $"https://api.open-meteo.com/v1/forecast?latitude={lat:F4}&longitude={lon:F4}&daily=precipitation_sum&timezone=auto&forecast_days=7");

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var daily = doc.RootElement.GetProperty("daily");
        var dates = daily.GetProperty("time").EnumerateArray().ToList();
        var rains = daily.GetProperty("precipitation_sum").EnumerateArray().ToList();

        var forecast = new List<RawForecastDayDto>();
        for (int i = 0; i < Math.Min(dates.Count, rains.Count); i++)
        {
            double rain = rains[i].ValueKind != JsonValueKind.Null ? rains[i].GetDouble() : 0.0;
            forecast.Add(new RawForecastDayDto(dates[i].GetString()!, Math.Round(rain, 2)));
        }

        return forecast;
    }

    private async Task<List<GeoPoint3DDto>> FetchTerrainGrid5KmAsync(double lat, double lon, double radiusKm, CancellationToken ct)
    {
        // Generamos una grilla de 5x5 (25 puntos) dentro del radio de 5 km
        double maxOffsetDeg = (radiusKm / 111.32);
        var coords = new List<(double Lat, double Lon)>();

        for (int r = -2; r <= 2; r++)
        {
            for (int c = -2; c <= 2; c++)
            {
                double ptLat = Math.Round(lat + (r * (maxOffsetDeg / 2.0)), 5);
                double ptLon = Math.Round(lon + (c * (maxOffsetDeg / 2.0)), 5);
                coords.Add((ptLat, ptLon));
            }
        }

        string latsParam = string.Join(",", coords.Select(p => p.Lat.ToString(CultureInfo.InvariantCulture)));
        string lonsParam = string.Join(",", coords.Select(p => p.Lon.ToString(CultureInfo.InvariantCulture)));

        string url = $"https://api.open-meteo.com/v1/elevation?latitude={latsParam}&longitude={lonsParam}";

        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var elevations = doc.RootElement.GetProperty("elevation").EnumerateArray().ToList();

        var grid3D = new List<GeoPoint3DDto>(coords.Count);
        for (int i = 0; i < coords.Count; i++)
        {
            double elev = i < elevations.Count && elevations[i].ValueKind != JsonValueKind.Null
                ? elevations[i].GetDouble()
                : 0.0;
            grid3D.Add(new GeoPoint3DDto(coords[i].Lat, coords[i].Lon, Math.Round(elev, 1)));
        }

        return grid3D;
    }
}