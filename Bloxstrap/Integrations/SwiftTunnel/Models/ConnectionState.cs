namespace Voidstrap.Integrations.SwiftTunnel.Models
{
    /// <summary>
    /// VPN connection state enumeration
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        FetchingConfig,
        CreatingAdapter,
        Connecting,
        ConfiguringSplitTunnel,
        Connected,
        Disconnecting,
        Error
    }
}
