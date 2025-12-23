namespace Hemglass.ETA.Core.Models;

public record Route(
    int RouteId,
    List<Stop> Stops
);
