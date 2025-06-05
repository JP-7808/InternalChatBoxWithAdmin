using InternalChatbox.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InternalChatbox.Controllers
{
    public class ContactsController : Controller
    {
        private readonly AppDbContext _context;

        public ContactsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            // Exclude current user from contact list
            var users = await _context.Users
                .Where(u => u.Id != userId)
                .ToListAsync();

            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.UserRole = HttpContext.Session.GetString("UserRole");
            ViewBag.UserDesignation = HttpContext.Session.GetString("UserDesignation");
            ViewBag.Users = users;

            return View();
        }
    }
}
