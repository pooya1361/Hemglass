using Hemglass.ETA.Core.Models;
using Hemglass.ETA.Core.Services;
using Moq;

namespace Hemglass.ETA.Tests;

public class EtaCalculatorTests
{
    private readonly Mock<IRoutingService> _routingMock;
    private readonly EtaCalculator _calculator;

    public EtaCalculatorTests()
    {
        _routingMock = new Mock<IRoutingService>();
        _calculator = new EtaCalculator(_routingMock.Object);
    }

    [Fact]
    public async Task CalculateEtas_WithSingleStop_ReturnsCorrectEta()
    {
        var route = new Route(1, new List<Stop>
        {
            new(1, "Stop A", new GeoCoordinate(59.0, 18.0), 1, DateTime.Today.AddHours(14))
        });
        var truckPosition = new GeoCoordinate(59.1, 18.1);

        _routingMock.Setup(r => r.GetSequentialTravelTimesMinutes(It.IsAny<IReadOnlyList<GeoCoordinate>>()))
            .ReturnsAsync(new[] { 10 });

        var result = await _calculator.CalculateEtas(route, truckPosition);

        Assert.Single(result.Stops);
        Assert.Equal(1, result.Stops[0].StopId);
        Assert.Equal(10, result.Stops[0].MinutesFromNow);
        Assert.Equal(1, result.RemainingStopsCount);
        Assert.Equal("Stop A", result.CurrentStopAddress);
    }

    [Fact]
    public async Task CalculateEtas_WithMultipleStops_AccumulatesTime()
    {
        // Schedule: Stop A at 14:00, Stop B at 14:15, Stop C at 14:30
        // Schedule gaps: 15, 15 minutes
        // Travel times: 5, 5, 5 minutes (avg = 5)
        // Average dwell = avg gap (15) - avg travel (5) = 10 minutes
        var route = new Route(1, new List<Stop>
        {
            new(1, "Stop A", new GeoCoordinate(59.0, 18.0), 1, DateTime.Today.AddHours(14)),
            new(2, "Stop B", new GeoCoordinate(59.1, 18.1), 2, DateTime.Today.AddHours(14).AddMinutes(15)),
            new(3, "Stop C", new GeoCoordinate(59.2, 18.2), 3, DateTime.Today.AddHours(14).AddMinutes(30))
        });
        var truckPosition = new GeoCoordinate(58.9, 17.9);

        _routingMock.Setup(r => r.GetSequentialTravelTimesMinutes(It.IsAny<IReadOnlyList<GeoCoordinate>>()))
            .ReturnsAsync(new[] { 5, 5, 5 });

        var result = await _calculator.CalculateEtas(route, truckPosition);

        Assert.Equal(3, result.Stops.Count);
        Assert.Equal(3, result.RemainingStopsCount);
        // Each stop should have increasing MinutesFromNow
        Assert.True(result.Stops[1].MinutesFromNow > result.Stops[0].MinutesFromNow);
        Assert.True(result.Stops[2].MinutesFromNow > result.Stops[1].MinutesFromNow);
        // Average dwell time at top level (avg gap 15 - avg travel 5 = 10)
        Assert.Equal(10, result.AverageDwellMinutes);
        // Each stop has its travel time
        Assert.Equal(5, result.Stops[0].TravelMinutes);
        Assert.Equal(5, result.Stops[1].TravelMinutes);
        Assert.Equal(5, result.Stops[2].TravelMinutes);
    }

    [Fact]
    public async Task CalculateEtas_CallsRoutingServiceOnce()
    {
        var route = new Route(1, new List<Stop>
        {
            new(1, "Stop A", new GeoCoordinate(59.0, 18.0), 1, DateTime.Today.AddHours(14)),
            new(2, "Stop B", new GeoCoordinate(59.1, 18.1), 2, DateTime.Today.AddHours(14).AddMinutes(15))
        });
        var truckPosition = new GeoCoordinate(58.9, 17.9);

        _routingMock.Setup(r => r.GetSequentialTravelTimesMinutes(It.IsAny<IReadOnlyList<GeoCoordinate>>()))
            .ReturnsAsync(new[] { 10, 10 });

        await _calculator.CalculateEtas(route, truckPosition);

        // Matrix API is called once with all points
        _routingMock.Verify(r => r.GetSequentialTravelTimesMinutes(It.IsAny<IReadOnlyList<GeoCoordinate>>()),
            Times.Once);
    }
}
