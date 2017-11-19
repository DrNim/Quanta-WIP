using System;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Quanta
{
    public sealed class Utilities
    {

        #region " Route Table "

        /// <summary>
        /// Flushes the IP path table on the local computer.
        /// </summary>
        public static bool FlushRouteTable()
        {
            return FlushRouteTable(ProtocolFamily.Unspecified);
        }

        /// <summary>
        /// Flushes the IP path table on the local computer.
        /// </summary>
        /// <param name="family">The address family to flush.</param>
        public static bool FlushRouteTable(ProtocolFamily family)
        {
            return (Win32.FlushIpPathTable((ushort)family) == 0);
        }

        #endregion

        #region " ARP Table "

        /// <summary>
        /// Deletes ARP table entries on the local computer.
        /// </summary> 
        public static bool FlushArpTable()
        {
            return FlushArpTable(ProtocolFamily.Unspecified, 0);
        }

        /// <summary>
        /// Deletes all ARP table entries for the specified interface on the local computer.
        /// </summary>
        /// <param name="interfaceIndex">The index of the interface for which to delete all ARP entries.</param>
        public static bool FlushArpTable(uint interfaceIndex)
        {
            return FlushArpTable(ProtocolFamily.Unspecified, interfaceIndex);
        }

        /// <summary>
        /// Deletes all ARP table entries for the specified interface on the local computer.
        /// </summary>
        /// <param name="family">The address family to delete.</param>
        /// <param name="interfaceIndex">The index of the interface for which to delete all ARP entries.</param>
        public static bool FlushArpTable(ProtocolFamily family, uint interfaceIndex)
        {
            return (Win32.FlushIpNetTable2((ushort)family, interfaceIndex) == 0);
        }

        #endregion

        #region " DNS "

        /// <summary>
        /// Flushes the DNS resolver cache on the local computer.
        /// </summary>
        public static bool FlushDnsCache()
        {
            return Win32.DnsFlushResolverCache();
        }

        /// <summary>
        /// Flushes an entry from the DNS resolver cache on the local computer.
        /// </summary>
        public static bool FlushDnsCacheEntry(string hostName)
        {
            return Win32.DnsFlushResolverCacheEntry(hostName);
        }

        ////TODO: Override that takes an Options param for things like DNS servers, cache handling, etc.
        //public static IPHostEntry GetHostEntry(string hostName)
        //{
        //    //Win32.SOCKADDR_IN sockAddr = new Win32.SOCKADDR_IN();
        //    //sockAddr.Addr = IPAddress.Parse("8.8.8.8").GetAddressBytes();
        //    //sockAddr.Family = (short)ProtocolFamily.InterNetwork;
        //    //sockAddr.Port = (ushort)IPAddress.HostToNetworkOrder((short)Win32.DNS_PORT_HOST_ORDER);
        //    //sockAddr.Zero = new byte[8]; //TODO: Remove this?

        //    //byte[] buffer = new byte[Marshal.SizeOf(sockAddr)];

        //    //IntPtr handle = Marshal.AllocHGlobal(buffer.Length);

        //    //try
        //    //{
        //    //    Marshal.StructureToPtr(sockAddr, handle, false);
        //    //    Marshal.Copy(handle, buffer, 0, buffer.Length);
        //    //}
        //    //finally
        //    //{
        //    //    Marshal.FreeHGlobal(handle);
        //    //}

        //    //Win32.DNS_ADDR dnsAddr = new Win32.DNS_ADDR();
        //    //dnsAddr.MaxSa = buffer;
        //    //dnsAddr.Data = new uint[8]; //TODO: Remove this?

        //    //Win32.DNS_ADDR_ARRAY dnsArray = new Win32.DNS_ADDR_ARRAY();
        //    //dnsArray.Family = (ushort)ProtocolFamily.InterNetwork;
        //    //dnsArray.AddrArray = new Win32.DNS_ADDR[] { dnsAddr };
        //    //dnsArray.AddrCount = (uint)dnsArray.AddrArray.Length;
        //    //dnsArray.MaxCount = Convert.ToUInt32(Marshal.SizeOf(dnsArray));

        //    //Win32.DNS_QUERY_REQUEST dnsRequest = new Win32.DNS_QUERY_REQUEST();
        //    //dnsRequest.Version = Win32.DNS_QUERY_REQUEST_VERSION1;
        //    //dnsRequest.QueryName = hostName;
        //    //dnsRequest.QueryType = Win32.DNS_TYPE_A;
        //    //dnsRequest.QueryOptions = 0; //TODO: Provide options to configure this.
        //    //dnsRequest.DnsServerList = dnsArray; //TODO: Provide options to configure this.
        //    //dnsRequest.InterfaceIndex = 0; //TODO: Provide option to configure this.

        //    //Win32.DNS_QUERY_RESULT dnsResult = new Win32.DNS_QUERY_RESULT();
        //    //dnsResult.Version = Win32.DNS_QUERY_REQUEST_VERSION1;

        //    //int result = Win32.DnsQueryEx(dnsRequest, ref dnsResult, IntPtr.Zero);

        //    //throw new Win32Exception(result);

        //    //return null;
        //}


        #endregion

        #region " DHCP "

        /// <summary>
        /// Releases an IPv4 address previously obtained through the Dynamic Host Configuration Protocol (DHCP).
        /// </summary>
        /// <param name="interfaceIndex">The index of the interface for which to release the IPv4 address.</param>
        public static bool ReleaseDhcpAddress(uint interfaceIndex)
        {
            return IpReleaseAddress(GetInterfaceMap(interfaceIndex));
        }

        /// <summary>
        /// Renews a lease on an IPv4 address previously obtained through Dynamic Host Configuration Protocol (DHCP).
        /// </summary>
        /// <param name="interfaceIndex">The index of the interface for which to renew the IPv4 address.</param>
        public static bool RenewDhcpAddress(uint interfaceIndex)
        {
            return IpRenewAddress(GetInterfaceMap(interfaceIndex));
        }

        /// <summary>
        /// Releases the IPv4 address previously obtained through the Dynamic Host Configuration Protocol (DHCP) on all interfaces.
        /// </summary>
        public static bool ReleaseDhcpAddress()
        {
            bool hasErrors = false;
            Win32.IP_INTERFACE_INFO info = GetInterfaceInfo();

            foreach (Win32.IP_ADAPTER_INDEX_MAP map in info.Adapters)
            {
                if (!IpReleaseAddress(map))
                {
                    hasErrors = true;
                }
            }

            return hasErrors;
        }

        /// <summary>
        /// Renews the lease on the IPv4 address previously obtained through Dynamic Host Configuration Protocol (DHCP) on all interfaces.
        /// </summary>
        public static bool RenewDhcpAddress()
        {
            bool hasErrors = false;
            Win32.IP_INTERFACE_INFO info = GetInterfaceInfo();

            foreach (Win32.IP_ADAPTER_INDEX_MAP map in info.Adapters)
            {
                if (!IpRenewAddress(map))
                {
                    hasErrors = true;
                }
            }

            return hasErrors;
        }

        private static bool IpReleaseAddress(Win32.IP_ADAPTER_INDEX_MAP map)
        {
            return (Win32.IpReleaseAddress(map) == 0);
        }

        private static bool IpRenewAddress(Win32.IP_ADAPTER_INDEX_MAP map)
        {
            return (Win32.IpRenewAddress(map) == 0);
        }

        private static Win32.IP_ADAPTER_INDEX_MAP GetInterfaceMap(uint interfaceIndex)
        {
            Win32.IP_INTERFACE_INFO info = GetInterfaceInfo();

            foreach (Win32.IP_ADAPTER_INDEX_MAP map in info.Adapters)
            {
                if (map.Index == interfaceIndex)
                {
                    return map;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(interfaceIndex));
        }

        private static Win32.IP_INTERFACE_INFO GetInterfaceInfo()
        {
            uint length = 0;
            byte[] table = new byte[] { };

            uint result = Win32.GetInterfaceInfo(table, ref length);

            if (result == Win32.ERROR_INSUFFICIENT_BUFFER)
            {
                table = new byte[length];
                result = Win32.GetInterfaceInfo(table, ref length);
            }

            if (result != 0)
            {
                throw new Win32Exception((int)result);
            }

            return Win32.IP_INTERFACE_INFO.Create(table);
        }

        #endregion

        #region " Interfaces "

        /// <summary>
        /// Returns an appropriate NetworkInterface object for the specified interface index.
        /// </summary>
        /// <param name="interfaceIndex">The index of the interface for which to retrieve a NetworkInterface object.</param> 
        public static NetworkInterface GetNetworkInterface(uint interfaceIndex)
        {
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                IPInterfaceProperties properties = item.GetIPProperties();

                if (item.Supports(NetworkInterfaceComponent.IPv4) && properties.GetIPv4Properties().Index == interfaceIndex)
                {
                    return item;
                }

                if (item.Supports(NetworkInterfaceComponent.IPv6) && properties.GetIPv6Properties().Index == interfaceIndex)
                {
                    return item;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(interfaceIndex));
        }

        /// <summary>
        /// Retrieves the index of the default IPv4 interface adapter.
        /// </summary> 
        public static uint GetDefaultInterface()
        {
            //TODO: Add support for IPv6 defaults (GetDefaultIPvNInterface?)
            return GetBestInterface(IPAddress.Any);
        }

        /// <summary>
        /// Retrieves the index of the interface that has the best route to the specified IPv4 address.
        /// </summary>
        /// <param name="address">The destination IPv4 address for which to retrieve the interface that has the best route.</param> 
        public static uint GetBestInterface(IPAddress address)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            //TODO: Add support for IPv6..
            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new NotSupportedException();
            }

            byte[] addressBytes = address.GetAddressBytes();

            uint index = 0;
            uint result = Win32.GetBestInterface(BitConverter.ToUInt32(addressBytes, 0), ref index);

            if (result != 0)
            {
                throw new Win32Exception((int)result);
            }

            return index;
        }

        #endregion 

    }
}
