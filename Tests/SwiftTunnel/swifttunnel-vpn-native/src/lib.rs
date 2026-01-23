//! SwiftTunnel VPN Native Library
//!
//! This library provides a C ABI interface for the SwiftTunnel VPN functionality,
//! allowing integration with external applications like Voidstrap (C#/.NET).
//!
//! # Safety
//! This library uses `unsafe` for FFI boundary. Callers must ensure:
//! - Pointers passed are valid and properly aligned
//! - Strings are null-terminated UTF-8
//! - Returned strings are freed using `swifttunnel_free_string`

use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::sync::atomic::{AtomicI32, Ordering};
use once_cell::sync::Lazy;
use parking_lot::Mutex;
use serde::{Deserialize, Serialize};

mod vpn;
mod error;
mod split_tunnel;
mod wfp;

use vpn::VpnManager;
#[allow(unused_imports)]
use error::VpnError;
use split_tunnel::{SplitTunnelDriver, SplitTunnelConfig, get_default_tunnel_apps};
#[allow(unused_imports)]
use wfp::{WfpEngine, setup_wfp_for_split_tunnel};

// Global VPN manager instance
static VPN_MANAGER: Lazy<Mutex<Option<VpnManager>>> = Lazy::new(|| Mutex::new(None));

// Global split tunnel driver instance (route-based, ZERO overhead)
static SPLIT_TUNNEL: Lazy<Mutex<Option<SplitTunnelDriver>>> = Lazy::new(|| Mutex::new(None));

// Global error message
static LAST_ERROR: Lazy<Mutex<Option<String>>> = Lazy::new(|| Mutex::new(None));

// Connection state codes
const STATE_DISCONNECTED: i32 = 0;
const STATE_FETCHING_CONFIG: i32 = 1;
const STATE_CREATING_ADAPTER: i32 = 2;
const STATE_CONNECTING: i32 = 3;
const STATE_CONFIGURING_SPLIT_TUNNEL: i32 = 4;
const STATE_CONNECTED: i32 = 5;
const STATE_DISCONNECTING: i32 = 6;
const STATE_ERROR: i32 = -1;

// Return codes
const SUCCESS: i32 = 0;
const ERROR_INVALID_PARAM: i32 = -1;
const ERROR_NOT_INITIALIZED: i32 = -2;
const ERROR_ALREADY_CONNECTED: i32 = -3;
const ERROR_NOT_CONNECTED: i32 = -4;
const ERROR_INTERNAL: i32 = -5;

/// Configuration passed from C# to connect
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ConnectConfig {
    pub access_token: String,
    pub region: String,
    pub endpoint: String,
    pub server_public_key: String,
    pub private_key: String,
    pub public_key: String,
    pub assigned_ip: String,
    pub dns: Vec<String>,
    pub split_tunnel_apps: Vec<String>,
}

fn set_error(msg: String) {
    *LAST_ERROR.lock() = Some(msg);
}

fn clear_error() {
    *LAST_ERROR.lock() = None;
}

/// Initialize the VPN library
/// Must be called before any other function
/// Returns: 0 on success, negative error code on failure
#[no_mangle]
pub extern "C" fn swifttunnel_init() -> i32 {
    // Initialize logging
    let _ = env_logger::try_init();

    log::info!("SwiftTunnel VPN Native Library initializing...");

    let mut manager = VPN_MANAGER.lock();
    if manager.is_some() {
        log::warn!("Already initialized");
        return SUCCESS;
    }

    *manager = Some(VpnManager::new());
    clear_error();

    log::info!("SwiftTunnel VPN Native Library initialized");
    SUCCESS
}

/// Cleanup the VPN library
/// Should be called when done using the library
#[no_mangle]
pub extern "C" fn swifttunnel_cleanup() {
    log::info!("SwiftTunnel VPN Native Library cleanup");

    let mut manager = VPN_MANAGER.lock();
    if let Some(ref mut mgr) = *manager {
        mgr.disconnect_sync();
    }
    *manager = None;
}

/// Connect to VPN server
///
/// # Arguments
/// * `config_json` - JSON string containing ConnectConfig
///
/// # Returns
/// 0 on success, negative error code on failure
#[no_mangle]
pub extern "C" fn swifttunnel_connect(config_json: *const c_char) -> i32 {
    clear_error();

    if config_json.is_null() {
        set_error("Config JSON is null".to_string());
        return ERROR_INVALID_PARAM;
    }

    let config_str = unsafe {
        match CStr::from_ptr(config_json).to_str() {
            Ok(s) => s,
            Err(e) => {
                set_error(format!("Invalid UTF-8 in config: {}", e));
                return ERROR_INVALID_PARAM;
            }
        }
    };

    let config: ConnectConfig = match serde_json::from_str(config_str) {
        Ok(c) => c,
        Err(e) => {
            set_error(format!("Failed to parse config JSON: {}", e));
            return ERROR_INVALID_PARAM;
        }
    };

    let mut manager = VPN_MANAGER.lock();
    let mgr = match manager.as_mut() {
        Some(m) => m,
        None => {
            set_error("Not initialized - call swifttunnel_init first".to_string());
            return ERROR_NOT_INITIALIZED;
        }
    };

    if mgr.is_connected() {
        set_error("Already connected".to_string());
        return ERROR_ALREADY_CONNECTED;
    }

    log::info!("Connecting to {} ({})...", config.region, config.endpoint);

    match mgr.connect(config) {
        Ok(()) => {
            log::info!("Connected successfully");
            SUCCESS
        }
        Err(e) => {
            set_error(e.to_string());
            log::error!("Connection failed: {}", e);
            ERROR_INTERNAL
        }
    }
}

