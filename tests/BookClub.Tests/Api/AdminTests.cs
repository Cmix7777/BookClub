namespace BookClub.Tests.Rest;

/// <summary>Админка: кто имеет к ней доступ, блокировка, смена ролей, модерация.</summary>
[Trait("Category", "Api")]
public class AdminTests
{
    // ---------- доступ ----------

    [Theory(DisplayName = "Разделы админки закрыты для гостя (401) и обычного пользователя (403)")]
    [InlineData("api/admin/stats")]
    [InlineData("api/admin/users")]
    [InlineData("api/admin/reviews")]
    [InlineData("api/admin/books/duplicates")]
    public async Task Admin_endpoints_are_closed_to_non_admins(string path)
    {
        var user = await TestUser.Create();

        (await Api.Get(path)).ShouldBe(401);
        (await Api.Get(path, user.Token)).ShouldBe(403);
    }

    [Fact(DisplayName = "Изменяющие действия админки тоже закрыты для обычного пользователя: 403")]
    public async Task Admin_actions_are_closed_to_regular_users()
    {
        var user = await TestUser.Create();
        var victim = await TestUser.Create();
        var bookId = await Make.Book(user);

        (await Api.Post($"api/admin/users/{victim.Id}/block", null, user.Token)).ShouldBe(403);
        (await Api.Put($"api/admin/users/{victim.Id}/role", new { role = "Admin" }, user.Token)).ShouldBe(403);
        (await Api.Delete($"api/admin/books/{bookId}", user.Token)).ShouldBe(403);
        (await Api.Delete($"api/admin/reviews/{Guid.NewGuid()}", user.Token)).ShouldBe(403);
    }

    // ---------- статистика и списки ----------

    [Fact(DisplayName = "Статистика растёт вместе с жизнью сайта: новый клуб, книга и рецензия учтены")]
    public async Task Stats_count_new_things()
    {
        var admin = await TestUser.CreateAdmin();
        var before = (await Api.Get("api/admin/stats", admin.Token)).ShouldBe(200);

        var author = await TestUser.Create();
        var bookId = await Make.Book(author);
        await Make.Club(author);
        await Api.Put($"api/books/{bookId}/reviews/mine", new { rating = 5 }, author.Token);

        var after = (await Api.Get("api/admin/stats", admin.Token)).ShouldBe(200);
        Assert.True(after.Int("users") >= before.Int("users") + 1);
        Assert.True(after.Int("books") >= before.Int("books") + 1);
        Assert.True(after.Int("clubs") >= before.Int("clubs") + 1);
        Assert.True(after.Int("reviews") >= before.Int("reviews") + 1);
        Assert.True(after.Int("admins") >= 1);
    }

    [Fact(DisplayName = "Список пользователей для админа: почта, роль, статус и число клубов")]
    public async Task User_list_shows_admin_details()
    {
        var admin = await TestUser.CreateAdmin();
        var user = await TestUser.Create();
        await Make.Club(user);

        var list = (await Api.Get("api/admin/users", admin.Token)).ShouldBe(200).Items();
        var row = list.Single(u => u.GetProperty("id").GetGuid() == user.Id);

        Assert.Equal(user.Email, row.GetProperty("email").GetString());
        Assert.Equal("User", row.GetProperty("role").GetString());
        Assert.False(row.GetProperty("isBlocked").GetBoolean());
        Assert.Equal(1, row.GetProperty("clubsCount").GetInt32());
        Assert.DoesNotContain("$2", (await Api.Get("api/admin/users", admin.Token)).Body);
    }

    // ---------- блокировка ----------

    [Fact(DisplayName = "Заблокированный не может войти (403), а его прежний токен сразу перестаёт работать (401)")]
    public async Task Blocked_user_is_locked_out_immediately()
    {
        var admin = await TestUser.CreateAdmin();
        var user = await TestUser.Create();
        (await Api.Get("api/auth/me", user.Token)).ShouldBe(200);

        (await Api.Post($"api/admin/users/{user.Id}/block", null, admin.Token)).ShouldBe(200);

        (await Api.Get("api/auth/me", user.Token)).ShouldBe(401);
        (await Api.Post("api/auth/login", new { login = user.Username, password = user.Password })).ShouldBe(403);
    }

    [Fact(DisplayName = "После разблокировки человек снова входит")]
    public async Task Unblocked_user_can_log_in_again()
    {
        var admin = await TestUser.CreateAdmin();
        var user = await TestUser.Create();
        await Api.Post($"api/admin/users/{user.Id}/block", null, admin.Token);

        (await Api.Post($"api/admin/users/{user.Id}/unblock", null, admin.Token)).ShouldBe(200);

        await user.LoginAgain();
        (await Api.Get("api/auth/me", user.Token)).ShouldBe(200);
    }

