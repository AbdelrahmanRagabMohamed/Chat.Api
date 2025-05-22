namespace Chat.Api.DTOs;

public class MessageResponseDto
{
    public int Id { get; set; }
    public string Content { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsSent { get; set; }
    public bool IsReceived { get; set; }
    public bool IsSeen { get; set; }
}