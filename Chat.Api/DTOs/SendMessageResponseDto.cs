namespace Chat.Api.DTOs;

public class SendMessageResponseDto
{
    public int ConversationId { get; set; }
    public int SenderId { get; set; }
    public MessageResponseDto Message { get; set; }
}
