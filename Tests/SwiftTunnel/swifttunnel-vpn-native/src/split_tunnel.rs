//! Route-Based Split Tunnel Driver
//!
//! Uses Windows routing table for ZERO-OVERHEAD split tunneling.
//! Game traffic is routed through the VPN via kernel routing - no packet
//! interception, no process lookup, no latency overhead.
//!
//! How it works:
//! 1. On configure(), adds routes for game server IPs through VPN interface
//! 2. Kernel routes game packets through VPN (ZERO userspace overhead!)
//! 3. Non-game traffic has no routes = bypasses VPN naturally
//! 4. On close(), removes all added routes
//!
//! This is the SAME approach used in SwiftTunnel-App v0.6.2+

use std::collections::HashSet;
use std::path::PathBuf;
use std::process::Command;
use sysinfo::{System, ProcessesToUpdate, ProcessRefreshKind};
use crate::error::VpnError;

// ═══════════════════════════════════════════════════════════════════════════════
//  GAME IP RANGES - Route-based split tunneling
// ═══════════════════════════════════════════════════════════════════════════════

/// Roblox IP ranges for route-based split tunneling
/// These cover all Roblox game servers globally
pub const ROBLOX_IP_RANGES: &[&str] = &[
    "128.116.0.0/17",     // Main Roblox range
    "209.206.40.0/21",
    "23.173.192.0/24",
    "141.193.3.0/24",
    "204.9.184.0/24",
    "204.13.168.0/24",
    "204.13.169.0/24",
    "204.13.170.0/24",
    "204.13.171.0/24",
    "204.13.172.0/24",
    "204.13.173.0/24",
    "205.201.62.0/24",
    "103.140.28.0/23",
    "103.142.220.0/24",
    "103.142.221.0/24",
    "23.34.81.0/24",
    "23.214.169.0/24",
];

/// Default apps to tunnel through VPN (Roblox)
/// Used for process detection notifications only (not for routing)
pub const DEFAULT_TUNNEL_APPS: &[&str] = &[
    "robloxplayerbeta.exe",
    "robloxplayerlauncher.exe",
    "robloxstudiobeta.exe",
    "robloxstudiolauncherbeta.exe",
    "robloxstudiolauncher.exe",
];

// ═══════════════════════════════════════════════════════════════════════════════
//  SPLIT TUNNEL CONFIGURATION
// ═══════════════════════════════════════════════════════════════════════════════

/// Split tunnel configuration
#[derive(Debug, Clone)]
pub struct SplitTunnelConfig {
    /// Apps that SHOULD use VPN - stored lowercase (for process detection only)
    pub tunnel_apps: HashSet<String>,
    /// VPN tunnel IP address
    pub tunnel_ip: String,
    /// VPN interface LUID (used to get interface index)
    pub tunnel_interface_luid: u64,
}

impl SplitTunnelConfig {
    /// Create new config from app list
    pub fn new(apps: Vec<String>, tunnel_ip: String, tunnel_interface_luid: u64) -> Self {
        Self {
            tunnel_apps: apps.into_iter().map(|s| s.to_lowercase()).collect(),
            tunnel_ip,
            tunnel_interface_luid,
        }
    }

    /// Legacy compatibility - convert to include_apps format
    pub fn include_apps(&self) -> Vec<String> {
        self.tunnel_apps.iter().cloned().collect()
    }
}

/// Driver state
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum DriverState {
    NotAvailable,
    NotConfigured,
    Active,
    Error(String),
}

// ═══════════════════════════════════════════════════════════════════════════════
//  ROUTE-BASED SPLIT TUNNEL DRIVER
// ═══════════════════════════════════════════════════════════════════════════════

/// Route-based split tunnel driver
///
/// Uses Windows routing table instead of kernel driver for ZERO overhead.
pub struct SplitTunnelDriver {
    /// Current configuration
    pub config: Option<SplitTunnelConfig>,
    /// Driver state
    state: DriverState,
    /// VPN interface index (from LUID)
    interface_index: Option<u32>,
    /// Routes added (CIDR strings for cleanup)
    added_routes: Vec<String>,
    /// System info for process detection
    system: System,
}

unsafe impl Send for SplitTunnelDriver {}
unsafe impl Sync for SplitTunnelDriver {}

impl SplitTunnelDriver {
    pub fn new() -> Self {
        Self {
            config: None,
            state: DriverState::NotConfigured,
            interface_index: None,
            added_routes: Vec::new(),
            system: System::new(),
        }
    }

    /// Route-based split tunnel is always available (uses built-in Windows routing)
    pub fn is_available() -> bool {
        true
    }

    /// Cleanup stale state on startup (no-op for route-based approach)
    pub fn cleanup_stale_state() {
        log::debug!("Route-based split tunnel: no stale state to clean");
    }

    /// Open driver connection (no-op for route-based approach)
    pub fn open(&mut self) -> Result<(), VpnError> {
        self.state = DriverState::NotConfigured;
        log::info!("Route-based split tunnel ready");
        Ok(())
    }

