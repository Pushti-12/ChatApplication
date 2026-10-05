namespace ChatApplication.Models
{
    public class ChatViewModel
    {
        public int ConversationId { get; set; }

        public User OtherUser { get; set; } = null!;

        public List<Message> Messages { get; set; } = new();

        public List<User> Users { get; set; } = new();
    }
}