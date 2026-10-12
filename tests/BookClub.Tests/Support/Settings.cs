namespace BookClub.Tests.Support;

/// <summary>
/// Настройки берутся из переменных окружения, чтобы одни и те же тесты работали и на компьютере,
/// и в GitHub Actions. Если переменной нет, подставляется значение для локального запуска.
/// </summary>
public static class Settings
{
    /// <summary>Адрес запущенного сайта. Тесты ходят по нему как обычный посетитель.</summary>
    public static string SiteUrl { get; } =
        (Environment.GetEnvironmentVariable("BOOKCLUB_URL") ?? "http://127.0.0.1:5153").TrimEnd('/');

    /// <summary>Строка подключения к ТЕСТОВОЙ базе. Нужна, чтобы подглядывать в таблицы и делать админа.</summary>
    public static string DbConnection { get; } =
        Environment.GetEnvironmentVariable("BOOKCLUB_TEST_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=bookclub_test;Username=postgres;Password=postgres";

    /// <summary>Секрет подписи токенов, с которым запущен сайт. Нужен для проверок «поддельных» токенов.</summary>
    public static string? JwtKey { get; } = Environment.GetEnvironmentVariable("BOOKCLUB_TEST_JWT_KEY");

    public const string JwtIssuer = "BookClub";
    public const string JwtAudience = "BookClub";
}
