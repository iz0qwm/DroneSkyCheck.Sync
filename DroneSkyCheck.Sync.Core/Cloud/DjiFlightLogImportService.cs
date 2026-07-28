using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DroneSkyCheck.Sync.Core.Cloud;

public sealed class DjiFlightLogImportService : IFlightLogImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly DjiFlightLogImportOptions _options;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _requestThrottle = new(1, 1);
    private DateTimeOffset _lastCloudRequestStartedAt = DateTimeOffset.MinValue;

    public DjiFlightLogImportService()
        : this(new DjiFlightLogImportOptions())
    {
    }

    public DjiFlightLogImportService(
        DjiFlightLogImportOptions options)
        : this(options, new HttpClient())
    {
    }

    public DjiFlightLogImportService(
        DjiFlightLogImportOptions options,
        HttpClient httpClient)
    {
        _options = options;
        _httpClient = httpClient;
    }

    public async Task<DjiFlightLogImportCheckResult> CheckDjiFlightLogImportAsync(
        string idToken,
        string operatorId,
        string ownerUid,
        string sha256,
        long sizeBytes,
        string filename,
        CancellationToken cancellationToken = default)
    {
        using var response =
            await SendWithRateLimitRetryAsync(
                CreateRequest,
                cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(cancellationToken);

        EnsureSuccessStatusCode(
            response,
            responseBody,
            "Controllo duplicati Flight Log non riuscito");

        var checkResponse =
            JsonSerializer.Deserialize<CheckDjiFlightLogImportResponse>(
                responseBody,
                JsonOptions);

        return new DjiFlightLogImportCheckResult(
            checkResponse?.AlreadyImported == true);

        HttpRequestMessage CreateRequest()
        {
            var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    _options.CheckDjiFlightLogImportUrl);

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", idToken);

            request.Headers.Add("X-DSC-Operator-ID", operatorId);
            request.Headers.Add("X-DSC-Owner-UID", ownerUid);

            request.Content = JsonContent.Create(
                new CheckDjiFlightLogImportRequest(sha256),
                options: JsonOptions);

            return request;
        }
    }

    public async Task<DjiFlightLogImportResult> ImportDjiFlightLogAsync(
        string idToken,
        string operatorId,
        string ownerUid,
        string sha256,
        byte[] content,
        string filename,
        CancellationToken cancellationToken = default)
    {
        using var response =
            await SendWithRateLimitRetryAsync(
                CreateRequest,
                cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(cancellationToken);

        EnsureSuccessStatusCode(
            response,
            responseBody,
            "Import Flight Log non riuscito");

        var importResponse =
            JsonSerializer.Deserialize<ImportDjiFlightLogResponse>(
                responseBody,
                JsonOptions);

        var importGateway =
            importResponse?.ImportGateway;

        var reconciliation =
            importGateway?.Reconciliation;

        var reconciliationStatus =
            reconciliation?.Status ?? string.Empty;

        var reconciliationAction =
            reconciliation?.Action ?? string.Empty;

        var reconciliationIssue =
            reconciliation?.GetIssue() ?? string.Empty;

        var requiresReview =
            IsReconciliationIssue(reconciliationStatus) ||
            IsReconciliationIssue(reconciliationIssue);

        return new DjiFlightLogImportResult(
            importGateway?.Duplicate == true,
            importGateway?.PersistedFleetRegistry == true,
            requiresReview,
            reconciliationStatus,
            reconciliationAction,
            reconciliationIssue);

        HttpRequestMessage CreateRequest()
        {
            var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    _options.ImportDjiFlightLogUrl);

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", idToken);

            request.Headers.Add("X-DSC-Operator-ID", operatorId);
            request.Headers.Add("X-DSC-Owner-UID", ownerUid);
            request.Headers.Add("X-DSC-Log-SHA256", sha256);

            var multipartContent =
                new MultipartFormDataContent();

            var fileContent =
                new ByteArrayContent(content);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue("text/plain");

            multipartContent.Add(
                fileContent,
                "logfile",
                filename);

            request.Content = multipartContent;

            return request;
        }
    }

    private async Task<HttpResponseMessage> SendWithRateLimitRetryAsync(
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            await WaitForRequestSlotAsync(cancellationToken);

            using var request =
                createRequest();

            var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            if (!IsRateLimitResponse(response) ||
                attempt >= _options.MaxRateLimitRetries)
            {
                return response;
            }

            var retryDelay =
                GetRateLimitRetryDelay(response, attempt);

            response.Dispose();

            await Task.Delay(
                retryDelay,
                cancellationToken);
        }
    }

    private async Task WaitForRequestSlotAsync(
        CancellationToken cancellationToken)
    {
        await _requestThrottle.WaitAsync(cancellationToken);

        try
        {
            var elapsed =
                DateTimeOffset.UtcNow - _lastCloudRequestStartedAt;

            var delay =
                _options.MinimumDelayBetweenCloudRequests - elapsed;

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(
                    delay,
                    cancellationToken);
            }

            _lastCloudRequestStartedAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            _requestThrottle.Release();
        }
    }

    private static bool IsRateLimitResponse(
        HttpResponseMessage response)
    {
        return (int)response.StatusCode == 429;
    }

    private static bool IsReconciliationIssue(
        string value)
    {
        return value is
            "missing_aircraft_serial" or
            "aircraft_serial_model_mismatch" or
            "multiple_manual_candidates";
    }

    private TimeSpan GetRateLimitRetryDelay(
        HttpResponseMessage response,
        int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta &&
            delta > TimeSpan.Zero)
        {
            return LimitRetryDelay(delta);
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;

            if (delay > TimeSpan.Zero)
                return LimitRetryDelay(delay);
        }

        var multiplier =
            Math.Pow(2, attempt);

        return LimitRetryDelay(
            TimeSpan.FromMilliseconds(
                _options.RateLimitRetryBaseDelay.TotalMilliseconds *
                multiplier));
    }

    private TimeSpan LimitRetryDelay(
        TimeSpan delay)
    {
        return delay <= _options.RateLimitRetryMaxDelay
            ? delay
            : _options.RateLimitRetryMaxDelay;
    }

    private static void EnsureSuccessStatusCode(
        HttpResponseMessage response,
        string responseBody,
        string message)
    {
        if (response.IsSuccessStatusCode)
            return;

        var detail = string.IsNullOrWhiteSpace(responseBody)
            ? response.ReasonPhrase
            : responseBody;

        throw new HttpRequestException(
            $"{message}: {(int)response.StatusCode} {detail}");
    }

    private sealed record CheckDjiFlightLogImportRequest(
        string Sha256);

    private sealed class CheckDjiFlightLogImportResponse
    {
        [JsonPropertyName("alreadyImported")]
        public bool? AlreadyImported { get; init; }
    }

    private sealed class ImportDjiFlightLogResponse
    {
        [JsonPropertyName("importGateway")]
        public ImportGatewayResponse? ImportGateway { get; init; }
    }

    private sealed class ImportGatewayResponse
    {
        [JsonPropertyName("duplicate")]
        public bool? Duplicate { get; init; }

        [JsonPropertyName("persistedFleetRegistry")]
        public bool? PersistedFleetRegistry { get; init; }

        [JsonPropertyName("reconciliation")]
        public ReconciliationResponse? Reconciliation { get; init; }
    }

    private sealed class ReconciliationResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; init; } = string.Empty;

        [JsonPropertyName("conflict")]
        public string Conflict { get; init; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; init; } = string.Empty;

        [JsonPropertyName("errorCode")]
        public string ErrorCode { get; init; } = string.Empty;

        public string GetIssue()
        {
            if (!string.IsNullOrWhiteSpace(Conflict))
                return Conflict;

            if (!string.IsNullOrWhiteSpace(Reason))
                return Reason;

            if (!string.IsNullOrWhiteSpace(Code))
                return Code;

            return ErrorCode;
        }
    }
}