    [Fact(DisplayName = "Блокировка: себя нельзя (400), админа нельзя (403), дважды нельзя (409), несуществующего нельзя (404)")]
    public async Task Block_rules()
    {
        var admin = await TestUser.CreateAdmin();
        var otherAdmin = await TestUser.CreateAdmin();
        var user = await TestUser.Create();

        (await Api.Post($"api/admin/users/{admin.Id}/block", null, admin.Token)).ShouldBe(400);
        (await Api.Post($"api/admin/users/{otherAdmin.Id}/block", null, admin.Token)).ShouldBe(403);
        (await Api.Post($"api/admin/users/{Guid.NewGuid()}/block", null, admin.Token)).ShouldBe(404);

        (await Api.Post($"api/admin/users/{user.Id}/block", null, admin.Token)).ShouldBe(200);
        (await Api.Post($"api/admin/users/{user.Id}/block", null, admin.Token)).ShouldBe(409);
    }

    [Fact(DisplayName = "Разблокировка: не заблокированного — 409, несуществующего — 404")]
    public async Task Unblock_rules()
    {
        var admin = await TestUser.CreateAdmin();
        var user = await TestUser.Create();

        (await Api.Post($"api/admin/users/{user.Id}/unblock", null, admin.Token)).ShouldBe(409);
        (await Api.Post($"api/admin/users/{Guid.NewGuid()}/unblock", null, admin.Token)).ShouldBe(404);
    }

    // ---------- роли ----------

    [Fact(DisplayName = "Новому админу нужно войти заново: токен со старой ролью отвергается, новый — открывает админку")]
    public async Task Role_change_invalidates_the_old_token()
    {
        var admin = await TestUser.CreateAdmin();
        var user = await TestUser.Create();
        (await Api.Get("api/admin/stats", user.Token)).ShouldBe(403);

        (await Api.Put($"api/admin/users/{user.Id}/role", new { role = "Admin" }, admin.Token)).ShouldBe(200);

        (await Api.Get("api/auth/me", user.Token)).ShouldBe(401);
        await user.LoginAgain();
        (await Api.Get("api/admin/stats", user.Token)).ShouldBe(200);
        Assert.Equal("Admin", (await Api.Get("api/auth/me", user.Token)).ShouldBe(200).Str("role"));
    }

    [Fact(DisplayName = "Админа можно разжаловать: после этого админка снова закрыта")]
    public async Task Admin_can_be_demoted()
    {
        var admin = await TestUser.CreateAdmin();
        var other = await TestUser.CreateAdmin();

        (await Api.Put($"api/admin/users/{other.Id}/role", new { role = "User" }, admin.Token)).ShouldBe(200);

        await other.LoginAgain();
        (await Api.Get("api/admin/stats", other.Token)).ShouldBe(403);
    }

    [Fact(DisplayName = "Правила смены роли: себе нельзя (400), ту же роль нельзя (409), несуществующую роль нельзя (400), чужого несуществующего нельзя (404)")]
    public async Task Role_change_rules()
    {
        var admin = await TestUser.CreateAdmin();
        var user = await TestUser.Create();

        (await Api.Put($"api/admin/users/{admin.Id}/role", new { role = "User" }, admin.Token)).ShouldBe(400);
        (await Api.Put($"api/admin/users/{user.Id}/role", new { role = "User" }, admin.Token)).ShouldBe(409);
        (await Api.Put($"api/admin/users/{user.Id}/role", new { role = "Root" }, admin.Token)).ShouldBe(400);
        (await Api.Put($"api/admin/users/{Guid.NewGuid()}/role", new { role = "Admin" }, admin.Token)).ShouldBe(404);
    }

    [Fact(DisplayName = "Заблокированного нельзя сделать админом, пока не разблокирован: 400")]
    public async Task Blocked_user_cannot_be_promoted()
    {
        var admin = await TestUser.CreateAdmin();
        var user = await TestUser.Create();
        await Api.Post($"api/admin/users/{user.Id}/block", null, admin.Token);

        (await Api.Put($"api/admin/users/{user.Id}/role", new { role = "Admin" }, admin.Token)).ShouldBe(400);
    }

    // ---------- модерация ----------

    [Fact(DisplayName = "Админ видит автора рецензии, даже анонимной (так задумано для модерации)")]
    public async Task Admin_sees_authors_of_anonymous_reviews()
    {
        var admin = await TestUser.CreateAdmin();
        var author = await TestUser.Create();
        var bookId = await Make.Book(author);
        await Api.Put($"api/books/{bookId}/reviews/mine", new { rating = 2, text = "Скучно", isAnonymous = true }, author.Token);

        var list = (await Api.Get("api/admin/reviews?take=200", admin.Token)).ShouldBe(200).Items();
        var row = list.Single(r => r.GetProperty("bookId").GetGuid() == bookId);

        Assert.Equal(author.Username, row.GetProperty("authorUsername").GetString());
        Assert.True(row.GetProperty("isAnonymous").GetBoolean());
    }

