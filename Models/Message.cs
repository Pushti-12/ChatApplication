namespace ChatApplication.Models
{
    public class Message
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }

        public int SenderId { get; set; }

        public string MessageText { get; set; }

        public DateTime SentAt { get; set; }

        public string Status { get; set; }
    }
}