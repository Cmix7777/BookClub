using System.ComponentModel.DataAnnotations;

namespace BookClub.Dtos;

public class RegisterDto
{
    [Required(ErrorMessage = "Логин обязателен")]
    [MinLength(3, ErrorMessage = "Логин не короче 3 символов")]
    [MaxLength(30, ErrorMessage = "Логин не длиннее 30 символов")]
    [RegularExpression(@"^[a-zA-Z0-9._]+$",
        ErrorMessage = "В логине допустимы только латиница, цифры, точка и подчёркивание")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email обязателен")]
    [EmailAddress(ErrorMessage = "Некорректный email")]
    [MaxLength(120)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Пароль обязателен")]
    [MinLength(8, ErrorMessage = "Пароль не короче 8 символов")]
    [MaxLength(100, ErrorMessage = "Пароль не длиннее 100 символов")]
    public string Password { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required(ErrorMessage = "Введите логин или email")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите пароль")]
    public string Password { get; set; } = string.Empty;
}
public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
}

public class MeDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}