/*
 * Purpose: Keep native decision transport telemetry free of request content.
 * Status and bounded authentication-error metadata are recorded, never bearer tokens
 * or ticket text. Authentication and payload handling stay in the SDK.
 */
using System.Text;
using System.Text.Json;

namespace SupportRouting;

public sealed class DecisionTransport(ILogger logger) : DelegatingHandler(
    new SocketsHttpHandler { AllowAutoRedirect = false })
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        logger.LogInformation("Native decision HTTP {StatusCode}", (int)response.StatusCode);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            logger.LogWarning("Native decision authentication challenge: {Challenge}",
                string.Join(", ", response.Headers.WwwAuthenticate));
            if (response.Content.Headers.ContentLength is > 0 and <= 4096)
            {
                try
                {
                    using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                    if (error.RootElement.TryGetProperty("error", out var details))
                    {
                        // Emit only known provider diagnostics, never an arbitrary response body.
                        if (details.TryGetProperty("code", out var code) && code.GetString() == "PermissionDenied")
                            logger.LogWarning("Native decision error code: PermissionDenied");
                        if (details.TryGetProperty("message", out var message)
                            && message.GetString()?.Contains("Principal does not have access to API/Operation", StringComparison.Ordinal) == true)
                            logger.LogWarning("Native decision error message: Principal does not have access to API/Operation");
                    }
                }
                catch (JsonException)
                {
                    logger.LogWarning("Native authentication error body was not JSON");
                }
            }
            // Decode only diagnostic audience/expiry claims, never log the token itself.
            if (request.Headers.Authorization?.Parameter?.Split('.') is [_, var payload, _])
            {
                try
                {
                    payload = payload.Replace('-', '+').Replace('_', '/');
                    payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                    using var claims = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
                    logger.LogWarning("Unvalidated bearer metadata: audience={Audience}, expires={Expires}",
                        claims.RootElement.GetProperty("aud").ToString(),
                        claims.RootElement.GetProperty("exp").GetInt64());
                }
                catch (Exception ex) when (ex is FormatException or JsonException or KeyNotFoundException or InvalidOperationException)
                {
                    logger.LogWarning("Bearer diagnostic metadata unavailable: {ErrorType}", ex.GetType().Name);
                }
            }
        }
        return response;
    }
}
