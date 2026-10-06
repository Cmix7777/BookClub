using System.ComponentModel.DataAnnotations;

namespace BookClub.Dtos;

public class CreatePollDto
{
    [Required(ErrorMessage = "Не указан клуб")]
    public Guid? ClubId { get; set; }

    [Required(ErrorMessage = "Вопрос обязателен")]
    [MinLength(3, ErrorMessage = "Вопрос не короче 3 символов")]
    [MaxLength(150, ErrorMessage = "Вопрос не длиннее 150 символов")]
    public string Question { get; set; } = string.Empty;

    [Required]
    [MinLength(2, ErrorMessage = "Нужно минимум две книги")]
    [MaxLength(10, ErrorMessage = "Не больше десяти книг")]
    public List<Guid> BookIds { get; set; } = new();
    [Range(1, 10080, ErrorMessage = "Срок голосования — от 1 минуты до 7 дней")]
    public int DurationMinutes { get; set; } = 2880;
}

public class PollOptionDto
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
}

public class PollDto
{
    public Guid Id { get; set; }
    public Guid ClubId { get; set; }
    public string Question { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public DateTime ClosesAt { get; set; }
    public int VotersCount { get; set; }
    public bool HasVoted { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PollOptionDto> Options { get; set; } = new();
}

public class VoteDto
{
    [Required(ErrorMessage = "Не выбран вариант")]
    public Guid? PollOptionId { get; set; }
}

public class PollOptionResultDto
{
    public Guid PollOptionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int VoteCount { get; set; }
    public double Percentage { get; set; }
    public bool IsWinner { get; set; }
}

public class PollResultsDto
{
    public Guid PollId { get; set; }
    public string Question { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public DateTime ClosesAt { get; set; }
    public int VotersCount { get; set; }
    public int MembersCount { get; set; }

    // заполняются только после закрытия отбора
    public int? TotalVotes { get; set; }
    public List<PollOptionResultDto>? Results { get; set; }
}
public class ClubPollDto
{
    public Guid Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public DateTime ClosesAt { get; set; }
    public DateTime CreatedAt { get; set; }
}