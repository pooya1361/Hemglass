namespace Hemglass.ETA.Core.Models;

public record TruckPosition(
    string TruckId,
    GeoCoordinate Position,
    double Speed,
    double Heading,
    double FreezerTemp,
    DateTime Timestamp
);
