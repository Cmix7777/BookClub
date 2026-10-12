namespace BookClub.Tests.Support;

/// <summary>Короткие помощники, которые готовят данные для теста: книги, клубы, отборы.</summary>
public static class Make
{
    /// <summary>Случайная метка, чтобы названия и логины не повторялись между тестами.</summary>
    public static string Tag() => Guid.NewGuid().ToString("N")[..8];

    public static async Task<Guid> Book(
        TestUser by,
        string? title = null,
        string? author = null,
        string genre = "Тест",
        int? year = 2000,
        int? pages = 300)
    {
        var reply = (await Api.Post("api/books", new
        {
            title = title ?? "Книга " + Tag(),
            author = author ?? "Автор " + Tag(),
            genre,
            year,
            pages
        }, by.Token)).ShouldBe(201);
        return reply.GuidOf("id");
    }

    public static async Task<Guid> Club(TestUser owner, string? name = null, string description = "Для проверок")
    {
        var reply = (await Api.Post("api/clubs", new
        {
            name = name ?? "Клуб " + Tag(),
            description
        }, owner.Token)).ShouldBe(201);
        return reply.GuidOf("id");
    }

    public static async Task Join(TestUser user, Guid clubId) =>
        (await Api.Post($"api/clubs/{clubId}/join", null, user.Token)).ShouldBe(200);

    /// <summary>Создаёт клуб с владельцем и несколькими участниками. Первым в списке идёт владелец-админ.</summary>
    public static async Task<(Guid ClubId, TestUser Owner, List<TestUser> Members)> ClubWithMembers(int membersBesidesOwner)
    {
        var owner = await TestUser.Create();
        var clubId = await Club(owner);
        var members = new List<TestUser>();
        for (var i = 0; i < membersBesidesOwner; i++)
        {
            var member = await TestUser.Create();
            await Join(member, clubId);
            members.Add(member);
        }

        return (clubId, owner, members);
    }

    /// <summary>Создаёт отбор из свежих книг (по умолчанию двух) на указанное число минут.</summary>
    public static async Task<PollInfo> Poll(TestUser creator, Guid clubId, int books = 2, int minutes = 60)
    {
        var bookIds = new List<Guid>();
        for (var i = 0; i < books; i++)
            bookIds.Add(await Book(creator, title: "Кандидат " + (char)('A' + i) + " " + Tag()));

        var reply = (await Api.Post("api/polls", new
        {
            clubId,
            question = "Что читаем " + Tag() + "?",
            bookIds,
            durationMinutes = minutes
        }, creator.Token)).ShouldBe(201);

        var options = reply.Json.GetProperty("options").EnumerateArray()
            .Select(o => (OptionId: o.GetProperty("id").GetGuid(), BookId: o.GetProperty("bookId").GetGuid()))
            .ToList();

        // порядок вариантов в ответе не гарантирован, поэтому сопоставляем с исходным списком книг по BookId
        var optionIds = bookIds.Select(b => options.Single(o => o.BookId == b).OptionId).ToList();
        return new PollInfo(reply.GuidOf("id"), clubId, optionIds, bookIds);
    }

    public static async Task Vote(TestUser user, PollInfo poll, int optionIndex) =>
        (await Api.Post($"api/polls/{poll.Id}/vote", new { pollOptionId = poll.OptionIds[optionIndex] }, user.Token))
            .ShouldBe(200);

    /// <summary>Закрывает отбор досрочно от имени админа клуба.</summary>
    public static async Task Close(TestUser clubAdmin, PollInfo poll) =>
        (await Api.Post($"api/polls/{poll.Id}/close", null, clubAdmin.Token)).ShouldBe(200);
}

/// <summary>Созданный отбор: его номер и номера вариантов в том же порядке, что и книги.</summary>
public sealed record PollInfo(Guid Id, Guid ClubId, List<Guid> OptionIds, List<Guid> BookIds);
