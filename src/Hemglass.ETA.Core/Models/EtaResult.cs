namespace Hemglass.ETA.Core.Models;

public record EtaResult(
    int RouteId,
    DateTime CalculatedAt,
    string CurrentStopAddress,
    int RemainingStopsCount,
    int AverageDwellMinutes,
    List<StopEta> Stops
);