/// Disconnect from VPN
///
/// # Returns
/// 0 on success, negative error code on failure
#[no_mangle]
pub extern "C" fn swifttunnel_disconnect() -> i32 {
    clear_error();

    let mut manager = VPN_MANAGER.lock();
    let mgr = match manager.as_mut() {
        Some(m) => m,
        None => {
            set_error("Not initialized".to_string());
            return ERROR_NOT_INITIALIZED;
        }
    };

    if !mgr.is_connected() && !mgr.is_connecting() {
        // Not an error, just a no-op
        return SUCCESS;
    }

    log::info!("Disconnecting...");

    match mgr.disconnect_sync() {
        Ok(()) => {
            log::info!("Disconnected successfully");
            SUCCESS
        }
        Err(e) => {
            set_error(e.to_string());
            log::error!("Disconnect failed: {}", e);
            ERROR_INTERNAL
        }
    }
}

/// Get current connection state
///
/// # Returns
/// State code (see STATE_* constants)
#[no_mangle]
pub extern "C" fn swifttunnel_get_state() -> i32 {
    let manager = VPN_MANAGER.lock();
    match manager.as_ref() {
        Some(mgr) => mgr.get_state_code(),
        None => STATE_DISCONNECTED,
    }
}

/// Get state as JSON string with detailed information
/// Caller must free the returned string with swifttunnel_free_string
///
/// # Returns
/// JSON string or null on error
#[no_mangle]
pub extern "C" fn swifttunnel_get_state_json() -> *mut c_char {
    let manager = VPN_MANAGER.lock();
    let json = match manager.as_ref() {
        Some(mgr) => mgr.get_state_json(),
        None => r#"{"state":"disconnected","code":0}"#.to_string(),
    };

    match CString::new(json) {
        Ok(s) => s.into_raw(),
        Err(_) => std::ptr::null_mut(),
    }
}

/// Get last error message
/// Caller must free the returned string with swifttunnel_free_string
///
/// # Returns
/// Error message string or null if no error
#[no_mangle]
pub extern "C" fn swifttunnel_get_error() -> *mut c_char {
    let error = LAST_ERROR.lock();
    match error.as_ref() {
        Some(msg) => {
            match CString::new(msg.clone()) {
                Ok(s) => s.into_raw(),
                Err(_) => std::ptr::null_mut(),
            }
        }
        None => std::ptr::null_mut(),
    }
}

/// Free a string returned by this library
#[no_mangle]
pub extern "C" fn swifttunnel_free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
    }
}

/// Check if VPN is connected
///
/// # Returns
/// 1 if connected, 0 otherwise
#[no_mangle]
pub extern "C" fn swifttunnel_is_connected() -> i32 {
    let manager = VPN_MANAGER.lock();
    match manager.as_ref() {
        Some(mgr) if mgr.is_connected() => 1,
        _ => 0,
    }
}

/// Check if WireGuard/Wintun is available on this system
///
/// # Returns
/// 1 if available, 0 otherwise
#[no_mangle]
pub extern "C" fn swifttunnel_is_available() -> i32 {
    // Check if wintun.dll exists in the current directory or system path
    if std::path::Path::new("wintun.dll").exists() {
        return 1;
    }

    // Check in AppData\Local\Voidstrap\SwiftTunnel
    if let Some(local_app_data) = std::env::var_os("LOCALAPPDATA") {
        let wintun_path = std::path::PathBuf::from(local_app_data)
            .join("Voidstrap")
            .join("SwiftTunnel")
            .join("wintun.dll");
        if wintun_path.exists() {
            return 1;
        }
    }

    0
}

// ═══════════════════════════════════════════════════════════════════════════════
//  SPLIT TUNNEL API (Route-Based - ZERO Overhead)
// ═══════════════════════════════════════════════════════════════════════════════

/// Check if split tunneling is available
///
/// Route-based split tunnel is ALWAYS available (uses built-in Windows routing).
/// No kernel driver required!
///
/// # Returns
/// 1 if available (always), 0 otherwise
#[no_mangle]
pub extern "C" fn swifttunnel_split_tunnel_available() -> i32 {
    // Route-based split tunnel is always available
    1
}

