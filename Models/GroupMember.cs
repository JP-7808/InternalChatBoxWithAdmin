// In GroupMember.cs
namespace InternalChatbox.Models
{
    public class GroupMember
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public bool IsAdmin { get; set; } // Add this property

        public ChatGroup Group { get; set; }
        public User User { get; set; }
    }
}