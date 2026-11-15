using Microsoft.AspNetCore.Mvc;
using NasaHazard.Api.Contracts.Responses;
using NasaHazard.Api.Services.Interfaces;

namespace NasaHazard.Api.Controllers;

[ApiController]
[Route("api/v1/hotspots")]
public sealed class GlobalHotspotsController : ControllerBase
{
    private readonly INasaEonetService _eonetService;
    private readonly ILogger<GlobalHotspotsController> _logger;

    private static readonly IReadOnlyList<HotspotPresetDto> Presets =
    [
        new("quirio-chosica", "Quebrada Quirio - Chosica", "Perú", -11.9365, -76.6912, 5.0, false,
            "Ubicación piloto proporcionada para pruebas; el preset no certifica un nivel de riesgo."),
        new("malanche-punta-hermosa", "Quebrada Malanche - Punta Hermosa", "Perú", -12.3314, -76.8231, 5.0, false,
            "Ubicación piloto proporcionada para pruebas; el preset no certifica un nivel de riesgo."),
        new("piura-river-basin", "Cuenca del río Piura", "Perú", -5.1945, -80.6328, 5.0, false,
            "Ubicación piloto proporcionada para pruebas; el preset no certifica un nivel de riesgo."),
        new("san-lazaro-arequipa", "Torrentera San Lázaro - Arequipa", "Perú", -16.3989, -71.5350, 5.0, false,
            "Ubicación piloto proporcionada para pruebas; el preset no certifica un nivel de riesgo."),
        new("petropolis-rio", "Petrópolis - Río de Janeiro", "Brasil", -22.5049, -43.1786, 5.0, false,
            "Ubicación piloto proporcionada para pruebas; el preset no certifica un nivel de riesgo."),
        new("mocoa-putumayo", "Mocoa - Putumayo", "Colombia", 1.1478, -76.6481, 5.0, false,
            "Ubicación piloto proporcionada para pruebas; Mocoa está en Putumayo.")
    ];

    public GlobalHotspotsController(INasaEonetService eonetService, ILogger<GlobalHotspotsController> logger)
    {
        _eonetService = eonetService;
        _logger = logger;
    }

    [HttpGet("presets")]
    [ProducesResponseType(typeof(HotspotPresetsResponse), StatusCodes.Status200OK)]
    public ActionResult<HotspotPresetsResponse> GetPresets()
    {
        return Ok(new HotspotPresetsResponse(
            Notice: "Presets de demostración. Sus coordenadas no representan por sí solas una clasificación validada de riesgo.",
            Presets: Presets));
    }

    [HttpGet("nasa-live-events")]
    [ProducesResponseType(typeof(NasaLiveEventsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<NasaLiveEventsResponse>> GetNasaLiveEvents(CancellationToken ct)
    {
        try
        {
            var events = await _eonetService.GetOpenEventsAsync(ct);
            return Ok(new NasaLiveEventsResponse(
                Source: "NASA EONET v3",
                Status: "open",
                RetrievedAtUtc: DateTimeOffset.UtcNow,
                Notice: "EONET reporta eventos abiertos según su curaduría; abiertos no significa necesariamente iniciados hoy. Las coordenadas son representativas de la geometría más reciente.",
                Events: events));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("NASA EONET agotó el tiempo de espera.");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "NASA EONET tardó demasiado en responder. Intenta de nuevo más tarde." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No fue posible consultar NASA EONET.");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "NASA EONET no está disponible temporalmente." });
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "NASA EONET devolvió una respuesta JSON no válida.");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "NASA EONET devolvió datos que no se pudieron interpretar." });
        }
    }
}
