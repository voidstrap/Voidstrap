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
            int HrInit();
            int AddTab(IntPtr hwnd);
            int DeleteTab(IntPtr hwnd);
            int ActivateTab(IntPtr hwnd);
            int SetActiveAlt(IntPtr hwnd);
            int MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
            int SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
            int SetProgressState(IntPtr hwnd, TaskbarStates state);
        }

        [ComImport]
        [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
        [ClassInterface(ClassInterfaceType.None)]
        private class TaskbarInstance { }

        private static readonly object _lock = new object();
        private static ITaskbarList3 _taskbar;

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

        public static int SetProgressState(IntPtr windowHandle, TaskbarItemProgressState taskbarState)
        {
            return GetTaskbar().SetProgressState(windowHandle, ConvertEnum(taskbarState));
        }

        public static int SetProgressValue(IntPtr windowHandle, int progressValue, int progressMax)
        {
            return GetTaskbar().SetProgressValue(windowHandle, (ulong)progressValue, (ulong)progressMax);
        }

        // Call this on application exit or when taskbar usage is no longer needed
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
