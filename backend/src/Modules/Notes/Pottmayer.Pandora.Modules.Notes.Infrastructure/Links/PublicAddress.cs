using System.Net;
using System.Net.Sockets;

namespace Pottmayer.Pandora.Modules.Notes.Infrastructure.Links;

/// <summary>
/// Whether an address is on the public internet. A link preview connects only to those, so a pasted
/// link can never make the server reach its own network (SSRF): loopback, private ranges, link-local
/// (cloud metadata lives at 169.254.169.254), carrier NAT, multicast and the reserved blocks are out.
/// </summary>
internal static class PublicAddress
{
    public static bool Is(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0                                   // "this" network
                || b[0] == 10                                     // private
                || b[0] == 127                                    // loopback
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)     // carrier-grade NAT
                || (b[0] == 169 && b[1] == 254)                   // link-local, cloud metadata
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)      // private
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)        // IETF protocol assignments
                || (b[0] == 192 && b[1] == 0 && b[2] == 2)        // documentation
                || (b[0] == 192 && b[1] == 168)                   // private
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))    // benchmarking
                || (b[0] == 198 && b[1] == 51 && b[2] == 100)     // documentation
                || (b[0] == 203 && b[1] == 0 && b[2] == 113)      // documentation
                || b[0] >= 224);                                  // multicast and reserved
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(IPAddress.IsLoopback(address)
                || address.Equals(IPAddress.IPv6None)
                || address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6Multicast
                || address.IsIPv6UniqueLocal
                || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8)    // documentation
                || (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b)    // NAT64, maps to IPv4
                || (b[0] == 0x20 && b[1] == 0x02));                                  // 6to4, maps to IPv4
        }

        return false;
    }
}
