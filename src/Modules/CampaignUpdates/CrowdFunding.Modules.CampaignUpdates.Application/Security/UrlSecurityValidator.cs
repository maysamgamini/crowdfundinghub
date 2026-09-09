using System.Net;
using System.Net.Sockets;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Security;

/// <summary>
/// Rejects webhook target URLs that could be used to probe private cloud infrastructure (SSRF) —
/// cloud metadata endpoints, RFC 1918 private ranges, loopback, and link-local addresses. Applied
/// once, at subscription registration: a URL a creator could not have registered can never be
/// dispatched to later, so the dispatcher itself doesn't need to repeat this check on every send.
/// See TICKET-035.
/// </summary>
public static class UrlSecurityValidator
{
    public static bool IsSafeExternalUrl(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        IPAddress[] addresses;
        try
        {
            addresses = Dns.GetHostAddresses(uri.DnsSafeHost);
        }
        catch (SocketException)
        {
            // An unresolvable host is not a safe target either — fail closed.
            return false;
        }

        return addresses.Length > 0 && addresses.All(address => !IsPrivateOrLinkLocal(address));
    }

    /// <summary>
    /// Whether <paramref name="address"/> falls in a loopback, RFC 1918 private, or link-local
    /// (including the cloud metadata range) block. Exposed publicly so the dispatcher's
    /// connect-time check — <c>WebhookDispatcherHttpHandlerFactory</c> — can re-run the exact
    /// same test against the IP actually resolved at TCP-connect time, not just the IP resolved
    /// once at registration time (DNS rebinding — TICKET-050).
    /// </summary>
    public static bool IsPrivateOrLinkLocal(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();

            return bytes[0] switch
            {
                10 => true, // 10.0.0.0/8
                127 => true, // 127.0.0.0/8
                169 when bytes[1] == 254 => true, // 169.254.0.0/16 (link-local / cloud metadata)
                172 when bytes[1] is >= 16 and <= 31 => true, // 172.16.0.0/12
                192 when bytes[1] == 168 => true, // 192.168.0.0/16
                0 => true, // 0.0.0.0/8
                _ => false,
            };
        }

        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || IPAddress.IPv6Loopback.Equals(address);
    }
}
