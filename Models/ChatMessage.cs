
namespace InternalChatbox.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int? ReceiverId { get; set; }
        public int? GroupId { get; set; }
        public string MessageText { get; set; }
        public DateTime SentAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;

        public User Sender { get; set; }
        public User Receiver { get; set; }

        public ChatGroup? Group { get; set; }
    }
}