    [Fact(DisplayName = "Параметр take ограничивает список рецензий, а странные значения не ломают запрос")]
    public async Task Review_list_size_is_limited()
    {
        var admin = await TestUser.CreateAdmin();
        var author = await TestUser.Create();
        for (var i = 0; i < 2; i++)
        {
            var bookId = await Make.Book(author);
            await Api.Put($"api/books/{bookId}/reviews/mine", new { rating = 3 }, author.Token);
        }

        Assert.Single((await Api.Get("api/admin/reviews?take=1", admin.Token)).ShouldBe(200).Items());
        Assert.Single((await Api.Get("api/admin/reviews?take=-5", admin.Token)).ShouldBe(200).Items());
        Assert.True((await Api.Get("api/admin/reviews?take=100000", admin.Token)).ShouldBe(200).Items().Count <= 200);
    }

    [Fact(DisplayName = "Админ удаляет рецензию: 204, она исчезает у книги; несуществующая — 404")]
    public async Task Admin_can_delete_a_review()
    {
        var admin = await TestUser.CreateAdmin();
        var author = await TestUser.Create();
        var bookId = await Make.Book(author);
        var review = (await Api.Put($"api/books/{bookId}/reviews/mine", new { rating = 1, text = "Грубо" }, author.Token)).ShouldBe(201);

        (await Api.Delete($"api/admin/reviews/{review.GuidOf("id")}", admin.Token)).ShouldBe(204);

        Assert.Equal(0, (await Api.Get($"api/books/{bookId}/reviews")).ShouldBe(200).Int("count"));
        (await Api.Delete($"api/admin/reviews/{review.GuidOf("id")}", admin.Token)).ShouldBe(404);
    }

    // ---------- книги ----------

    [Fact(DisplayName = "Дубликаты книг находятся без учёта регистра; лишнюю копию можно удалить")]
    public async Task Duplicate_books_are_found_and_removable()
    {
        var admin = await TestUser.CreateAdmin();
        var user = await TestUser.Create();
        var tag = Make.Tag();
        // латиница нарочно: поведение lower() для кириллицы зависит от настроек базы, а проверяем мы не их
        var first = await Make.Book(user, title: "Twin " + tag, author: "Twin Author");
        var second = await Make.Book(user, title: "TWIN " + tag, author: "twin author");

        var groups = (await Api.Get("api/admin/books/duplicates", admin.Token)).ShouldBe(200).Items();
        var group = groups.Single(g => g.GetProperty("bookIds").EnumerateArray().Any(id => id.GetGuid() == first));

        Assert.Contains(group.GetProperty("bookIds").EnumerateArray(), id => id.GetGuid() == second);

        (await Api.Delete($"api/admin/books/{second}", admin.Token)).ShouldBe(204);
        (await Api.Get($"api/books/{second}")).ShouldBe(404);
        (await Api.Get($"api/books/{first}")).ShouldBe(200);
    }

    [Fact(DisplayName = "Книгу из отбора клуба удалить нельзя: 409; несуществующую — 404")]
    public async Task Book_used_in_a_poll_cannot_be_deleted()
    {
        var admin = await TestUser.CreateAdmin();
        var owner = await TestUser.Create();
        var poll = await Make.Poll(owner, await Make.Club(owner));

        (await Api.Delete($"api/admin/books/{poll.BookIds[0]}", admin.Token)).ShouldBe(409);
        (await Api.Get($"api/books/{poll.BookIds[0]}")).ShouldBe(200);
        (await Api.Delete($"api/admin/books/{Guid.NewGuid()}", admin.Token)).ShouldBe(404);
    }

    // ---------- порядок списков, счётчики, дубликаты ----------

    [Fact(DisplayName = "Список пользователей для админа идёт в порядке регистрации: сначала самые давние")]
    public async Task User_list_is_ordered_by_registration()
    {
        var admin = await TestUser.CreateAdmin();
        var first = await TestUser.Create();
        await Task.Delay(50);
        var second = await TestUser.Create();

        var ids = (await Api.Get("api/admin/users", admin.Token)).ShouldBe(200).Items()
            .Select(u => u.GetProperty("id").GetGuid()).ToList();

        Assert.True(ids.IndexOf(first.Id) >= 0 && ids.IndexOf(first.Id) < ids.IndexOf(second.Id));
    }

