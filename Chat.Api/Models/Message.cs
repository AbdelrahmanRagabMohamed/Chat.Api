using ChatApi.Models;

public class Message
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; }
    public int SenderId { get; set; }
    public User Sender { get; set; }
    public int ReceiverId { get; set; }
    public User Receiver { get; set; }
    public string Content { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsSent { get; set; }    // جديد: هل الرسالة اتبعتت؟
    public bool IsReceived { get; set; } // جديد: هل الرسالة استلمت؟
    public bool IsSeen { get; set; }     // موجود: هل الرسالة اتشافت؟
}