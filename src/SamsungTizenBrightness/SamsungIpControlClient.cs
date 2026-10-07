using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SamsungTizenBrightness;

internal sealed record IpControlAuthorization(string Host, string Token, string CertificateSha256);

internal sealed class IpControlException : IOException
{
    public int Code { get; }
    public IpControlException(int code) : base(L.T("IpControlRpcFailed", code)) => Code = code;
}

/// <summary>Samsung's HTTPS JSON-RPC backlight channel; no TV app or inbound listener.</summary>
internal sealed class SamsungIpControlClient : IDisposable
{
    private readonly string _host;
    private readonly Uri _endpoint;
    private readonly HttpClient _http;
    private readonly object _certificateGate = new();
    private string? _token;
    private string? _certificateHash;
    private string? _pairCertificateHash;
    private bool _pairing;
    private long _requestId;

    public SamsungIpControlClient(
        string host, IpControlAuthorization? authorization,
        HttpMessageHandler? handler = null)
    {
        _host = host;
        _endpoint = new UriBuilder(Uri.UriSchemeHttps, host, 1516).Uri;
        if (authorization is not null)
        {
            if (!authorization.Host.Equals(host, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Authorization belongs to a different display.");
            _token = authorization.Token;
            _certificateHash = authorization.CertificateSha256;
        }
        handler ??= new HttpClientHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = ValidateCertificate
        };
        _http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public bool HasAuthorization =>
        !string.IsNullOrWhiteSpace(_token) && !string.IsNullOrWhiteSpace(_certificateHash);

    private bool ValidateCertificate(
        HttpRequestMessage request, System.Security.Cryptography.X509Certificates.X509Certificate2? certificate,
        System.Security.Cryptography.X509Certificates.X509Chain? chain, SslPolicyErrors errors)
    {
        if (certificate is null || request.RequestUri?.Host != _endpoint.Host)
            return false;
        string hash = Convert.ToHexString(SHA256.HashData(certificate.RawData));
        lock (_certificateGate)
        {
            // Self-signed TV certificates are trusted only after explicit on-TV pairing.
            // Pin the certificate seen during that exchange, including across restarts.
            if (_pairing)
            {
                if (_pairCertificateHash is not null && _pairCertificateHash != hash)
                    return false;
                _pairCertificateHash = hash;
                return true;
            }
            return string.Equals(_certificateHash, hash, StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task<IpControlAuthorization> PairAsync(CancellationToken cancellationToken)
    {
        lock (_certificateGate)
        {
            _pairing = true;
            _pairCertificateHash = null;
        }
        try
        {
            JsonElement result = await CallAsync(
                "createAccessToken", null, TimeSpan.FromSeconds(45), cancellationToken, pair: true);
            string token = result.GetProperty("AccessToken").GetString() ?? string.Empty;
            string? certificate;
            lock (_certificateGate) certificate = _pairCertificateHash ?? _certificateHash;
            if (string.IsNullOrWhiteSpace(token) || certificate is null)
                throw new InvalidDataException(L.T("IpControlInvalidReply"));
            _token = token;
            _certificateHash = certificate;
            return new IpControlAuthorization(_host, token, certificate);
        }
        finally
        {
            lock (_certificateGate) _pairing = false;
        }
    }

    public async Task<int> GetBacklightAsync(CancellationToken cancellationToken = default)
    {
        JsonElement result = await CallAsync("backlightControl", null,
            TimeSpan.FromSeconds(3), cancellationToken);
        return ReadBacklight(result);
    }

    public async Task<int> SetBacklightAsync(int value, CancellationToken cancellationToken = default)
    {
        if (value is < 0 or > 50)
            throw new ArgumentOutOfRangeException(nameof(value));
        JsonElement result = await CallAsync("backlightControl",
            new Dictionary<string, object> { ["backlight"] = value },
            TimeSpan.FromSeconds(3), cancellationToken);
        int actual = ReadBacklight(result);
        if (actual != value)
            throw new InvalidDataException(L.T("IpControlValueMismatch", value, actual));
        return actual;
    }

    public async Task<string> GetPowerAsync(CancellationToken cancellationToken)
    {
        JsonElement result = await CallAsync("powerControl", null,
            TimeSpan.FromSeconds(3), cancellationToken);
        return result.GetProperty("power").GetString() ?? string.Empty;
    }

    private async Task<JsonElement> CallAsync(
        string method, Dictionary<string, object>? parameters,
        TimeSpan timeout, CancellationToken cancellationToken, bool pair = false)
    {
        if (!pair && !HasAuthorization)
            throw new InvalidOperationException(L.T("IpControlPairRequired"));
        var envelope = new Dictionary<string, object>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Interlocked.Increment(ref _requestId).ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["method"] = method
        };
        if (!pair)
        {
            parameters = parameters is null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(parameters);
            parameters["AccessToken"] = _token!;
            envelope["params"] = parameters;
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = new StringContent(JsonSerializer.Serialize(envelope), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.ParseAdd("application/json");
        // StringContent sets Content-Length; Samsung does not accept chunked requests.
        using HttpResponseMessage response = await _http.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using Stream body = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(body,
            cancellationToken: deadline.Token).ConfigureAwait(false);
        JsonElement root = document.RootElement;
        JsonElement error = root.TryGetProperty("error", out JsonElement nested) ? nested : root;
        if (error.TryGetProperty("code", out JsonElement code) && code.TryGetInt32(out int number))
        {
            if (number == -32010)
                throw new UnauthorizedAccessException(L.T("IpControlPairRequired"));
            throw new IpControlException(number);
        }
        if (!root.TryGetProperty("result", out JsonElement result))
            throw new InvalidDataException(L.T("IpControlInvalidReply"));
        return result.Clone();
    }

    private static int ReadBacklight(JsonElement result)
    {
        if (!result.TryGetProperty("backlight", out JsonElement field))
            throw new InvalidDataException(L.T("IpControlInvalidReply"));
        int value;
        bool valid;
        if (field.ValueKind == JsonValueKind.Number)
            valid = field.TryGetInt32(out value);
        else if (field.ValueKind == JsonValueKind.String)
            valid = int.TryParse(field.GetString(), out value);
        else
            throw new InvalidDataException(L.T("IpControlInvalidReply"));
        if (!valid || value is < 0 or > 50)
            throw new InvalidDataException(L.T("IpControlInvalidReply"));
        return value;
    }

    public void Dispose() => _http.Dispose();
}
