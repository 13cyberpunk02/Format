using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Format.ServiceDefaults;

public static class ServiceDefaults
{
    /// <summary>
    /// Общие настройки всех сервисов. Секреты Docker из /run/secrets становятся настройками:
    /// файл ConnectionStrings__Auth → ConnectionStrings:Auth.
    /// </summary>
    public static WebApplicationBuilder AddFormatDefaults(this WebApplicationBuilder builder)
    {
        builder.Configuration.AddKeyPerFile(directoryPath: "/run/secrets", optional: true);
        return builder;
    }
}