using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace BookClub.Tests.E2E;

/// <summary>Главная страница, регистрация и вход глазами посетителя в настоящем браузере.</summary>
[Collection("browser")]
[Trait("Category", "E2E")]
public class AccountUiTests(BrowserFixture browser)
{
    [Fact(DisplayName = "Гость видит главную: заголовок, четыре пункта меню без «Админки», счётчики и кнопку «Войти»")]
    public async Task Home_page_greets_a_guest()
    {
        await using var visit = await SiteVisit.Open(browser);

        await visit.Check(async page =>
        {
            await Expect(page.Locator("h1")).ToContainTextAsync("Читать в одиночку");
            await Expect(page.Locator(".nav-btn:visible")).ToHaveCountAsync(4);
            await Expect(page.Locator("#navAdmin")).ToBeHiddenAsync();
            await Expect(page.Locator("#who")).ToContainTextAsync("Войти");
            await Expect(page.Locator("#homeStats .stat")).ToHaveCountAsync(4);
            await Expect(page.Locator("#homeStats")).ToContainTextAsync("книг в каталоге");
            await Expect(page.Locator("#homeStats")).ToContainTextAsync("отборов идёт сейчас");
        });
    }

    [Fact(DisplayName = "Регистрация через форму: аккаунт создан, человек сразу вошёл и попал в раздел «Клубы»")]
    public async Task Registration_logs_the_new_user_in()
    {
        var name = "ui" + Make.Tag();
        await using var visit = await SiteVisit.Open(browser);

        await visit.Check(async page =>
        {
            await page.Locator("#who button").ClickAsync();
            await Expect(page.Locator("#page-auth")).ToHaveClassAsync(new Regex("active"));
            await page.Locator(".auth-tab[data-tab='register']").ClickAsync();
            await page.Locator("#rg-user").FillAsync(name);
            await page.Locator("#rg-email").FillAsync(name + "@example.test");
            await page.Locator("#rg-pass").FillAsync(TestUser.DefaultPassword);
            await page.Locator("#registerForm button[type='submit']").ClickAsync();

            await Expect(page.Locator("#who .uname")).ToHaveTextAsync(name);
            await Expect(page.Locator("#page-clubs")).ToHaveClassAsync(new Regex("active"));
            Assert.False(string.IsNullOrEmpty(await page.EvaluateAsync<string?>("() => localStorage.getItem('tr_token')")));
        });

        (await Api.Post("api/auth/login", new { login = name, password = TestUser.DefaultPassword })).ShouldBe(200);
    }

    [Fact(DisplayName = "Слишком короткий пароль: под формой появляется причина, вход не происходит")]
    public async Task Registration_shows_why_it_failed()
    {
        var name = "ui" + Make.Tag();
        await using var visit = await SiteVisit.Open(browser);

        await visit.Check(async page =>
        {
            await page.Locator("#who button").ClickAsync();
            await page.Locator(".auth-tab[data-tab='register']").ClickAsync();
            await page.Locator("#rg-user").FillAsync(name);
            await page.Locator("#rg-email").FillAsync(name + "@example.test");
            await page.Locator("#rg-pass").FillAsync("123");
            await page.Locator("#registerForm button[type='submit']").ClickAsync();

            await Expect(page.Locator("#authErr")).ToHaveTextAsync("Пароль не короче 8 символов");
            await Expect(page.Locator("#who")).ToContainTextAsync("Войти");
        });
    }

    [Fact(DisplayName = "Занятый логин при регистрации: сообщение «уже заняты»")]
    public async Task Registration_rejects_a_taken_login()
    {
        var existing = await TestUser.Create();
        await using var visit = await SiteVisit.Open(browser);

        await visit.Check(async page =>
        {
            await page.Locator("#who button").ClickAsync();
            await page.Locator(".auth-tab[data-tab='register']").ClickAsync();
            await page.Locator("#rg-user").FillAsync(existing.Username);
            await page.Locator("#rg-email").FillAsync("new" + Make.Tag() + "@example.test");
            await page.Locator("#rg-pass").FillAsync(TestUser.DefaultPassword);
            await page.Locator("#registerForm button[type='submit']").ClickAsync();

            await Expect(page.Locator("#authErr")).ToContainTextAsync("уже заняты");
        });
    }

