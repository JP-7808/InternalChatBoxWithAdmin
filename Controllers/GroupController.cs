using InternalChatbox.Data;
using InternalChatbox.Models;
using Microsoft.AspNetCore.Mvc;

namespace InternalChatbox.Controllers
{
    public class GroupController : Controller
    {
        private readonly AppDbContext _context;

        public GroupController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(string groupName, List<int> userIds)
        {
            var group = new ChatGroup { GroupName = groupName };
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();

            foreach (var userId in userIds)
            {
                _context.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = userId });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Chat");
        }
    }
}
