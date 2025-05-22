namespace Chat.Api.DTOs;

public class SearchResponseDto
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public int OtherUserId { get; set; }
    public string Content { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsSent { get; set; }
    public bool IsReceived { get; set; }
    public bool IsSeen { get; set; }
}
