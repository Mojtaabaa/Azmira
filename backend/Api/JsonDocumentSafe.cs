using System.Text.Json;

namespace KonkoorHelp.Api.Api;

public static class JsonDocumentSafe
{
    public static async Task<JsonDocument?> Read(HttpContext http, CancellationToken cancellationToken)
    {
        if (http.Request.ContentLength is 0) return null;
        try
        {
            return await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            throw new BadHttpRequestException("درخواست درست نیست.", 400);
        }
    }
}
