using System.Security.Claims;
using ChatApplication.Data;
using ChatApplication.Hubs;
using ChatApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatApplication.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatController(
            ApplicationDbContext db,
            IHubContext<ChatHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }


        // =====================================================
        // CHAT INDEX
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUserId = GetCurrentUserId();

            if (currentUserId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Auth"
                );
            }

            var users = await _db.Users
                .Where(u =>
                    u.Id != currentUserId.Value)
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.CurrentUserId =
                currentUserId.Value;

            return View(users);
        }


        // =====================================================
        // CONVERSATION
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Conversation(
            int userId)
        {
            var currentUserId =
                GetCurrentUserId();

            if (currentUserId == null)
            {
                return RedirectToAction(
                    "Login",
                    "Auth"
                );
            }

            if (currentUserId.Value == userId)
            {
                return BadRequest();
            }

            var otherUser =
                await _db.Users
                    .FirstOrDefaultAsync(
                        u => u.Id == userId
                    );

            if (otherUser == null)
            {
                return NotFound();
            }

            var conversation =
                await _db.Conversations
                    .FirstOrDefaultAsync(c =>
                        (c.User1Id ==
                            currentUserId.Value &&
                         c.User2Id == userId)
                        ||
                        (c.User1Id == userId &&
                         c.User2Id ==
                            currentUserId.Value)
                    );

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    User1Id =
                        currentUserId.Value,

                    User2Id =
                        userId,

                    CreatedAt =
                        DateTime.Now
                };

                _db.Conversations.Add(
                    conversation
                );

                await _db.SaveChangesAsync();
            }


            // Mark incoming unread messages as read
            var unreadMessages =
                await _db.Messages
                    .Where(m =>
                        m.ConversationId ==
                            conversation.Id &&
                        m.SenderId !=
                            currentUserId.Value &&
                        m.Status != "Read")
                    .ToListAsync();

            if (unreadMessages.Any())
            {
                foreach (var message
                    in unreadMessages)
                {
                    message.Status = "Read";
                }

                await _db.SaveChangesAsync();

                foreach (var message
                    in unreadMessages)
                {
                    await _hubContext
                        .Clients
                        .User(
                            message.SenderId.ToString()
                        )
                        .SendAsync(
                            "MessageRead",
                            message.Id
                        );
                }
            }


            var messages =
                await _db.Messages
                    .Where(m =>
                        m.ConversationId ==
                            conversation.Id)
                    .OrderBy(m =>
                        m.SentAt)
                    .ToListAsync();


            var users =
                await _db.Users
                    .Where(u =>
                        u.Id !=
                            currentUserId.Value)
                    .OrderBy(u =>
                        u.Name)
                    .ToListAsync();


            var viewModel =
                new ChatViewModel
                {
                    ConversationId =
                        conversation.Id,

                    OtherUser =
                        otherUser,

                    Messages =
                        messages,

                    Users =
                        users
                };


            ViewBag.CurrentUserId =
                currentUserId.Value;


            return View(viewModel);
        }


        // =====================================================
        // SEND MESSAGE
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(
            int conversationId,
            string messageText)
        {
            var currentUserId =
                GetCurrentUserId();

            if (currentUserId == null)
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(
                messageText))
            {
                return BadRequest(
                    "Message cannot be empty."
                );
            }


            var conversation =
                await _db.Conversations
                    .FirstOrDefaultAsync(c =>
                        c.Id ==
                            conversationId &&
                        (
                            c.User1Id ==
                                currentUserId.Value
                            ||
                            c.User2Id ==
                                currentUserId.Value
                        )
                    );

            if (conversation == null)
            {
                return NotFound();
            }


            var message = new Message
            {
                ConversationId =
                    conversationId,

                SenderId =
                    currentUserId.Value,

                MessageText =
                    messageText.Trim(),

                SentAt =
                    DateTime.Now,

                Status =
                    "Sent"
            };


            _db.Messages.Add(message);

            await _db.SaveChangesAsync();


            var receiverId =
                conversation.User1Id ==
                    currentUserId.Value
                    ? conversation.User2Id
                    : conversation.User1Id;


            // =================================================
            // REAL-TIME MESSAGE
            // =================================================

            await _hubContext
                .Clients
                .User(
                    receiverId.ToString()
                )
                .SendAsync(
                    "ReceiveMessage",
                    new
                    {
                        messageId =
                            message.Id,

                        conversationId =
                            message.ConversationId,

                        senderId =
                            message.SenderId,

                        messageText =
                            message.MessageText,

                        sentAt =
                            message.SentAt
                                .ToString(
                                    "hh:mm tt"
                                ),

                        status =
                            message.Status
                    }
                );


            return RedirectToAction(
                nameof(Conversation),
                new
                {
                    userId =
                        receiverId
                }
            );
        }


        // =====================================================
        // CURRENT USER
        // =====================================================

        private int? GetCurrentUserId()
        {
            var userId =
                User.FindFirstValue(
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