using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Aurora {
public static class RegistryTraces {
    public static string PathFor(string appName){
        if(!Regex.IsMatch(appName??"",@"^[A-Za-z0-9][A-Za-z0-9 ._+-]{4,79}$")||Regex.IsMatch(appName,@"^(Microsoft|Windows|Classes|Policies|CurrentVersion|Common Files)$",RegexOptions.IgnoreCase))return null;
        return @"Software\"+appName;
    }
    public static bool Exists(string appName){string path=PathFor(appName);if(path==null)return false;using(var key=Registry.CurrentUser.OpenSubKey(path))return key!=null;}
    public static string Quarantine(string appName,string folder){string path=PathFor(appName);if(path==null||!Exists(appName))throw new InvalidOperationException("Registry trace unavailable");Directory.CreateDirectory(folder);
        string file=System.IO.Path.Combine(folder,"RegistryTrace-"+DateTime.Now.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N")+".reg");
        var export=Runner.Run("reg.exe","export "+Args.Q(@"HKCU\"+path)+" "+Args.Q(file)+" /y",30000);if(export.Code!=0||!File.Exists(file))throw new IOException("Registry backup failed: "+export.Output);
        File.WriteAllText(file+".name",appName);Registry.CurrentUser.DeleteSubKeyTree(path);return file;
    }
    public static string Restore(string file,string folder){string root=System.IO.Path.GetFullPath(folder).TrimEnd('\\')+"\\",path=System.IO.Path.GetFullPath(file);if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!System.IO.Path.GetFileName(path).StartsWith("RegistryTrace-",StringComparison.OrdinalIgnoreCase)||!File.Exists(path)||!File.Exists(path+".name"))throw new ArgumentException("Invalid backup path");
        string name=File.ReadAllText(path+".name");if(PathFor(name)==null||Exists(name))throw new InvalidOperationException("Target registry key is already present or invalid");
        var result=Runner.Run("reg.exe","import "+Args.Q(path),30000);if(result.Code!=0)throw new IOException(result.Output);return path;
    }
}
}
