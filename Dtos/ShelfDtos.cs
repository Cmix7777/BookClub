using System.ComponentModel.DataAnnotations;

namespace BookClub.Dtos;

public class ShelfItemDto
{
    public Guid BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public int? Year { get; set; }
    public int? Pages { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public DateTime AddedAt { get; set; }
}

public class AddToShelfDto
{
    [Required(ErrorMessage = "Не указана книга")]
    public Guid? BookId { get; set; }

    [RegularExpression("^(WantToRead|Reading|Finished)$",
        ErrorMessage = "Статус: WantToRead, Reading или Finished")]
    public string Status { get; set; } = "WantToRead";
}

public class UpdateShelfItemDto
{
    [Required(ErrorMessage = "Укажите статус")]
    [RegularExpression("^(WantToRead|Reading|Finished)$",
        ErrorMessage = "Статус: WantToRead, Reading или Finished")]
    public string Status { get; set; } = string.Empty;

    [Range(1, 5, ErrorMessage = "Оценка от 1 до 5")]
    public int? Rating { get; set; }
}