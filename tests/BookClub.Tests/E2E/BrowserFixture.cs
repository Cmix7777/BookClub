using Microsoft.Playwright;

namespace BookClub.Tests.E2E;

/// <summary>Один настоящий браузер (Chromium) на все сквозные тесты: запускается один раз, а у каждого теста свой «профиль».</summary>
public sealed class BrowserFixture : IAsyncLifetime
{
    public IPlaywright Engine { get; private set; } = null!;

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Assertions.SetDefaultExpectTimeout(10_000);
        Engine = await Playwright.CreateAsync();
        Browser = await Engine.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        await Browser.CloseAsync();
        Engine.Dispose();
    }
}

[CollectionDefinition("browser")]
public sealed class BrowserCollection : ICollectionFixture<BrowserFixture>
{
}
