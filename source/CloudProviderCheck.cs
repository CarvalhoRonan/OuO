using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace Aurora {
// A disposable Cloud Files provider exercises the real OS hydration API without
// an Internet account or access to the user's OneDrive. Never runs on the host.
internal static class CloudProviderCheck {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Registration {public uint Size;[MarshalAs(UnmanagedType.LPWStr)]public string Name;[MarshalAs(UnmanagedType.LPWStr)]public string Version;public IntPtr RootIdentity;public uint RootLength;public IntPtr FileIdentity;public uint FileLength;public Guid Provider;}
    [StructLayout(LayoutKind.Sequential)]struct Policies {public uint Size;public ushort Hydration,HydrationModifier,Population,PopulationModifier;public uint InSync,HardLinks,Management;}
    [StructLayout(LayoutKind.Sequential)]struct Metadata {public long Created,Accessed,Written,Changed;public uint Attributes;public long Size;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Placeholder {[MarshalAs(UnmanagedType.LPWStr)]public string Name;public Metadata Metadata;public IntPtr Identity;public uint IdentityLength,Flags;public int Result;public long Usn;}
    [StructLayout(LayoutKind.Sequential)]struct CallbackRegistration {public int Type;public IntPtr Callback;}
    [StructLayout(LayoutKind.Sequential)]struct CallbackInfo {public uint Size;public long Connection;public IntPtr Context,VolumeGuid,VolumeDos;public uint VolumeSerial;public long RootId;public IntPtr RootIdentity;public uint RootLength;public long FileId,FileSize;public IntPtr FileIdentity;public uint FileLength;public IntPtr Path;public long Transfer;public byte Priority;public IntPtr Correlation,Process;public long Request;}
    [StructLayout(LayoutKind.Sequential)]struct Operation {public uint Size;public int Type;public long Connection,Transfer;public IntPtr Correlation,Status;public long Request;}
    [StructLayout(LayoutKind.Explicit,Size=40)]struct Transfer {[FieldOffset(0)]public uint Size;[FieldOffset(8)]public uint Flags;[FieldOffset(12)]public int Status;[FieldOffset(16)]public IntPtr Buffer;[FieldOffset(24)]public long Offset;[FieldOffset(32)]public long Length;}
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Fetch(ref CallbackInfo info,IntPtr parameters);
    [DllImport("cldapi.dll",CharSet=CharSet.Unicode)]static extern int CfRegisterSyncRoot(string path,ref Registration registration,ref Policies policies,uint flags);
    [DllImport("cldapi.dll",CharSet=CharSet.Unicode)]static extern int CfUnregisterSyncRoot(string path);
    [DllImport("cldapi.dll",CharSet=CharSet.Unicode)]static extern int CfConnectSyncRoot(string path,[In]CallbackRegistration[] callbacks,IntPtr context,uint flags,out long connection);
    [DllImport("cldapi.dll")]static extern int CfDisconnectSyncRoot(long connection);
    [DllImport("cldapi.dll",CharSet=CharSet.Unicode)]static extern int CfCreatePlaceholders(string path,[In,Out]Placeholder[] files,uint count,uint flags,out uint processed);
    [DllImport("cldapi.dll")]static extern int CfExecute(ref Operation operation,ref Transfer transfer);
    static void Check(int hr,string stage="CfExecute"){if(hr<0)throw new COMException(stage+": "+Marshal.GetExceptionForHR(hr).Message,hr);}
    public static string Run(string folder){
        if(!ExtendedChecks.Allowed())throw new InvalidOperationException("Disposable guest only");
        string root=Path.Combine(folder,"CloudProvider"),key=@"Software\Microsoft\OneDrive\Accounts\OuOLab-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(root);
        bool registered=false,connected=false;long connection=0;string callbackError=null;int requests=0;byte[] content=new byte[131072];for(int i=0;i<content.Length;i++)content[i]=(byte)(i%251);
        Fetch callback=(ref CallbackInfo info,IntPtr parameters)=>{IntPtr buffer=Marshal.AllocHGlobal(content.Length);try{Marshal.Copy(content,0,buffer,content.Length);var op=new Operation{Size=(uint)Marshal.SizeOf(typeof(Operation)),Connection=info.Connection,Transfer=info.Transfer,Request=info.Request,Correlation=info.Correlation};var data=new Transfer{Size=40,Buffer=buffer,Offset=0,Length=content.Length};Check(CfExecute(ref op,ref data));System.Threading.Interlocked.Increment(ref requests);}catch(Exception ex){callbackError=ex.ToString();}finally{Marshal.FreeHGlobal(buffer);}};
        try{
            var registration=new Registration{Size=(uint)Marshal.SizeOf(typeof(Registration)),Name="OuO disposable validation",Version="1",Provider=Guid.NewGuid()};var policies=new Policies{Size=(uint)Marshal.SizeOf(typeof(Policies)),Population=3};
            Check(CfRegisterSyncRoot(root,ref registration,ref policies,0),"CfRegisterSyncRoot");registered=true;
            Check(CfConnectSyncRoot(root,new[]{new CallbackRegistration{Type=0,Callback=Marshal.GetFunctionPointerForDelegate(callback)},new CallbackRegistration{Type=-1}},IntPtr.Zero,0,out connection),"CfConnectSyncRoot");connected=true;
            long time=DateTime.UtcNow.ToFileTimeUtc();var files=new[]{new Placeholder{Name="owned-placeholder.bin",Metadata=new Metadata{Created=time,Accessed=time,Written=time,Changed=time,Attributes=128,Size=content.Length},Flags=2}};uint processed;IntPtr identity=Marshal.AllocHGlobal(16);try{Marshal.Copy(Guid.NewGuid().ToByteArray(),0,identity,16);files[0].Identity=identity;files[0].IdentityLength=16;Check(CfCreatePlaceholders(root,files,1,0,out processed),"CfCreatePlaceholders");Check(files[0].Result,"placeholder result");}finally{Marshal.FreeHGlobal(identity);}if(processed!=1)throw new InvalidDataException("Placeholder was not created");
            string path=Path.Combine(root,files[0].Name);var before=CloudFiles.Read(path);if(!before.Cloud||!before.OnlineOnly)throw new InvalidDataException("Fixture is not an online-only cloud placeholder");
            using(var account=Registry.CurrentUser.CreateSubKey(key))account.SetValue("UserFolder",root);
            int code=CloudFiles.HydrateChild(path);if(code!=0)throw new InvalidDataException("Production hydration returned "+code+"; callback: "+callbackError);
            if(callbackError!=null||requests==0||CloudFiles.Read(path).OnlineOnly||!File.ReadAllBytes(path).SequenceEqual(content))throw new InvalidDataException("Hydrated data or callback differs: "+callbackError);
            return "Real CfCreatePlaceholders + CfHydratePlaceholder: "+content.Length+" bytes verified; callbacks="+requests+"; no Internet account used.";
        }finally{Registry.CurrentUser.DeleteSubKeyTree(key,false);if(connected)CfDisconnectSyncRoot(connection);if(registered)CfUnregisterSyncRoot(root);GC.KeepAlive(callback);}
    }
}
}
