using Hemglass.ETA.Core.Models;

namespace Hemglass.ETA.Core.Services;

public class EtaCalculator
{
    private readonly IRoutingService _routing;
    private const int MaxStops = 10;
    private const int DefaultDwellMinutes = 3; // Fallback if schedule data unavailable

    public EtaCalculator(IRoutingService routing)
    {
        _routing = routing;
    }

    public async Task<EtaResult> CalculateEtas(Route route, GeoCoordinate truckPosition, int? fromStopId = null)
    {
        // Filter stops: if fromStopId provided, start from the stop AFTER that one
        var orderedStops = route.Stops.OrderBy(s => s.Sequence).ToList();
        IEnumerable<Stop> stopsToConsider = orderedStops;

        if (fromStopId.HasValue)
        {
            var fromIndex = orderedStops.FindIndex(s => s.StopId == fromStopId.Value);
            if (fromIndex >= 0)
            {
                stopsToConsider = orderedStops.Skip(fromIndex + 1);
            }
        }

        var remainingStops = stopsToConsider.Take(MaxStops).ToList();
        var stopEtas = new List<StopEta>();
        var startTime = DateTime.UtcNow;
        var currentTime = startTime;

        // Build list of all points: truck position followed by stops
        var points = new List<GeoCoordinate> { truckPosition };
        points.AddRange(remainingStops.Select(s => s.Position));

        // Get all travel times in a single API call
        var travelTimes = await _routing.GetSequentialTravelTimesMinutes(points);

        // Calculate average dwell time from the full route schedule
        var avgDwellMinutes = CalculateAverageDwellTime(route.Stops, travelTimes);

        for (int i = 0; i < remainingStops.Count; i++)
        {
            var stop = remainingStops[i];
            var travelMinutes = i < travelTimes.Length ? travelTimes[i] : 0;

            currentTime = currentTime.AddMinutes(travelMinutes);
            var minutesFromNow = (int)(currentTime - startTime).TotalMinutes;

            stopEtas.Add(new StopEta(
                stop.StopId,
                stop.Name,
                stop.Position.Latitude,
                stop.Position.Longitude,
                currentTime,
                minutesFromNow,
                travelMinutes));

            currentTime = currentTime.AddMinutes(avgDwellMinutes);
        }

        // Determine current stop address
        var remainingCount = stopsToConsider.Count();
        string currentStopAddress;

        if (fromStopId.HasValue)
        {
            var currentStop = orderedStops.FirstOrDefault(s => s.StopId == fromStopId.Value);
            currentStopAddress = currentStop?.Name ?? "Unknown";
        }
        else
        {
            currentStopAddress = orderedStops.FirstOrDefault()?.Name ?? "Unknown";
        }

        return new EtaResult(
            route.RouteId,
            DateTime.UtcNow,
            currentStopAddress,
            remainingCount,
            avgDwellMinutes,
            stopEtas
        );
    }

    private int CalculateAverageDwellTime(List<Stop> allStops, int[] travelTimes)
    {
        if (allStops.Count < 2)
            return DefaultDwellMinutes;

        // Calculate schedule gaps between consecutive stops
        var scheduleGaps = new List<int>();
        for (int i = 0; i < allStops.Count - 1; i++)
        {
            var current = allStops[i];
            var next = allStops[i + 1];

            if (current.PlannedArrival != DateTime.MinValue && next.PlannedArrival != DateTime.MinValue)
            {
                var gap = (int)(next.PlannedArrival - current.PlannedArrival).TotalMinutes;
                if (gap > 0)
                    scheduleGaps.Add(gap);
            }
        }

        if (scheduleGaps.Count == 0)
            return DefaultDwellMinutes;

        // Average schedule gap
        var avgGap = scheduleGaps.Average();

        // Calculate average travel time from the samples we have
        var avgTravel = travelTimes.Length > 0 ? travelTimes.Average() : 0;

        // Average dwell = average gap - average travel (minimum 1 minute)
        var avgDwell = (int)Math.Max(1, avgGap - avgTravel);

        return avgDwell;
    }
}
