namespace BookClub.Tests.Support;

/// <summary>Тест, которому нужен секрет подписи токенов. Если секрет не задан, тест пропускается, а не падает.</summary>
public sealed class NeedsJwtKeyFactAttribute : FactAttribute
{
    public NeedsJwtKeyFactAttribute()
    {
        if (Settings.JwtKey is null)
            Skip = "Не задана переменная BOOKCLUB_TEST_JWT_KEY";
    }
}
