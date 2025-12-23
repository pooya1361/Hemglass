using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hemglass.ETA.Core.Models;
using Hemglass.ETA.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Hemglass.ETA.Infrastructure;

public class OpenRouteService : IRoutingService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<OpenRouteService> _logger;
    private const string BaseUrl = "https://api.openrouteservice.org/v2";

    public OpenRouteService(HttpClient http, IConfiguration config, ILogger<OpenRouteService> logger)
    {
        _http = http;
        _apiKey = config["OpenRouteService:ApiKey"] ?? "";
        _logger = logger;

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }
    }

    public async Task<int[]> GetSequentialTravelTimesMinutes(IReadOnlyList<GeoCoordinate> points)
    {
        if (points.Count < 2)
            return Array.Empty<int>();

        try
        {
            // OpenRouteService uses [longitude, latitude] order (GeoJSON)
            var locations = points.Select(p => new[] { p.Longitude, p.Latitude }).ToArray();

            var request = new OrsMatrixRequest
            {
                Locations = locations,
                Metrics = new[] { "duration" }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling OpenRouteService Matrix API for {PointCount} points", points.Count);

            var httpResponse = await _http.PostAsync($"{BaseUrl}/matrix/driving-car", content);
            var rawJson = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenRouteService API error: {Status} - {Response}",
                    httpResponse.StatusCode, rawJson.Length > 200 ? rawJson[..200] : rawJson);
                return new int[points.Count - 1];
            }

            var response = JsonSerializer.Deserialize<OrsMatrixResponse>(rawJson);

            if (response?.Durations == null)
            {
                _logger.LogWarning("OpenRouteService Matrix API returned null durations");
                return new int[points.Count - 1];
            }

            // Extract sequential travel times: durations[i][i+1]
            // OpenRouteService returns times in SECONDS
            var result = new int[points.Count - 1];
            for (int i = 0; i < points.Count - 1; i++)
            {
                var seconds = response.Durations[i][i + 1];
                result[i] = (int)Math.Ceiling(seconds / 60.0);
            }

            _logger.LogInformation("Travel times: {Times} min", string.Join(", ", result));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenRouteService Matrix API call failed for {PointCount} points", points.Count);
            return new int[points.Count - 1];
        }
    }
}

// Request/Response models
file class OrsMatrixRequest
{
    [JsonPropertyName("locations")]
    public double[][] Locations { get; set; } = Array.Empty<double[]>();

    [JsonPropertyName("metrics")]
    public string[] Metrics { get; set; } = Array.Empty<string>();
}

file class OrsMatrixResponse
{
    [JsonPropertyName("durations")]
    public double[][]? Durations { get; set; }
}
