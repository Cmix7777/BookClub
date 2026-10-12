using System.Text;

namespace BookClub.Tests.Rest;

/// <summary>Регистрация, вход и проверка токена: «кто ты» и «пускать ли тебя».</summary>
[Trait("Category", "Api")]
public class AuthTests
{
    // ---------- регистрация ----------

    [Fact(DisplayName = "Регистрация с верными данными: 201, адрес записи и ничего лишнего в ответе")]
    public async Task Register_valid_data_creates_user()
    {
        var name = "reg" + Make.Tag();

        var reply = (await Api.Post("api/auth/register",
            new { username = name, email = name + "@example.test", password = "Passw0rd!test" })).ShouldBe(201);

        Assert.Equal(name, reply.Str("username"));
        Assert.NotEqual(Guid.Empty, reply.GuidOf("id"));
        Assert.EndsWith("/api/users/" + reply.GuidOf("id"), reply.Location);
        Assert.False(reply.Has("email"), "в ответе не должно быть почты");
        Assert.False(reply.Has("passwordHash"), "в ответе не должно быть хеша пароля");
        Assert.False(reply.Has("password"), "в ответе не должно быть пароля");
    }

    [Fact(DisplayName = "Занятый логин нельзя взять второй раз: 409")]
    public async Task Register_duplicate_username_is_rejected()
    {
        var first = await TestUser.Create();

        var reply = await Api.Post("api/auth/register",
            new { username = first.Username, email = "other" + Make.Tag() + "@example.test", password = "Passw0rd!test" });

        reply.ShouldBe(409);
    }

    [Fact(DisplayName = "Занятую почту нельзя взять второй раз, даже в другом регистре: 409")]
    public async Task Register_duplicate_email_is_rejected_regardless_of_case()
    {
        var first = await TestUser.Create();

        var reply = await Api.Post("api/auth/register",
            new { username = "other" + Make.Tag(), email = first.Email.ToUpperInvariant(), password = "Passw0rd!test" });

        reply.ShouldBe(409);
    }

    [Theory(DisplayName = "Неверные данные при регистрации отклоняются: 400")]
    [InlineData("username", "ab")]
    [InlineData("username", "login_longer_than_thirty_characters_x")]
    [InlineData("username", "bad name")]
    [InlineData("username", "имя_кириллицей")]
    [InlineData("username", "")]
    [InlineData("email", "not-an-email")]
    [InlineData("email", "")]
    [InlineData("password", "short")]
    [InlineData("password", "")]
    public async Task Register_invalid_field_is_rejected(string field, string value)
    {
        var name = "val" + Make.Tag();
        var data = new Dictionary<string, string>
        {
            ["username"] = name,
            ["email"] = name + "@example.test",
            ["password"] = "Passw0rd!test"
        };
        data[field] = value;

        (await Api.Post("api/auth/register", data)).ShouldBe(400);
    }

    [Fact(DisplayName = "Пароль длиннее 100 знаков отклоняется: 400")]
    public async Task Register_too_long_password_is_rejected()
    {
        var name = "val" + Make.Tag();

        (await Api.Post("api/auth/register",
            new { username = name, email = name + "@example.test", password = new string('a', 101) })).ShouldBe(400);
    }

    [Fact(DisplayName = "Сломанный JSON при регистрации даёт 400, а не 500")]
    public async Task Register_malformed_json_is_a_client_error()
    {
        (await Api.Post("api/auth/register", "{this is not json")).ShouldBe(400);
    }

    [Fact(DisplayName = "Пароль в базе хранится хешем, а не открытым текстом")]
    public async Task Password_is_stored_as_a_bcrypt_hash()
    {
        var user = await TestUser.Create();

        var hash = await Db.Scalar<string>("SELECT \"PasswordHash\" FROM \"Users\" WHERE \"Id\" = @id", ("id", user.Id));

        Assert.NotNull(hash);
        Assert.NotEqual(user.Password, hash);
        Assert.DoesNotContain(user.Password, hash);
        Assert.StartsWith("$2", hash);
        Assert.Equal(60, hash!.Length);
    }

    [Fact(DisplayName = "У двух пользователей с одинаковым паролем разные хеши (есть «соль»)")]
    public async Task Same_password_gives_different_hashes()
    {
        var first = await TestUser.Create();
        var second = await TestUser.Create();

        var hashes = await Db.Column(
            "SELECT \"PasswordHash\" FROM \"Users\" WHERE \"Id\" IN (@a, @b)", ("a", first.Id), ("b", second.Id));

        Assert.Equal(2, hashes.Count);
        Assert.NotEqual(hashes[0], hashes[1]);
    }

