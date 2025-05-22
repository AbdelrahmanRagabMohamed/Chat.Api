namespace Chat.Api.DTOs;

public class OpenConversationResponseDto
{
    public int ConversationId { get; set; }
    public int ReceiverId { get; set; }
    public string ReceiverName { get; set; }
    public string ReceiverImage { get; set; }
    public List<MessageResponseDto> Messages { get; set; }
}
