namespace Format.Auth.Api.Data;

public enum UserRole
{
    User,
    Admin,
}

public enum UserStatus
{
    /// <summary>Зарегистрировался, но не перешёл по ссылке из письма. Войти нельзя.</summary>
    PendingActivation,
    Active,
    /// <summary>Заблокирован администратором (например, уволен). Войти нельзя.</summary>
    Disabled,
}

public sealed class User
{
    public Guid Id { get; init; }

    /// <summary>Логин. Всегда в нормализованном виде - см. NormalizeEmail.</summary>
    public required string Email { get; set; }

    /// <summary>ФИО для отображения: «кто загрузил», «кто печатал».</summary>
    public required string DisplayName { get; set; }

    /// <summary>Хэш пароля с солью. Сам пароль нигде не хранится.</summary>
    public string PasswordHash { get; set; } = "";

    public UserRole Role { get; set; }
    public UserStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>Единый вид почты: без пробелов по краям, в нижнем регистре.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}