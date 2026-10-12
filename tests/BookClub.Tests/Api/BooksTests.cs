namespace BookClub.Tests.Rest;

/// <summary>Каталог книг: смотреть может кто угодно, добавлять только вошедшие.</summary>
[Trait("Category", "Api")]
public class BooksTests
{
    [Fact(DisplayName = "Список книг открыт без входа")]
    public async Task Catalog_is_public()
    {
        var reply = (await Api.Get("api/books")).ShouldBe(200);

        Assert.Equal(System.Text.Json.JsonValueKind.Array, reply.Json.ValueKind);
    }

    [Fact(DisplayName = "Добавить книгу без входа нельзя: 401")]
    public async Task Creating_a_book_requires_login()
    {
        (await Api.Post("api/books", new { title = "Без входа", author = "Аноним" })).ShouldBe(401);
    }

    [Fact(DisplayName = "Добавление книги: 201, адрес записи, лишние пробелы обрезаны, книгу можно открыть по номеру")]
    public async Task Creating_a_book_works_and_trims_spaces()
    {
        var user = await TestUser.Create();
        var tag = Make.Tag();

        var created = (await Api.Post("api/books", new
        {
            title = "  Дюна " + tag + "  ",
            author = "  Фрэнк Герберт  ",
            genre = " Фантастика ",
            year = 1965,
            pages = 688
        }, user.Token)).ShouldBe(201);

        Assert.Equal("Дюна " + tag, created.Str("title"));
        Assert.Equal("Фрэнк Герберт", created.Str("author"));
        Assert.Equal("Фантастика", created.Str("genre"));
        Assert.EndsWith("/api/books/" + created.GuidOf("id"), created.Location);

        var fetched = (await Api.Get("api/books/" + created.GuidOf("id"))).ShouldBe(200);
        Assert.Equal("Дюна " + tag, fetched.Str("title"));
        Assert.Equal(1965, fetched.Int("year"));
        Assert.Equal(688, fetched.Int("pages"));
    }

    [Fact(DisplayName = "Год и страницы можно не указывать")]
    public async Task Year_and_pages_are_optional()
    {
        var user = await TestUser.Create();

        var created = (await Api.Post("api/books", new { title = "Без года " + Make.Tag(), author = "Автор" }, user.Token))
            .ShouldBe(201);

        Assert.Equal(System.Text.Json.JsonValueKind.Null, created.Json.GetProperty("year").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, created.Json.GetProperty("pages").ValueKind);
    }

    [Theory(DisplayName = "Неверные данные книги отклоняются: 400")]
    [InlineData("title", "")]
    [InlineData("author", "")]
    [InlineData("year", 999)]
    [InlineData("year", 2101)]
    [InlineData("pages", 0)]
    [InlineData("pages", 10001)]
    public async Task Invalid_book_is_rejected(string field, object value)
    {
        var user = await TestUser.Create();
        var data = new Dictionary<string, object>
        {
            ["title"] = "Книга " + Make.Tag(),
            ["author"] = "Автор",
            ["year"] = 2000,
            ["pages"] = 300
        };
        data[field] = value;

        (await Api.Post("api/books", data, user.Token)).ShouldBe(400);
    }

    [Fact(DisplayName = "Слишком длинное название (больше 200 знаков) отклоняется")]
    public async Task Too_long_title_is_rejected()
    {
        var user = await TestUser.Create();

        (await Api.Post("api/books", new { title = new string('я', 201), author = "Автор" }, user.Token)).ShouldBe(400);
    }

    [Fact(DisplayName = "Граничные значения принимаются: год 1000 и 2100, страниц 1 и 10000")]
    public async Task Boundary_values_are_accepted()
    {
        var user = await TestUser.Create();

        (await Api.Post("api/books", new { title = "Граница1 " + Make.Tag(), author = "А", year = 1000, pages = 1 }, user.Token)).ShouldBe(201);
        (await Api.Post("api/books", new { title = "Граница2 " + Make.Tag(), author = "А", year = 2100, pages = 10000 }, user.Token)).ShouldBe(201);
    }

    [Fact(DisplayName = "Несуществующая книга: 404")]
    public async Task Unknown_book_is_not_found()
    {
        (await Api.Get("api/books/" + Guid.NewGuid())).ShouldBe(404);
    }

    [Fact(DisplayName = "Номер книги не в формате GUID даёт ошибку клиента, а не 500")]
    public async Task Malformed_book_id_is_a_client_error()
    {
        var reply = await Api.Get("api/books/not-a-guid");

        Assert.InRange(reply.Code, 400, 404);
    }

    [Fact(DisplayName = "Каталог отсортирован по названию")]
    public async Task Catalog_is_sorted_by_title()
    {
        var user = await TestUser.Create();
        var tag = Make.Tag();
        // порядок создания намеренно перепутан
        await Make.Book(user, title: "C-" + tag);
        await Make.Book(user, title: "A-" + tag);
        await Make.Book(user, title: "B-" + tag);

        var list = (await Api.Get("api/books")).ShouldBe(200).Items();
        var mine = list.Select(b => b.GetProperty("title").GetString()!).Where(t => t.EndsWith("-" + tag)).ToList();

        Assert.Equal(new[] { "A-" + tag, "B-" + tag, "C-" + tag }, mine);
    }

    [Fact(DisplayName = "Спецсимволы в названии сохраняются как текст и не ломают ответ")]
    public async Task Special_characters_are_stored_as_plain_text()
    {
        var user = await TestUser.Create();
        var title = "<script>alert(1)</script> & \"кавычки\" " + Make.Tag();

        var id = await Make.Book(user, title: title);

        var fetched = (await Api.Get("api/books/" + id)).ShouldBe(200);
        Assert.Equal(title, fetched.Str("title"));
    }
}
