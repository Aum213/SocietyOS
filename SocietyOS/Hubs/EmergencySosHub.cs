using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SocietyOS.Hubs
{
    [Authorize(Roles = "SecurityGuard")]
    public class EmergencySosHub : Hub
    {
        public const string GuardsGroupName = "SecurityGuardsResponderGroup";

        public override async Task OnConnectedAsync()
        {
            if (Context.User?.IsInRole("SecurityGuard") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GuardsGroupName);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GuardsGroupName);
            await base.OnDisconnectedAsync(exception);
        }
    }
}