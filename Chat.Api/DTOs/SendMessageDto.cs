namespace Chat.Api.DTOs;
public class SendMessageDto
{
    public int ConversationId { get; set; }
    public string Content { get; set; }
}