/// Configure and enable split tunneling (route-based, ZERO overhead)
///
/// This uses the Windows routing table to route game traffic through VPN.
/// NO kernel driver required, NO packet interception, ZERO latency overhead!
///
/// # Arguments
/// * `tunnel_ip` - The VPN tunnel IP address (e.g., "10.0.0.77")
/// * `interface_luid` - The Wintun adapter LUID
/// * `apps_json` - JSON array of app names (for process detection only, not routing)
///                 Pass null or "[]" to use default Roblox apps
///
/// # Returns
/// 0 on success, negative error code on failure
#[no_mangle]
pub extern "C" fn swifttunnel_split_tunnel_configure(
    tunnel_ip: *const c_char,
    interface_luid: u64,
    apps_json: *const c_char,
) -> i32 {
    clear_error();

    // Parse tunnel IP
    let ip_str = if tunnel_ip.is_null() {
        set_error("tunnel_ip is null".to_string());
        return ERROR_INVALID_PARAM;
    } else {
        unsafe {
            match CStr::from_ptr(tunnel_ip).to_str() {
                Ok(s) => s.to_string(),
                Err(e) => {
                    set_error(format!("Invalid UTF-8 in tunnel_ip: {}", e));
                    return ERROR_INVALID_PARAM;
                }
            }
        }
    };

    // Parse apps list (for process detection/notifications only)
    let apps: Vec<String> = if apps_json.is_null() {
        get_default_tunnel_apps()
    } else {
        let apps_str = unsafe {
            match CStr::from_ptr(apps_json).to_str() {
                Ok(s) => s,
                Err(e) => {
                    set_error(format!("Invalid UTF-8 in apps_json: {}", e));
                    return ERROR_INVALID_PARAM;
                }
            }
        };

        if apps_str.is_empty() || apps_str == "[]" {
            get_default_tunnel_apps()
        } else {
            match serde_json::from_str::<Vec<String>>(apps_str) {
                Ok(a) => a,
                Err(e) => {
                    set_error(format!("Invalid JSON in apps_json: {}", e));
                    return ERROR_INVALID_PARAM;
                }
            }
        }
    };

    log::info!("Configuring route-based split tunnel: IP={}, LUID={}", ip_str, interface_luid);

    // Configure split tunnel driver (route-based)
    let mut driver_guard = SPLIT_TUNNEL.lock();

    // Close existing driver if any
    if let Some(ref mut driver) = *driver_guard {
        let _ = driver.close();
    }

    let mut driver = SplitTunnelDriver::new();

    if let Err(e) = driver.open() {
        set_error(e.to_string());
        return ERROR_INTERNAL;
    }

    let config = SplitTunnelConfig::new(apps, ip_str, interface_luid);

    if let Err(e) = driver.configure(config) {
        set_error(e.to_string());
        let _ = driver.close();
        return ERROR_INTERNAL;
    }

    *driver_guard = Some(driver);
    log::info!("Route-based split tunnel configured - ZERO overhead!");
    SUCCESS
}

/// Refresh split tunnel process detection
/// Call this periodically to detect newly started Roblox processes
///
/// # Returns
/// JSON array of currently tunneled process names, or null on error
/// Caller must free with swifttunnel_free_string
#[no_mangle]
pub extern "C" fn swifttunnel_split_tunnel_refresh() -> *mut c_char {
    let mut driver_guard = SPLIT_TUNNEL.lock();

    let driver = match driver_guard.as_mut() {
        Some(d) => d,
        None => {
            return std::ptr::null_mut();
        }
    };

    match driver.refresh_processes() {
        Ok(names) => {
            let json = serde_json::to_string(&names).unwrap_or_else(|_| "[]".to_string());
            match CString::new(json) {
                Ok(s) => s.into_raw(),
                Err(_) => std::ptr::null_mut(),
            }
        }
        Err(e) => {
            log::warn!("Error refreshing processes: {}", e);
            std::ptr::null_mut()
        }
    }
}

/// Disable and close split tunneling (removes routes)
///
/// # Returns
/// 0 on success, negative error code on failure
#[no_mangle]
pub extern "C" fn swifttunnel_split_tunnel_close() -> i32 {
    clear_error();

    // Close split tunnel driver (removes routes)
    {
        let mut driver_guard = SPLIT_TUNNEL.lock();
        if let Some(ref mut driver) = *driver_guard {
            if let Err(e) = driver.close() {
                log::warn!("Error closing split tunnel: {}", e);
            }
        }
        *driver_guard = None;
    }

    log::info!("Route-based split tunnel closed");
    SUCCESS
}

/// Get default apps that will be tunneled (Roblox processes)
/// Returns JSON array of app names
/// Caller must free with swifttunnel_free_string
#[no_mangle]
pub extern "C" fn swifttunnel_split_tunnel_get_default_apps() -> *mut c_char {
    let apps = get_default_tunnel_apps();
    let json = serde_json::to_string(&apps).unwrap_or_else(|_| "[]".to_string());

    match CString::new(json) {
        Ok(s) => s.into_raw(),
        Err(_) => std::ptr::null_mut(),
    }
}
