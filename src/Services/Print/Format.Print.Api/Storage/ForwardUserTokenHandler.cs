using Microsoft.Net.Http.Headers;

namespace Format.Print.Api.Storage;

/// <summary>Прикладывает к исходящему запросу токен пользователя из текущего входящего запроса.</summary>
public sealed class ForwardUserTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var authorization = httpContextAccessor.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString();

        if (!string.IsNullOrEmpty(authorization))
            request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, authorization);

        return base.SendAsync(request, ct);
    }
}