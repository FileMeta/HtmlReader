using System;
using System.Runtime.InteropServices;


namespace Win32Interop
{
    static class ConsoleHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetConsoleProcessList(
            uint[] ProcessList,
            uint ProcessCount
            );

        public static bool IsSoleConsoleOwner
        {
            get
            {
                uint[] procIds = new uint[4];
                uint count = GetConsoleProcessList(procIds, (uint)procIds.Length);
                return count <= 1;
            }
        }
    }
}