    [Fact(DisplayName = "Вход с неверным паролем: сообщение «Неверный логин или пароль»")]
    public async Task Login_with_wrong_password_is_explained()
    {
        var user = await TestUser.Create();
        await using var visit = await SiteVisit.Open(browser);

        await visit.Check(async page =>
        {
            await page.Locator("#who button").ClickAsync();
            await page.Locator("#lg-login").FillAsync(user.Username);
            await page.Locator("#lg-pass").FillAsync("WrongPassword1");
            await page.Locator("#loginForm button[type='submit']").ClickAsync();

            await Expect(page.Locator("#authErr")).ToHaveTextAsync("Неверный логин или пароль");
            await Expect(page.Locator("#who")).ToContainTextAsync("Войти");
        });
    }

    [Fact(DisplayName = "Вход и выход: имя в шапке появляется и пропадает, токен стирается, открывается главная")]
    public async Task Login_and_logout_round_trip()
    {
        var user = await TestUser.Create();
        await using var visit = await SiteVisit.Open(browser);

        await visit.Check(async page =>
        {
            await page.Locator("#who button").ClickAsync();
            await page.Locator("#lg-login").FillAsync(user.Username);
            await page.Locator("#lg-pass").FillAsync(user.Password);
            await page.Locator("#loginForm button[type='submit']").ClickAsync();
            await Expect(page.Locator("#who .uname")).ToHaveTextAsync(user.Username);

            await page.Locator("#logoutBtn").ClickAsync();

            await Expect(page.Locator("#who")).ToContainTextAsync("Войти");
            await Expect(page.Locator("#who .uname")).ToHaveCountAsync(0);
            await Expect(page.Locator("#page-home")).ToHaveClassAsync(new Regex("active"));
            Assert.Null(await page.EvaluateAsync<string?>("() => localStorage.getItem('tr_token')"));
        });
    }

    [Fact(DisplayName = "Гость нажимает «Создать клуб»: его отправляют на вход и объясняют почему")]
    public async Task Guest_is_sent_to_login_when_creating_a_club()
    {
        await using var visit = await SiteVisit.Open(browser);

        await visit.Check(async page =>
        {
            await visit.Go("clubs");
            await page.Locator("#newClubBtn").ClickAsync();

            await Expect(page.Locator("#page-auth")).ToHaveClassAsync(new Regex("active"));
            await Expect(page.Locator("#toast")).ToContainTextAsync("Сначала войдите");
        });
    }

    [Fact(DisplayName = "Просроченный или поддельный токен в браузере: сайт вежливо выводит из аккаунта")]
    public async Task Broken_session_is_ended_politely()
    {
        await using var visit = await SiteVisit.Open(browser, junkToken: "not-a-real-token");

        await visit.Check(async page =>
        {
            // сайт сам замечает, что токен не годится (сервер ответил 401), и выводит человека из аккаунта
            await Expect(page.Locator("#who")).ToContainTextAsync("Войти");
            await Expect(page.Locator("#who .uname")).ToHaveCountAsync(0);
            await Expect(page.Locator("#toast")).ToContainTextAsync("Сессия завершена");

            // и в закрытый раздел после этого не пускает: вместо профиля открывается вход
            await visit.Go("profile");
            await Expect(page.Locator("#page-auth")).ToHaveClassAsync(new Regex("active"));
        });
    }

    [Fact(DisplayName = "Гость открывает клуб: вместо участников и отборов видит приглашение войти")]
    public async Task Guest_sees_a_login_prompt_inside_a_club()
    {
        var owner = await TestUser.Create();
        var name = "Закрытый " + Make.Tag();
        await Make.Club(owner, name: name);
        await using var visit = await SiteVisit.Open(browser);

        await visit.Check(async page =>
        {
            await visit.Go("clubs");
            await page.Locator(".club", new PageLocatorOptions { HasText = name }).ClickAsync();

            await Expect(page.Locator("#clubName")).ToHaveTextAsync(name);
            await Expect(page.Locator("#clubBody")).ToContainTextAsync("Войдите, чтобы увидеть клуб");
            await Expect(page.Locator("#joinBtn")).ToBeHiddenAsync();
            await Expect(page.Locator("#newPollBtn")).ToBeHiddenAsync();
        });
    }
}
