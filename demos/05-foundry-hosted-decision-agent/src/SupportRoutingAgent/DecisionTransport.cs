/*
 * Purpose: Keep native decision transport telemetry free of request content.
 * Status and bounded, unvalidated authentication metadata support private diagnosis.
 * Never record bearer tokens or ticket text. Authentication stays in the SDK.
 */
using System.Security.Cryptography;
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
            logger.LogWarning("Native decision authentication challenge present: {Present}",
                response.Headers.WwwAuthenticate.Count > 0);
            LogProviderRequestIds(response, logger);
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
            LogUnvalidatedBearerMetadata(request.Headers.Authorization?.Parameter, logger);
        }
        return response;
    }

    internal static void LogUnvalidatedBearerMetadata(string? token, ILogger logger)
    {
        // Comparison metadata is not token validation or authorization evidence.
        if (token is null || token.Length > 16384 || token.Split('.') is not [_, var payload, _]
            || payload.Length > 8192)
        {
            logger.LogWarning("Unvalidated bearer metadata unavailable");
            return;
        }

        try
        {
            payload = payload.Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var claims = JsonDocument.Parse(Convert.FromBase64String(payload));
            var root = claims.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                logger.LogWarning("Unvalidated bearer metadata unavailable");
                return;
            }

            var audience = root.TryGetProperty("aud", out var aud)
                && aud.ValueKind == JsonValueKind.String
                && aud.GetString() == "https://cognitiveservices.azure.com"
                    ? "https://cognitiveservices.azure.com" : "unrecognized";
            long? expiry = root.TryGetProperty("exp", out var exp)
                && exp.ValueKind == JsonValueKind.Number && exp.TryGetInt64(out var seconds)
                && seconds >= 0 ? seconds : null;
            var fingerprint = root.TryGetProperty("oid", out var oid)
                && oid.ValueKind == JsonValueKind.String
                ? FingerprintPrincipal(oid.GetString()) : null;
            logger.LogWarning(
                "Unvalidated bearer metadata: audience={Audience}, expires={Expires}, oidSha256={OidSha256}",
                audience, expiry, fingerprint ?? "unavailable");
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            logger.LogWarning("Unvalidated bearer metadata unavailable: {ErrorType}", ex.GetType().Name);
        }
    }

    internal static string? FingerprintPrincipal(string? value) =>
        value is { Length: 36 } && Guid.TryParseExact(value, "D", out var principal)
            && principal != Guid.Empty
                ? Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(principal.ToString("D"))))
                : null;

    private static void LogProviderRequestIds(HttpResponseMessage response, ILogger logger)
    {
        foreach (var header in new[] { "x-ms-request-id", "apim-request-id", "x-ms-correlation-request-id", "x-ms-correlation-id" })
        {
            if (!response.Headers.TryGetValues(header, out var values))
                continue;
            var ids = values.Take(2).ToArray();
            if (ids is [var value] && value.Length == 36 && Guid.TryParseExact(value, "D", out var id))
                logger.LogWarning("Native decision provider identifier: {Header}={Identifier}", header, id.ToString("D"));
        }
    }
}
