# SwiftTunnel VPN Native Library

This is the Rust native library that provides WireGuard VPN and split tunnel functionality for Voidstrap's SwiftTunnel integration.

## Building

### Prerequisites
- Rust 1.75+ (with `cargo`)
- Windows 10/11 x64

### Build Commands

```bash
cd swifttunnel-vpn-native
cargo build --release
```

The compiled DLL will be at `target/release/swifttunnel_vpn.dll`.

## Architecture

### Route-Based Split Tunneling (v0.6.2+)

This library uses **route-based split tunneling** instead of kernel drivers:

- **ZERO overhead**: Uses Windows routing table, no packet interception
- **No driver required**: Works on any Windows system
- **Always available**: `swifttunnel_split_tunnel_available()` always returns 1

### How It Works

1. On `configure()`, adds routes for Roblox IP ranges through the VPN interface
2. Windows kernel routes game traffic through VPN automatically
3. Non-game traffic has no routes = bypasses VPN naturally
4. On `close()`, removes all added routes

### Roblox IP Ranges

The library routes the following Roblox server IP ranges through VPN:

- `128.116.0.0/17` (Main Roblox range)
- `209.206.40.0/21`
- `23.173.192.0/24`
- And 14 more ranges

## FFI API

The library exposes a C ABI for use from C#. See `NativeVpn.cs` for the P/Invoke declarations.

### Key Functions

| Function | Description |
|----------|-------------|
| `swifttunnel_init` | Initialize the library |
| `swifttunnel_connect` | Connect to VPN server |
| `swifttunnel_disconnect` | Disconnect from VPN |
| `swifttunnel_split_tunnel_available` | Check if split tunnel is available (always 1) |
| `swifttunnel_split_tunnel_configure` | Configure route-based split tunnel |
| `swifttunnel_split_tunnel_refresh` | Get list of running Roblox processes |
| `swifttunnel_split_tunnel_close` | Remove routes and cleanup |

## Source Files

- `lib.rs` - FFI exports and global state
- `vpn.rs` - WireGuard tunnel implementation
- `split_tunnel.rs` - Route-based split tunnel implementation
- `wfp.rs` - Stub for backward compatibility (WFP no longer used)
- `error.rs` - Error types

## License

This code is part of SwiftTunnel and is provided for integration with Voidstrap.
