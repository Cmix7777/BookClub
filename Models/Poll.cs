namespace BookClub.Models;

public class Poll
{
    public Guid Id { get; set; }

    public Guid ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public string Question { get; set; } = string.Empty;
    public bool IsClosed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<PollOption> Options { get; set; } = new();

    public DateTime ClosesAt { get; set; }
}