using InternalChatbox.Data;
using InternalChatbox.Models;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace InternalChatbox.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AppDbContext _context;

        public ChatHub(AppDbContext context)
        {
            _context = context;
        }

        // Map user ID to connection ID when a client connects
        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var userId = httpContext?.Session.GetInt32("UserId")?.ToString();
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
                System.Console.WriteLine($"User {userId} connected with Connection ID: {Context.ConnectionId}");
            }
            await base.OnConnectedAsync();
        }

        public async Task SendPrivateMessage(int senderId, int receiverId, string message, string senderName, string profileImagePath)
        {
            try
            {
                // Save message to database
                var chatMessage = new ChatMessage
                {
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    MessageText = message,
                    SentAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();
                System.Console.WriteLine($"Message saved to DB: ID {chatMessage.Id}, Sender {senderId}, Receiver {receiverId}");

                // Convert to local time and format
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(chatMessage.SentAt, TimeZoneInfo.Local);
                var timestamp = localTime.ToString("hh:mm:ss tt");

                // Broadcast to sender and receiver
                await Clients.Group(senderId.ToString()).SendAsync("ReceivePrivateMessage",
                    senderId, senderName, message, profileImagePath, timestamp, chatMessage.Id);
                await Clients.Group(receiverId.ToString()).SendAsync("ReceivePrivateMessage",
                    senderId, senderName, message, profileImagePath, timestamp, chatMessage.Id);

                System.Console.WriteLine($"Message broadcast to Sender {senderId} and Receiver {receiverId}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error in SendPrivateMessage: {ex.Message}");
                throw;
            }
        }

        public async Task SendGroupMessage(string groupId, int senderId, string message, string senderName, string profileImagePath)
        {
            try
            {
                // Save message to database
                var chatMessage = new ChatMessage
                {
                    SenderId = senderId,
                    GroupId = int.Parse(groupId),
                    MessageText = message,
                    SentAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();
                System.Console.WriteLine($"Group message saved to DB: ID {chatMessage.Id}, Group {groupId}");

                // Convert to local time and format
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(chatMessage.SentAt, TimeZoneInfo.Local);
                var timestamp = localTime.ToString("hh:mm:ss tt");

                // Broadcast to group (excluding sender)
                await Clients.GroupExcept(groupId, Context.ConnectionId).SendAsync(
                    "ReceiveGroupMessage", groupId, senderId, senderName, message, profileImagePath, timestamp, chatMessage.Id);
                System.Console.WriteLine($"Group message broadcast to Group {groupId}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error in SendGroupMessage: {ex.Message}");
                throw;
            }
        }

        public async Task JoinGroup(string groupId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupId);
            System.Console.WriteLine($"Connection {Context.ConnectionId} joined group {groupId}");
        }

        public async Task LeaveGroup(string groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId);
            System.Console.WriteLine($"Connection {Context.ConnectionId} left group {groupId}");
        }
    }
}