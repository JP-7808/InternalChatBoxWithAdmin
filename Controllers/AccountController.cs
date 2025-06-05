using InternalChatbox.Data;
using InternalChatbox.Helpers;
using InternalChatbox.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using MimeKit;
using MailKit.Net.Smtp;
using System;

namespace InternalChatbox.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AccountController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(User model, string password)
        {
            ModelState.Remove("PasswordHash");

            if (ModelState.IsValid)
            {
                if (model.EmployeeId != null && await _context.Users.AnyAsync(u => u.EmployeeId == model.EmployeeId))
                {
                    ModelState.AddModelError("EmployeeId", "Employee ID is already in use.");
                    return View(model);
                }

                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email already registered.");
                    return View(model);
                }

                // Generate and send OTP
                Random random = new Random();
                int otp = random.Next(100000, 999999);
                DateTime expiry = DateTime.Now.AddMinutes(5);

                HttpContext.Session.SetString("RegistrationEmail", model.Email);
                HttpContext.Session.SetString("RegistrationOTP", otp.ToString());
                HttpContext.Session.SetString("OTPExpiry", expiry.ToString());
                HttpContext.Session.SetString("PendingName", model.Name);
                HttpContext.Session.SetString("PendingDesignation", model.Designation);
                HttpContext.Session.SetString("PendingLocation", model.Location);
                HttpContext.Session.SetString("PendingEmployeeId", model.EmployeeId);
                HttpContext.Session.SetString("PendingPassword", password);

                await SendOTPEmail(model.Email, otp);

                return RedirectToAction("VerifyOTP");
            }

            return View(model);
        }

        public IActionResult VerifyOTP()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOTP(string EnteredOTP)
        {
            var otp = HttpContext.Session.GetString("RegistrationOTP");
            var expiry = HttpContext.Session.GetString("OTPExpiry");
            var email = HttpContext.Session.GetString("RegistrationEmail");

            if (otp == null || expiry == null || email == null)
                return RedirectToAction("Register");

            if (DateTime.Now > DateTime.Parse(expiry))
            {
                TempData["Error"] = "OTP expired. Please register again.";
                return RedirectToAction("Register");
            }

            if (otp != EnteredOTP)
            {
                TempData["Error"] = "Incorrect OTP. Try again.";
                return View();
            }

            var user = new User
            {
                Name = HttpContext.Session.GetString("PendingName"),
                EmployeeId = HttpContext.Session.GetString("PendingEmployeeId"),
                Designation = HttpContext.Session.GetString("PendingDesignation"),
                Location = HttpContext.Session.GetString("PendingLocation"),
                Email = email,
                PasswordHash = PasswordHelper.HashPassword(HttpContext.Session.GetString("PendingPassword")),
                Role = "Employee",
                Status = "Offline"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("RegistrationEmail");
            HttpContext.Session.Remove("RegistrationOTP");
            HttpContext.Session.Remove("OTPExpiry");
            HttpContext.Session.Remove("PendingName");
            HttpContext.Session.Remove("PendingDesignation");
            HttpContext.Session.Remove("PendingLocation");
            HttpContext.Session.Remove("PendingEmployeeId");
            HttpContext.Session.Remove("PendingPassword");

            TempData["Success"] = "Registration successful. You may now login.";
            return RedirectToAction("Login");
        }

        private async Task SendOTPEmail(string email, int otp)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Chatbox App", "einfratechsolution@gmail.com"));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Your OTP Code";

            message.Body = new TextPart("plain")
            {
                Text = $"Your OTP is: {otp}. It is valid for 5 minutes."
            };

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync("smtp.gmail.com", 587, false);
                await client.AuthenticateAsync("einfratechsolution@gmail.com", "fdfe onao tctb nrsw");
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
        }

        public async Task<IActionResult> ResendOTP()
        {
            var email = HttpContext.Session.GetString("RegistrationEmail");
            if (string.IsNullOrEmpty(email))
            {
                TempData["Error"] = "Session expired. Please register again.";
                return RedirectToAction("Register");
            }

            Random random = new Random();
            int otp = random.Next(100000, 999999);
            DateTime expiry = DateTime.Now.AddMinutes(5);

            HttpContext.Session.SetString("RegistrationOTP", otp.ToString());
            HttpContext.Session.SetString("OTPExpiry", expiry.ToString());

            await SendOTPEmail(email, otp);

            TempData["Success"] = "New OTP has been sent to your email.";
            return RedirectToAction("VerifyOTP");
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var hashedPassword = PasswordHelper.HashPassword(password);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == hashedPassword);

            if (user != null)
            {
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("UserRole", user.Role);
                HttpContext.Session.SetString("UserName", user.Name);
                return RedirectToAction("Index", "Chat");
            }

            ViewBag.Error = "Invalid email or password.";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return RedirectToAction("Login");

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(User updatedUser, string currentPassword, string newPassword, IFormFile profileImage)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return RedirectToAction("Login");

            user.Name = updatedUser.Name;
            user.Designation = updatedUser.Designation;
            user.Location = updatedUser.Location;

            if (profileImage != null && profileImage.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var extension = Path.GetExtension(profileImage.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ViewBag.ErrorMessage = "Only JPG and PNG images are allowed.";
                    return View("Profile", user);
                }
                if (profileImage.Length > 2 * 1024 * 1024)
                {
                    ViewBag.ErrorMessage = "Image size must not exceed 2MB.";
                    return View("Profile", user);
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "Uploads", "ProfileImages");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = $"user_{user.Id}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImage.CopyToAsync(stream);
                }

                user.ProfileImagePath = $"/Uploads/ProfileImages/{fileName}";
            }

            if (!string.IsNullOrEmpty(currentPassword) && !string.IsNullOrEmpty(newPassword))
            {
                if (PasswordHelper.HashPassword(currentPassword) != user.PasswordHash)
                {
                    ViewBag.ErrorMessage = "Current password is incorrect.";
                    return View("Profile", user);
                }

                user.PasswordHash = PasswordHelper.HashPassword(newPassword);
            }

            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("UserName", user.Name);

            ViewBag.SuccessMessage = "Profile updated successfully!";
            return View("Profile", user);
        }

        [HttpGet]
        public async Task<IActionResult> UpdateEmployeeIds()
        {
            var users = await _context.Users.Where(u => u.EmployeeId == null).ToListAsync();
            foreach (var user in users)
            {
                user.EmployeeId = $"EMP{user.Id.ToString("D6")}";
                _context.Users.Update(user);
            }
            await _context.SaveChangesAsync();
            return Ok("Employee IDs updated for existing users.");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public async Task<IActionResult> Help()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return RedirectToAction("Login");

            ViewBag.Name = user.Name;
            ViewBag.Email = user.Email;
            ViewBag.Designation = user.Designation;
            ViewBag.EmployeeId = user.EmployeeId;
            ViewBag.Role = user.Role;

            // Generate 6-digit Ticket ID and store in Session
            Random random = new Random();
            int ticketId = random.Next(100000, 999999);
            HttpContext.Session.SetString("HelpTicketId", ticketId.ToString());
            ViewBag.TicketId = ticketId; 

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitHelpTicket(string Problem)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return RedirectToAction("Login");

            var ticketId = HttpContext.Session.GetString("HelpTicketId") ?? "Unknown";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Chatbox Help", "helpdeskeinfratech@gmail.com"));
            message.To.Add(new MailboxAddress("Support Team", "einfratechsolution@gmail.com"));
            message.Subject = $"Help Ticket - {ticketId}";

            message.Body = new TextPart("plain")
            {
                Text = $"Ticket ID: {ticketId}\n\n" +
                    $"Name: {user.Name}\n" +
                    $"Email: {user.Email}\n" +
                    $"Designation: {user.Designation}\n" +
                    $"Employee ID: {user.EmployeeId}\n" +
                    $"Role: {user.Role}\n\n" +
                    $"Problem:\n{Problem}"
            };

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync("smtp.gmail.com", 587, false);
                await client.AuthenticateAsync("helpdeskeinfratech@gmail.com", "fgbg vegd oyib icev");
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }

            TempData["SuccessMessage"] = "Your help ticket has been submitted successfully!";
            return RedirectToAction("Settings", "Settings");
        }
    }
}
