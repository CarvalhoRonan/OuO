using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
namespace Aurora {
internal static class VirtualDriverCheck {
    [StructLayout(LayoutKind.Sequential)]struct DeviceInfo {public uint Size;public Guid Class;public uint Instance;public IntPtr Reserved;}
    [DllImport("setupapi.dll",SetLastError=true)]static extern IntPtr SetupDiCreateDeviceInfoList(ref Guid type,IntPtr owner);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiCreateDeviceInfo(IntPtr list,string name,ref Guid type,string description,IntPtr owner,uint flags,ref DeviceInfo info);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiSetDeviceRegistryProperty(IntPtr list,ref DeviceInfo info,uint property,byte[] data,uint length);
    [DllImport("setupapi.dll",SetLastError=true)]static extern bool SetupDiCallClassInstaller(uint function,IntPtr list,ref DeviceInfo info);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDeviceInstanceId(IntPtr list,ref DeviceInfo info,StringBuilder id,int size,out int required);
    [DllImport("setupapi.dll")]static extern bool SetupDiDestroyDeviceInfoList(IntPtr list);
    [DllImport("newdev.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool UpdateDriverForPlugAndPlayDevices(IntPtr owner,string hardwareId,string inf,uint flags,out bool reboot);
    static void Check(bool success){if(!success)throw new Win32Exception(Marshal.GetLastWin32Error());}
    public static string Run(){
        if(!ExtendedChecks.Allowed())throw new InvalidOperationException("Disposable guest only");string inf=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"INF\netloop.inf");if(!File.Exists(inf))throw new PlatformNotSupportedException("Guest has no Microsoft loopback INF.");
        if(DriverInventory.Read(true).Any(x=>x.HardwareIds.Contains("*MSLOOP",StringComparer.OrdinalIgnoreCase)))throw new PlatformNotSupportedException("Guest already contains a loopback device; refusing to update an unrelated device.");
        Guid type=new Guid("4d36e972-e325-11ce-bfc1-08002be10318");IntPtr list=SetupDiCreateDeviceInfoList(ref type,IntPtr.Zero);if(list==new IntPtr(-1))throw new Win32Exception(Marshal.GetLastWin32Error());var info=new DeviceInfo{Size=(uint)Marshal.SizeOf(typeof(DeviceInfo))};bool created=false;
        try{Check(SetupDiCreateDeviceInfo(list,"Net",ref type,"OuO disposable loopback",IntPtr.Zero,1,ref info));byte[] ids=Encoding.Unicode.GetBytes("*MSLOOP\0\0");Check(SetupDiSetDeviceRegistryProperty(list,ref info,1,ids,(uint)ids.Length));Check(SetupDiCallClassInstaller(0x19,list,ref info));created=true;var id=new StringBuilder(512);int required;Check(SetupDiGetDeviceInstanceId(list,ref info,id,id.Capacity,out required));bool reboot;Check(UpdateDriverForPlugAndPlayDevices(IntPtr.Zero,"*MSLOOP",inf,1,out reboot));
            DriverRecord installed=null;var watch=System.Diagnostics.Stopwatch.StartNew();do{installed=DriverInventory.Read(true).SingleOrDefault(x=>String.Equals(x.DeviceId,id.ToString(),StringComparison.OrdinalIgnoreCase));if(installed!=null&&!String.IsNullOrWhiteSpace(installed.Version))break;System.Threading.Thread.Sleep(1000);}while(watch.Elapsed.TotalSeconds<30);if(installed==null||String.IsNullOrWhiteSpace(installed.Version))throw new InvalidDataException("Installed virtual driver version was not returned by the production inventory: "+id);return installed.Name+" • "+installed.Provider+" • "+installed.Version+" • reboot="+reboot+". Only the created virtual device is removed afterwards; physical/OEM updates remain in testing.";
        }finally{try{if(created)Check(SetupDiCallClassInstaller(5,list,ref info));}finally{SetupDiDestroyDeviceInfoList(list);DriverInventory.Invalidate();}}
    }
}
}
