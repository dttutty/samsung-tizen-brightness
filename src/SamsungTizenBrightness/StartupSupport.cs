// SPDX-FileCopyrightText: 2026 dttutty
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Xml.Linq;

namespace SamsungTizenBrightness;

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal static class BoundedProcess
{
    // Read both pipes concurrently. The deadline covers process exit AND pipe EOF
    // (a descendant can keep a pipe open even after its parent exits).
    internal static async Task<ProcessResult> RunAsync(ProcessStartInfo start, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        using Process process = Process.Start(start)
            ?? throw new IOException("Could not start the command.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        Task<string> error = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            await Task.WhenAll(output, error, process.WaitForExitAsync(deadline.Token))
                .WaitAsync(deadline.Token).ConfigureAwait(false);
            return new ProcessResult(process.ExitCode, await output.ConfigureAwait(false),
                await error.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
            throw new IOException("Windows command timed out.");
        }
    }
}

internal sealed record StartupTaskState(string Command, string Arguments, bool Enabled);

internal static class StartupPolicy
{
    internal static bool IsAppExecutable(string path) => Path.GetFileName(path) is string name &&
        new[] { "Samsung.Tizen.Brightness.exe", "Samsung Tizen 亮度.exe" }
            .Contains(name, StringComparer.OrdinalIgnoreCase);

    internal static bool IsStartupArguments(string arguments) =>
        arguments.Trim().Equals("--startup", StringComparison.OrdinalIgnoreCase) ||
        arguments.Trim().StartsWith("--startup ", StringComparison.OrdinalIgnoreCase);

    internal static bool IsLegacyCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        string trimmed = command.Trim();
        int end = trimmed.StartsWith('"') ? trimmed.IndexOf('"', 1) : trimmed.IndexOf(' ');
        if (end < 1) return false;
        string path = trimmed.StartsWith('"') ? trimmed[1..end] : trimmed[..end];
        return IsAppExecutable(path) && IsStartupArguments(trimmed[(end + 1)..]);
    }

    internal static StartupTaskState? ReadTask(string xml)
    {
        try
        {
            XElement? root = XDocument.Parse(xml).Root;
            if (root is null) return null;
            XNamespace ns = root.Name.Namespace;
            XElement? settings = root.Element(ns + "Settings");
            XElement? trigger = root.Element(ns + "Triggers")?.Element(ns + "LogonTrigger");
            XElement[] actions = root.Element(ns + "Actions")?.Elements().ToArray() ?? [];
            if (trigger is null || actions.Length != 1 || actions[0].Name != ns + "Exec") return null;
            string command = actions[0].Element(ns + "Command")?.Value ?? "";
            string arguments = actions[0].Element(ns + "Arguments")?.Value ?? "";
            if (!IsAppExecutable(command) || !IsStartupArguments(arguments)) return null;
            bool enabled = !string.Equals(settings?.Element(ns + "Enabled")?.Value,
                "false", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(trigger.Element(ns + "Enabled")?.Value,
                    "false", StringComparison.OrdinalIgnoreCase);
            return new StartupTaskState(command, arguments, enabled);
        }
        catch (System.Xml.XmlException) { return null; }
    }

    internal static bool ShouldMigrate(StartupTaskState? task, bool legacyEnabled, string currentPath) =>
        task is not null
            ? task.Enabled && !task.Command.Equals(currentPath, StringComparison.OrdinalIgnoreCase)
            : legacyEnabled;

    internal static string RetargetTask(string xml, string currentPath)
    {
        if (ReadTask(xml) is not { Enabled: true })
            throw new InvalidOperationException("Only an enabled app startup task may be retargeted.");
        XDocument document = XDocument.Parse(xml);
        XNamespace ns = document.Root!.Name.Namespace;
        document.Root.Element(ns + "Actions")!.Element(ns + "Exec")!
            .Element(ns + "Command")!.Value = currentPath;
        return document.ToString();
    }
}
