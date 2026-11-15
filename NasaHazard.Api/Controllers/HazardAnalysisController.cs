using Microsoft.AspNetCore.Mvc;
using NasaHazard.Api.Algorithms;
using NasaHazard.Api.Contracts.Requests;
using NasaHazard.Api.Contracts.Responses;
using NasaHazard.Api.Services.Interfaces;

namespace NasaHazard.Api.Controllers;

[ApiController]
[Route("api/v1/hazard")]
public class HazardAnalysisController : ControllerBase
{
    private readonly ISatelliteDataService _satelliteService;
    private readonly HuaicoRiskEngine _riskEngine;

    public HazardAnalysisController(
        ISatelliteDataService satelliteService,
        HuaicoRiskEngine riskEngine)
    {
        _satelliteService = satelliteService;
        _riskEngine = riskEngine;
    }

    [HttpGet("analyze")]
    public async Task<ActionResult<HazardSimulationResponse>> AnalyzeLocationGet(
        [FromQuery] double latitude = -11.9365,
        [FromQuery] double longitude = -76.6912,
        [FromQuery] double radiusKm = 5.0,
        [FromQuery] double? simulatedRainMm = null,
        CancellationToken ct = default)
    {
        return await ExecuteAnalysisAsync(
            new AnalyzeLocationRequest(latitude, longitude, radiusKm, simulatedRainMm), ct);
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<HazardSimulationResponse>> AnalyzeLocationPost(
        [FromBody] AnalyzeLocationRequest? request = null,
        CancellationToken ct = default)
    {
        var req = request ?? new AnalyzeLocationRequest();
        return await ExecuteAnalysisAsync(req, ct);
    }

    private async Task<ActionResult<HazardSimulationResponse>> ExecuteAnalysisAsync(
        AnalyzeLocationRequest request, CancellationToken ct)
    {
        if (request.Latitude < -90 || request.Latitude > 90 || request.Longitude < -180 || request.Longitude > 180)
        {
            return BadRequest(new { message = "Coordenadas fuera de rango válido." });
        }

        double safeRadius = Math.Clamp(request.RadiusKm <= 0 ? 5.0 : request.RadiusKm, 1.0, 20.0);

        var rawData = await _satelliteService.GetNormalizedSatelliteDataAsync(
            request.Latitude,
            request.Longitude,
            safeRadius,
            ct);

        var response = _riskEngine.CalculateRiskAndSimulation(rawData, request.SimulatedRainMm);
        return Ok(response);
    }
}