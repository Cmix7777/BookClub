using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Playwright;

namespace BookClub.Tests.E2E;

/// <summary>
/// Один «визит на сайт»: отдельное окно браузера со своей памятью (так что токен входа одного теста
/// не попадёт в другой). Умеет сразу открыть сайт уже вошедшим пользователем.
/// </summary>
public sealed class SiteVisit : IAsyncDisposable
{
    private static readonly string ArtifactsDir =
        Environment.GetEnvironmentVariable("BOOKCLUB_ARTIFACTS") ?? Path.Combine(Directory.GetCurrentDirectory(), "e2e-failures");

    private readonly IBrowserContext _context;

    private SiteVisit(IBrowserContext context, IPage page)
    {
        _context = context;
        Page = page;
    }

    public IPage Page { get; }

    /// <summary>Открывает пустую страницу сайта. Если передан пользователь, он уже вошёл (токен лежит в памяти браузера).</summary>
    public static async Task<SiteVisit> Open(BrowserFixture fixture, TestUser? loggedInAs = null, string? junkToken = null)
    {
        var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = "ru-RU",
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 }
        });

        // внешние ресурсы (шрифты Google) тестам не нужны, а ждать их долго и ненадёжно
        await context.RouteAsync("**/*", route =>
            route.Request.Url.StartsWith(Settings.SiteUrl, StringComparison.OrdinalIgnoreCase)
                ? route.ContinueAsync()
                : route.AbortAsync());

        if (loggedInAs is not null)
        {
            var me = (await Api.Get("api/auth/me", loggedInAs.Token)).ShouldBe(200);
            await context.AddInitScriptAsync(
                "localStorage.setItem('tr_token', " + JsonSerializer.Serialize(loggedInAs.Token) + ");" +
                "localStorage.setItem('tr_user', " + JsonSerializer.Serialize(me.Body) + ");");
        }
        else if (junkToken is not null)
        {
            var fakeUser = "{\"id\":\"00000000-0000-0000-0000-000000000000\",\"username\":\"ghost\",\"role\":\"User\"}";
            await context.AddInitScriptAsync(
                "localStorage.setItem('tr_token', " + JsonSerializer.Serialize(junkToken) + ");" +
                "localStorage.setItem('tr_user', " + JsonSerializer.Serialize(fakeUser) + ");");
        }

        var page = await context.NewPageAsync();
        // в интерфейсе есть окна confirm(): везде отвечаем «ОК»
        page.Dialog += (_, dialog) => dialog.AcceptAsync();
        await page.GotoAsync(Settings.SiteUrl + "/", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        return new SiteVisit(context, page);
    }

    /// <summary>Нажимает кнопку верхнего меню: clubs, catalog, profile, admin, home.</summary>
    public Task Go(string section) => Page.Locator($".nav-btn[data-go='{section}']").ClickAsync();

    /// <summary>
    /// Выполняет проверку. Если она упала, сохраняет снимок экрана и текст страницы (их потом можно скачать из GitHub Actions),
    /// а затем пробрасывает исходную ошибку дальше.
    /// </summary>
    public async Task Check(Func<IPage, Task> body, [CallerMemberName] string name = "")
    {
        try
        {
            await body(Page);
        }
        catch
        {
            try
            {
                Directory.CreateDirectory(ArtifactsDir);
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(ArtifactsDir, name + ".png"), FullPage = true });
                await File.WriteAllTextAsync(Path.Combine(ArtifactsDir, name + ".txt"), await Page.InnerTextAsync("body"));
            }
            catch
            {
                // диагностика не должна заслонять настоящую причину падения
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync() => await _context.CloseAsync();
}
