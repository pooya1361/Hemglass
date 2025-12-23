using Microsoft.AspNetCore.SignalR;

namespace Hemglass.ETA.Api.Hubs;

public class EtaHub : Hub
{
    public async Task JoinRoute(int routeId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"route-{routeId}");
    }

    public async Task LeaveRoute(int routeId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"route-{routeId}");
    }
}
