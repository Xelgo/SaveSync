using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SaveSync
{
    internal static class GameProcess
    {
        private const uint QueryLimitedInformation = 0x1000;
        private const int InvalidParameter = 87;

        // null means a process with the right name could not be inspected.
        public static bool? IsRunning(string exe)
        {
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe));
            bool unknown = false;
            try
            {
                foreach (Process process in processes)
                {
                    using (var handle = OpenProcess(QueryLimitedInformation, false, process.Id))
                    {
                        if (handle.IsInvalid)
                        {
                            // A process may have exited since the enumeration.
                            if (Marshal.GetLastWin32Error() != InvalidParameter)
                                unknown = true;
                            continue;
                        }

                        var image = new StringBuilder(32768);
                        int size = image.Capacity;
                        if (!QueryFullProcessImageName(handle, 0, image, ref size))
                            unknown = true;
                        else if (string.Equals(image.ToString(), exe, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
                return unknown ? (bool?)null : false;
            }
            finally
            {
                foreach (Process process in processes)
                    process.Dispose();
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(SafeProcessHandle process, int flags, StringBuilder name, ref int size);
    }
}