    /// Get interface index from LUID using PowerShell
    fn get_interface_index_from_luid(luid: u64) -> Result<u32, VpnError> {
        // Try to get interface index using PowerShell
        let ps_script = format!(
            r#"
            $adapters = Get-NetAdapter | Where-Object {{ $_.InterfaceDescription -like '*Wintun*' -or $_.InterfaceDescription -like '*SwiftTunnel*' }}
            if ($adapters) {{
                $adapters | Select-Object -First 1 -ExpandProperty ifIndex
            }}
            "#
        );

        let output = Command::new("powershell")
            .args(["-NoProfile", "-Command", &ps_script])
            .output()
            .map_err(|e| VpnError::Route(format!("Failed to run PowerShell: {}", e)))?;

        if output.status.success() {
            let index_str = String::from_utf8_lossy(&output.stdout).trim().to_string();
            if !index_str.is_empty() {
                if let Ok(index) = index_str.parse::<u32>() {
                    log::info!("Found VPN interface index: {}", index);
                    return Ok(index);
                }
            }
        }

        // Fallback: try by name
        let output = Command::new("powershell")
            .args([
                "-NoProfile",
                "-Command",
                "(Get-NetAdapter -Name 'SwiftTunnel' -ErrorAction SilentlyContinue).ifIndex",
            ])
            .output()
            .map_err(|e| VpnError::Route(format!("Failed to get interface index: {}", e)))?;

        if output.status.success() {
            let index_str = String::from_utf8_lossy(&output.stdout).trim().to_string();
            if !index_str.is_empty() {
                if let Ok(index) = index_str.parse::<u32>() {
                    log::info!("Found VPN interface by name, index: {}", index);
                    return Ok(index);
                }
            }
        }

        Err(VpnError::Route("Could not find VPN interface index".to_string()))
    }

    /// Add a single CIDR route through the VPN interface
    fn add_cidr_route(&self, cidr: &str, interface_index: u32) -> Result<(), VpnError> {
        // Parse CIDR notation (e.g., "128.116.0.0/17")
        let parts: Vec<&str> = cidr.split('/').collect();
        if parts.len() != 2 {
            return Err(VpnError::Route(format!("Invalid CIDR: {}", cidr)));
        }

        let network = parts[0];
        let prefix_len: u8 = parts[1].parse()
            .map_err(|_| VpnError::Route(format!("Invalid prefix length: {}", parts[1])))?;

        // Convert prefix length to subnet mask
        let mask = if prefix_len == 0 {
            0u32
        } else {
            !((1u32 << (32 - prefix_len)) - 1)
        };
        let mask_str = format!(
            "{}.{}.{}.{}",
            (mask >> 24) & 0xFF,
            (mask >> 16) & 0xFF,
            (mask >> 8) & 0xFF,
            mask & 0xFF
        );

        // Add route through VPN interface
        let output = Command::new("route")
            .args([
                "add",
                network,
                "mask",
                &mask_str,
                "10.0.0.1",  // VPN internal gateway
                "metric",
                "5",
                "if",
                &interface_index.to_string(),
            ])
            .output()
            .map_err(|e| VpnError::Route(format!("Failed to add route: {}", e)))?;

        if output.status.success() {
            log::debug!("Added route: {} -> VPN (if {})", cidr, interface_index);
            Ok(())
        } else {
            let stderr = String::from_utf8_lossy(&output.stderr);
            // Route might already exist - that's OK
            if stderr.contains("already exists") || stderr.contains("object already exists") {
                log::debug!("Route already exists: {}", cidr);
                Ok(())
            } else {
                log::warn!("Failed to add route {}: {}", cidr, stderr);
                Err(VpnError::Route(format!("Failed to add route {}", cidr)))
            }
        }
    }

    /// Remove a CIDR route
    fn remove_cidr_route(&self, cidr: &str) {
        let parts: Vec<&str> = cidr.split('/').collect();
        if parts.is_empty() {
            return;
        }
        let network = parts[0];

        let _ = Command::new("route")
            .args(["delete", network])
            .output();
    }

    /// Configure split tunnel with route-based approach
    pub fn configure(&mut self, config: SplitTunnelConfig) -> Result<(), VpnError> {
        log::info!(
            "Configuring route-based split tunnel - {} game IP ranges",
            ROBLOX_IP_RANGES.len()
        );

        // Get interface index from LUID
        let interface_index = Self::get_interface_index_from_luid(config.tunnel_interface_luid)?;
        self.interface_index = Some(interface_index);

        // Add routes for all Roblox IP ranges
        let mut added_count = 0;
        for cidr in ROBLOX_IP_RANGES {
            if self.add_cidr_route(cidr, interface_index).is_ok() {
                self.added_routes.push(cidr.to_string());
                added_count += 1;
            }
        }

        log::info!(
            "Added {}/{} routes for Roblox (route-based split tunnel)",
            added_count,
            ROBLOX_IP_RANGES.len()
        );

        self.config = Some(config);
        self.state = DriverState::Active;

        log::info!("Route-based split tunnel configured - ZERO overhead!");
        Ok(())
    }

