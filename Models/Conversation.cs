namespace ChatApplication.Models
{
    public class Conversation
    {
        public int Id { get; set; }

        public int User1Id { get; set; }

        public int User2Id { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
