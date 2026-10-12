using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BookClub.Tests.Support;

/// <summary>
/// Собирает токены вручную, подписью HS256. Так можно проверить, что сайт отвергает чужие и просроченные токены,
/// а заодно убедиться, что правильно собранный токен принимается (иначе проверки «отвергает» ничего не доказывают).
/// </summary>
public static class Jwt
{
    public static string Build(
        string key,
        Guid userId,
        string username,
        string role,
        DateTime expiresUtc,
        string issuer = Settings.JwtIssuer,
        string audience = Settings.JwtAudience)
    {
        var header = Header("HS256");
        var payload = Payload(userId, username, role, expiresUtc, issuer, audience);

        var signature = Base64Url(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(key),
            Encoding.ASCII.GetBytes(header + "." + payload)));

        return header + "." + payload + "." + signature;
    }

    /// <summary>Токен без подписи («alg: none») — классическая попытка обмануть проверку.</summary>
    public static string BuildUnsigned(Guid userId, string username, string role, DateTime expiresUtc) =>
        Header("none") + "." + Payload(userId, username, role, expiresUtc, Settings.JwtIssuer, Settings.JwtAudience) + ".";

    private static string Header(string algorithm) => Base64Url(JsonSerializer.SerializeToUtf8Bytes(
        new Dictionary<string, object> { ["alg"] = algorithm, ["typ"] = "JWT" }));

    private static string Payload(Guid userId, string username, string role, DateTime expiresUtc, string issuer, string audience) =>
        Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["sub"] = userId.ToString(),
            ["unique_name"] = username,
            ["role"] = role,
            ["iss"] = issuer,
            ["aud"] = audience,
            ["nbf"] = new DateTimeOffset(expiresUtc.AddDays(-1)).ToUnixTimeSeconds(),
            ["exp"] = new DateTimeOffset(expiresUtc).ToUnixTimeSeconds()
        }));

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
