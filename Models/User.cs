using System.ComponentModel.DataAnnotations;

namespace InternalChatbox.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string Designation { get; set; }

        public string Location { get; set; }

        [Required]
        [EmailAddress]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@einfratechsys\.(com|tech)$",
        ErrorMessage = "Email must be in the format example@einfratechsys.com or example@einfratechsys.tech")]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        [Required]
        [RegularExpression(@"^[A-Za-z0-9]{6,20}$", ErrorMessage = "Employee ID must be 6-20 alphanumeric characters.")]
        public string? EmployeeId { get; set; }

        public string? ProfileImagePath { get; set; } // New property for profile image path

        public string Role { get; set; } = "Employee";

        public string Status { get; set; } = "Offline";

        public ICollection<ChatMessage> SentMessages { get; set; } = new List<ChatMessage>();
        public ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
    }
}