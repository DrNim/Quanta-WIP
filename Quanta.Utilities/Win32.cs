using System;
using System.Runtime.InteropServices;

namespace Quanta
{
    internal sealed class Win32
    {

        public const int MAX_ADAPTER_NAME = 128;
        public const int DNS_ADDR_MAX_SOCKADDR_LENGTH = 32;
        public const int ERROR_INSUFFICIENT_BUFFER = 122;
        public const int DNS_QUERY_REQUEST_VERSION1 = 1;
        public const int DNS_TYPE_A = 1;
        public const int SIZEOF_IP4_ADDRESS = 4;

        [DllImport("iphlpapi.dll", EntryPoint = "FlushIpPathTable")]
        public static extern uint FlushIpPathTable(
            ushort family
        );

        [DllImport("iphlpapi.dll", EntryPoint = "FlushIpNetTable2")]
        public static extern uint FlushIpNetTable2(
            ushort family,
            uint interfaceIndex
        );

        [DllImport("iphlpapi.dll", EntryPoint = "GetBestInterface")]
        public static extern uint GetBestInterface(
            uint destAddr,
            ref uint index
        );

        //[DllImport("iphlpapi.dll", EntryPoint = "GetInterfaceInfo")]
        //public static extern uint GetInterfaceInfo(
        //    ref IP_INTERFACE_INFO ifTable,
        //    ref uint outBufLen
        //);

        [DllImport("iphlpapi.dll", EntryPoint = "GetInterfaceInfo")]
        public static extern uint GetInterfaceInfo(
            byte[] ifTable,
            ref uint outBufLen
        );

        [DllImport("iphlpapi.dll", EntryPoint = "IpReleaseAddress")]
        public static extern uint IpReleaseAddress(
            IP_ADAPTER_INDEX_MAP adapterInfo
        );

        [DllImport("iphlpapi.dll", EntryPoint = "IpRenewAddress")]
        public static extern uint IpRenewAddress(
            IP_ADAPTER_INDEX_MAP adapterInfo
        );

        //WARNING: Undocumented
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        public static extern bool DnsFlushResolverCache();

        //WARNING: Undocumented
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCacheEntry_W")]
        public static extern bool DnsFlushResolverCacheEntry(
            [MarshalAs(UnmanagedType.LPWStr)] string hostName
        );

        [DllImport("dnsapi.dll", EntryPoint = "DnsQuery_W")]
        public static extern int DnsQuery(
            string name,
            ushort type,
            uint options,
            ref IP4_ARRAY aipServers, //TODO: Ref?
            ref DNS_QUERY_RESULT queryResults,
            ref IntPtr reserved
        );

        [DllImport("dnsapi.dll", EntryPoint = "DnsValidateName_W")]
        public static extern int DnsValidateName(
            string name,
            int format
        );

        //NOTE: Requires Windows 7
        [DllImport("dnsapi.dll", EntryPoint = "DnsValidateServerStatus")]
        public static extern int DnsValidateServerStatus(
            SOCKADDR_IN server,
            [MarshalAs(UnmanagedType.LPWStr)] string queryName,
            ref uint serverStatus
        );

        [DllImport("dnsapi.dll", EntryPoint = "DnsRecordListFree")]
        public static extern void DnsRecordListFree(
            ref IntPtr recordList,
            int freeType
        );

