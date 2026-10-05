using System.Collections.Concurrent;
using System.Security.Claims;
using ChatApplication.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatApplication.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _db;

        // UserId -> number of active SignalR connections
        private static readonly ConcurrentDictionary<int, int> OnlineUsers = new();

        public ChatHub(ApplicationDbContext db)
        {
            _db = db;
        }

        // =====================================================
        // USER CONNECTED
        // =====================================================

        public override async Task OnConnectedAsync()
        {
            var userId = GetCurrentUserId();

            if (userId.HasValue)
            {
                OnlineUsers.AddOrUpdate(
                    userId.Value,
                    1,
                    (_, count) => count + 1
                );

                // Tell everyone this user is online
                await Clients.All.SendAsync(
                    "UserOnline",
                    userId.Value
                );
            }

            await base.OnConnectedAsync();
        }


        // =====================================================
        // USER DISCONNECTED
        // =====================================================

        public override async Task OnDisconnectedAsync(
            Exception? exception)
        {
            var userId = GetCurrentUserId();

            if (userId.HasValue)
            {
                if (OnlineUsers.TryGetValue(
                    userId.Value,
                    out var count))
                {
                    if (count <= 1)
                    {
                        OnlineUsers.TryRemove(
                            userId.Value,
                            out _
                        );

                        // Tell everyone this user is offline
                        await Clients.All.SendAsync(
                            "UserOffline",
                            userId.Value
                        );
                    }
                    else
                    {
                        OnlineUsers[userId.Value] =
                            count - 1;
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }


        // =====================================================
        // GET CURRENT ONLINE USERS
        // =====================================================

        public Task<List<int>> GetOnlineUsers()
        {
            return Task.FromResult(
                OnlineUsers.Keys.ToList()
            );
        }


        // =====================================================
        // TYPING STARTED
        // =====================================================

        public async Task StartTyping(int receiverId)
        {
            var senderId = GetCurrentUserId();

            if (!senderId.HasValue)
            {
                return;
            }

            await Clients.User(receiverId.ToString())
                .SendAsync(
                    "UserTyping",
                    senderId.Value
                );
        }


        // =====================================================
        // TYPING STOPPED
        // =====================================================

        public async Task StopTyping(int receiverId)
        {
            var senderId = GetCurrentUserId();

            if (!senderId.HasValue)
            {
                return;
            }

            await Clients.User(receiverId.ToString())
                .SendAsync(
                    "UserStoppedTyping",
                    senderId.Value
                );
        }


        // =====================================================
        // MESSAGE DELIVERED
        // =====================================================

        public async Task MarkDelivered(
            int messageId,
            int senderId)
        {
            var currentUserId = GetCurrentUserId();

            if (!currentUserId.HasValue)
            {
                return;
            }

            var message = await _db.Messages
                .FirstOrDefaultAsync(
                    m => m.Id == messageId
                );

            if (message == null)
            {
                return;
            }

            // Only receiver can mark message delivered
            if (message.SenderId == currentUserId.Value)
            {
                return;
            }

            message.Status = "Delivered";

            await _db.SaveChangesAsync();

            await Clients.User(senderId.ToString())
                .SendAsync(
                    "MessageDelivered",
                    messageId
                );
        }


        // =====================================================
        // MESSAGE READ
        // =====================================================

        public async Task MarkRead(
            int messageId,
            int senderId)
        {
            var currentUserId = GetCurrentUserId();

            if (!currentUserId.HasValue)
            {
                return;
            }

            var message = await _db.Messages
                .FirstOrDefaultAsync(
                    m => m.Id == messageId
                );

            if (message == null)
            {
                return;
            }

            // Only receiver can mark message as read
            if (message.SenderId == currentUserId.Value)
            {
                return;
            }

            message.Status = "Read";

            await _db.SaveChangesAsync();

            await Clients.User(senderId.ToString())
                .SendAsync(
                    "MessageRead",
                    messageId
                );
        }


        // =====================================================
        // OLD TEST METHOD
        // =====================================================

        public async Task SendMessage(string message)
        {
            await Clients.All.SendAsync(
                "ReceiveMessage",
                message
            );
        }


        // =====================================================
        // GET CURRENT USER ID
        // =====================================================

        private int? GetCurrentUserId()
        {
            var userId = Context.User?
                .FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

            if (int.TryParse(
                userId,
                out var id))
            {
                return id;
            }

            return null;
        }
    }
}