using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Aurora {
public static class DiskBinding {
    // Bind the selected device to the handle that will actually be used.
    // The Windows device GUID is stable during the attachment, including on
    // VHDs without a serial. It is not the mutable GPT/MBR disk identifier.
    [DllImport("kernel32.dll",EntryPoint="DeviceIoControl",SetLastError=true)]static extern bool Query(SafeFileHandle handle,uint control,byte[] input,int inputSize,byte[] output,int outputSize,out int returned,IntPtr overlapped);
    public static string DeviceGuid(byte[] data,int returned,int number){
        if(data==null||returned<40||returned>data.Length||BitConverter.ToUInt32(data,0)<40||BitConverter.ToUInt32(data,4)<40||BitConverter.ToUInt32(data,4)>returned)throw new InvalidDataException("Incomplete device identity descriptor");
        if(BitConverter.ToUInt32(data,12)!=7||BitConverter.ToUInt32(data,16)!=(uint)number)throw new InvalidDataException("Opened device number does not match the selection");
        byte[] bytes=new byte[16];Buffer.BlockCopy(data,20,bytes,0,16);Guid id=new Guid(bytes);return id==Guid.Empty?"":"device-guid:"+id.ToString("D");
    }
    public static string Read(SafeFileHandle handle,int number){
        byte[] info=new byte[40];int returned;
        if(Query(handle,0x2d1084,null,0,info,info.Length,out returned,IntPtr.Zero)){
            string id=DeviceGuid(info,returned,number);if(id.Length>0)return id;
        }
        // Windows 7 and older controllers can still expose a serial descriptor.
        byte[] descriptor=new byte[4096];
        if(!Query(handle,0x2d1400,new byte[12],12,descriptor,descriptor.Length,out returned,IntPtr.Zero))throw new Win32Exception(Marshal.GetLastWin32Error());
        string serial=DirectClone.DescriptorSerial(descriptor,returned);
        if(String.IsNullOrWhiteSpace(serial))throw new InvalidDataException("Device exposes neither a connection GUID nor a hardware serial");
        return "serial:"+serial.Trim().ToUpperInvariant();
    }
}
}
