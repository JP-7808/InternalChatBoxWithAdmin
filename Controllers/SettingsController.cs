using InternalChatbox.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalChatbox.Controllers
{
    public class SettingsController : Controller
    {
        private readonly AppDbContext _context;

        public SettingsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Settings()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return RedirectToAction("Login", "Account");

            ViewBag.UserName = user.Name;
            ViewBag.UserRole = user.Role;
            ViewBag.UserStatus = user.Status;
            ViewBag.CurrentUserId = userId;
            ViewBag.UserAvatarUrl = user.ProfileImagePath; // Ensure profile image path is set

            return View();
        }
    }
}