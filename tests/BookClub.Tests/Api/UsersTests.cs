namespace BookClub.Tests.Rest;

/// <summary>Публичный список пользователей: что он показывает и чего не должен.</summary>
[Trait("Category", "Api")]
public class UsersTests
{
    [Fact(DisplayName = "Профиль пользователя виден всем, но без почты и пароля")]
    public async Task Public_profile_has_no_private_fields()
    {
        var user = await TestUser.Create();

        var reply = (await Api.Get("api/users/" + user.Id)).ShouldBe(200);

        Assert.Equal(user.Username, reply.Str("username"));
        Assert.False(reply.Has("email"));
        Assert.False(reply.Has("passwordHash"));
        Assert.DoesNotContain(user.Email, reply.Body);
    }

    [Fact(DisplayName = "В общем списке пользователей нет почт и хешей паролей")]
    public async Task Public_list_has_no_private_fields()
    {
        var user = await TestUser.Create();

        var reply = (await Api.Get("api/users")).ShouldBe(200);

        Assert.Contains(user.Username, reply.Body);
        Assert.DoesNotContain(user.Email, reply.Body);
        Assert.DoesNotContain("$2", reply.Body);
        Assert.DoesNotContain("passwordHash", reply.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Несуществующий пользователь: 404")]
    public async Task Unknown_user_is_not_found()
    {
        (await Api.Get("api/users/" + Guid.NewGuid())).ShouldBe(404);
    }

    [Fact(DisplayName = "Общий список пользователей идёт по алфавиту логинов")]
    public async Task Public_list_is_sorted_by_username()
    {
        var first = await TestUser.Create();
        var second = await TestUser.Create();
        var (smaller, larger) = string.CompareOrdinal(first.Username, second.Username) < 0
            ? (first, second)
            : (second, first);

        var names = (await Api.Get("api/users")).ShouldBe(200).Items()
            .Select(u => u.GetProperty("username").GetString()!).ToList();

        Assert.True(names.IndexOf(smaller.Username) < names.IndexOf(larger.Username));
    }
}
