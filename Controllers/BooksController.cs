using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookClub.Data;
using BookClub.Dtos;
using BookClub.Models;

namespace BookClub.Controllers;

[ApiController]
[Route("api/books")]
public class BooksController : ControllerBase
{
    private readonly AppDbContext _context;

    public BooksController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<BookDto>>> GetBooks()
    {
        return await _context.Books
            .OrderBy(b => b.Title)
            .Select(b => new BookDto
            {
                Id = b.Id,
                Title = b.Title,
                Author = b.Author,
                Genre = b.Genre,
                Year = b.Year,
                Pages = b.Pages
            })
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookDto>> GetBook(Guid id)
    {
        var book = await _context.Books
            .Where(b => b.Id == id)
            .Select(b => new BookDto
            {
                Id = b.Id,
                Title = b.Title,
                Author = b.Author,
                Genre = b.Genre,
                Year = b.Year,
                Pages = b.Pages
            })
            .FirstOrDefaultAsync();

        if (book is null)
            return NotFound("Книга не найдена");

        return book;
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<BookDto>> CreateBook(CreateBookDto dto)
    {
        var book = new Book
        {
            Id = Guid.NewGuid(),
            Title = dto.Title.Trim(),
            Author = dto.Author.Trim(),
            Genre = dto.Genre.Trim(),
            Year = dto.Year,
            Pages = dto.Pages
        };

        _context.Books.Add(book);
        await _context.SaveChangesAsync();

        var result = new BookDto
        {
            Id = book.Id,
            Title = book.Title,
            Author = book.Author,
            Genre = book.Genre,
            Year = book.Year,
            Pages = book.Pages
        };

        return CreatedAtAction(nameof(GetBook), new { id = book.Id }, result);
    }
}