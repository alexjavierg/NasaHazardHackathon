using Microsoft.AspNetCore.Mvc;
using NasaHazard.Api.Contracts.Responses;
using NasaHazard.Api.Services.Interfaces;

namespace NasaHazard.Api.Controllers;

[ApiController]
[Route("api/v1/emergency")]
public sealed class EmergencyInfrastructureController : ControllerBase
{
    private const double WaterLitersPerPersonPerDay = 15.0;
    private const int PlanningPeriodDays = 3;
    private const int PeoplePerFamilyTent = 5;

    private readonly IEmergencyInfrastructureService _infrastructureService;
    private readonly ILogger<EmergencyInfrastructureController> _logger;

    public EmergencyInfrastructureController(
        IEmergencyInfrastructureService infrastructureService,
        ILogger<EmergencyInfrastructureController> logger)
    {
        _infrastructureService = infrastructureService;
        _logger = logger;
    }

    [HttpGet("shelters-and-aid")]
    [ProducesResponseType(typeof(EmergencyInfrastructureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<EmergencyInfrastructureResponse>> GetSheltersAndAid(
        [FromQuery] double latitude = -11.9365,
        [FromQuery] double longitude = -76.6912,
        [FromQuery] double radiusKm = 5.0,
        [FromQuery] int estimatedPopulation = 500,
        CancellationToken ct = default)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90 ||
            !double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            return BadRequest(new { message = "Indica latitud y longitud válidas." });
        }

        if (!double.IsFinite(radiusKm) || radiusKm is <= 0 or > 5)
        {
            return BadRequest(new { message = "radiusKm debe ser mayor que 0 y no superar 5 km." });
        }

        if (estimatedPopulation is < 1 or > 1_000_000)
        {
            return BadRequest(new { message = "estimatedPopulation debe estar entre 1 y 1,000,000." });
        }

        try
        {
            var facilities = await _infrastructureService.FindNearbyFacilitiesAsync(latitude, longitude, radiusKm, ct);
            var responseFacilities = facilities.Select(facility => new EmergencyFacilityDto(
                OsmType: facility.OsmType,
                OsmId: facility.OsmId,
                Category: facility.Category,
                Name: facility.Name,
                Latitude: facility.Latitude,
                Longitude: facility.Longitude,
                DistanceKm: facility.DistanceKm,
                SafetyStatus: "not_assessed",
                Tags: facility.Tags
            )).ToArray();

            var waterLiters = (long)estimatedPopulation * (long)WaterLitersPerPersonPerDay * PlanningPeriodDays;
            var tents = (int)Math.Ceiling(estimatedPopulation / (double)PeoplePerFamilyTent);
            var logistics = new HumanitarianLogisticsDto(
                PlanningPeriodDays: PlanningPeriodDays,
                WaterLitersPerPersonPerDay: WaterLitersPerPersonPerDay,
                DrinkingAndDomesticWaterLiters: waterLiters,
                PeoplePerFamilyTentAssumption: PeoplePerFamilyTent,
                FamilyTents: tents,
                FirstAidKits: null,
                FirstAidKitEstimateStatus: "not_calculated_no_defined_ratio",
                RescueBrigades: null,
                RescueBrigadeEstimateStatus: "not_calculated_no_defined_capacity",
                Assumptions:
                [
                    "El agua se estima a 15 litros por persona por día durante 72 horas; ajustar según contexto, accesibilidad y necesidades locales.",
                    "Las carpas se estiman a razón de una por cada cinco personas y se redondean hacia arriba; es un supuesto de planificación, no un estándar universal.",
                    "No se calculan kits médicos ni brigadas sin definir el tipo/capacidad del kit y la capacidad operativa de cada brigada."
                ]);

            return Ok(new EmergencyInfrastructureResponse(
                Latitude: latitude,
                Longitude: longitude,
                RadiusKm: radiusKm,
                EstimatedPopulation: estimatedPopulation,
                Source: "OpenStreetMap contributors vía Overpass API",
                RetrievedAtUtc: DateTimeOffset.UtcNow,
                Notice: "Los centros proceden de datos colaborativos OSM: su ausencia no prueba que no existan y su presencia no confirma que estén abiertos, operativos o disponibles como refugio. SafetyStatus queda sin evaluar; esta respuesta no determina lugares seguros para evacuar.",
                Facilities: responseFacilities,
                HumanitarianLogistics: logistics));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Overpass agotó el tiempo de espera al buscar infraestructura de emergencia.");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "OpenStreetMap Overpass tardó demasiado en responder. Intenta de nuevo más tarde." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No fue posible consultar OpenStreetMap Overpass.");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "La consulta de infraestructura está temporalmente no disponible." });
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Overpass devolvió una respuesta JSON no válida.");
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "OpenStreetMap Overpass devolvió datos que no se pudieron interpretar." });
        }
    }
}
