using System;
using System.Runtime.InteropServices;
using System.Windows.Shell;

namespace Voidstrap.UI.Utility
{
    internal static class TaskbarProgress
    {
        private enum TaskbarStates
        {
            NoProgress = 0,
            Indeterminate = 0x1,
            Normal = 0x2,
            Error = 0x4,
            Paused = 0x8,
        }

        [ComImport]
        [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            void HrInit();
            void AddTab(IntPtr hwnd);
            void DeleteTab(IntPtr hwnd);
            void ActivateTab(IntPtr hwnd);
            void SetActiveAlt(IntPtr hwnd);
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
            void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
            void SetProgressState(IntPtr hwnd, TaskbarStates state);
        }

        [ComImport]
        [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
        [ClassInterface(ClassInterfaceType.None)]
        private class TaskbarInstance { }

        private static readonly object _lock = new();
        private static ITaskbarList3? _taskbar;

        private static ITaskbarList3 GetTaskbar()
        {
            lock (_lock)
            {
                if (_taskbar == null)
                {
                    _taskbar = (ITaskbarList3)new TaskbarInstance();
                    _taskbar.HrInit();
                }
                return _taskbar;
            }
        }

        private static TaskbarStates ConvertEnum(TaskbarItemProgressState state) => state switch
        {
            TaskbarItemProgressState.None => TaskbarStates.NoProgress,
            TaskbarItemProgressState.Indeterminate => TaskbarStates.Indeterminate,
            TaskbarItemProgressState.Normal => TaskbarStates.Normal,
            TaskbarItemProgressState.Error => TaskbarStates.Error,
            TaskbarItemProgressState.Paused => TaskbarStates.Paused,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown TaskbarItemProgressState")
        };

        /// <summary>
        /// Sets the taskbar progress state (e.g. Normal, Error, Paused).
        /// </summary>
        public static void SetProgressState(IntPtr windowHandle, TaskbarItemProgressState state)
        {
            GetTaskbar().SetProgressState(windowHandle, ConvertEnum(state));
        }

        /// <summary>
        /// Sets the progress value shown in the taskbar.
        /// </summary>
        public static void SetProgressValue(IntPtr windowHandle, int value, int maximum)
        {
            GetTaskbar().SetProgressValue(windowHandle, (ulong)value, (ulong)maximum);
        }

        /// <summary>
        /// Releases COM resources. Call this on shutdown.
        /// </summary>
        public static void Dispose()
        {
            lock (_lock)
            {
                if (_taskbar != null)
                {
                    Marshal.ReleaseComObject(_taskbar);
                    _taskbar = null;
                }
            }
        }
    }
}
