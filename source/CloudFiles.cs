using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
namespace Aurora {
public sealed class CloudEntry {
    public string Path;public long Bytes;public uint Attributes,Tag;
    public bool Directory{get{return (Attributes&16)!=0;}}
    public bool Cloud{get{return (Tag&0xFFFF0FFFu)==0x9000001Au;}}
    public bool Reparse{get{return (Attributes&1024)!=0;}}
    public bool OnlineOnly{get{return !Directory&&(Attributes&(0x1000u|0x40000u|0x400000u))!=0;}}
}
public sealed class OneDriveScan {
    public string[] Roots;public int Files;public readonly List<CloudEntry> Online=new List<CloudEntry>();public readonly List<string> Issues=new List<string>();
    public string Description{get{return (Online.Count>0||Issues.Count>0?L.T("AVISO • ","WARNING • "):"OK • ")+Files+L.T(" arquivo(s) verificado(s); "," file(s) checked; ")+Online.Count+L.T(" somente online."," online-only.")+(Issues.Count>0?"\n"+Issues.Count+L.T(" caminho(s) não puderam ser conferidos. "," path(s) could not be checked. ")+String.Join("\n",Issues.Take(6)):"")+"\n"+L.T("A desinstalação não exclui a pasta local nem os dados na nuvem.","Uninstall does not delete the local folder or cloud data.");}}
}
public static class CloudFiles {
    [StructLayout(LayoutKind.Sequential)]struct AttributeTag{public uint Attributes,Tag;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint sharing,IntPtr security,uint disposition,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandleEx(SafeFileHandle handle,int type,out AttributeTag value,uint size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,StringBuilder text,uint capacity,uint flags);
    [DllImport("CldApi.dll")]static extern int CfSetPinState(SafeFileHandle handle,int state,uint flags,IntPtr overlapped);
    [DllImport("CldApi.dll")]static extern int CfHydratePlaceholder(SafeFileHandle handle,long offset,long length,uint flags,IntPtr overlapped);
    public static bool Within(string path,string root){string full=System.IO.Path.GetFullPath(path),parent=System.IO.Path.GetFullPath(root).TrimEnd('\\');return full.StartsWith(parent+"\\",StringComparison.OrdinalIgnoreCase);}
    public static CloudEntry Read(string path){
        using(var file=CreateFile(path,0,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero)){if(file.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());AttributeTag info;if(!GetFileInformationByHandleEx(file,9,out info,8))throw new Win32Exception(Marshal.GetLastWin32Error());return new CloudEntry{Path=path,Attributes=info.Attributes,Tag=info.Tag,Bytes=(info.Attributes&16)==0?new FileInfo(path).Length:0};}
    }
    public static string[] Roots(){
        var roots=new List<string>();foreach(string name in new[]{"OneDrive","OneDriveConsumer","OneDriveCommercial"}){string value=Environment.GetEnvironmentVariable(name);if(!String.IsNullOrWhiteSpace(value))roots.Add(value);}
        try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive"))if(key!=null){string folder=Convert.ToString(key.GetValue("UserFolder"));if(!String.IsNullOrWhiteSpace(folder))roots.Add(folder);}using(var accounts=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts"))if(accounts!=null)foreach(string account in accounts.GetSubKeyNames())using(var key=accounts.OpenSubKey(account)){string folder=Convert.ToString(key.GetValue("UserFolder"));if(!String.IsNullOrWhiteSpace(folder))roots.Add(folder);}}catch{}
        roots.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"OneDrive"));
        var result=new List<string>();foreach(string folder in roots)try{string path=Args.LocalPath(folder).TrimEnd('\\');if(path.Length>3&&Directory.Exists(path)&&!result.Any(x=>String.Equals(x,path,StringComparison.OrdinalIgnoreCase)))result.Add(path);}catch{}
        return result.Where(x=>!result.Any(other=>!String.Equals(other,x,StringComparison.OrdinalIgnoreCase)&&Within(x,other))).ToArray();
    }
    public static OneDriveScan Scan(IEnumerable<string> roots,Func<string,CloudEntry> read=null,CancellationToken token=default(CancellationToken)){
        read=read??Read;var result=new OneDriveScan{Roots=roots.ToArray()};var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int examined=0;
        foreach(string root in result.Roots){var pending=new Stack<string>();pending.Push(root);while(pending.Count>0){token.ThrowIfCancellationRequested();string folder=pending.Pop();try{
            var parent=read(folder);if(parent.Reparse&&!parent.Cloud){result.Issues.Add(folder+L.T(" • link externo ignorado"," • external link skipped"));continue;}if(!visited.Add(folder))continue;
            foreach(string path in Directory.EnumerateFileSystemEntries(folder)){token.ThrowIfCancellationRequested();if(++examined>200000){result.Issues.Add(L.T("Limite de 200 mil itens atingido; revisão incompleta.","200,000-item limit reached; review is incomplete."));return result;}try{var entry=read(path);if(entry.Reparse&&!entry.Cloud){result.Issues.Add(path+L.T(" • link ignorado"," • link skipped"));continue;}if(entry.Directory)pending.Push(path);else{result.Files++;if(entry.OnlineOnly)result.Online.Add(entry);}}catch(Exception error){result.Issues.Add(path+" • "+error.Message);}}
        }catch(Exception error){result.Issues.Add(folder+" • "+error.Message);}}}return result;
    }
    static void CheckAncestors(string path){for(string at=path;!String.IsNullOrEmpty(at);at=System.IO.Path.GetDirectoryName(at)){var item=Read(at);if(item.Reparse&&!item.Cloud)throw new IOException("Cloud download cannot follow a non-cloud link");}}
    public static int HydrateChild(string input){
        try{string path=Args.LocalPath(input);if(!Roots().Any(root=>Within(path,root)))throw new IOException("File is outside this account's OneDrive folders");CheckAncestors(path);var entry=Read(path);if(entry.Directory)throw new IOException("Select a file");if(!entry.OnlineOnly)return 0;if(!entry.Cloud)throw new IOException(L.T("O provedor deste arquivo não oferece o download Cloud Files. Baixe pelo OneDrive e tente novamente.","This file provider does not support Cloud Files hydration. Download through OneDrive and retry."));
            using(var file=CreateFile(path,0x80000000u,7,IntPtr.Zero,3,0x00200000,IntPtr.Zero)){if(file.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());var final=new StringBuilder(32768);uint length=GetFinalPathNameByHandle(file,final,(uint)final.Capacity,0);string actual=final.ToString();if(actual.StartsWith(@"\\?\"))actual=actual.Substring(4);if(length==0||length>=final.Capacity||!String.Equals(actual,path,StringComparison.OrdinalIgnoreCase))throw new IOException("Cloud file location changed before download");int pin=CfSetPinState(file,1,0,IntPtr.Zero);if(pin<0)Marshal.ThrowExceptionForHR(pin);int hydrated=CfHydratePlaceholder(file,0,-1,0,IntPtr.Zero);if(hydrated<0)Marshal.ThrowExceptionForHR(hydrated);}
            if(Read(path).OnlineOnly)throw new IOException(L.T("O provedor ainda informa conteúdo somente online.","Provider still reports online-only content."));return 0;
        }catch(Exception error){Console.WriteLine("ERROR • "+error.Message);return 1;}
    }
    public static string DownloadAndRemove(string[] roots,bool download,CancellationToken token,Action<string> stage,Func<string,CloudEntry> reader,Action<CloudEntry,CancellationToken> hydrate,Func<string> uninstall){
        var scan=Scan(roots,reader,token);if(download){if(scan.Issues.Count>0)throw new IOException(L.T("A revisão dos arquivos está incompleta. Resolva os caminhos não conferidos antes de baixar e remover.","File review is incomplete. Resolve unchecked paths before downloading and removing."));
            int done=0;foreach(var file in scan.Online){token.ThrowIfCancellationRequested();if(stage!=null)stage(L.T("Baixando ","Downloading ")+(done+1)+"/"+scan.Online.Count+" • "+System.IO.Path.GetFileName(file.Path));hydrate(file,token);if((reader??Read)(file.Path).OnlineOnly)throw new IOException(L.T("Download não confirmado: ","Download not confirmed: ")+file.Path);done++;}
            var after=Scan(roots,reader,token);if(after.Issues.Count>0||after.Online.Count>0)throw new IOException(L.T("Ainda há arquivos online ou não conferidos. O OneDrive foi mantido instalado.","Online-only or unchecked files remain. OneDrive was kept installed."));
        }
        token.ThrowIfCancellationRequested();if(stage!=null)stage(L.T("Desinstalando OneDrive","Uninstalling OneDrive"));string result=uninstall();return result+(!download&&(scan.Online.Count>0||scan.Issues.Count>0)?"\n"+L.T("AVISO • Arquivos somente online continuam na nuvem. Caminhos não conferidos não têm cópia local confirmada.","WARNING • Online-only files remain in the cloud. Unchecked paths have no confirmed local copy."):"");
    }
}
}
