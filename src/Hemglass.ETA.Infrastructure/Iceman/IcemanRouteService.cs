using System.Net.Http.Json;
using Hemglass.ETA.Core.Models;
using Hemglass.ETA.Core.Services;

namespace Hemglass.ETA.Infrastructure.Iceman;

public class IcemanRouteService : IRouteService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://iceman-prod.azurewebsites.net/api/tracker";

    public IcemanRouteService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Route?> GetRouteByStopAsync(int stopId)
    {
        var response = await _httpClient.GetFromJsonAsync<IcemanApiResponse>(
            $"{BaseUrl}/getroutebystop?stopId={stopId}");

        if (response?.Data == null || response.Data.Count == 0)
            return null;

        // Return ALL stops on the route for map display
        var allStops = response.Data
            .Select((s, index) => new Stop(
                StopId: s.StopId,
                Name: FormatAddress(s.StreetAddress, s.StreetNumber),
                Position: new GeoCoordinate(s.Latitude, s.Longitude),
                Sequence: index + 1,
                PlannedArrival: ParseScheduledTime(s.NextTime)
            ))
            .ToList();

        if (allStops.Count == 0)
            return null;

        return new Route(
            RouteId: stopId,
            Stops: allStops
        );
    }

    private static DateTime ParseScheduledTime(string nextTime)
    {
        // Parse time like "16:25" and combine with today's date
        if (TimeOnly.TryParse(nextTime, out var time))
        {
            return DateTime.Today.Add(time.ToTimeSpan());
        }
        return DateTime.MinValue;
    }

    private static string FormatAddress(string street, string number)
    {
        return string.IsNullOrWhiteSpace(number) ? street : $"{street} {number}";
    }
}