    /// Get names of currently running tunnel apps (for UI/notifications)
    pub fn get_running_tunnel_apps(&mut self) -> Vec<String> {
        let Some(config) = &self.config else {
            return Vec::new();
        };

        self.system.refresh_processes_specifics(
            ProcessesToUpdate::All,
            true,
            ProcessRefreshKind::new(),
        );

        let mut running = Vec::new();
        for (_pid, process) in self.system.processes() {
            let name = process.name().to_string_lossy().to_lowercase();
            if config.tunnel_apps.contains(&name) {
                running.push(process.name().to_string_lossy().to_string());
            }
        }
        running
    }

    /// Refresh process detection (returns running tunnel apps)
    /// Note: Route-based split tunnel doesn't need process refresh for routing,
    /// this is only for UI notifications about running games.
    pub fn refresh_processes(&mut self) -> Result<Vec<String>, VpnError> {
        Ok(self.get_running_tunnel_apps())
    }

    /// Clear configuration (removes routes)
    pub fn clear(&mut self) -> Result<(), VpnError> {
        log::info!("Removing {} game routes", self.added_routes.len());

        for cidr in &self.added_routes {
            self.remove_cidr_route(cidr);
        }

        self.added_routes.clear();
        self.config = None;
        self.interface_index = None;
        self.state = DriverState::NotConfigured;

        log::info!("Split tunnel routes cleared");
        Ok(())
    }

    /// Close the split tunnel driver
    pub fn close(&mut self) -> Result<(), VpnError> {
        self.clear()
    }

    /// Get driver state
    pub fn state(&self) -> &DriverState {
        &self.state
    }

    /// Get driver state for legacy compatibility
    pub fn get_driver_state(&self) -> Result<u64, VpnError> {
        // Return state codes compatible with the old driver:
        // 0 = NONE, 1 = STARTED, 2 = INITIALIZED, 3 = READY, 4 = ENGAGED
        match &self.state {
            DriverState::NotAvailable => Ok(0),
            DriverState::NotConfigured => Ok(1),
            DriverState::Active => Ok(4),  // ENGAGED
            DriverState::Error(_) => Ok(0),
        }
    }
}

impl Default for SplitTunnelDriver {
    fn default() -> Self {
        Self::new()
    }
}

impl Drop for SplitTunnelDriver {
    fn drop(&mut self) {
        let _ = self.close();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  LEGACY COMPATIBILITY
// ═══════════════════════════════════════════════════════════════════════════════

/// A running process that should be tunneled (legacy type for compatibility)
#[derive(Debug, Clone)]
pub struct TunneledProcess {
    pub pid: u32,
    pub parent_pid: u32,
    pub exe_path: String,
    pub name: String,
}

/// Get default apps to tunnel (Roblox processes)
pub fn get_default_tunnel_apps() -> Vec<String> {
    DEFAULT_TUNNEL_APPS.iter().map(|s| s.to_string()).collect()
}

/// Find Roblox installation path
pub fn find_roblox_path() -> Option<PathBuf> {
    let local = dirs::data_local_dir()?;
    let versions = local.join("Roblox").join("Versions");
    if versions.exists() {
        if let Ok(entries) = std::fs::read_dir(&versions) {
            for entry in entries.flatten() {
                let exe = entry.path().join("RobloxPlayerBeta.exe");
                if exe.exists() {
                    return Some(exe);
                }
            }
        }
    }
    None
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_driver_available() {
        // Route-based approach is always available
        assert!(SplitTunnelDriver::is_available());
    }

    #[test]
    fn test_config_lowercase() {
        let config = SplitTunnelConfig::new(
            vec!["RobloxPlayerBeta.exe".to_string()],
            "10.0.0.1".to_string(),
            0,
        );

        // Should be stored lowercase
        assert!(config.tunnel_apps.contains("robloxplayerbeta.exe"));
        assert!(!config.tunnel_apps.contains("RobloxPlayerBeta.exe"));
    }

    #[test]
    fn test_roblox_ip_ranges_valid() {
        // Verify all CIDR ranges are valid
        for cidr in ROBLOX_IP_RANGES {
            let parts: Vec<&str> = cidr.split('/').collect();
            assert_eq!(parts.len(), 2, "Invalid CIDR format: {}", cidr);

            // Verify network address is valid
            let network: Result<std::net::Ipv4Addr, _> = parts[0].parse();
            assert!(network.is_ok(), "Invalid network address in {}", cidr);

            // Verify prefix is valid
            let prefix: Result<u8, _> = parts[1].parse();
            assert!(prefix.is_ok(), "Invalid prefix in {}", cidr);
            assert!(prefix.unwrap() <= 32, "Prefix too large in {}", cidr);
        }
    }
}
