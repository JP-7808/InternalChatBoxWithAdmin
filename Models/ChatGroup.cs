using System.Collections.Generic;

namespace InternalChatbox.Models
{
    public class ChatGroup
    {
        public int Id { get; set; }
        public string GroupName { get; set; }

        public ICollection<GroupMember> Members { get; set; }  // ✅ List of users
        public ICollection<ChatMessage> Messages { get; set; }  // ✅ Messages in the group
        public string? GroupImagePath { get; set; } // Add this property for group avatar image
    }
}
