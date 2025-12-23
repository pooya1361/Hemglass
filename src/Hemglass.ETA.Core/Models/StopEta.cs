namespace Hemglass.ETA.Core.Models;

public record StopEta(
    int StopId,
    string Name,
    double Latitude,
    double Longitude,
    DateTime EstimatedArrival,
    int MinutesFromNow,
    int TravelMinutes
);