        //typedef struct _IP_ADAPTER_INDEX_MAP
        //{
        //    ULONG Index;
        //    WCHAR Name[MAX_ADAPTER_NAME];
        //}
        //IP_ADAPTER_INDEX_MAP, *PIP_ADAPTER_INDEX_MAP;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct IP_ADAPTER_INDEX_MAP
        {
            public uint Index;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_ADAPTER_NAME)]
            public string Name;
        }

        //typedef struct _IP_INTERFACE_INFO
        //{
        //    LONG NumAdapters;
        //    IP_ADAPTER_INDEX_MAP Adapter[1];
        //}
        //IP_INTERFACE_INFO, *PIP_INTERFACE_INFO;

        [StructLayout(LayoutKind.Sequential)]
        public struct IP_INTERFACE_INFO
        {
            public int NumAdapters;
            public IP_ADAPTER_INDEX_MAP[] Adapters;

            public static IP_INTERFACE_INFO Create(byte[] buffer)
            {
                IP_INTERFACE_INFO info;

                IntPtr handle = Marshal.AllocHGlobal(buffer.Length);
                IntPtr pointer = handle;

                try
                {
                    Marshal.Copy(buffer, 0, pointer, buffer.Length);

                    info.NumAdapters = Marshal.ReadInt32(pointer);
                    info.Adapters = new IP_ADAPTER_INDEX_MAP[info.NumAdapters];

                    pointer += 4;

                    for (int i = 0; i < info.NumAdapters; i++)
                    {
                        info.Adapters[i] = Marshal.PtrToStructure<IP_ADAPTER_INDEX_MAP>(pointer);
                        pointer += Marshal.SizeOf<IP_ADAPTER_INDEX_MAP>();
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(handle);
                }

                return info;
            }
        }

        //typedef struct _DNS_QUERY_REQUEST
        //{
        //    ULONG Version;
        //    PCWSTR QueryName;
        //    WORD QueryType;
        //    ULONG64 QueryOptions;
        //    PDNS_ADDR_ARRAY pDnsServerList;
        //    ULONG InterfaceIndex;
        //    PDNS_QUERY_COMPLETION_ROUTINE pQueryCompletionCallback;
        //    PVOID pQueryContext;
        //}
        //DNS_QUERY_REQUEST, *PDNS_QUERY_REQUEST;

        [StructLayout(LayoutKind.Sequential)]
        public struct DNS_QUERY_REQUEST
        {
            public uint Version;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string QueryName;
            public ushort QueryType;
            public ulong QueryOptions;
            public DNS_ADDR_ARRAY DnsServerList;
            public uint InterfaceIndex;
            public IntPtr QueryCompletionCallback;
            public IntPtr QueryContext;
        }

        //typedef struct _DnsAddrArray
        //{
        //    DWORD MaxCount;
        //    DWORD AddrCount;
        //    DWORD Tag;
        //    WORD Family;
        //    WORD WordReserved;
        //    DWORD Flags;
        //    DWORD MatchFlag;
        //    DWORD Reserved1;
        //    DWORD Reserved2;
        //    DNS_ADDR AddrArray[];
        //}
        //DNS_ADDR_ARRAY, *PDNS_ADDR_ARRAY;

        [StructLayout(LayoutKind.Sequential)]
        public struct DNS_ADDR_ARRAY
        {
            public uint MaxCount;
            public uint AddrCount;
            public uint Tag;
            public ushort Family;
            public ushort WordReserved;
            public uint Flags;
            public uint MatchFlag;
            public uint Reserved1;
            public uint Reserved2;
            public DNS_ADDR[] AddrArray;
        }

        //typedef struct _DnsAddr
        //{
        //    CHAR MaxSa[DNS_ADDR_MAX_SOCKADDR_LENGTH];
        //    DWORD DnsAddrUserDword[8];
        //}
        //DNS_ADDR, *PDNS_ADDR;

        [StructLayout(LayoutKind.Sequential)]
        public struct DNS_ADDR
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = DNS_ADDR_MAX_SOCKADDR_LENGTH)]
            public byte[] MaxSa;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public uint[] Data;
        }

        //typedef struct _DNS_QUERY_RESULT
        //{
        //    ULONG Version;
        //    DNS_STATUS QueryStatus;
        //    ULONG64 QueryOptions;
        //    PDNS_RECORDS pQueryRecords;
        //    PVOID reserved;
        //}
        //DNS_QUERY_RESULT, *PDNS_QUERY_RESULT;

        [StructLayout(LayoutKind.Sequential)]
        public struct DNS_QUERY_RESULT
        {
            public uint Version;
            public int QueryStatus;
            public ulong QueryOptions;
            public DNS_RECORD QueryRecords;
            public IntPtr Reserved;
        }

        //        typedef struct _DnsRecord
        //        {
        //            DNS_RECORD* pNext;
        //            PWSTR pName;
        //            WORD wType;
        //            WORD wDataLength;
        //            union {
        //            DWORD DW;
        //            DNS_RECORD_FLAGS S;
        //        }
        //        Flags;
        //        DWORD dwTtl;
        //        DWORD dwReserved;
        //        union {
        //        DNS_A_DATA A;
        //        DNS_SOA_DATA SOA, Soa;
        //        DNS_PTR_DATA PTR, Ptr, NS, Ns, CNAME, Cname, DNAME, Dname, MB, Mb, MD, Md, MF, Mf, MG, Mg, MR, Mr;
        //        DNS_MINFO_DATA MINFO, Minfo, RP, Rp;
        //        DNS_MX_DATA MX, Mx, AFSDB, Afsdb, RT, Rt;
        //        DNS_TXT_DATA HINFO, Hinfo, ISDN, Isdn, TXT, Txt, X25;
        //        DNS_NULL_DATA Null;
        //        DNS_WKS_DATA WKS, Wks;
        //        DNS_AAAA_DATA AAAA;
        //        DNS_KEY_DATA KEY, Key;
        //        DNS_SIG_DATA SIG, Sig;
        //        DNS_ATMA_DATA ATMA, Atma;
        //        DNS_NXT_DATA NXT, Nxt;
        //        DNS_SRV_DATA SRV, Srv;
        //        DNS_NAPTR_DATA NAPTR, Naptr;
        //        DNS_OPT_DATA OPT, Opt;
        //        DNS_DS_DATA DS, Ds;
        //        DNS_RRSIG_DATA RRSIG, Rrsig;
        //        DNS_NSEC_DATA NSEC, Nsec;
        //        DNS_DNSKEY_DATA DNSKEY, Dnskey;
        //        DNS_TKEY_DATA TKEY, Tkey;
        //        DNS_TSIG_DATA TSIG, Tsig;
        //        DNS_WINS_DATA WINS, Wins;
        //        DNS_WINSR_DATA WINSR, WinsR, NBSTAT, Nbstat;
        //        DNS_DHCID_DATA DHCID;
        //    }
        //    Data;
        //}
        //DNS_RECORD, * PDNS_RECORD;

        [StructLayout(LayoutKind.Sequential)]
        public struct DNS_RECORD
        {
            public IntPtr Next;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string Name;
            public ushort Type;
            public ushort DataLength;
            public uint Flags;
            public uint Ttl;
            public uint Reserved;
            public DNS_A_DATA Data;
        }

        //  typedef struct {
        //      IP4_ADDRESS IpAddress;
        //  }
        //  DNS_A_DATA, * PDNS_A_DATA;

        [StructLayout(LayoutKind.Sequential)]
        public struct DNS_A_DATA
        {
            public uint IP4_ADDRESS;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SOCKADDR_IN
        {
            public short Family;
            public ushort Port;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIZEOF_IP4_ADDRESS)]
            public byte[] Addr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] Zero;
        }

        //typedef struct _IP4_ARRAY
        //{
        //    DWORD AddrCount;
        //    IP4_ADDRESS AddrArray[1];
        //}
        //IP4_ARRAY, *PIP4_ARRAY;

        [StructLayout(LayoutKind.Sequential)]
        public struct IP4_ARRAY
        {
            public uint AddrCount;
            public uint[] AddrArray;
        }

    }
}
