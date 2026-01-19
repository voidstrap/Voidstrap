using System.Runtime.InteropServices;

namespace Voidstrap.Integrations.SwiftTunnel
{
    /// <summary>
    /// P/Invoke declarations for swifttunnel_vpn.dll
    /// </summary>
    internal static class NativeVpn
    {
        private const string DllName = "swifttunnel_vpn.dll";

        // State codes
        public const int StateDisconnected = 0;
        public const int StateFetchingConfig = 1;
        public const int StateCreatingAdapter = 2;
        public const int StateConnecting = 3;
        public const int StateConfiguringSplitTunnel = 4;
        public const int StateConnected = 5;
        public const int StateDisconnecting = 6;
        public const int StateError = -1;

        // Return codes
        public const int Success = 0;
        public const int ErrorInvalidParam = -1;
        public const int ErrorNotInitialized = -2;
        public const int ErrorAlreadyConnected = -3;
        public const int ErrorNotConnected = -4;
        public const int ErrorInternal = -5;

        /// <summary>
        /// Initialize the VPN library. Must be called before any other function.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int swifttunnel_init();

        /// <summary>
        /// Cleanup the VPN library. Should be called when done using the library.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void swifttunnel_cleanup();

        /// <summary>
        /// Connect to VPN server.
        /// </summary>
        /// <param name="configJson">JSON string containing connection configuration</param>
        /// <returns>0 on success, negative error code on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int swifttunnel_connect([MarshalAs(UnmanagedType.LPUTF8Str)] string configJson);

        /// <summary>
        /// Disconnect from VPN.
        /// </summary>
        /// <returns>0 on success, negative error code on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int swifttunnel_disconnect();

        /// <summary>
        /// Get current connection state code.
        /// </summary>
        /// <returns>State code (see State* constants)</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int swifttunnel_get_state();

        /// <summary>
        /// Get state as JSON string with detailed information.
        /// Caller must free the returned string with swifttunnel_free_string.
        /// </summary>
        /// <returns>Pointer to JSON string or null on error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr swifttunnel_get_state_json();

        /// <summary>
        /// Get last error message.
        /// Caller must free the returned string with swifttunnel_free_string.
        /// </summary>
        /// <returns>Pointer to error message or null if no error</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr swifttunnel_get_error();

        /// <summary>
        /// Free a string returned by this library.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void swifttunnel_free_string(IntPtr ptr);

        /// <summary>
        /// Check if VPN is connected.
        /// </summary>
        /// <returns>1 if connected, 0 otherwise</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int swifttunnel_is_connected();

        /// <summary>
        /// Check if WireGuard/Wintun is available on this system.
        /// </summary>
        /// <returns>1 if available, 0 otherwise</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int swifttunnel_is_available();

        // ═══════════════════════════════════════════════════════════════════════════════
        //  SPLIT TUNNEL API
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Check if the Mullvad split tunnel driver is available.
        /// </summary>
        /// <returns>1 if available, 0 otherwise</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int swifttunnel_split_tunnel_available();

        /// <summary>
        /// Configure and enable split tunneling.
        /// </summary>
        /// <param name="tunnelIp">The VPN tunnel IP address (e.g., "10.0.0.77")</param>
        /// <param name="interfaceLuid">The Wintun adapter LUID</param>
        /// <param name="appsJson">JSON array of app names to tunnel, or null for defaults</param>
        /// <returns>0 on success, negative error code on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int swifttunnel_split_tunnel_configure(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string tunnelIp,
            ulong interfaceLuid,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? appsJson);

        /// <summary>
        /// Refresh split tunnel process detection.
        /// Call this periodically to detect newly started Roblox processes.
        /// </summary>
        /// <returns>Pointer to JSON array of currently tunneled process names, or null</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr swifttunnel_split_tunnel_refresh();

        /// <summary>
        /// Disable and close split tunneling.
        /// </summary>
        /// <returns>0 on success, negative error code on failure</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int swifttunnel_split_tunnel_close();

        /// <summary>
        /// Get default apps that will be tunneled (Roblox processes).
        /// </summary>
        /// <returns>Pointer to JSON array of app names</returns>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr swifttunnel_split_tunnel_get_default_apps();

        /// <summary>
        /// Helper method to get string from native pointer and free it
        /// </summary>
        public static string? GetStringAndFree(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            try
            {
                return Marshal.PtrToStringUTF8(ptr);
            }
            finally
            {
                swifttunnel_free_string(ptr);
            }
        }

        /// <summary>
        /// Check if split tunnel driver is available
        /// </summary>
        public static bool IsSplitTunnelAvailable() => swifttunnel_split_tunnel_available() == 1;

        /// <summary>
        /// Get default tunnel apps as a list
        /// </summary>
        public static List<string> GetDefaultTunnelApps()
        {
            var ptr = swifttunnel_split_tunnel_get_default_apps();
            var json = GetStringAndFree(ptr);
            if (string.IsNullOrEmpty(json))
                return new List<string>();

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Refresh split tunnel and get currently tunneled process names
        /// </summary>
        public static List<string> RefreshSplitTunnel()
        {
            var ptr = swifttunnel_split_tunnel_refresh();
            var json = GetStringAndFree(ptr);
            if (string.IsNullOrEmpty(json))
                return new List<string>();

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
