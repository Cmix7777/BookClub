using System.ComponentModel.DataAnnotations;

namespace BookClub.Dtos;

public class ClubDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MembersCount { get; set; }
    public bool HasActivePoll { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClubMemberDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

public class CreateClubDto
{
    [Required(ErrorMessage = "Название клуба обязательно")]
    [MinLength(2, ErrorMessage = "Название не короче 2 символов")]
    [MaxLength(60, ErrorMessage = "Название не длиннее 60 символов")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300, ErrorMessage = "Описание не длиннее 300 символов")]
    public string Description { get; set; } = string.Empty;
}