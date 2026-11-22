using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace Aurora {
public partial class MainWindow {
    [StructLayout(LayoutKind.Sequential)]struct NativePoint {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]struct MinMaxInfo {public NativePoint Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
    [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]struct MonitorInfo {public int Size;public NativeRect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    void InitializeWindowBounds(){SourceInitialized+=(s,e)=>{IntPtr handle=new WindowInteropHelper(this).Handle;if(handle==IntPtr.Zero)return;var source=HwndSource.FromHwnd(handle);if(source!=null&&!source.IsDisposed)source.AddHook(WindowBoundsHook);};}
    IntPtr WindowBoundsHook(IntPtr window,int message,IntPtr wparam,IntPtr lparam,ref bool handled){
        if(message==0x24){var monitor=MonitorFromWindow(window,2);var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(monitor!=IntPtr.Zero&&GetMonitorInfo(monitor,ref info)){var size=(MinMaxInfo)Marshal.PtrToStructure(lparam,typeof(MinMaxInfo));size.MaxPosition.X=info.Work.Left-info.Monitor.Left;size.MaxPosition.Y=info.Work.Top-info.Monitor.Top;size.MaxSize.X=info.Work.Right-info.Work.Left;size.MaxSize.Y=info.Work.Bottom-info.Work.Top;Marshal.StructureToPtr(size,lparam,false);handled=true;}}
        return IntPtr.Zero;
    }
}
}