    [Fact(DisplayName = "В списке пользователей у каждого свой счётчик рецензий")]
    public async Task User_list_counts_reviews_per_user()
    {
        var admin = await TestUser.CreateAdmin();
        var one = await TestUser.Create();
        var two = await TestUser.Create();
        var none = await TestUser.Create();
        var oneBook = await Make.Book(one);
        await Api.Put($"api/books/{oneBook}/reviews/mine", new { rating = 4 }, one.Token);
        for (var i = 0; i < 2; i++)
        {
            var book = await Make.Book(two);
            await Api.Put($"api/books/{book}/reviews/mine", new { rating = 3 }, two.Token);
        }

        var list = (await Api.Get("api/admin/users", admin.Token)).ShouldBe(200).Items();
        int ReviewsOf(TestUser u) =>
            list.Single(r => r.GetProperty("id").GetGuid() == u.Id).GetProperty("reviewsCount").GetInt32();

        Assert.Equal(1, ReviewsOf(one));
        Assert.Equal(2, ReviewsOf(two));
        Assert.Equal(0, ReviewsOf(none));
    }

    [Fact(DisplayName = "Список рецензий для админа: самые свежие сверху")]
    public async Task Review_list_shows_newest_first()
    {
        var admin = await TestUser.CreateAdmin();
        var author = await TestUser.Create();
        var olderBook = await Make.Book(author);
        var newerBook = await Make.Book(author);
        await Api.Put($"api/books/{olderBook}/reviews/mine", new { rating = 3 }, author.Token);
        await Task.Delay(50);
        await Api.Put($"api/books/{newerBook}/reviews/mine", new { rating = 3 }, author.Token);

        var books = (await Api.Get("api/admin/reviews?take=200", admin.Token)).ShouldBe(200).Items()
            .Select(r => r.GetProperty("bookId").GetGuid()).ToList();
        var newer = books.IndexOf(newerBook);
        var older = books.IndexOf(olderBook);

        // если за это время набежало больше 200 более свежих рецензий, старая просто не поместилась в список
        Assert.True(newer >= 0 && (older == -1 || newer < older));
    }

    [Fact(DisplayName = "В списке дубликатов нет книг, у которых нет двойника")]
    public async Task Unique_books_are_not_listed_as_duplicates()
    {
        var admin = await TestUser.CreateAdmin();
        var user = await TestUser.Create();
        var lone = await Make.Book(user);

        var groups = (await Api.Get("api/admin/books/duplicates", admin.Token)).ShouldBe(200).Items();

        Assert.DoesNotContain(groups, g => g.GetProperty("bookIds").EnumerateArray().Any(id => id.GetGuid() == lone));
    }

    private static async Task<int> StatOf(TestUser admin, string name) =>
        (await Api.Get("api/admin/stats", admin.Token)).ShouldBe(200).Int(name);

    /// <summary>
    /// Счётчики админки общие для всех тестов, идущих одновременно, поэтому точное число не проверить.
    /// Меряем разницу «до и после» своего действия, а если вмешался чужой тест, повторяем попытку.
    /// <paramref name="prepare"/> готовит всё нужное и возвращает само действие, которое и измеряется.
    /// </summary>
    private static async Task<bool> ChangesBy(TestUser admin, string stat, int expected, Func<Task<Func<Task>>> prepare)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var act = await prepare();
            var before = await StatOf(admin, stat);
            await act();
            var after = await StatOf(admin, stat);
            if (after - before == expected)
                return true;
        }

        return false;
    }

    [Fact(DisplayName = "Счётчик админов в статистике растёт на единицу, когда появляется новый админ")]
    public async Task Stats_count_admins()
    {
        var admin = await TestUser.CreateAdmin();

        var counted = await ChangesBy(admin, "admins", 1, () =>
            Task.FromResult<Func<Task>>(async () => await TestUser.CreateAdmin()));

        Assert.True(counted, "новый админ не прибавил единицу к счётчику админов");
    }

    [Fact(DisplayName = "Счётчик идущих отборов: новый отбор добавляет единицу, закрытый убавляет")]
    public async Task Stats_count_active_polls()
    {
        var admin = await TestUser.CreateAdmin();

        var created = await ChangesBy(admin, "activePolls", 1, async () =>
        {
            var owner = await TestUser.Create();
            var clubId = await Make.Club(owner);
            return async () => await Make.Poll(owner, clubId);
        });
        Assert.True(created, "новый отбор не прибавил единицу к счётчику идущих отборов");

        var closed = await ChangesBy(admin, "activePolls", -1, async () =>
        {
            var owner = await TestUser.Create();
            var poll = await Make.Poll(owner, await Make.Club(owner));
            return async () => await Make.Close(owner, poll);
        });
        Assert.True(closed, "закрытый отбор не убавил единицу в счётчике идущих отборов");
    }
}
