using Hemglass.ETA.Api.Hubs;
using Hemglass.ETA.Core.Models;
using Hemglass.ETA.Core.Services;
using Hemglass.ETA.Infrastructure;
using Hemglass.ETA.Infrastructure.Iceman;

var builder = WebApplication.CreateBuilder(args);

// Application Insights telemetry
builder.Services.AddApplicationInsightsTelemetry();

// swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Adds real-time communication hub support (stubbed for future use).
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173", // Vite dev server
                "https://victorious-meadow-099b28103.4.azurestaticapps.net" // Azure Static Web Apps
              )
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// HttpClient for external API calls
builder.Services.AddHttpClient<OpenRouteService>();
builder.Services.AddHttpClient<IcemanRouteService>();
builder.Services.AddScoped<IRoutingService, OpenRouteService>();
builder.Services.AddScoped<IRouteService, IcemanRouteService>();
builder.Services.AddSingleton<RouteStore>();
builder.Services.AddScoped<EtaCalculator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Get route info (cached)
app.MapGet("/api/route/{stopId}", async (int stopId, IRouteService routeService, RouteStore store) =>
{
    var route = await store.GetOrFetchAsync(stopId, routeService);
    if (route == null) return Results.NotFound(new { error = "Route not found" });

    return Results.Ok(new
    {
        route.RouteId,
        Stops = route.Stops.Select(s => new {
            s.StopId,
            s.Name,
            s.Position.Latitude,
            s.Position.Longitude
        })
    });
})
.WithName("GetRoute")
.WithOpenApi();

// Get ETA for stops (uses cached route)
app.MapGet("/api/eta/{stopId}", async (int stopId, double lat, double lon, int? fromStopId, IRouteService routeService, RouteStore store, EtaCalculator calculator) =>
{
    var route = await store.GetOrFetchAsync(stopId, routeService);
    if (route == null) return Results.NotFound(new { error = "Route not found" });

    var position = new GeoCoordinate(lat, lon);
    var result = await calculator.CalculateEtas(route, position, fromStopId);
    return Results.Ok(result);
})
.WithName("GetEta")
.WithOpenApi();

// Webhook endpoint for receiving real-time truck GPS positions from fleet tracking system
app.MapPost("/api/webhook/position", (TruckPosition position) =>
{
    return Results.Ok(new { received = true, truckId = position.TruckId });
})
.WithName("ReceivePosition")
.WithOpenApi();

app.MapHub<EtaHub>("/eta-hub");

app.Run();
