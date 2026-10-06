using System.ComponentModel.DataAnnotations;

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

public class ChangeRoleDto
{
    [Required(ErrorMessage = "Укажите роль")]
    [RegularExpression("^(User|Admin)$", ErrorMessage = "Роль может быть только User или Admin")]
    public string Role { get; set; } = string.Empty;
}

public class AdminReviewDto
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public bool IsAnonymous { get; set; }
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class DuplicateBooksDto
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<Guid> BookIds { get; set; } = new();
}