namespace Format.Auth.Api.Data;

public sealed class RefreshToken
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }

    /// <summary>Семейство: все токены одной сессии (один вход на одном устройстве).</summary>
    public Guid FamilyId { get; init; }

    /// <summary>SHA-256 от значения токена, в шестнадцатеричном виде. Сам токен не хранится.</summary>
    public required string TokenHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Когда отозван: при обмене, выходе, блокировке или смене пароля.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Каким токеном заменён при обмене. Пусто - отозван по другой причине.</summary>
    public Guid? ReplacedById { get; set; }
}