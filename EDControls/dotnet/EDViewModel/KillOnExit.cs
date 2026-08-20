using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EDControls.EDViewModel;

/// <summary>
/// Bind child process tak, aby zemřel společně se shellem - i při hard-killu (VS Stop debugging,
/// crash, taskkill), kdy <c>WindowNavigator.CloseAll()</c> nestihne proběhnout.
/// </summary>
/// <remarks>
/// Mechanika: Win32 Job Object s flagem <c>KILL_ON_JOB_CLOSE</c>. Static handle žije s shellem.
/// Když shell zemře jakkoli, Windows automaticky uvolní handle → kernel killne celý Job →
/// všechny přiřazené procesy. Žádné polling / heartbeat / cleanup eventy nejsou potřeba -
/// vše dělá kernel synchronně při process termination, bez ohledu na to jak shell skončí.
/// </remarks>
internal static class KillOnExit
{
    private static readonly IntPtr _handle = Initialize();

    public static void Bind(Process process)
    {
        if (_handle == IntPtr.Zero) return;
        try { AssignProcessToJobObject(_handle, process.Handle); }
        catch { /* může selhat ve VS sandboxu / starších Windows - best effort, ne fatální */ }
    }

    private static IntPtr Initialize()
    {
        var handle = CreateJobObject(IntPtr.Zero, null);
        if (handle == IntPtr.Zero) return IntPtr.Zero;

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = { LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE }
        };
        var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var ptr = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, ptr, false);
            SetInformationJobObject(handle, JobObjectExtendedLimitInformation, ptr, (uint)length);
        }
        finally { Marshal.FreeHGlobal(ptr); }
        return handle;
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    private const int JobObjectExtendedLimitInformation = 9;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int infoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
