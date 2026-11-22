using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Aurora {
// A job can terminate only processes started by this command, never unrelated tasks.
public sealed class OwnedCommandJob:IDisposable {
    [StructLayout(LayoutKind.Sequential)]struct BasicLimits {public long ProcessTime,JobTime;public uint Flags;public UIntPtr MinimumWorkingSet,MaximumWorkingSet;public uint ActiveLimit;public UIntPtr Affinity;public uint Priority,Scheduling;}
    [StructLayout(LayoutKind.Sequential)]struct IoCounters {public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;}
    [StructLayout(LayoutKind.Sequential)]struct ExtendedLimits {public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;}
    [StructLayout(LayoutKind.Sequential)]struct Accounting {public long User,Kernel,PeriodUser,PeriodKernel;public uint PageFaults,Total,Active,Terminated;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateJobObject(IntPtr security,string name);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(IntPtr job,int information,ref ExtendedLimits limits,uint size);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll")]static extern bool QueryInformationJobObject(IntPtr job,int information,out Accounting data,uint size,IntPtr returned);
    [DllImport("kernel32.dll")]static extern bool TerminateJobObject(IntPtr job,uint code);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    IntPtr handle;bool assigned,terminated;
    public bool Attached{get{return assigned;}}
    public OwnedCommandJob(Process process){handle=CreateJobObject(IntPtr.Zero,null);if(handle==IntPtr.Zero)return;var limits=new ExtendedLimits();limits.Basic.Flags=0x2000;if(!SetInformationJobObject(handle,9,ref limits,(uint)Marshal.SizeOf(typeof(ExtendedLimits)))||!AssignProcessToJobObject(handle,process.Handle)){CloseHandle(handle);handle=IntPtr.Zero;return;}assigned=true;}
    public int Active{get{Accounting data;return assigned&&QueryInformationJobObject(handle,1,out data,(uint)Marshal.SizeOf(typeof(Accounting)),IntPtr.Zero)?(int)data.Active:-1;}}
    public void Terminate(){if(assigned){terminated=true;TerminateJobObject(handle,1);}}
    public void Dispose(){if(handle==IntPtr.Zero)return;if(!terminated){var limits=new ExtendedLimits();SetInformationJobObject(handle,9,ref limits,(uint)Marshal.SizeOf(typeof(ExtendedLimits)));}CloseHandle(handle);handle=IntPtr.Zero;}
}
}
