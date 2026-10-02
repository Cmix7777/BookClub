namespace BookClub.Dtos;

public class AdminStatsDto
{
    public int Users { get; set; }
    public int Admins { get; set; }
    public int BlockedUsers { get; set; }
    public int Books { get; set; }
    public int Clubs { get; set; }
    public int ActivePolls { get; set; }
    public int Votes { get; set; }
    public int Reviews { get; set; }
}

public class AdminUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ClubsCount { get; set; }
    public int ReviewsCount { get; set; }
}