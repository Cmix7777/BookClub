namespace BookClub.Models;

public class ClubMember
{
    public Guid Id { get; set; }

    public Guid ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Role { get; set; } = "Member"; // Admin | Member
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}