namespace Chat.Api.DTOs;

public class MessageResponseDto
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public int SenderId { get; set; }
    public UserResponseDto Sender { get; set; }
    public int ReceiverId { get; set; } // إضافة الـ ReceiverId
    public UserResponseDto Receiver { get; set; } // إضافة بيانات المستلم
    public string Content { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsSeen { get; set; }

}