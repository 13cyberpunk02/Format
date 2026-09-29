using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Format.Auth.Api.Tokens;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "format-auth";
    public string Audience { get; set; } = "format";
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Путь к закрытому ключу RSA в формате PEM. Относительный путь - от папки сервиса.</summary>
    public string PrivateKeyPath { get; set; } = "keys/jwt-signing.pem";
}

/// <summary>Открытый ключ в формате JWK (RFC 7517). Только открытая часть - показывать можно всем.</summary>
public sealed record PublicJwk(string Kty, string Use, string Alg, string Kid, string N, string E);

/// <summary>Закрытый ключ RSA, которым сервис подписывает токены. Один на всё приложение.</summary>
public sealed class JwtSigningKey : IDisposable
{
    private readonly RSA _rsa;

    private JwtSigningKey(RSA rsa)
    {
        _rsa = rsa;
        SecurityKey = new RsaSecurityKey(rsa) { KeyId = ComputeKeyId(rsa) };
        Credentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256);

        var publicParameters = rsa.ExportParameters(includePrivateParameters: false);
        PublicJwk = new PublicJwk(
            Kty: "RSA",
            Use: "sig",
            Alg: SecurityAlgorithms.RsaSha256,
            Kid: SecurityKey.KeyId,
            N: Base64UrlEncoder.Encode(publicParameters.Modulus),
            E: Base64UrlEncoder.Encode(publicParameters.Exponent));
    }

    public RsaSecurityKey SecurityKey { get; }
    public SigningCredentials Credentials { get; }
    public PublicJwk PublicJwk { get; }

    public static JwtSigningKey Load(string path, bool createIfMissing, ILogger logger)
    {
        if (File.Exists(path))
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(path));
            return new JwtSigningKey(rsa);
        }

        if (!createIfMissing)
            throw new InvalidOperationException($"Не найден ключ подписи JWT: {path}");

        // Только для разработки: создаём ключ сами, чтобы не возиться с ним вручную
        var created = RSA.Create(3072);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, created.ExportPkcs8PrivateKeyPem());

        logger.LogWarning("Создан новый ключ подписи JWT: {Path}. В продакшене ключ должен задаваться явно.", path);
        return new JwtSigningKey(created);
    }

    /// <summary>Идентификатор ключа (kid): по нему проверяющая сторона поймёт, каким ключом подписан токен.</summary>
    private static string ComputeKeyId(RSA rsa)
    {
        var hash = SHA256.HashData(rsa.ExportSubjectPublicKeyInfo());
        return Base64UrlEncoder.Encode(hash[..16]);
    }

    public void Dispose() => _rsa.Dispose();
}