//! Error types for the VPN native library

use thiserror::Error;

#[derive(Debug, Error)]
pub enum VpnError {
    #[error("Failed to create Wintun adapter: {0}")]
    AdapterCreate(String),

    #[error("Failed to initialize WireGuard tunnel: {0}")]
    TunnelInit(String),

    #[error("WireGuard handshake failed: {0}")]
    HandshakeFailed(String),

    #[error("Split tunnel error: {0}")]
    SplitTunnel(String),

    #[error("Route error: {0}")]
    Route(String),

    #[error("Connection error: {0}")]
    Connection(String),

    #[error("Invalid configuration: {0}")]
    InvalidConfig(String),

    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),
}
