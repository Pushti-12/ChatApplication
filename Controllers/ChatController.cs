
using System.Security.Claims;
using ChatApplication.Data;
using ChatApplication.Hubs;
using ChatApplication.Models;
using ChatApplication.Services;
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
        private readonly ChatTextPredictionService _predictionService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            ApplicationDbContext db,
            IHubContext<ChatHub> hubContext,
            ChatTextPredictionService predictionService,
            ILogger<ChatController> logger)
        {
            _db = db;
            _hubContext = hubContext;
            _predictionService = predictionService;
            _logger = logger;
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
                return RedirectToAction("Login", "Auth");
            }

            var users = await _db.Users
                .Where(u => u.Id != currentUserId.Value)
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.CurrentUserId = currentUserId.Value;

            return View(users);
        }

        // =====================================================
        // CONVERSATION
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Conversation(int userId)
        {
            var currentUserId = GetCurrentUserId();

            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (currentUserId.Value == userId)
            {
                return BadRequest();
            }

            var otherUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (otherUser == null)
            {
                return NotFound();
            }

            var conversation = await _db.Conversations
                .FirstOrDefaultAsync(c =>
                    (c.User1Id == currentUserId.Value &&
                     c.User2Id == userId)
                    ||
                    (c.User1Id == userId &&
                     c.User2Id == currentUserId.Value));

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    User1Id = currentUserId.Value,
                    User2Id = userId,
                    CreatedAt = DateTime.Now
                };

                _db.Conversations.Add(conversation);
                await _db.SaveChangesAsync();
            }

            // Mark incoming unread messages as read.
            var unreadMessages = await _db.Messages
                .Where(m =>
                    m.ConversationId == conversation.Id &&
                    m.SenderId != currentUserId.Value &&
                    m.Status != "Read")
                .ToListAsync();

            if (unreadMessages.Any())
            {
                foreach (var message in unreadMessages)
                {
                    message.Status = "Read";
                }

                await _db.SaveChangesAsync();

                foreach (var message in unreadMessages)
                {
                    await _hubContext.Clients
                        .User(message.SenderId.ToString())
                        .SendAsync("MessageRead", message.Id);
                }
            }

            var messages = await _db.Messages
                .Where(m => m.ConversationId == conversation.Id)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            var users = await _db.Users
                .Where(u => u.Id != currentUserId.Value)
                .OrderBy(u => u.Name)
                .ToListAsync();

            var viewModel = new ChatViewModel
            {
                ConversationId = conversation.Id,
                OtherUser = otherUser,
                Messages = messages,
                Users = users
            };

            ViewBag.CurrentUserId = currentUserId.Value;

            // ML.NET feedback for the last submitted message.
            ViewBag.ChatTextPrediction =
                TempData["ChatTextPrediction"];

            ViewBag.ChatTextPredictionMessage =
                TempData["ChatTextPredictionMessage"];

            ViewBag.ChatTextPredictionConfidence =
                TempData["ChatTextPredictionConfidence"];

            return View(viewModel);
        }

        // =====================================================
        // SEND MESSAGE + ML.NET PREDICTION
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(
            int conversationId,
            string messageText)
        {
            var currentUserId = GetCurrentUserId();

            if (currentUserId == null)
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(messageText))
            {
                return BadRequest("Message cannot be empty.");
            }

            var conversation = await _db.Conversations
                .FirstOrDefaultAsync(c =>
                    c.Id == conversationId &&
                    (c.User1Id == currentUserId.Value ||
                     c.User2Id == currentUserId.Value));

            if (conversation == null)
            {
                return NotFound();
            }

            messageText = messageText.Trim();

            // 1. Classify the message.
            // Prediction failure will not block normal messaging.
            string? prediction = null;
            float confidence = 0f;

            try
            {
                var result = _predictionService.PredictWithConfidence(messageText);

                prediction = result.Label;
                confidence = result.Confidence;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "ML prediction failed for conversation {ConversationId}.",
                    conversationId);
            }

            // 2. Save the message to the existing database.
            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = currentUserId.Value,
                MessageText = messageText,
                SentAt = DateTime.Now,
                Status = "Sent"
            };

            _db.Messages.Add(message);
            await _db.SaveChangesAsync();

            var receiverId =
                conversation.User1Id == currentUserId.Value
                    ? conversation.User2Id
                    : conversation.User1Id;

            // 3. Deliver the message using the existing SignalR flow.
            await _hubContext.Clients
                .User(receiverId.ToString())
                .SendAsync(
                    "ReceiveMessage",
                    new
                    {
                        messageId = message.Id,
                        conversationId = message.ConversationId,
                        senderId = message.SenderId,
                        messageText = message.MessageText,
                        sentAt = message.SentAt.ToString("hh:mm tt"),
                        status = message.Status
                    });

            // 4. Keep the prediction for the redirected page.
            TempData["ChatTextPrediction"] =
                prediction ?? "Prediction unavailable";

            TempData["ChatTextPredictionMessage"] = messageText;
            TempData["ChatTextPredictionConfidence"] = confidence.ToString(
    System.Globalization.CultureInfo.InvariantCulture
);
            // Preserve the existing redirect behavior.
            return RedirectToAction(
                nameof(Conversation),
                new { userId = receiverId });
        }

        // =====================================================
        // CURRENT USER
        // =====================================================

        private int? GetCurrentUserId()
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (int.TryParse(userId, out var id))
            {
                return id;
            }

            return null;
        }
    }
}
