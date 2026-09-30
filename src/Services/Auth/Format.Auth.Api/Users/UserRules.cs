using System.Net.Mail;

namespace Format.Auth.Api.Users;

public sealed class UserPolicyOptions
{
    /// <summary>Разрешённый домен почты, например "company.ru". Пусто - любой.</summary>
    public string? AllowedEmailDomain { get; set; }
}

public static class UserRules
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    /// <summary>Проверяет уже нормализованную почту. Возвращает текст ошибки или null.</summary>
    public static string? ValidateEmail(string email, UserPolicyOptions options)
    {
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email)
            return "Некорректный адрес почты.";

        var domain = options.AllowedEmailDomain;
        if (!string.IsNullOrWhiteSpace(domain) && !address.Host.Equals(domain, StringComparison.OrdinalIgnoreCase))
            return $"Разрешены только адреса @{domain}.";

        return null;
    }

    public static string? ValidatePassword(string? password, string email)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            return $"Пароль должен быть не короче {MinPasswordLength} символов.";

        if (password.Length > MaxPasswordLength)
            return $"Пароль должен быть не длиннее {MaxPasswordLength} символов.";

        var localPart = email.Split('@')[0];
        if (password.Equals(email, StringComparison.OrdinalIgnoreCase) ||
            password.Equals(localPart, StringComparison.OrdinalIgnoreCase))
            return "Пароль не должен совпадать с почтой.";

        return null;
    }

    public static string? ValidateDisplayName(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? "Укажите ФИО." :
        displayName.Trim().Length > 200 ? "ФИО слишком длинное." :
        null;
    
    public static string? ValidateDepartment(string? department) =>
        string.IsNullOrWhiteSpace(department) ? "Укажите отдел." :
        department.Trim().Length > 200 ? "Название отдела слишком длинное." :
        null;
}