namespace Chat.Api.DTOs
{
    public class ConversationResponseDto
    {
        public int Id { get; set; }
        public int OtherUserId { get; set; }
        public string OtherUserName { get; set; }
        public string OtherUserProfileImage { get; set; }
        public LastMessageDto LastMessage { get; set; }
        public int UnreadCount { get; set; }
    }
}