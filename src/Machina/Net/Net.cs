using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Machina.Net;

public static class Net
{
    /// <summary>
    /// Returns true if input it is a valid IPv4 address.
    /// </summary>
    /// <param name="ipString"></param>
    /// <returns></returns>
    public static bool ValidateIPv4(string ipString)
    {
        return
            !string.IsNullOrWhiteSpace(ipString) &&
            IPAddress.TryParse(ipString, out var address) && address.AddressFamily == AddressFamily.InterNetwork;
    }

    /// <summary>
    /// Returns true if input it is a valid IPv4 address + port, like "127.0.0.1:7000"
    /// </summary>
    /// <param name="ipString"></param>
    /// <returns></returns>
    public static bool ValidateIPv4Port(string ipString)
    {
        return IPEndPoint.TryParse(ipString, out var endPoint) && endPoint.AddressFamily == AddressFamily.InterNetwork;

    }

    /// <summary>
    /// Given a remote IP address and a subnet mask, tries to find the local IP address of this host in the same subnet.
    /// This is useful to figure out which IP this host is using in the same network as the remote. 
    /// Inspired by https://stackoverflow.com/a/6803109/1934487
    /// </summary>
    /// <param name="remoteIP">The remote IP of the device we are trying to find the local network for.</param>
    /// <param name="subnetMask">Typically "255.255.255.0", filters how many hosts are accepted in the subnet. https://www.iplocation.net/subnet-mask </param>
    /// <param name="localIP">The found localIP</param>
    /// <returns></returns>
    public static bool GetLocalIPAddressInNetwork(string remoteIP, string subnetMask, out string localIP)
    {
        if (!IPAddress.TryParse(remoteIP, out var remote) || !IPAddress.TryParse(subnetMask, out var mask))
        {
            localIP = null;
            return false;
        }

        IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

        foreach (IPAddress ip in from IPAddress ip in host.AddressList.Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                                 where IsInSameSubnet(remote, ip, mask)
                                 select ip)
        {
            localIP = ip.ToString();
            return true;
        }

        localIP = null;
        return false;
    }


    // https://blogs.msdn.microsoft.com/knom/2008/12/31/ip-address-calculations-with-c-subnetmasks-networks/
    private static IPAddress GetBroadcastAddress(this IPAddress address, IPAddress subnetMask)
    {
        byte[] ipAdressBytes = address.GetAddressBytes();
        byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

        if (ipAdressBytes.Length != subnetMaskBytes.Length)
            throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

        byte[] broadcastAddress = new byte[ipAdressBytes.Length];
        for (int i = 0; i < broadcastAddress.Length; i++)
        {
            broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
        }
        return new IPAddress(broadcastAddress);
    }

    private static IPAddress GetNetworkAddress(this IPAddress address, IPAddress subnetMask)
    {
        byte[] ipAdressBytes = address.GetAddressBytes();
        byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

        if (ipAdressBytes.Length != subnetMaskBytes.Length)
            throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

        byte[] broadcastAddress = new byte[ipAdressBytes.Length];
        for (int i = 0; i < broadcastAddress.Length; i++)
        {
            broadcastAddress[i] = (byte)(ipAdressBytes[i] & (subnetMaskBytes[i]));
        }
        return new IPAddress(broadcastAddress);
    }

    private static bool IsInSameSubnet(this IPAddress remoteAddress, IPAddress localAddress, IPAddress subnetMask)
    {
        IPAddress remoteNetwork = remoteAddress.GetNetworkAddress(subnetMask);
        IPAddress localNetwork = localAddress.GetNetworkAddress(subnetMask);
        return remoteNetwork.Equals(localNetwork);
    }


}
