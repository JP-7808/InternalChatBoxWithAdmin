using InternalChatbox.Data;
using InternalChatbox.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using InternalChatbox.Helpers;
using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace InternalChatbox.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AdminController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // Middleware to check if user is admin
        private async Task<bool> IsAdmin()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return false;

            var user = await _context.Users.FindAsync(userId);
            return user?.Role == "Admin";
        }

        // GET: /Admin/Login
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Admin/Login
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var hashedPassword = PasswordHelper.HashPassword(password);
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Email == email && u.PasswordHash == hashedPassword && u.Role == "Admin");

            if (user != null)
            {
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("UserRole", user.Role);
                HttpContext.Session.SetString("UserName", user.Name);
                return RedirectToAction("Dashboard");
            }

            ViewBag.Error = "Invalid admin credentials.";
            return View();
        }

        // GET: /Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var stats = new AdminDashboardStats
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalGroups = await _context.Groups.CountAsync(),
                TotalMessages = await _context.ChatMessages.CountAsync(),
                OnlineUsers = await _context.Users.CountAsync(u => u.Status == "Online")
            };

            ViewBag.Stats = stats;
            return View();
        }

        // GET: /Admin/Users
        public async Task<IActionResult> Users()
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var users = await _context.Users.ToListAsync();
            return View(users);
        }

        // GET: /Admin/CreateUser
        public IActionResult CreateUser()
        {
            return View();
        }

        // POST: /Admin/CreateUser
        [HttpPost]
        public async Task<IActionResult> CreateUser(User model, string password, IFormFile profileImage)
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            ModelState.Remove("PasswordHash");

            if (ModelState.IsValid)
            {
                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email already registered.");
                    return View(model);
                }

                var user = new User
                {
                    Name = model.Name,
                    EmployeeId = model.EmployeeId ?? $"EMP{DateTime.Now.Ticks.ToString().Substring(10)}",
                    Designation = model.Designation,
                    Location = model.Location,
                    Email = model.Email,
                    PasswordHash = PasswordHelper.HashPassword(password),
                    Role = model.Role,
                    Status = "Offline"
                };

                // Handle profile image upload
                if (profileImage != null && profileImage.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var extension = Path.GetExtension(profileImage.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(extension))
                    {
                        ModelState.AddModelError("ProfileImage", "Only JPG and PNG images are allowed.");
                        return View(model);
                    }

                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "Uploads", "ProfileImages");
                    Directory.CreateDirectory(uploadsFolder);

                    var fileName = $"user_{DateTime.Now.Ticks}{extension}";
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await profileImage.CopyToAsync(stream);
                    }

                    user.ProfileImagePath = $"/Uploads/ProfileImages/{fileName}";
                }

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction("Users");
            }

            return View(model);
        }

        // GET: /Admin/EditUser/5
        public async Task<IActionResult> EditUser(int id)
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        // POST: /Admin/EditUser/5
        [HttpPost]
        public async Task<IActionResult> EditUser(int id, User model, string newPassword, IFormFile profileImage)
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Name = model.Name;
            user.Designation = model.Designation;
            user.Location = model.Location;
            user.Role = model.Role;

            if (!string.IsNullOrEmpty(newPassword))
            {
                user.PasswordHash = PasswordHelper.HashPassword(newPassword);
            }

            // Handle profile image upload
            if (profileImage != null && profileImage.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var extension = Path.GetExtension(profileImage.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("ProfileImage", "Only JPG and PNG images are allowed.");
                    return View(model);
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "Uploads", "ProfileImages");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"user_{DateTime.Now.Ticks}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profileImage.CopyToAsync(stream);
                }

                // Delete old image if exists
                if (!string.IsNullOrEmpty(user.ProfileImagePath))
                {
                    var oldFilePath = Path.Combine(_environment.WebRootPath, user.ProfileImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                user.ProfileImagePath = $"/Uploads/ProfileImages/{fileName}";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Users");
        }

        // POST: /Admin/DeleteUser/5
        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Delete profile image if exists
            if (!string.IsNullOrEmpty(user.ProfileImagePath))
            {
                var filePath = Path.Combine(_environment.WebRootPath, user.ProfileImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return RedirectToAction("Users");
        }

        // GET: /Admin/Groups
        public async Task<IActionResult> Groups()
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var groups = await _context.Groups
                .Include(g => g.Members)
                .ThenInclude(m => m.User)
                .ToListAsync();

            return View(groups);
        }

        // GET: /Admin/CreateGroup
        public async Task<IActionResult> CreateGroup()
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            ViewBag.Users = await _context.Users.ToListAsync();
            return View();
        }

        // POST: /Admin/CreateGroup
        [HttpPost]
        public async Task<IActionResult> CreateGroup(string groupName, List<int> userIds, IFormFile groupImage)
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var group = new ChatGroup
            {
                GroupName = groupName,
                GroupImagePath = "/lib/icons/group.png" // Default image path
            };

            // Handle group image upload
            if (groupImage != null && groupImage.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var extension = Path.GetExtension(groupImage.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("GroupImage", "Only JPG and PNG images are allowed.");
                    ViewBag.Users = await _context.Users.ToListAsync();
                    return View();
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "Uploads", "GroupImages");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"group_{DateTime.Now.Ticks}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await groupImage.CopyToAsync(stream);
                }

                group.GroupImagePath = $"/Uploads/GroupImages/{fileName}";
            }

            _context.Groups.Add(group);
            await _context.SaveChangesAsync();

            // Add members to group
            foreach (var userId in userIds.Distinct())
            {
                _context.GroupMembers.Add(new GroupMember
                {
                    GroupId = group.Id,
                    UserId = userId,
                    IsAdmin = false
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Groups");
        }

        // GET: /Admin/EditGroup/5
        public async Task<IActionResult> EditGroup(int id)
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var group = await _context.Groups
                .Include(g => g.Members)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null) return NotFound();

            ViewBag.AllUsers = await _context.Users.ToListAsync();
            return View(group);
        }

        // POST: /Admin/EditGroup/5
        [HttpPost]
        public async Task<IActionResult> EditGroup(int id, [Bind("Id,GroupName")] ChatGroup model,
    List<int> newMemberIds, IFormFile groupImage, bool removeImage = false)
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var group = await _context.Groups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null)
            {
                return NotFound();
            }

            // Update group name
            group.GroupName = model.GroupName;

            // Handle image removal
            if (removeImage && !string.IsNullOrEmpty(group.GroupImagePath))
            {
                var oldFilePath = Path.Combine(_environment.WebRootPath, group.GroupImagePath.TrimStart('/'));
                if (System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Delete(oldFilePath);
                }
                group.GroupImagePath = null;
            }

            // Handle new image upload
            if (groupImage != null && groupImage.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var extension = Path.GetExtension(groupImage.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("GroupImage", "Only JPG and PNG images are allowed.");
                    ViewBag.AllUsers = await _context.Users.ToListAsync();
                    return View(group);
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "Uploads", "GroupImages");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"group_{DateTime.Now.Ticks}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await groupImage.CopyToAsync(stream);
                }

                // Delete old image if exists
                if (!string.IsNullOrEmpty(group.GroupImagePath) && !removeImage)
                {
                    var oldFilePath = Path.Combine(_environment.WebRootPath, group.GroupImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                group.GroupImagePath = $"/Uploads/GroupImages/{fileName}";
            }

            // Add new members if any were selected
            if (newMemberIds != null)
            {
                foreach (var userId in newMemberIds.Distinct())
                {
                    if (!group.Members.Any(m => m.UserId == userId))
                    {
                        _context.GroupMembers.Add(new GroupMember
                        {
                            GroupId = id,
                            UserId = userId,
                            IsAdmin = false
                        });
                    }
                }
            }

            try
            {
                await _context.SaveChangesAsync();
                return RedirectToAction("Groups");
            }
            catch (DbUpdateException ex)
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.AllUsers = await _context.Users.ToListAsync();
                return View(group);
            }
        }

        // POST: /Admin/DeleteGroup/5
        [HttpPost]
        public async Task<IActionResult> DeleteGroup(int id)
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var group = await _context.Groups.FindAsync(id);
            if (group == null) return NotFound();

            // Delete group image if exists
            if (!string.IsNullOrEmpty(group.GroupImagePath))
            {
                var filePath = Path.Combine(_environment.WebRootPath, group.GroupImagePath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            // Delete all messages in this group
            var messages = _context.ChatMessages.Where(m => m.GroupId == id);
            _context.ChatMessages.RemoveRange(messages);

            // Delete all group members
            var members = _context.GroupMembers.Where(gm => gm.GroupId == id);
            _context.GroupMembers.RemoveRange(members);

            _context.Groups.Remove(group);
            await _context.SaveChangesAsync();
            return RedirectToAction("Groups");
        }

        // POST: /Admin/RemoveGroupMember
        [HttpPost]
        public async Task<IActionResult> RemoveGroupMember(int groupId, int userId)
        {
            if (!await IsAdmin()) return Unauthorized();

            var member = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (member != null)
            {
                _context.GroupMembers.Remove(member);
                await _context.SaveChangesAsync();
                return Ok();
            }

            return NotFound();
        }

        // POST: /Admin/ToggleGroupAdmin
        [HttpPost]
        public async Task<IActionResult> ToggleGroupAdmin(int groupId, int userId)
        {
            if (!await IsAdmin()) return Unauthorized();

            var member = await _context.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (member != null)
            {
                member.IsAdmin = !member.IsAdmin;
                await _context.SaveChangesAsync();
                return Ok();
            }

            return NotFound();
        }

        // GET: /Admin/GroupMessages/5
        public async Task<IActionResult> GroupMessages(int id)
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.GroupId == id)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            ViewBag.Group = await _context.Groups.FindAsync(id);
            return View(messages);
        }

        // POST: /Admin/DeleteMessage/5
        [HttpPost]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            if (!await IsAdmin()) return Unauthorized();

            var message = await _context.ChatMessages.FindAsync(id);
            if (message == null) return NotFound();

            _context.ChatMessages.Remove(message);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // GET: /Admin/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }



        // Download Groups chats
        [HttpGet]
        public async Task<IActionResult> DownloadGroupChats(int id, string format = "csv")
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var group = await _context.Groups.FindAsync(id);
            if (group == null) return NotFound();

            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.GroupId == id)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            return format.ToLower() switch
            {
                "txt" => DownloadAsTxt(messages, group.GroupName),
                "pdf" => await DownloadAsPdf(messages, group.GroupName),
                _ => DownloadAsCsv(messages, group.GroupName),
            };
        }

        private FileContentResult DownloadAsCsv(List<ChatMessage> messages, string groupName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Sender,Message,Timestamp");

            foreach (var message in messages)
            {
                sb.AppendLine($"\"{message.Sender.Name}\",\"{message.MessageText}\",\"{message.SentAt}\"");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"{groupName}_messages.csv");
        }

        private FileContentResult DownloadAsTxt(List<ChatMessage> messages, string groupName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Chat messages for group: {groupName}");
            sb.AppendLine("====================================");
            sb.AppendLine();

            foreach (var message in messages)
            {
                sb.AppendLine($"[{message.SentAt}] {message.Sender.Name}:");
                sb.AppendLine(message.MessageText);
                sb.AppendLine();
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/plain", $"{groupName}_messages.txt");
        }

        private async Task<FileContentResult> DownloadAsPdf(List<ChatMessage> messages, string groupName)
        {
            using (var memoryStream = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 50, 50, 25, 25);
                var writer = PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                // Add title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                var title = new Paragraph($"Chat History: {groupName}", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                // Add messages
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.GRAY);

                foreach (var message in messages)
                {
                    // Add sender and timestamp
                    var senderInfo = new Paragraph
            {
                new Chunk(message.Sender.Name, normalFont),
                new Chunk($" - {message.SentAt}", smallFont)
            };
                    document.Add(senderInfo);

                    // Add message text
                    var messagePara = new Paragraph(message.MessageText, normalFont)
                    {
                        IndentationLeft = 20f,
                        SpacingAfter = 15f
                    };
                    document.Add(messagePara);
                }

                document.Close();
                writer.Close();

                return File(memoryStream.ToArray(), "application/pdf", $"{groupName}_messages.pdf");
            }
        }


        // GET: /Admin/PrivateChats
        public async Task<IActionResult> PrivateChats()
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var privateMessages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => m.ReceiverId != null && m.GroupId == null)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            return View(privateMessages);
        }

        // GET: /Admin/ViewPrivateChat/{userId1}/{userId2}
        public async Task<IActionResult> ViewPrivateChat(int userId1, int userId2)
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            // Get the conversation between these two users
            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m =>
                    (m.SenderId == userId1 && m.ReceiverId == userId2) ||
                    (m.SenderId == userId2 && m.ReceiverId == userId1))
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            var user1 = await _context.Users.FindAsync(userId1);
            var user2 = await _context.Users.FindAsync(userId2);

            ViewBag.User1 = user1;
            ViewBag.User2 = user2;

            return View(messages);
        }



        // Download ont to one chat functionality
        [HttpGet]
        public async Task<IActionResult> DownloadPrivateChat(int userId1, int userId2, string format = "csv")
        {
            if (!await IsAdmin()) return RedirectToAction("Login");

            var user1 = await _context.Users.FindAsync(userId1);
            var user2 = await _context.Users.FindAsync(userId2);

            if (user1 == null || user2 == null) return NotFound();

            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m =>
                    (m.SenderId == userId1 && m.ReceiverId == userId2) ||
                    (m.SenderId == userId2 && m.ReceiverId == userId1))
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            string fileName = $"{user1.Name}_and_{user2.Name}_chat.{format}";

            Response.Headers.Add("Content-Disposition", $"attachment; filename={fileName}");

            return format.ToLower() switch
            {
                "txt" => DownloadPrivateChatAsTxt(messages, user1, user2),
                "pdf" => await DownloadPrivateChatAsPdf(messages, user1, user2),
                _ => DownloadPrivateChatAsCsv(messages, user1, user2),
            };
        }

        private FileContentResult DownloadPrivateChatAsCsv(List<ChatMessage> messages, User user1, User user2)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Sender,Receiver,Message,Timestamp");

            foreach (var message in messages)
            {
                var senderName = message.SenderId == user1.Id ? user1.Name : user2.Name;
                var receiverName = message.SenderId == user1.Id ? user2.Name : user1.Name;
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(message.SentAt, TimeZoneInfo.Local);

                sb.AppendLine($"\"{senderName}\",\"{receiverName}\",\"{message.MessageText}\",\"{localTime:hh:mm:ss tt}\"");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv");
        }

        private FileContentResult DownloadPrivateChatAsTxt(List<ChatMessage> messages, User user1, User user2)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Chat between {user1.Name} and {user2.Name}");
            sb.AppendLine("=========================================");
            sb.AppendLine();

            foreach (var message in messages)
            {
                var senderName = message.SenderId == user1.Id ? user1.Name : user2.Name;
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(message.SentAt, TimeZoneInfo.Local);

                sb.AppendLine($"[{localTime:hh:mm:ss tt}] {senderName}:");
                sb.AppendLine(message.MessageText);
                sb.AppendLine();
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/plain");
        }

        private async Task<FileContentResult> DownloadPrivateChatAsPdf(List<ChatMessage> messages, User user1, User user2)
        {
            using (var memoryStream = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 50, 50, 25, 25);
                var writer = PdfWriter.GetInstance(document, memoryStream);
                document.Open();

                // Add title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                var title = new Paragraph($"Chat History: {user1.Name} and {user2.Name}", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20f
                };
                document.Add(title);

                // Add messages
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.GRAY);

                foreach (var message in messages)
                {
                    var senderName = message.SenderId == user1.Id ? user1.Name : user2.Name;
                    var localTime = TimeZoneInfo.ConvertTimeFromUtc(message.SentAt, TimeZoneInfo.Local);

                    // Add sender and timestamp
                    var senderInfo = new Paragraph
            {
                new Chunk(senderName, normalFont),
                new Chunk($" - {localTime:hh:mm:ss tt}", smallFont)
            };
                    document.Add(senderInfo);

                    // Add message text
                    var messagePara = new Paragraph(message.MessageText, normalFont)
                    {
                        IndentationLeft = 20f,
                        SpacingAfter = 15f
                    };
                    document.Add(messagePara);
                }

                document.Close();
                writer.Close();

                return File(memoryStream.ToArray(), "application/pdf");
            }
        }

    }


    public class AdminDashboardStats
    {
        public int TotalUsers { get; set; }
        public int TotalGroups { get; set; }
        public int TotalMessages { get; set; }
        public int OnlineUsers { get; set; }
    }
}