using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;

public static class TcpHelper
{
    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_ALL = 5;

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public uint owningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int dwOutBufLen,
        bool sort,
        int ipVersion,
        int tblClass,
        int reserved
    );

    public static List<(IPAddress localIP, int localPort, IPAddress remoteIP, int remotePort, int pid)> GetTcpConnections()
    {
        int bufferSize = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);

        IntPtr tcpTablePtr = Marshal.AllocHGlobal(bufferSize);
        try
        {
            uint result = GetExtendedTcpTable(tcpTablePtr, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
            if (result != 0) return new List<(IPAddress, int, IPAddress, int, int)>();

            int rowStructSize = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));
            int numEntries = Marshal.ReadInt32(tcpTablePtr);

            List<(IPAddress, int, IPAddress, int, int)> connections = new List<(IPAddress, int, IPAddress, int, int)>();

            IntPtr rowPtr = IntPtr.Add(tcpTablePtr, 4);
            for (int i = 0; i < numEntries; i++)
            {
                MIB_TCPROW_OWNER_PID row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);

                IPAddress localIP = new IPAddress(row.localAddr);
                int localPort = ((int)row.localPort >> 8 & 0xFF) | ((int)row.localPort & 0xFF) << 8;

                IPAddress remoteIP = new IPAddress(row.remoteAddr);
                int remotePort = ((int)row.remotePort >> 8 & 0xFF) | ((int)row.remotePort & 0xFF) << 8;

                int pid = (int)row.owningPid;

                connections.Add((localIP, localPort, remoteIP, remotePort, pid));

                rowPtr = IntPtr.Add(rowPtr, rowStructSize);
            }

            return connections;
        }
        finally
        {
            Marshal.FreeHGlobal(tcpTablePtr);
        }
    }
}
