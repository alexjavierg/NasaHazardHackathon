namespace NasaHazard.Api.Contracts.Responses;

public sealed record EmergencyInfrastructureResponse(
    double Latitude,
    double Longitude,
    double RadiusKm,
    int EstimatedPopulation,
    string Source,
    DateTimeOffset RetrievedAtUtc,
    string Notice,
    IReadOnlyList<EmergencyFacilityDto> Facilities,
    HumanitarianLogisticsDto HumanitarianLogistics
);

public sealed record EmergencyFacilityDto(
    string OsmType,
    long OsmId,
    string Category,
    string Name,
    double Latitude,
    double Longitude,
    double DistanceKm,
    string SafetyStatus,
    IReadOnlyDictionary<string, string> Tags
);

public sealed record HumanitarianLogisticsDto(
    int PlanningPeriodDays,
    double WaterLitersPerPersonPerDay,
    long DrinkingAndDomesticWaterLiters,
    int PeoplePerFamilyTentAssumption,
    int FamilyTents,
    int? FirstAidKits,
    string FirstAidKitEstimateStatus,
    int? RescueBrigades,
    string RescueBrigadeEstimateStatus,
    IReadOnlyList<string> Assumptions
);
