using Hemglass.ETA.Core.Models;

namespace Hemglass.ETA.Core.Services;

public interface IRoutingService
{
    /// <summary>
    /// Gets travel times between consecutive points in a single API call.
    /// Returns array where result[i] = travel time from points[i] to points[i+1].
    /// </summary>
    Task<int[]> GetSequentialTravelTimesMinutes(IReadOnlyList<GeoCoordinate> points);
}
