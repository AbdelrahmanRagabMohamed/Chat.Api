namespace Chat.Api.DTOs
{
    public class SendMessageDto
    {
        public int ReceiverId { get; set; } // بدل ConversationId
        public string Content { get; set; }
    }
}