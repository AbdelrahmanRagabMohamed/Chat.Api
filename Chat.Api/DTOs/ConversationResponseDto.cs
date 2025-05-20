using ChatApi.Controllers;

namespace Chat.Api.DTOs;

public class ConversationResponseDto
{
    public int Id { get; set; }
    public int User1Id { get; set; }
    public UserResponseDto User1 { get; set; }
    public int User2Id { get; set; }
    public UserResponseDto User2 { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<MessageResponseDto>? Messages { get; set; }
}