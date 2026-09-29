namespace BookClub.Models;

public class Vote
{
    public Guid Id { get; set; }

    public Guid PollOptionId { get; set; }
    public PollOption PollOption { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}