namespace Chat.Api.DTOs;

public class OpenConversationResponseDto
{
    public int ConversationId { get; set; }
    public int OtherUserId { get; set; }
    public string OtherUserName { get; set; }
    public string OtherUserImage { get; set; }
    public List<MessageResponseDto> Messages { get; set; }
}
