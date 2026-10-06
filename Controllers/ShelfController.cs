using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookClub.Data;
using BookClub.Dtos;
using BookClub.Extensions;
using BookClub.Models;

namespace BookClub.Controllers;

[ApiController]
[Route("api/me/books")]
[Authorize]
public class ShelfController : ControllerBase
{
    private static readonly string[] Statuses = { "WantToRead", "Reading", "Finished" };

    private readonly AppDbContext _context;

    public ShelfController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ShelfItemDto>>> GetShelf([FromQuery] string? status)
    {
        if (status is not null && !Statuses.Contains(status))
            return BadRequest("Статус: WantToRead, Reading или Finished");

        var userId = User.GetUserId();

        var query = _context.UserBooks.Where(ub => ub.UserId == userId);
        if (status is not null)
            query = query.Where(ub => ub.Status == status);

        return await query
            .OrderByDescending(ub => ub.AddedAt)
            .Select(ub => new ShelfItemDto
            {
                BookId = ub.BookId,
                Title = ub.Book.Title,
                Author = ub.Book.Author,
                Genre = ub.Book.Genre,
                Year = ub.Book.Year,
                Pages = ub.Book.Pages,
                Status = ub.Status,
                Rating = ub.Rating,
                AddedAt = ub.AddedAt
            })
            .ToListAsync();
    }

    [HttpPost]
    public async Task<IActionResult> AddToShelf(AddToShelfDto dto)
    {
        var userId = User.GetUserId();
        var bookId = dto.BookId!.Value;

        var bookExists = await _context.Books.AnyAsync(b => b.Id == bookId);
        if (!bookExists)
            return NotFound("Книга не найдена");

        var already = await _context.UserBooks
            .AnyAsync(ub => ub.UserId == userId && ub.BookId == bookId);
        if (already)
            return Conflict("Эта книга уже на вашей полке");

        _context.UserBooks.Add(new UserBook
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BookId = bookId,
            Status = dto.Status
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("Эта книга уже на вашей полке");
        }

        return StatusCode(201, "Книга добавлена на полку");
    }

    [HttpPut("{bookId}")]
    public async Task<IActionResult> UpdateShelfItem(Guid bookId, UpdateShelfItemDto dto)
    {
        if (dto.Rating is not null && dto.Status != "Finished")
            return BadRequest("Оценку можно поставить только прочитанной книге");

        var userId = User.GetUserId();

        var item = await _context.UserBooks
            .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BookId == bookId);
        if (item is null)
            return NotFound("Этой книги нет на вашей полке");

        item.Status = dto.Status;
        item.Rating = dto.Status == "Finished" ? dto.Rating : null;

        await _context.SaveChangesAsync();
        return Ok("Полка обновлена");
    }

    [HttpDelete("{bookId}")]
    public async Task<IActionResult> RemoveFromShelf(Guid bookId)
    {
        var userId = User.GetUserId();

        var item = await _context.UserBooks
            .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BookId == bookId);
        if (item is null)
            return NotFound("Этой книги нет на вашей полке");

        _context.UserBooks.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}