    // ---------- вход ----------

    [Fact(DisplayName = "Вход по логину: 200, токен, срок действия около семи дней")]
    public async Task Login_by_username_returns_token()
    {
        var user = await TestUser.Create();

        var reply = (await Api.Post("api/auth/login", new { login = user.Username, password = user.Password })).ShouldBe(200);

        Assert.False(string.IsNullOrWhiteSpace(reply.Str("token")));
        Assert.Equal(3, reply.Str("token").Split('.').Length);
        Assert.Equal(user.Id, reply.Json.GetProperty("user").GetProperty("id").GetGuid());

        var expires = reply.Json.GetProperty("expiresAt").GetDateTime().ToUniversalTime();
        Assert.InRange(expires, DateTime.UtcNow.AddDays(6), DateTime.UtcNow.AddDays(8));
        Assert.DoesNotContain("$2", reply.Body);
    }

    [Fact(DisplayName = "Вход по почте работает, регистр букв в почте не важен")]
    public async Task Login_by_email_ignores_case()
    {
        var user = await TestUser.Create();

        (await Api.Post("api/auth/login", new { login = user.Email.ToUpperInvariant(), password = user.Password })).ShouldBe(200);
    }

    [Fact(DisplayName = "Пробелы вокруг логина при входе не мешают")]
    public async Task Login_trims_spaces_around_login()
    {
        var user = await TestUser.Create();

        (await Api.Post("api/auth/login", new { login = "  " + user.Username + "  ", password = user.Password })).ShouldBe(200);
    }

    [Fact(DisplayName = "Неверный пароль: 401")]
    public async Task Login_wrong_password_is_unauthorized()
    {
        var user = await TestUser.Create();

        (await Api.Post("api/auth/login", new { login = user.Username, password = "WrongPassword1" })).ShouldBe(401);
    }

    [Fact(DisplayName = "Ответ на неверный пароль и на несуществующий логин одинаков (нельзя выяснять, кто зарегистрирован)")]
    public async Task Login_does_not_reveal_which_part_was_wrong()
    {
        var user = await TestUser.Create();

        var wrongPassword = (await Api.Post("api/auth/login", new { login = user.Username, password = "WrongPassword1" })).ShouldBe(401);
        var unknownUser = (await Api.Post("api/auth/login", new { login = "nobody" + Make.Tag(), password = "WrongPassword1" })).ShouldBe(401);

        Assert.Equal(wrongPassword.Body, unknownUser.Body);
    }

    [Theory(DisplayName = "Вход без логина или без пароля: 400")]
    [InlineData("", "Passw0rd!test")]
    [InlineData("someone", "")]
    public async Task Login_requires_both_fields(string login, string password)
    {
        (await Api.Post("api/auth/login", new { login, password })).ShouldBe(400);
    }

    // ---------- «кто я» и проверка токена ----------

    [Fact(DisplayName = "«Кто я» с верным токеном: профиль, роль User")]
    public async Task Me_returns_profile()
    {
        var user = await TestUser.Create();

        var reply = (await Api.Get("api/auth/me", user.Token)).ShouldBe(200);

        Assert.Equal(user.Id, reply.GuidOf("id"));
        Assert.Equal(user.Username, reply.Str("username"));
        Assert.Equal(user.Email, reply.Str("email"));
        Assert.Equal("User", reply.Str("role"));
    }

    [Fact(DisplayName = "«Кто я» без токена: 401")]
    public async Task Me_without_token_is_unauthorized()
    {
        (await Api.Get("api/auth/me")).ShouldBe(401);
    }

    [Theory(DisplayName = "«Кто я» с мусором вместо токена: 401")]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    public async Task Me_with_garbage_token_is_unauthorized(string token)
    {
        (await Api.Get("api/auth/me", token)).ShouldBe(401);
    }

    [Fact(DisplayName = "Токен с подменённой ролью (подпись осталась старой) отвергается")]
    public async Task Token_with_tampered_role_is_rejected()
    {
        var user = await TestUser.Create();
        var parts = user.Token.Split('.');
        var payload = Encoding.UTF8.GetString(FromBase64Url(parts[1]));
        Assert.Contains("\"role\":\"User\"", payload);

        var forgedPayload = ToBase64Url(Encoding.UTF8.GetBytes(payload.Replace("\"role\":\"User\"", "\"role\":\"Admin\"")));
        var forged = parts[0] + "." + forgedPayload + "." + parts[2];

        (await Api.Get("api/admin/stats", forged)).ShouldBe(401);
    }

