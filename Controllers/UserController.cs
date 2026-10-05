using ChatApplication.Data;
using ChatApplication.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApplication.Controllers
{
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _db;

        public UserController(ApplicationDbContext db)
        {
            _db = db;
        }

        // =========================================================
        // READ
        // GET: /User
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index(string? search)
        {
            var query = _db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                    u.Name.Contains(search) ||
                    u.Email.Contains(search) ||
                    u.PhoneNumber.Contains(search) ||
                    u.Role.Contains(search));
            }

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            ViewBag.Search = search;

            return View(users);
        }


        // =========================================================
        // CREATE - GET
        // GET: /User/Create
        // =========================================================
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }


        // =========================================================
        // CREATE - POST
        // POST: /User/Create
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            if (!ModelState.IsValid)
            {
                return View(user);
            }

            // PasswordHash ko abhi manually set nahi kar rahe.
            // Authentication ke time proper password hashing add karenge.
            user.PasswordHash = "NOT_SET";

            user.CreatedAt = DateTime.Now;

            _db.Users.Add(user);

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


        // =========================================================
        // UPDATE - GET
        // GET: /User/Edit/5
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _db.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }


        // =========================================================
        // UPDATE - POST
        // POST: /User/Edit/5
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(user);
            }

            var existingUser = await _db.Users.FindAsync(id);

            if (existingUser == null)
            {
                return NotFound();
            }

            // Sirf editable fields update kar rahe hain
            existingUser.Name = user.Name;
            existingUser.Email = user.Email;
            existingUser.PhoneNumber = user.PhoneNumber;
            existingUser.Role = user.Role;

            // PasswordHash ko yahan change nahi kar rahe.
            // Password change ke liye later separate functionality banayenge.

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


        // =========================================================
        // DELETE
        // POST: /User/Delete/5
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            _db.Users.Remove(user);

            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}