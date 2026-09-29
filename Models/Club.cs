namespace BookClub.Models;

public class Club
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ClubMember> Members { get; set; } = new();
    public List<Poll> Polls { get; set; } = new();
}