    [NeedsJwtKeyFact(DisplayName = "Контроль: правильно собранный вручную токен принимается (значит, проверки «отвергает» чего-то стоят)")]
    public async Task Hand_made_token_with_the_real_key_is_accepted()
    {
        var user = await TestUser.Create();
        var token = Jwt.Build(Settings.JwtKey!, user.Id, user.Username, "User", DateTime.UtcNow.AddHours(1));

        var reply = (await Api.Get("api/auth/me", token)).ShouldBe(200);

        Assert.Equal(user.Id, reply.GuidOf("id"));
    }

    [NeedsJwtKeyFact(DisplayName = "Токен, подписанный чужим ключом, отвергается")]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var user = await TestUser.Create();
        var token = Jwt.Build("another-key-another-key-another-key-123456", user.Id, user.Username, "User", DateTime.UtcNow.AddHours(1));

        (await Api.Get("api/auth/me", token)).ShouldBe(401);
    }

    [NeedsJwtKeyFact(DisplayName = "Просроченный токен отвергается")]
    public async Task Expired_token_is_rejected()
    {
        var user = await TestUser.Create();
        var token = Jwt.Build(Settings.JwtKey!, user.Id, user.Username, "User", DateTime.UtcNow.AddMinutes(-10));

        (await Api.Get("api/auth/me", token)).ShouldBe(401);
    }

    [NeedsJwtKeyFact(DisplayName = "Токен, выпущенный «не для этого сайта» (другой издатель или адресат), отвергается")]
    public async Task Token_for_another_issuer_or_audience_is_rejected()
    {
        var user = await TestUser.Create();
        var wrongIssuer = Jwt.Build(Settings.JwtKey!, user.Id, user.Username, "User", DateTime.UtcNow.AddHours(1), issuer: "SomeoneElse");
        var wrongAudience = Jwt.Build(Settings.JwtKey!, user.Id, user.Username, "User", DateTime.UtcNow.AddHours(1), audience: "SomeoneElse");

        (await Api.Get("api/auth/me", wrongIssuer)).ShouldBe(401);
        (await Api.Get("api/auth/me", wrongAudience)).ShouldBe(401);
    }

    [NeedsJwtKeyFact(DisplayName = "Токен с ролью Admin у обычного пользователя отвергается: сайт сверяет роль с базой")]
    public async Task Token_claiming_a_role_the_user_does_not_have_is_rejected()
    {
        var user = await TestUser.Create();
        var token = Jwt.Build(Settings.JwtKey!, user.Id, user.Username, "Admin", DateTime.UtcNow.AddHours(1));

        (await Api.Get("api/admin/stats", token)).ShouldBe(401);
    }

    [NeedsJwtKeyFact(DisplayName = "Токен несуществующего пользователя отвергается")]
    public async Task Token_of_unknown_user_is_rejected()
    {
        var token = Jwt.Build(Settings.JwtKey!, Guid.NewGuid(), "ghost", "User", DateTime.UtcNow.AddHours(1));

        (await Api.Get("api/auth/me", token)).ShouldBe(401);
    }

    [NeedsJwtKeyFact(DisplayName = "Токен без подписи (alg: none) отвергается")]
    public async Task Unsigned_token_is_rejected()
    {
        var user = await TestUser.Create();
        var token = Jwt.BuildUnsigned(user.Id, user.Username, "User", DateTime.UtcNow.AddHours(1));

        (await Api.Get("api/auth/me", token)).ShouldBe(401);
    }

    private static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        s = s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
        return Convert.FromBase64String(s);
    }

    private static string ToBase64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact(DisplayName = "Двенадцать одновременных регистраций одного логина: создан один человек, остальные получают 409, ни одной ошибки сервера")]
    public async Task Parallel_registrations_of_one_username_create_one_user()
    {
        var name = "race" + Make.Tag();

        var replies = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Api.Post("api/auth/register",
            new { username = name, email = name + i + "@example.test", password = "Passw0rd!test" })));

        Assert.Equal(1, replies.Count(r => r.Code == 201));
        Assert.All(replies.Where(r => r.Code != 201), r => Assert.Equal(409, r.Code));
    }
}
