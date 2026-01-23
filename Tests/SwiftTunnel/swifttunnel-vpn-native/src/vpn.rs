//! VPN Manager implementation
//!
//! Simplified VPN manager for the native library that wraps WireGuard/BoringTun + Wintun

use std::net::{SocketAddr, UdpSocket, Ipv4Addr};
use std::sync::atomic::{AtomicBool, AtomicI32, Ordering};
use std::sync::Arc;
use std::thread;
use std::time::{Duration, Instant};
use parking_lot::Mutex;
use serde::Serialize;

use boringtun::noise::{Tunn, TunnResult, errors::WireGuardError};
use boringtun::x25519::{StaticSecret, PublicKey};
use base64::Engine;
use base64::engine::general_purpose::STANDARD as BASE64;

use crate::error::VpnError;
use crate::ConnectConfig;

// State codes matching lib.rs
const STATE_DISCONNECTED: i32 = 0;
const STATE_FETCHING_CONFIG: i32 = 1;
const STATE_CREATING_ADAPTER: i32 = 2;
const STATE_CONNECTING: i32 = 3;
const STATE_CONFIGURING_SPLIT_TUNNEL: i32 = 4;
const STATE_CONNECTED: i32 = 5;
const STATE_DISCONNECTING: i32 = 6;
const STATE_ERROR: i32 = -1;

// Constants
const MAX_PACKET_SIZE: usize = 65535;
const TICK_INTERVAL_MS: u64 = 100;
const HANDSHAKE_TIMEOUT_SECS: u64 = 10;

/// VPN Manager state
#[derive(Debug, Serialize)]
pub struct VpnState {
    pub state: String,
    pub code: i32,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub region: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub endpoint: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub assigned_ip: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub connected_since_secs: Option<u64>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<String>,
}

/// VPN Manager
pub struct VpnManager {
    state_code: Arc<AtomicI32>,
    stop_flag: Arc<AtomicBool>,
    error_msg: Arc<Mutex<Option<String>>>,
    connection_info: Arc<Mutex<Option<ConnectionInfo>>>,
    tunnel_thread: Option<thread::JoinHandle<()>>,
}

struct ConnectionInfo {
    region: String,
    endpoint: String,
    assigned_ip: String,
    connected_at: Instant,
}

impl VpnManager {
    pub fn new() -> Self {
        Self {
            state_code: Arc::new(AtomicI32::new(STATE_DISCONNECTED)),
            stop_flag: Arc::new(AtomicBool::new(false)),
            error_msg: Arc::new(Mutex::new(None)),
            connection_info: Arc::new(Mutex::new(None)),
            tunnel_thread: None,
        }
    }

    pub fn is_connected(&self) -> bool {
        self.state_code.load(Ordering::SeqCst) == STATE_CONNECTED
    }

    pub fn is_connecting(&self) -> bool {
        let code = self.state_code.load(Ordering::SeqCst);
        code >= STATE_FETCHING_CONFIG && code <= STATE_CONFIGURING_SPLIT_TUNNEL
    }

    pub fn get_state_code(&self) -> i32 {
        self.state_code.load(Ordering::SeqCst)
    }

