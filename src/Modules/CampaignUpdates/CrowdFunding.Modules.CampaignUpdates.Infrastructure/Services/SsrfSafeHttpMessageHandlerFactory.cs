using System.Net;
using System.Net.Sockets;
using CrowdFunding.Modules.CampaignUpdates.Application.Security;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Services;

/// <summary>
/// Builds the <see cref="SocketsHttpHandler"/> used for every outbound webhook delivery — the
/// dispatch-time half of SSRF defense that <see cref="UrlSecurityValidator"/> alone cannot
/// provide (TICKET-050):
/// <list type="number">
/// <item><description><see cref="SocketsHttpHandler.AllowAutoRedirect"/> is disabled, so a
/// malicious target cannot respond <c>302 Found</c> with a <c>Location</c> pointing at
/// <c>169.254.169.254</c> (cloud instance metadata) or an internal service and have
/// <see cref="HttpClient"/> follow it automatically.</description></item>
/// <item><description><see cref="SocketsHttpHandler.ConnectCallback"/> re-resolves DNS and
/// re-checks the resolved IP against the same private/link-local block list
/// <see cref="UrlSecurityValidator"/> uses, immediately before the TCP handshake — closing the
/// DNS-rebinding TOCTOU window where a hostname that resolved to a public IP at registration
/// time is repointed at <c>127.0.0.1</c> or an internal service by the time the dispatcher
/// actually sends to it.</description></item>
/// </list>
/// </summary>
public static class SsrfSafeHttpMessageHandlerFactory
{
    /// <param name="allowPrivateNetworkTargets">Must stay <see langword="false"/> in every real
    /// deployment — this exists solely so integration tests can point a webhook subscription at
    /// an in-process loopback receiver to exercise the dispatcher's signing/delivery-recording
    /// logic without an actual public endpoint. Never wire this to anything but a hardcoded
    /// <see langword="false"/> outside of test host configuration; it is intentionally not read
    /// from any setting an operator could accidentally leave enabled (see
    /// <c>CampaignUpdatesInfrastructureDependencyInjection</c> for where the real value comes
    /// from). Auto-redirect stays disabled regardless.</param>
    public static SocketsHttpHandler Create(bool allowPrivateNetworkTargets = false) => new()
    {
        AllowAutoRedirect = false,
        ConnectCallback = (context, cancellationToken) => ConnectAsync(context, allowPrivateNetworkTargets, cancellationToken),
    };

    private static async ValueTask<System.IO.Stream> ConnectAsync(
        SocketsHttpConnectionContext context, bool allowPrivateNetworkTargets, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;

        try
        {
            addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new HttpRequestException(
                $"Unable to resolve webhook target host '{context.DnsEndPoint.Host}'.", exception);
        }

        var safeAddress = allowPrivateNetworkTargets
            ? addresses.FirstOrDefault()
            : addresses.FirstOrDefault(address => !UrlSecurityValidator.IsPrivateOrLinkLocal(address));

        if (safeAddress is null)
        {
            // Fails closed: no resolved address, or every resolved address is
            // private/loopback/link-local (including a DNS-rebound hostname that passed
            // registration-time validation but now points somewhere internal).
            throw new HttpRequestException(
                $"Refusing to connect to webhook target host '{context.DnsEndPoint.Host}' — it resolves to a private, loopback, or link-local address.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        try
        {
            await socket.ConnectAsync(new IPEndPoint(safeAddress, context.DnsEndPoint.Port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
