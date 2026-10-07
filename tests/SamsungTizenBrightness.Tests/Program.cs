// SPDX-FileCopyrightText: 2026 dttutty
// SPDX-License-Identifier: GPL-3.0-only

using System.Net;
using System.Text;
using System.Text.Json;
using SamsungTizenBrightness;

if (args.Length == 2 && args[0] == "--ui-preview")
{
    UiRegression.CheckSetupLayout(args[1]);
    Console.WriteLine("Four-language setup layout verified and rendered offline.");
    return;
}

// Private child-process fixtures exercise real redirected pipes without touching
// the display, registry, startup task or installed tray process.
if (args.Length == 2 && args[0] == "--process-fixture")
{
    if (args[1] == "sleep") await Task.Delay(10_000);
    else
    {
        Console.Out.Write(new string('O', 256 * 1024));
        Console.Error.Write(new string('E', 256 * 1024));
        Environment.ExitCode = 7;
    }
    return;
}

if (args.Length == 2 && (args[0] == "--live" || args[0] == "--live-readonly"))
{
    using var doc = JsonDocument.Parse(File.ReadAllText(args[1]));
    var auth = doc.RootElement;
    string token = Encoding.UTF8.GetString(System.Security.Cryptography.ProtectedData.Unprotect(
        Convert.FromBase64String(auth.GetProperty("ProtectedToken").GetString()!),
        Encoding.UTF8.GetBytes("Samsung-IP-Control-v1"),
        System.Security.Cryptography.DataProtectionScope.CurrentUser));
    using var direct = new SamsungIpControlClient(auth.GetProperty("Host").GetString()!,
        new IpControlAuthorization(auth.GetProperty("Host").GetString()!, token,
            auth.GetProperty("CertificateSha256").GetString()!));
    int original = await direct.GetBacklightAsync();
    if (args[0] == "--live-readonly")
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Backlight = original,
            Power = await direct.GetPowerAsync(CancellationToken.None),
            ReadOnly = true,
            Verified = original is >= 0 and <= 50
        }));
        return;
    }
    int target = original == 50 ? 49 : original + 1;
    int written = original, readback = original, restored = original;
    try
    {
        written = await direct.SetBacklightAsync(target);
        readback = await direct.GetBacklightAsync();
        await Task.Delay(1000);
    }
    finally
    {
        await direct.SetBacklightAsync(original);
        restored = await direct.GetBacklightAsync();
    }
    string power = await direct.GetPowerAsync(CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Original = original, Target = target, Written = written, Readback = readback,
        Restored = restored, Power = power,
        Verified = written == target && readback == target && restored == original
    }));
    return;
}

