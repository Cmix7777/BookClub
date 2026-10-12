namespace BookClub.Tests.Support;

/// <summary>Свежий пользователь для одного теста. У каждого теста свои люди, поэтому тесты не мешают друг другу.</summary>
public sealed class TestUser
{
    public const string DefaultPassword = "Passw0rd!test";

    private TestUser(Guid id, string username, string email, string token)
    {
        Id = id;
        Username = username;
        Email = email;
        Token = token;
    }

    public Guid Id { get; }

    public string Username { get; }

    public string Email { get; }

    public string Password => DefaultPassword;

    /// <summary>Токен входа. После смены роли его нужно получить заново: <see cref="LoginAgain"/>.</summary>
    public string Token { get; private set; }

    /// <summary>Регистрирует нового пользователя через API и входит под ним.</summary>
    public static async Task<TestUser> Create()
    {
        var name = "u" + Make.Tag() + Make.Tag()[..4];
        var email = name + "@example.test";

        var registered = (await Api.Post("api/auth/register",
            new { username = name, email, password = DefaultPassword })).ShouldBe(201);

        var login = (await Api.Post("api/auth/login", new { login = name, password = DefaultPassword })).ShouldBe(200);
        return new TestUser(registered.GuidOf("id"), name, email, login.Str("token"));
    }

    /// <summary>Создаёт пользователя и делает его админом сайта напрямую в базе (через сайт это сделать нельзя).</summary>
    public static async Task<TestUser> CreateAdmin()
    {
        var user = await Create();
        await Db.Exec("UPDATE \"Users\" SET \"Role\" = 'Admin' WHERE \"Id\" = @id", ("id", user.Id));
        await user.LoginAgain();
        return user;
    }

    /// <summary>Входит заново и запоминает новый токен (роль в токене могла измениться).</summary>
    public async Task LoginAgain()
    {
        var login = (await Api.Post("api/auth/login", new { login = Username, password = Password })).ShouldBe(200);
        Token = login.Str("token");
    }
}