    pub fn get_state_json(&self) -> String {
        let code = self.state_code.load(Ordering::SeqCst);
        let state_name = match code {
            STATE_DISCONNECTED => "disconnected",
            STATE_FETCHING_CONFIG => "fetching_config",
            STATE_CREATING_ADAPTER => "creating_adapter",
            STATE_CONNECTING => "connecting",
            STATE_CONFIGURING_SPLIT_TUNNEL => "configuring_split_tunnel",
            STATE_CONNECTED => "connected",
            STATE_DISCONNECTING => "disconnecting",
            STATE_ERROR => "error",
            _ => "unknown",
        };

        let info = self.connection_info.lock();
        let error = self.error_msg.lock();

        let state = VpnState {
            state: state_name.to_string(),
            code,
            region: info.as_ref().map(|i| i.region.clone()),
            endpoint: info.as_ref().map(|i| i.endpoint.clone()),
            assigned_ip: info.as_ref().map(|i| i.assigned_ip.clone()),
            connected_since_secs: info.as_ref().map(|i| i.connected_at.elapsed().as_secs()),
            error: error.clone(),
        };

        serde_json::to_string(&state).unwrap_or_else(|_| {
            format!(r#"{{"state":"{}","code":{}}}"#, state_name, code)
        })
    }

    pub fn connect(&mut self, config: ConnectConfig) -> Result<(), VpnError> {
        // Reset state
        self.stop_flag.store(false, Ordering::SeqCst);
        *self.error_msg.lock() = None;
        *self.connection_info.lock() = None;

        // Parse keys
        self.state_code.store(STATE_FETCHING_CONFIG, Ordering::SeqCst);

        let private_key = parse_private_key(&config.private_key)?;
        let server_public_key = parse_public_key(&config.server_public_key)?;
        let endpoint: SocketAddr = config.endpoint.parse()
            .map_err(|e| VpnError::InvalidConfig(format!("Invalid endpoint: {}", e)))?;

        // Parse assigned IP
        let (ip, _cidr) = parse_ip_cidr(&config.assigned_ip)?;

        // Create Wintun adapter
        self.state_code.store(STATE_CREATING_ADAPTER, Ordering::SeqCst);
        log::info!("Creating Wintun adapter with IP: {}", ip);

        // SAFETY: wintun.dll must be available in the executable directory or system path.
        // This is checked by swifttunnel_is_available() before connecting.
        let wintun = match unsafe { wintun::load() } {
            Ok(w) => w,
            Err(e) => {
                let msg = format!("Failed to load wintun.dll: {}", e);
                *self.error_msg.lock() = Some(msg.clone());
                self.state_code.store(STATE_ERROR, Ordering::SeqCst);
                return Err(VpnError::AdapterCreate(msg));
            }
        };

        let adapter = match wintun::Adapter::create(&wintun, "SwiftTunnel", "SwiftTunnel", None) {
            Ok(a) => a,
            Err(e) => {
                let msg = format!("Failed to create adapter: {}", e);
                *self.error_msg.lock() = Some(msg.clone());
                self.state_code.store(STATE_ERROR, Ordering::SeqCst);
                return Err(VpnError::AdapterCreate(msg));
            }
        };

        // Configure IP address using netsh
        let ip_str = format!("{}/32", ip);
        configure_adapter_ip(&ip_str)?;

        // Set DNS using netsh
        if !config.dns.is_empty() {
            configure_adapter_dns(&config.dns)?;
        }

        // Create session
        let session = match adapter.start_session(wintun::MAX_RING_CAPACITY) {
            Ok(s) => Arc::new(s),
            Err(e) => {
                let msg = format!("Failed to start adapter session: {}", e);
                *self.error_msg.lock() = Some(msg.clone());
                self.state_code.store(STATE_ERROR, Ordering::SeqCst);
                return Err(VpnError::AdapterCreate(msg));
            }
        };

        // Create WireGuard tunnel
        self.state_code.store(STATE_CONNECTING, Ordering::SeqCst);
        log::info!("Creating WireGuard tunnel to {}", endpoint);

        let tunn = match Tunn::new(
            private_key,
            server_public_key,
            None, // No preshared key
            Some(25), // Keepalive interval
            0,    // Index
            None, // Rate limiter
        ) {
            Ok(t) => t,
            Err(e) => {
                let msg = format!("Failed to create tunnel: {:?}", e);
                *self.error_msg.lock() = Some(msg.clone());
                self.state_code.store(STATE_ERROR, Ordering::SeqCst);
                return Err(VpnError::TunnelInit(msg));
            }
        };

        // Create UDP socket
        let socket = match UdpSocket::bind("0.0.0.0:0") {
            Ok(s) => {
                s.connect(endpoint).map_err(|e| {
                    let msg = format!("Failed to connect socket: {}", e);
                    *self.error_msg.lock() = Some(msg.clone());
                    self.state_code.store(STATE_ERROR, Ordering::SeqCst);
                    VpnError::Connection(msg)
                })?;
                s.set_nonblocking(true).ok();
                Arc::new(s)
            }
            Err(e) => {
                let msg = format!("Failed to create socket: {}", e);
                *self.error_msg.lock() = Some(msg.clone());
                self.state_code.store(STATE_ERROR, Ordering::SeqCst);
                return Err(VpnError::Connection(msg));
            }
        };

        // Store connection info
        *self.connection_info.lock() = Some(ConnectionInfo {
            region: config.region.clone(),
            endpoint: config.endpoint.clone(),
            assigned_ip: config.assigned_ip.clone(),
            connected_at: Instant::now(),
        });

        // Start tunnel thread
        let state_code = Arc::clone(&self.state_code);
        let stop_flag = Arc::clone(&self.stop_flag);
        let error_msg = Arc::clone(&self.error_msg);

        let tunnel_thread = thread::spawn(move || {
            run_tunnel(Box::new(tunn), socket, session, state_code, stop_flag, error_msg);
        });

        self.tunnel_thread = Some(tunnel_thread);

        // Wait for handshake
        let handshake_start = Instant::now();
        while handshake_start.elapsed() < Duration::from_secs(HANDSHAKE_TIMEOUT_SECS) {
            if self.state_code.load(Ordering::SeqCst) == STATE_CONNECTED {
                log::info!("VPN connected successfully");
                return Ok(());
            }
            if self.state_code.load(Ordering::SeqCst) == STATE_ERROR {
                let err = self.error_msg.lock().clone().unwrap_or_else(|| "Unknown error".to_string());
                return Err(VpnError::Connection(err));
            }
            thread::sleep(Duration::from_millis(100));
        }

        // Timeout
        self.stop_flag.store(true, Ordering::SeqCst);
        let msg = "Handshake timeout".to_string();
        *self.error_msg.lock() = Some(msg.clone());
        self.state_code.store(STATE_ERROR, Ordering::SeqCst);
        Err(VpnError::HandshakeFailed(msg))
    }

    pub fn disconnect_sync(&mut self) -> Result<(), VpnError> {
        if self.state_code.load(Ordering::SeqCst) == STATE_DISCONNECTED {
            return Ok(());
        }

        self.state_code.store(STATE_DISCONNECTING, Ordering::SeqCst);
        self.stop_flag.store(true, Ordering::SeqCst);

        // Wait for tunnel thread to stop
        if let Some(thread) = self.tunnel_thread.take() {
            let _ = thread.join();
        }

        *self.connection_info.lock() = None;
        self.state_code.store(STATE_DISCONNECTED, Ordering::SeqCst);

        Ok(())
    }
}

impl Drop for VpnManager {
    fn drop(&mut self) {
        self.stop_flag.store(true, Ordering::SeqCst);
        if let Some(thread) = self.tunnel_thread.take() {
            let _ = thread.join();
        }
    }
}

/// Run the tunnel loop
fn run_tunnel(
    mut tunn: Box<Tunn>,
    socket: Arc<UdpSocket>,
    session: Arc<wintun::Session>,
    state_code: Arc<AtomicI32>,
    stop_flag: Arc<AtomicBool>,
    error_msg: Arc<Mutex<Option<String>>>,
) {
    let mut buf = vec![0u8; MAX_PACKET_SIZE];
    let mut handshake_done = false;
    let tick_interval = Duration::from_millis(TICK_INTERVAL_MS);
    let mut last_tick = Instant::now();

    // Initiate handshake
    match tunn.format_handshake_initiation(&mut buf, false) {
        TunnResult::WriteToNetwork(packet) => {
            if let Err(e) = socket.send(packet) {
                log::error!("Failed to send handshake: {}", e);
            }
        }
        _ => {}
    }

    loop {
        if stop_flag.load(Ordering::SeqCst) {
            break;
        }

        // Process timer ticks
        if last_tick.elapsed() >= tick_interval {
            last_tick = Instant::now();
            match tunn.update_timers(&mut buf) {
                TunnResult::WriteToNetwork(packet) => {
                    let _ = socket.send(packet);
                }
                TunnResult::Err(e) => {
                    log::warn!("Timer error: {:?}", e);
                }
                _ => {}
            }
        }

        // Read from network (VPN server)
        let mut network_buf = [0u8; MAX_PACKET_SIZE];
        match socket.recv(&mut network_buf) {
            Ok(n) if n > 0 => {
                let mut decrypt_buf = vec![0u8; MAX_PACKET_SIZE];
                match tunn.decapsulate(None, &network_buf[..n], &mut decrypt_buf) {
                    TunnResult::WriteToTunnelV4(packet, _) | TunnResult::WriteToTunnelV6(packet, _) => {
                        // Write decrypted packet to Wintun
                        if let Ok(mut write_pack) = session.allocate_send_packet(packet.len() as u16) {
                            write_pack.bytes_mut().copy_from_slice(packet);
                            session.send_packet(write_pack);
                        }
                    }
                    TunnResult::WriteToNetwork(response) => {
                        let _ = socket.send(response);
                    }
                    TunnResult::Done => {
                        // Handshake complete
                        if !handshake_done {
                            handshake_done = true;
                            state_code.store(STATE_CONNECTED, Ordering::SeqCst);
                            log::info!("WireGuard handshake complete");
                        }
                    }
                    TunnResult::Err(e) => {
                        log::warn!("Decapsulate error: {:?}", e);
                    }
                }
            }
            Err(ref e) if e.kind() == std::io::ErrorKind::WouldBlock => {
                // No data available, continue
            }
            Err(e) => {
                log::warn!("Socket recv error: {}", e);
            }
            _ => {}
        }

        // Read from Wintun (local applications)
        match session.receive_blocking() {
            Ok(packet) => {
                let data = packet.bytes();
                let mut encrypt_buf = vec![0u8; MAX_PACKET_SIZE];
                match tunn.encapsulate(data, &mut encrypt_buf) {
                    TunnResult::WriteToNetwork(encrypted) => {
                        let _ = socket.send(encrypted);
                    }
                    TunnResult::Err(e) => {
                        log::warn!("Encapsulate error: {:?}", e);
                    }
                    _ => {}
                }
            }
            Err(e) => {
                if !stop_flag.load(Ordering::SeqCst) {
                    log::warn!("Wintun recv error: {}", e);
                }
                break;
            }
        }

        // Small sleep to prevent busy loop
        thread::sleep(Duration::from_micros(100));
    }

    log::info!("Tunnel thread exiting");
}

fn parse_private_key(key_base64: &str) -> Result<StaticSecret, VpnError> {
    let bytes = BASE64.decode(key_base64)
        .map_err(|e| VpnError::InvalidConfig(format!("Invalid private key: {}", e)))?;

    if bytes.len() != 32 {
        return Err(VpnError::InvalidConfig("Private key must be 32 bytes".to_string()));
    }

    let mut arr = [0u8; 32];
    arr.copy_from_slice(&bytes);
    Ok(StaticSecret::from(arr))
}

fn parse_public_key(key_base64: &str) -> Result<PublicKey, VpnError> {
    let bytes = BASE64.decode(key_base64)
        .map_err(|e| VpnError::InvalidConfig(format!("Invalid public key: {}", e)))?;

    if bytes.len() != 32 {
        return Err(VpnError::InvalidConfig("Public key must be 32 bytes".to_string()));
    }

    let mut arr = [0u8; 32];
    arr.copy_from_slice(&bytes);
    Ok(PublicKey::from(arr))
}

fn parse_ip_cidr(ip_cidr: &str) -> Result<(Ipv4Addr, u8), VpnError> {
    let parts: Vec<&str> = ip_cidr.split('/').collect();
    if parts.len() != 2 {
        return Err(VpnError::InvalidConfig(format!("Invalid IP/CIDR: {}", ip_cidr)));
    }

    let ip: Ipv4Addr = parts[0].parse()
        .map_err(|e| VpnError::InvalidConfig(format!("Invalid IP: {}", e)))?;
    let cidr: u8 = parts[1].parse()
        .map_err(|e| VpnError::InvalidConfig(format!("Invalid CIDR: {}", e)))?;

    Ok((ip, cidr))
}

fn configure_adapter_ip(ip_cidr: &str) -> Result<(), VpnError> {
    let output = std::process::Command::new("netsh")
        .args([
            "interface", "ip", "set", "address",
            "name=SwiftTunnel",
            "source=static",
            &format!("addr={}", ip_cidr.split('/').next().unwrap_or("")),
            "mask=255.255.255.255",
            "gateway=none",
        ])
        .output()
        .map_err(|e| VpnError::AdapterCreate(format!("Failed to run netsh: {}", e)))?;

    if !output.status.success() {
        let stderr = String::from_utf8_lossy(&output.stderr);
        log::warn!("netsh IP config warning: {}", stderr);
        // Don't fail on netsh errors - the adapter might still work
    }

    Ok(())
}

fn configure_adapter_dns(dns_servers: &[String]) -> Result<(), VpnError> {
    if dns_servers.is_empty() {
        return Ok(());
    }

    // Set primary DNS
    let output = std::process::Command::new("netsh")
        .args([
            "interface", "ip", "set", "dns",
            "name=SwiftTunnel",
            "source=static",
            &format!("addr={}", dns_servers[0]),
            "validate=no",
        ])
        .output()
        .map_err(|e| VpnError::AdapterCreate(format!("Failed to run netsh: {}", e)))?;

    if !output.status.success() {
        let stderr = String::from_utf8_lossy(&output.stderr);
        log::warn!("netsh DNS config warning: {}", stderr);
    }

    // Add secondary DNS if available
    if dns_servers.len() > 1 {
        let _ = std::process::Command::new("netsh")
            .args([
                "interface", "ip", "add", "dns",
                "name=SwiftTunnel",
                &format!("addr={}", dns_servers[1]),
                "index=2",
                "validate=no",
            ])
            .output();
    }

    Ok(())
}
