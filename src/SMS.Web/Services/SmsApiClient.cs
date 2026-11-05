using System.Net.Http.Json;
using System.Text.Json;

namespace SMS.Web.Services;

public sealed record SmsCreateInput(string Content, string ContentType, string Sender, string Recipient, string Medium);

public sealed record SmsOutput(
    string ExternalId,
    string Content,
    string ContentType,
    string Sender,
    string Recipient,
    string Medium,
    string Status);

public sealed record SmsResult(bool Succeeded, SmsOutput? Sms, int StatusCode, string? ErrorMessage, string? TraceId);

public sealed class SmsApiClient
{
    private const string Route = "api/v1/sms";

    private readonly HttpClient _http;

    public SmsApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<SmsResult> CreateSms(SmsCreateInput input, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(Route, input, cancellationToken);

            return await ReadResult(response, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new SmsResult(false, null, 0, exception.Message, null);
        }
    }

    public async Task<SmsResult> GetSms(string externalId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Route}/{Uri.EscapeDataString(externalId)}", cancellationToken);

            return await ReadResult(response, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new SmsResult(false, null, 0, exception.Message, null);
        }
    }

    private static async Task<SmsResult> ReadResult(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var problem = await ReadProblem(response, cancellationToken);

            return new SmsResult(false, null, (int)response.StatusCode, problem.Message, problem.TraceId);
        }

        var sms = await response.Content.ReadFromJsonAsync<SmsOutput>(cancellationToken);

        return new SmsResult(true, sms, (int)response.StatusCode, null, null);
    }

    // The api answers failures with ProblemDetails, whose extensions — traceId among them — sit at the json root.
    private static async Task<(string Message, string? TraceId)> ReadProblem(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var fallback = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                return (fallback, null);
            }

            var root = document.RootElement;
            var message = Text(root, "detail")
                          ?? Text(root, "title")
                          ?? fallback;

            return (message, Text(root, "traceId"));
        }
        catch (JsonException)
        {
            return (fallback, null);
        }
    }

    private static string? Text(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
