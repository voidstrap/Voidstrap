//! Windows Filtering Platform (WFP) Integration - STUB
//!
//! This module is a STUB for backward compatibility.
//!
//! The route-based split tunnel approach (v0.6.2+) does NOT require WFP.
//! Game traffic is routed through kernel routing table, not packet filtering.
//!
//! This file is kept for API compatibility with existing code that imports
//! the WfpEngine type and setup_wfp_for_split_tunnel function.

use crate::error::VpnError;

/// WFP Engine handle wrapper - STUB for compatibility
pub struct WfpEngine {
    _private: (),
}

unsafe impl Send for WfpEngine {}
unsafe impl Sync for WfpEngine {}

impl WfpEngine {
    /// Open a new WFP engine session - STUB
    pub fn open() -> Result<Self, VpnError> {
        log::debug!("WFP not needed for route-based split tunnel");
        Ok(Self { _private: () })
    }

    /// Register as a WFP provider - STUB
    pub fn register_provider(&mut self) -> Result<(), VpnError> {
        Ok(())
    }

    /// Create the split tunnel sublayer - STUB
    pub fn create_sublayer(&mut self) -> Result<(), VpnError> {
        Ok(())
    }

    /// Close the WFP engine - STUB
    pub fn close(&mut self) {
        // No-op
    }

    pub fn is_ready(&self) -> bool {
        true
    }
}

impl Drop for WfpEngine {
    fn drop(&mut self) {
        self.close();
    }
}

/// WFP filter layers - kept for API compatibility
#[derive(Debug, Clone, Copy)]
pub enum FilterLayer {
    AleConnectV4,
    AleConnectV6,
    AleRecvAcceptV4,
    AleRecvAcceptV6,
}

/// Setup WFP for split tunneling - STUB
///
/// Route-based split tunnel doesn't need WFP. This function returns
/// a stub WfpEngine for backward compatibility.
pub fn setup_wfp_for_split_tunnel(_interface_luid: u64) -> Result<WfpEngine, VpnError> {
    log::info!("WFP not needed for route-based split tunnel (ZERO overhead mode)");
    WfpEngine::open()
}
