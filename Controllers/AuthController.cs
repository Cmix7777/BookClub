using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookClub.Data;
using BookClub.Dtos;
using BookClub.Models;
using Microsoft.AspNetCore.Authorization;
using BookClub.Extensions;
using BookClub.Services;

namespace BookClub.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokens;

    public AuthController(AppDbContext context, TokenService tokens)
    {
        _context = context;
        _tokens = tokens;
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(RegisterDto dto)
    {
        var username = dto.Username.Trim();
        var email = dto.Email.Trim().ToLowerInvariant();

        var taken = await _context.Users
            .AnyAsync(u => u.Username == username || u.Email == email);
        if (taken)
            return Conflict("Логин или email уже заняты");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("Логин или email уже заняты");
        }

        var result = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            CreatedAt = user.CreatedAt
        };

        return CreatedAtAction(
            nameof(UsersController.GetUser), "Users", new { id = user.Id }, result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var login = dto.Login.Trim();
        var loginAsEmail = login.ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == login || u.Email == loginAsEmail);

        if (user is null || !PasswordMatches(dto.Password, user.PasswordHash))
            return Unauthorized("Неверный логин или пароль");

        var (token, expiresAt) = _tokens.CreateToken(user);

        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                CreatedAt = user.CreatedAt
            }
        };
    }

    [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<MeDto>> Me()
        {
            var userId = User.GetUserId();

            var me = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new MeDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (me is null)
                return Unauthorized("Пользователь из токена не найден");

            return me;
        }

    private static bool PasswordMatches(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}