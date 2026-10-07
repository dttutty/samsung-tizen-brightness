// SPDX-FileCopyrightText: 2026 dttutty
// SPDX-License-Identifier: GPL-3.0-only

using System.Net;
using System.Net.Http;

namespace SamsungTizenBrightness;

internal sealed class IpControlAuthorizationRequiredException : InvalidOperationException
{
    internal IpControlAuthorizationRequiredException() : base(L.T("IpControlPairRequired")) { }
}

internal enum ConnectionFailureKind { Unavailable, Authorization, Network, Unsupported, Remote, InvalidReply }

internal static class ConnectionFailure
{
    internal static ConnectionFailureKind Classify(Exception? error) => error switch
    {
        IpControlAuthorizationRequiredException or UnauthorizedAccessException => ConnectionFailureKind.Authorization,
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => ConnectionFailureKind.Authorization,
        OperationCanceledException or TimeoutException or HttpRequestException => ConnectionFailureKind.Network,
        IpControlException { Code: -32601 } or NotSupportedException => ConnectionFailureKind.Unsupported,
        IpControlException => ConnectionFailureKind.Remote,
        InvalidDataException or System.Text.Json.JsonException => ConnectionFailureKind.InvalidReply,
        _ => ConnectionFailureKind.Unavailable
    };

    internal static string Summary(Exception? error) => L.T("Failure" + Classify(error));

    internal static string Guidance(Exception? error) => Classify(error) switch
    {
        ConnectionFailureKind.Authorization => L.T("IpControlPairRequired"),
        ConnectionFailureKind.Network => L.T("FailureNetworkHelp"),
        ConnectionFailureKind.Unsupported => L.T("FailureUnsupportedHelp"),
        _ => error?.Message ?? L.T("DisplayUnavailable")
    };
}
