namespace Chat.Api.DTOs
{
    public class LastMessageDto
    {
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
        public string Status { get; set; }
    }
}