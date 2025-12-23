using System.Collections.Concurrent;
using Hemglass.ETA.Core.Models;
using Hemglass.ETA.Core.Services;

namespace Hemglass.ETA.Infrastructure;

public class RouteStore
{
    private readonly ConcurrentDictionary<int, CachedRoute> _cache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

    public Route? Get(int stopId)
    {
        if (_cache.TryGetValue(stopId, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            return cached.Route;
        }
        return null;
    }

    public void Set(Route route, int requestedStopId)
    {
        var expiresAt = DateTime.UtcNow.Add(_cacheDuration);
        // Cache only by the requested stopId - each stopId has different remaining stops
        _cache[requestedStopId] = new CachedRoute(route, expiresAt);
    }

    public async Task<Route?> GetOrFetchAsync(int stopId, IRouteService routeService)
    {
        var route = Get(stopId);
        if (route == null)
        {
            route = await routeService.GetRouteByStopAsync(stopId);
            if (route != null)
                Set(route, stopId);
        }
        return route;
    }

    public void Invalidate(int stopId)
    {
        _cache.TryRemove(stopId, out _);
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public int Count => _cache.Count;

    private record CachedRoute(Route Route, DateTime ExpiresAt);
}
