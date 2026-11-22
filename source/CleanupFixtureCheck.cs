using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
namespace Aurora {
internal static class CleanupFixtureCheck {
    public static string Run(string folder){
        if(!ExtendedChecks.Allowed())throw new InvalidOperationException("Disposable guest only");string name="OuOLab"+Guid.NewGuid().ToString("N")+".exe",key=@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+name,target=Path.Combine(folder,name),backup=Path.Combine(folder,"registry-backup");
        using(var entry=Registry.CurrentUser.CreateSubKey(key))entry.SetValue("",target,RegistryValueKind.String);
        try{var finding=RegistryCleanup.Scan(CancellationToken.None,null).SingleOrDefault(x=>x.Hive=="HKCU"&&x.Key.Equals(key,StringComparison.OrdinalIgnoreCase)&&x.View==(Environment.Is64BitOperatingSystem?RegistryView.Registry64:RegistryView.Registry32));if(finding==null)throw new InvalidDataException("Owned dangling App Paths reference was not found.");string saved=RegistryCleanup.Delete(finding,backup);using(var check=Registry.CurrentUser.OpenSubKey(key))if(check!=null&&check.GetValue("")!=null)throw new InvalidDataException("Owned reference was not deleted.");RegistryCleanup.Restore(saved,backup);using(var check=Registry.CurrentUser.OpenSubKey(key))if(check==null||Convert.ToString(check.GetValue(""))!=target)throw new InvalidDataException("Owned reference was not restored exactly.");
        }finally{Registry.CurrentUser.DeleteSubKeyTree(key,false);}
        string temp=Path.Combine(UserCleanup.Root,"OuO-owned-"+Guid.NewGuid().ToString("N")+".tmp");File.WriteAllText(temp,"Disposable cleanup fixture");File.SetLastWriteTimeUtc(temp,DateTime.UtcNow.AddDays(-21));try{var item=UserCleanup.Scan().SingleOrDefault(x=>x.Path.Equals(temp,StringComparison.OrdinalIgnoreCase));if(item==null)throw new InvalidDataException("Owned aged temporary file was not found.");string result=UserCleanup.DeleteMany(new[]{item},true);if(File.Exists(temp)||ResultTone.IsFailure(result))throw new InvalidDataException("Permanent cleanup did not remove the owned temporary file: "+result);}finally{if(File.Exists(temp))File.Delete(temp);}
        return "Real HKCU orphan detected, exported, deleted and restored; one owned aged temporary file detected and permanently deleted. No unrelated findings were modified.";
    }
}
}
