namespace Hemglass.ETA.Core.Models;

public record Stop(
    int StopId,
    string Name,
    GeoCoordinate Position,
    int Sequence,
    DateTime PlannedArrival
);
