using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatApplication.Hubs
{
    [Authorize]
    public class ScriptHub : Hub
    {
        private const string UserDashboardGroup = "UserDashboards";

        private const int MaxScriptLength = 5000;


        // =========================================================
        // CONNECTION
        // =========================================================

        public override async Task OnConnectedAsync()
        {
            try
            {
                // Only normal users should receive admin dashboard updates.
                // IsInRole() uses the role configuration of ASP.NET Core
                // instead of manually looking for a particular claim.
                if (!Context.User!.IsInRole("ADMIN"))
                {
                    await Groups.AddToGroupAsync(
                        Context.ConnectionId,
                        UserDashboardGroup
                    );
                }

                Console.WriteLine(
                    $"ScriptHub connected: " +
                    $"ConnectionId={Context.ConnectionId}, " +
                    $"User={Context.User?.Identity?.Name}, " +
                    $"IsAdmin={Context.User?.IsInRole("ADMIN")}"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"ScriptHub OnConnectedAsync error: {ex}"
                );

                throw;
            }

            await base.OnConnectedAsync();
        }


        // =========================================================
        // DISCONNECTION
        // =========================================================

        public override async Task OnDisconnectedAsync(
            Exception? exception)
        {
            Console.WriteLine(
                $"ScriptHub disconnected: " +
                $"ConnectionId={Context.ConnectionId}, " +
                $"User={Context.User?.Identity?.Name}"
            );

            await base.OnDisconnectedAsync(exception);
        }


        // =========================================================
        // SEND SCRIPT
        // =========================================================


        public async Task SendScript(string script)
        {
            // Only ADMIN can broadcast scripts.
            if (Context.User?.IsInRole("ADMIN") != true)
            {
                throw new HubException(
                    "Only administrators can send scripts."
                );
            }

            if (string.IsNullOrWhiteSpace(script))
            {
                throw new HubException(
                    "Script cannot be empty."
                );
            }

            if (script.Length > MaxScriptLength)
            {
                throw new HubException(
                    $"Script cannot exceed {MaxScriptLength} characters."
                );
            }

            Console.WriteLine(
                $"Admin script sent by: {Context.User.Identity?.Name}"
            );

            Console.WriteLine(
                $"Script length: {script.Length}"
            );

            await Clients
                .Group(UserDashboardGroup)
                .SendAsync("ReceiveScript", script);
        }

    }
}