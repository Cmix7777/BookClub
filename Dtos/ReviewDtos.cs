using System.ComponentModel.DataAnnotations;

namespace BookClub.Dtos;

public class UpsertReviewDto
{
    [Required(ErrorMessage = "Поставьте оценку")]
    [Range(1, 5, ErrorMessage = "Оценка от 1 до 5")]
    public int? Rating { get; set; }

    [MaxLength(2000, ErrorMessage = "Текст не длиннее 2000 символов")]
    public string Text { get; set; } = string.Empty;

    public bool IsAnonymous { get; set; }
}

public class ReviewDto
{
    public Guid Id { get; set; }
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Author { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsMine { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BookReviewsDto
{
    public Guid BookId { get; set; }
    public int Count { get; set; }
    public double? AverageRating { get; set; }
    public List<ReviewDto> Reviews { get; set; } = new();
}