using ChatApplication.Data;
using ChatApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApplication.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AdminController(ApplicationDbContext db)
        {
            _db = db;
        }

        // ============================
        // DASHBOARD
        // ============================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewBag.TotalUsers = await _db.Users.CountAsync();
            ViewBag.TotalConversations =
                await _db.Conversations.CountAsync();
            ViewBag.TotalMessages =
                await _db.Messages.CountAsync();

            ViewBag.RecentUsers = await _db.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .ToListAsync();

            return View();
        }

        // ============================
        // USERS
        // ============================
        [HttpGet]
        public async Task<IActionResult> Users(string? search)
        {
            var query = _db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                    u.Name.Contains(search) ||
                    u.Email.Contains(search) ||
                    u.Role.Contains(search));
            }

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            ViewBag.Search = search;

            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> Conversations()
        {
            var conversations = await _db.Conversations
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(conversations);
        }

        [HttpGet]
        public async Task<IActionResult> Messages()
        {
            var messages = await _db.Messages
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            return View(messages);
        }
    }
}