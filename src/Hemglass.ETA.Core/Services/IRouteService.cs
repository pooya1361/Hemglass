using Hemglass.ETA.Core.Models;

namespace Hemglass.ETA.Core.Services;

public interface IRouteService
{
    Task<Route?> GetRouteByStopAsync(int stopId);
}