int passed = 0;
async Task Test(string name, Func<Task> run)
{
    await run();
    passed++;
    Console.WriteLine($"PASS {name}");
}
void Check(bool value) { if (!value) throw new Exception("Assertion failed."); }
async Task Throws<T>(Func<Task> run) where T : Exception
{
    try { await run(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}
IpControlAuthorization Auth(string host = "192.168.1.1") => new(host, "test-token", new string('A', 64));
SamsungIpControlClient Client(FakeHandler handler, IpControlAuthorization? auth = null) =>
    new("192.168.1.1", auth ?? Auth(), handler);

await Test("get numeric backlight; HTTPS/1516 and token envelope", async () =>
{
    using var handler = new FakeHandler(request =>
    {
        Check(request.RequestUri!.Scheme == "https" && request.RequestUri.Port == 1516);
        Check(request.Version == HttpVersion.Version11);
        Check(request.Headers.Accept.Any(h => h.MediaType == "application/json"));
        using var json = JsonDocument.Parse(handlerBody(request));
        var root = json.RootElement;
        Check(root.GetProperty("method").GetString() == "backlightControl");
        Check(root.GetProperty("params").EnumerateObject().Count() == 1);
        Check(root.GetProperty("params").GetProperty("AccessToken").GetString() == "test-token");
        return "{\"result\":{\"backlight\":27}}";
    });
    using var client = Client(handler);
    Check(await client.GetBacklightAsync() == 27);
});
await Test("string numeric backlight", async () =>
{
    using var client = Client(new FakeHandler(_ => "{\"result\":{\"backlight\":\"20\"}}"));
    Check(await client.GetBacklightAsync() == 20);
});
await Test("absolute backlight write; not black-level brightnessControl", async () =>
{
    using var handler = new FakeHandler(request =>
    {
        using var json = JsonDocument.Parse(handlerBody(request));
        Check(json.RootElement.GetProperty("method").GetString() == "backlightControl");
        Check(json.RootElement.GetProperty("params").GetProperty("backlight").GetInt32() == 34);
        return "{\"result\":{\"backlight\":34}}";
    });
    using var client = Client(handler);
    Check(await client.SetBacklightAsync(34) == 34);
});
await Test("missing auth does not request permission or send network traffic", async () =>
{
    using var handler = new FakeHandler(_ => throw new Exception("Unexpected network call."));
    using var client = new SamsungIpControlClient("192.168.1.1", null, handler);
    await Throws<InvalidOperationException>(() => client.GetBacklightAsync());
    Check(handler.Calls == 0);
});
await Test("authorization bound to display host", () =>
{
    try { using var client = new SamsungIpControlClient("192.168.1.2", Auth(), new FakeHandler(_ => "{}")); }
    catch (ArgumentException) { return Task.CompletedTask; }
    throw new Exception("Cross-host authorization accepted.");
});
await Test("rejected token does not automatically re-pair", async () =>
{
    using var handler = new FakeHandler(_ => "{\"error\":{\"code\":-32010}}");
    using var client = Client(handler);
    await Throws<UnauthorizedAccessException>(() => client.GetBacklightAsync());
    Check(handler.Calls == 1);
});
await Test("unsupported method fails safely", async () =>
{
    using var client = Client(new FakeHandler(_ => "{\"error\":{\"code\":-32601}}"));
    await Throws<IpControlException>(() => client.GetBacklightAsync());
});
await Test("flat parse error handled", async () =>
{
    using var client = Client(new FakeHandler(_ => "{\"code\":-32700,\"message\":\"Parse error\"}"));
    await Throws<IpControlException>(() => client.GetBacklightAsync());
});
await Test("invalid backlight response rejected", async () =>
{
    foreach (string field in new[] { "51", "-1", "true", "\"bad\"" })
    {
        using var client = Client(new FakeHandler(_ => "{\"result\":{\"backlight\":" + field + "}}"));
        await Throws<InvalidDataException>(() => client.GetBacklightAsync());
    }
});
await Test("write outside range never sent", async () =>
{
    using var handler = new FakeHandler(_ => throw new Exception("Unexpected write."));
    using var client = Client(handler);
    await Throws<ArgumentOutOfRangeException>(() => client.SetBacklightAsync(-1));
    await Throws<ArgumentOutOfRangeException>(() => client.SetBacklightAsync(51));
    Check(handler.Calls == 0);
});
await Test("mismatched write response rejected", async () =>
{
    using var client = Client(new FakeHandler(_ => "{\"result\":{\"backlight\":20}}"));
    await Throws<InvalidDataException>(() => client.SetBacklightAsync(34));
});
await Test("network requests honor cancellation", async () =>
{
    using var client = Client(new FakeHandler(async (_, ct) =>
    {
        await Task.Delay(Timeout.Infinite, ct);
        return "{}";
    }));
    using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
    await Throws<OperationCanceledException>(() => client.GetBacklightAsync(deadline.Token));
});
await Test("read power state", async () =>
{
    using var client = Client(new FakeHandler(_ => "{\"result\":{\"power\":\"powerOn\"}}"));
    Check(await client.GetPowerAsync(CancellationToken.None) == "powerOn");
});
await Test("power-changing APIs, countdown and wake detection removed", () =>
{
    var assembly = typeof(SamsungIpControlClient).Assembly;
    var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
    Check(typeof(SamsungIpControlClient).GetMethod("SetPowerAsync", flags) is null);
    Check(typeof(SamsungBrightnessSession).GetMethods(flags).All(method =>
        !method.Name.Contains("Wake") && !method.Name.StartsWith("SendPower")));
    Check(assembly.GetType("SamsungTizenBrightness.PowerCountdownPopup") is null);
    Check(assembly.GetType("SamsungTizenBrightness.UsbWakePresence") is null);
    Check(assembly.GetType("SamsungTizenBrightness.BrightnessBridgeServer") is null);
    return Task.CompletedTask;
});
System.Diagnostics.ProcessStartInfo Fixture(string mode)
{
    var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true
    };
    start.ArgumentList.Add("--process-fixture");
    start.ArgumentList.Add(mode);
    return start;
}
await Test("process deadline includes pipe reads; sleeping child is stopped", async () =>
{
    var watch = System.Diagnostics.Stopwatch.StartNew();
    await Throws<IOException>(() => BoundedProcess.RunAsync(Fixture("sleep"), TimeSpan.FromMilliseconds(500)));
    Check(watch.Elapsed < TimeSpan.FromSeconds(4));
});
await Test("stdout and stderr are drained concurrently; exit code preserved", async () =>
{
    ProcessResult result = await BoundedProcess.RunAsync(Fixture("output"), TimeSpan.FromSeconds(10));
    Check(result.ExitCode == 7 && result.StandardOutput == new string('O', 256 * 1024) &&
        result.StandardError == new string('E', 256 * 1024));
});

string TaskXml(string path, bool settingsEnabled = true, bool triggerEnabled = true,
    string arguments = "--startup --host &quot;192.0.2.1&quot;") => $"""
    <Task xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
      <Triggers><LogonTrigger><Enabled>{triggerEnabled.ToString().ToLowerInvariant()}</Enabled></LogonTrigger></Triggers>
      <Settings><Enabled>{settingsEnabled.ToString().ToLowerInvariant()}</Enabled></Settings>
      <Actions><Exec><Command>{path}</Command><Arguments>{arguments}</Arguments></Exec></Actions>
    </Task>
    """;
const string oldPath = @"C:\Portable\Samsung.Tizen.Brightness.exe";
const string newPath = @"C:\Installed\Samsung.Tizen.Brightness.exe";
await Test("enabled task follows portable-to-installed path; host backup retained", () =>
{
    StartupTaskState state = StartupPolicy.ReadTask(TaskXml(oldPath))!;
    Check(StartupPolicy.ShouldMigrate(state, false, newPath));
    Check(state.Arguments == "--startup --host \"192.0.2.1\"");
    Check(!StartupPolicy.ShouldMigrate(state, false, oldPath));
    StartupTaskState retargeted = StartupPolicy.ReadTask(StartupPolicy.RetargetTask(TaskXml(oldPath), newPath))!;
    Check(retargeted.Command == newPath && retargeted.Arguments == state.Arguments && retargeted.Enabled);
    return Task.CompletedTask;
});
await Test("disabled task or disabled logon trigger is never re-enabled during migration", () =>
{
    foreach (var state in new[] {
        StartupPolicy.ReadTask(TaskXml(oldPath, settingsEnabled: false)),
        StartupPolicy.ReadTask(TaskXml(oldPath, triggerEnabled: false)) })
    {
        Check(state is { Enabled: false });
        Check(!StartupPolicy.ShouldMigrate(state, true, newPath));
    }
    return Task.CompletedTask;
});
await Test("legacy Run command recognized after path change; unrelated commands rejected", () =>
{
    Check(StartupPolicy.IsLegacyCommand($"\"{oldPath}\" --startup"));
    Check(StartupPolicy.IsLegacyCommand(@"""C:\Old\Samsung Tizen 亮度.exe"" --startup"));
    Check(!StartupPolicy.IsLegacyCommand(@"""C:\Old\Unrelated.exe"" --startup"));
    Check(!StartupPolicy.IsLegacyCommand($"\"{oldPath}\" --startup-malicious"));
    Check(StartupPolicy.ShouldMigrate(null, true, newPath));
    Check(!StartupPolicy.ShouldMigrate(null, false, newPath));
    return Task.CompletedTask;
});
await Test("unrelated or malformed scheduled tasks are not adopted", () =>
{
    Check(StartupPolicy.ReadTask(TaskXml(@"C:\Other.exe")) is null);
    Check(StartupPolicy.ReadTask(TaskXml(oldPath, arguments: "--open")) is null);
    Check(StartupPolicy.ReadTask("not XML") is null);
    return Task.CompletedTask;
});
await Test("connection errors distinguish authorization, timeout, unsupported and invalid response", () =>
{
    Check(ConnectionFailure.Classify(new IpControlAuthorizationRequiredException()) == ConnectionFailureKind.Authorization);
    Check(ConnectionFailure.Classify(new UnauthorizedAccessException()) == ConnectionFailureKind.Authorization);
    Check(ConnectionFailure.Classify(new HttpRequestException("Forbidden", null, HttpStatusCode.Forbidden)) == ConnectionFailureKind.Authorization);
    Check(ConnectionFailure.Classify(new TaskCanceledException()) == ConnectionFailureKind.Network);
    Check(ConnectionFailure.Classify(new HttpRequestException()) == ConnectionFailureKind.Network);
    Check(ConnectionFailure.Classify(new IpControlException(-32601)) == ConnectionFailureKind.Unsupported);
    Check(ConnectionFailure.Classify(new IpControlException(-32700)) == ConnectionFailureKind.Remote);
    Check(ConnectionFailure.Classify(new InvalidDataException()) == ConnectionFailureKind.InvalidReply);
    Check(ConnectionFailure.Classify(null) == ConnectionFailureKind.Unavailable);
    return Task.CompletedTask;
});
await Test("four-language setup paths and error guidance are localized", () =>
{
    UiLanguage previous = L.Current;
    string[] connectionNames = ["连接", "Connection", "연결", "Conexión"];
    int index = 0;
    foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
    {
        L.Initialize(language.ToString());
        string guide = L.T("SetupIpRemoteBody");
        Check(guide.Contains(connectionNames[index++]) && guide.Contains("IP Remote") && guide.Contains("Enable"));
        foreach (ConnectionFailureKind kind in Enum.GetValues<ConnectionFailureKind>())
            Check(L.T("Failure" + kind) != "Failure" + kind);
        Check(ConnectionFailure.Guidance(new IpControlAuthorizationRequiredException()) == L.T("IpControlPairRequired"));
    }
    L.Initialize(previous.ToString());
    return Task.CompletedTask;
});
await Test("four-language setup labels fit; saved host and disabled startup are preserved", () =>
{
    UiRegression.CheckSetupLayout();
    return Task.CompletedTask;
});
await Test("Language entry remains English in every locale and has a transparent globe icon", () =>
{
    UiLanguage previous = L.Current;
    try
    {
        foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
        {
            L.Initialize(language.ToString());
            Check(L.T("Language") == "Language");
        }
        using var icon = IconVisuals.CreateGlobe();
        Check(icon.Width == 16 && icon.Height == 16 && icon.GetPixel(0, 0).A == 0);
        Check(icon.GetPixel(8, 8).A > 0);
    }
    finally { L.Initialize(previous.ToString()); }
    return Task.CompletedTask;
});
Console.WriteLine($"{passed} tests passed.");
string handlerBody(HttpRequestMessage request) => request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

sealed class FakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<string>> _response;
    public int Calls { get; private set; }
    public FakeHandler(Func<HttpRequestMessage, string> response) =>
        _response = (request, _) => Task.FromResult(response(request));
    public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<string>> response) => _response = response;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(await _response(request, cancellationToken), Encoding.UTF8, "application/json")
        };
    }
}
