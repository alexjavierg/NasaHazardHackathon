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

    [HttpPost("analyze")]
    public async Task<ActionResult<HazardSimulationResponse>> AnalyzeLocation(
        [FromBody] AnalyzeLocationRequest request,
        CancellationToken ct)
    {
        if (request.Latitude < -90 || request.Latitude > 90 || request.Longitude < -180 || request.Longitude > 180)
        {
            return BadRequest(new { message = "Coordenadas fuera de rango válido." });
        }

        var rawData = await _satelliteService.GetNormalizedSatelliteDataAsync(
            request.Latitude,
            request.Longitude,
            request.RadiusKm,
            ct);

        var response = _riskEngine.CalculateRiskAndSimulation(rawData, request.SimulatedRainMm);

        return Ok(response);
    }
}