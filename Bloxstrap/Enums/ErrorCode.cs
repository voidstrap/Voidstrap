namespace Hellstrap.Enums
{
    /// <summary>
    /// Represents common error codes used within the Hellstrap application.
    /// References:
    /// - https://learn.microsoft.com/en-us/windows/win32/msi/error-codes
    /// - https://i-logic.com/serial/errorcodes.htm
    /// - https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/705fb797-2175-4a90-b5a3-3918024b10b8
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>Operation completed successfully.</summary>
        ERROR_SUCCESS = 0,

        /// <summary>Incorrect function was called or an invalid function was used.</summary>
        ERROR_INVALID_FUNCTION = 1,

        /// <summary>The system cannot find the file specified.</summary>
        ERROR_FILE_NOT_FOUND = 2,

        /// <summary>The operation was canceled by the user.</summary>
        ERROR_CANCELLED = 1223,

        /// <summary>The installation was canceled by the user.</summary>
        ERROR_INSTALL_USEREXIT = 1602,

        /// <summary>Fatal error occurred during installation.</summary>
        ERROR_INSTALL_FAILURE = 1603,

        /// <summary>Application not found.</summary>
        CO_E_APPNOTFOUND = -2147221003
    }
}
