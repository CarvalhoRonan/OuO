using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Aurora {
public sealed class MsiApp {public string Name,Publisher,ProductCode,Location,Scope;public override string ToString(){return Name+" • "+Publisher+" • "+Scope+" • "+ProductCode;}}
public static class MsiRemoval {
    static readonly Regex GuidPattern=new Regex(@"^\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}$");
    public static bool ValidCode(string code){return GuidPattern.IsMatch(code??"");}
    public static bool IsMsiProduct(string code,object installerFlag,string command){
        if(!ValidCode(code))return false;
        if(Convert.ToString(installerFlag,System.Globalization.CultureInfo.InvariantCulture)=="1")return true;
        string expanded=Environment.ExpandEnvironmentVariables((command??"").Trim());
        Match parsed=Regex.Match(expanded,@"^(?:""(?<exe>[^""]+)""|(?<exe>.*?\.exe))(?=\s|$)(?<args>.*)$",RegexOptions.IgnoreCase);
        return parsed.Success&&Path.GetFileName(parsed.Groups["exe"].Value).Equals("msiexec.exe",StringComparison.OrdinalIgnoreCase)&&Regex.IsMatch(parsed.Groups["args"].Value,@"(?:^|\s)/(?:i|x)\s*""?"+Regex.Escape(code)+@"""?(?=\s|$)",RegexOptions.IgnoreCase);
    }
    public static List<MsiApp> Inventory(){var result=new List<MsiApp>();foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64})try{
        using(var root=RegistryKey.OpenBaseKey(hive,view))using(var uninstall=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))if(uninstall!=null)foreach(string code in uninstall.GetSubKeyNames()){
            if(!ValidCode(code))continue;try{using(var key=uninstall.OpenSubKey(code)){string name=Convert.ToString(key.GetValue("DisplayName"));if(String.IsNullOrWhiteSpace(name)||Convert.ToString(key.GetValue("SystemComponent"))=="1"||key.GetValue("ParentKeyName")!=null||!IsMsiProduct(code,key.GetValue("WindowsInstaller"),Convert.ToString(key.GetValue("UninstallString"))))continue;
                result.Add(new MsiApp{Name=name,Publisher=Convert.ToString(key.GetValue("Publisher")),ProductCode=code,Location=Convert.ToString(key.GetValue("InstallLocation")),Scope=hive==RegistryHive.CurrentUser?"User":"Machine"});
            }}catch{}
        }
    }catch{}
        return result.GroupBy(x=>x.Scope+"|"+x.ProductCode,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).OrderBy(x=>x.Name).ToList();
    }
    public static CommandResult Uninstall(MsiApp app){if(app==null||!ValidCode(app.ProductCode))throw new ArgumentException("Invalid MSI product");var result=Runner.Run("msiexec.exe","/x "+app.ProductCode+" /passive /norestart",1800000,allowBackground:true);if(result.Code==1618)result.Output=L.T("Outro instalador está em execução. Aguarde a conclusão antes de tentar novamente.","Another installer is running. Wait for it to finish before retrying.")+"\n"+result.Output;return result;}
    public static bool ValidRestorePath(string path){try{string full=Path.GetFullPath(path).TrimEnd('\\');string[] roots={Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs")};return Path.GetFileName(full).Length>=5&&roots.Any(root=>!String.IsNullOrWhiteSpace(root)&&String.Equals(Path.GetDirectoryName(full),Path.GetFullPath(root).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase));}catch{return false;}}
    public static bool SafeRemainder(MsiApp app){
        if(app==null||!ValidCode(app.ProductCode)||String.IsNullOrWhiteSpace(app.Location))return false;
        try{string full=Path.GetFullPath(app.Location).TrimEnd('\\');if(!Directory.Exists(full))return false;
            if(!ValidRestorePath(full))return false;
            string leaf=Regex.Replace(Path.GetFileName(full),"[^A-Za-z0-9]","").ToLowerInvariant(),name=Regex.Replace(app.Name??"","[^A-Za-z0-9]","").ToLowerInvariant();
            if(leaf.Length<5||!name.Contains(leaf))return false;
            if((new DirectoryInfo(full).Attributes&FileAttributes.ReparsePoint)!=0)return false;
            foreach(var other in Inventory())if(other.ProductCode!=app.ProductCode&&String.Equals(Path.GetFullPath(other.Location??"."),full,StringComparison.OrdinalIgnoreCase))return false;
            foreach(Process process in Process.GetProcesses())using(process)try{string executable=process.MainModule.FileName;if(executable.StartsWith(full+"\\",StringComparison.OrdinalIgnoreCase))return false;}catch{}
            return true;
        }catch{return false;}
    }
}
}
