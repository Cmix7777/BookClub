using System.ComponentModel.DataAnnotations;

namespace BookClub.Dtos;

// то, что сервер отдаёт наружу
public class BookDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public int? Year { get; set; }
}

// то, что сервер принимает при создании книги
public class CreateBookDto
{
    [Required(ErrorMessage = "Название обязательно")]
    [MaxLength(200, ErrorMessage = "Название не длиннее 200 символов")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Автор обязателен")]
    [MaxLength(150, ErrorMessage = "Имя автора не длиннее 150 символов")]
    public string Author { get; set; } = string.Empty;

    [MaxLength(60, ErrorMessage = "Жанр не длиннее 60 символов")]
    public string Genre { get; set; } = string.Empty;

    [Range(1000, 2100, ErrorMessage = "Год должен быть от 1000 до 2100")]
    public int? Year { get; set; }
}