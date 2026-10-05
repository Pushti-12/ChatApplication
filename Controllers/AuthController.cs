using System.Security.Claims;
using System.Security.Cryptography;
using ChatApplication.Data;
using ChatApplication.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatApplication.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthController(ApplicationDbContext db)
        {
            _db = db;
            _passwordHasher = new PasswordHasher<User>();
        }


        // =====================================================
        // REGISTER - GET
        // =====================================================

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel
            {
                Role = "USER"
            });
        }


        // =====================================================
        // REGISTER - POST
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Normalize role
            model.Role = model.Role.Trim().ToUpper();

            // Only allow valid roles
            if (model.Role != "USER" && model.Role != "ADMIN")
            {
                ModelState.AddModelError(
                    "Role",
                    "Please select a valid role."
                );

                return View(model);
            }

            // Check duplicate email
            var emailExists = await _db.Users
                .AnyAsync(u => u.Email == model.Email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    "Email",
                    "An account with this email already exists."
                );

                return View(model);
            }

            // Create user
            var user = new User
            {
                Name = model.Name.Trim(),
                Email = model.Email.Trim(),
                PhoneNumber = model.PhoneNumber.Trim(),

                // Role selected from registration form
                Role = model.Role,

                CreatedAt = DateTime.Now
            };

            // Hash password
            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                model.Password
            );

            _db.Users.Add(user);

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Account created successfully as {user.Role}. Please login.";

            return RedirectToAction(nameof(Login));
        }


        // =====================================================
        // LOGIN - GET
        // =====================================================

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }


        // =====================================================
        // LOGIN - POST
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Find user
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null)
            {
                ModelState.AddModelError(
                    "",
                    "Invalid email or password."
                );

                return View(model);
            }

            // Verify password
            var passwordResult =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    model.Password
                );

            if (passwordResult == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(
                    "",
                    "Invalid email or password."
                );

                return View(model);
            }

            // Normalize role from database
            var role = user.Role?.Trim().ToUpper();

            // Allow only known roles
            if (role != "ADMIN" && role != "USER")
            {
                ModelState.AddModelError(
                    "",
                    "Your account has an invalid role. Please contact administrator."
                );

                return View(model);
            }

            // =================================================
            // CREATE AUTHENTICATION CLAIMS
            // =================================================

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()
                ),

                new Claim(
                    ClaimTypes.Name,
                    user.Name
                ),

                new Claim(
                    ClaimTypes.Email,
                    user.Email
                ),

                new Claim(
                    ClaimTypes.Role,
                    role
                )
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            var principal = new ClaimsPrincipal(identity);

            // Create login cookie
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal
            );


            // =================================================
            // ROLE BASED REDIRECTION
            // =================================================

            if (role == "ADMIN")
            {
                return RedirectToAction(
                    "Index",
                    "Admin"
                );
            }

            // USER
            return RedirectToAction(
                "Index",
                "Chat"
            );
        }


        // =====================================================
        // LOGOUT
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme
            );

            return RedirectToAction(nameof(Login));
        }


        // =====================================================
        // FORGOT PASSWORD - GET
        // =====================================================

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }


        // =====================================================
        // FORGOT PASSWORD - POST
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(
            ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            // Don't reveal whether email exists
            if (user == null)
            {
                TempData["SuccessMessage"] =
                    "If an account exists with this email, a reset link has been sent.";

                return RedirectToAction(nameof(ForgotPassword));
            }

            // Generate secure random token
            var randomBytes = RandomNumberGenerator.GetBytes(32);

            user.PasswordResetToken =
                Convert.ToBase64String(randomBytes);

            user.PasswordResetTokenExpiry =
                DateTime.UtcNow.AddMinutes(30);

            await _db.SaveChangesAsync();

            // Create reset URL
            var resetUrl = Url.Action(
                nameof(ResetPassword),
                "Auth",
                new
                {
                    email = user.Email,
                    token = user.PasswordResetToken
                },
                Request.Scheme
            );

            // Development purpose only
            TempData["ResetLink"] = resetUrl;

            TempData["SuccessMessage"] =
                "Password reset link generated.";

            return RedirectToAction(nameof(ForgotPassword));
        }


        // =====================================================
        // RESET PASSWORD - GET
        // =====================================================

        [HttpGet]
        public IActionResult ResetPassword(
            string email,
            string token)
        {
            if (
                string.IsNullOrEmpty(email) ||
                string.IsNullOrEmpty(token)
            )
            {
                return RedirectToAction(nameof(Login));
            }

            var model = new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }


        // =====================================================
        // RESET PASSWORD - POST
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == model.Email &&
                    u.PasswordResetToken == model.Token
                );

            if (user == null)
            {
                ModelState.AddModelError(
                    "",
                    "Invalid password reset request."
                );

                return View(model);
            }

            if (
                user.PasswordResetTokenExpiry == null ||
                user.PasswordResetTokenExpiry < DateTime.UtcNow
            )
            {
                ModelState.AddModelError(
                    "",
                    "This password reset link has expired."
                );

                return View(model);
            }

            // Hash new password
            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    model.Password
                );

            // Invalidate reset token
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "Password reset successfully. Please login.";

            return RedirectToAction(nameof(Login));
        }


        // =====================================================
        // ACCESS DENIED
        // =====================================================

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}