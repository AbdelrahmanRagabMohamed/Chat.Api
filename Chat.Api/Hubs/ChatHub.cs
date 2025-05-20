using ChatApi.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatApi.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;

        public ChatHub(IHubContext<ChatHub> hubContext, IServiceScopeFactory scopeFactory)
        {
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
        }


        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
                Console.WriteLine($"User {userId} connected and added to group {userId}");

                // تحديث حالة LastSeen
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var user = await context.Users.FindAsync(int.Parse(userId));
                    if (user != null)
                    {
                        user.LastSeen = DateTime.UtcNow;
                        await context.SaveChangesAsync();
                    }
                }

                // إرسال حالة الأونلاين لكل المستخدمين
                await Clients.All.SendAsync("UserStatusChanged", userId, true);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
                Console.WriteLine($"User {userId} disconnected and removed from group {userId}");

                // تحديث حالة LastSeen
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var user = await context.Users.FindAsync(int.Parse(userId));
                    if (user != null)
                    {
                        user.LastSeen = DateTime.UtcNow;
                        await context.SaveChangesAsync();
                    }
                }

                // إرسال حالة الأوفلاين لكل المستخدمين
                await Clients.All.SendAsync("UserStatusChanged", userId, false);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(int conversationId, string content)
        {
            var userId = int.Parse(Context.User.FindFirst(ClaimTypes.NameIdentifier).Value);
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var conversation = await context.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId);

                if (conversation == null) return;

                var sender = await context.Users.FindAsync(userId);
                var message = new
                {
                    ConversationId = conversationId,
                    SenderId = userId,
                    SenderName = sender.UserName,
                    Content = content,
                    SentAt = DateTime.UtcNow
                };

                var otherUserId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;
                await _hubContext.Clients.Group(otherUserId.ToString()).SendAsync("ReceiveMessage", message);
            }
        }


        //  لتأكيد قراءة الرسالة
        public async Task MarkMessageAsSeen(int messageId)
        {
            var userId = int.Parse(Context.User.FindFirst(ClaimTypes.NameIdentifier).Value);
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var message = await context.Messages
                    .Include(m => m.Conversation)
                    .FirstOrDefaultAsync(m => m.Id == messageId);

                if (message == null) return;

                // التأكد إن المستخدم هو المستقبل للرسالة
                var otherUserId = message.Conversation.User1Id == message.SenderId ? message.Conversation.User2Id : message.Conversation.User1Id;
                if (userId != otherUserId) return;

                message.IsSeen = true;
                await context.SaveChangesAsync();

                // إرسال إشعار للمرسل
                await _hubContext.Clients.Group(message.SenderId.ToString()).SendAsync("MessageSeen", messageId);
            }
        }
    }
}
