using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Aurora {
public static class AppxRemnants {
    public static string SafePath(string fullName){
        if(String.IsNullOrWhiteSpace(fullName)||!Regex.IsMatch(fullName,@"^[A-Za-z0-9.]+_[A-Za-z0-9.]+_[A-Za-z0-9]+_[A-Za-z0-9]*_[A-Za-z0-9]+$"))return null;
        string[] parts=fullName.Split('_');if(parts.Length!=5)return null;
        string family=parts[0]+"_"+parts[4];
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Packages");
        string path=Path.Combine(root,family);
        return ValidPath(path)?path:null;
    }
    public static bool ValidPath(string path){
        try{
            string root=Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Packages")).TrimEnd(Path.DirectorySeparatorChar);
            string full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            if(!String.Equals(Path.GetDirectoryName(full),root,StringComparison.OrdinalIgnoreCase)||!Regex.IsMatch(Path.GetFileName(full),@"^[A-Za-z0-9.]+_[A-Za-z0-9]+$"))return false;
            if(Directory.Exists(full)&&(new DirectoryInfo(full).Attributes&FileAttributes.ReparsePoint)!=0)return false;
            return true;
        }catch{return false;}
    }
    public static bool Installed(string fullName){
        string path=SafePath(fullName);if(path==null)return true;
        string name=fullName.Split('_')[0];
        var result=Runner.PS("Get-AppxPackage -Name "+Runner.QuotePS(name)+" -ErrorAction Stop | Select-Object -ExpandProperty PackageFullName",30000);
        return result.Code!=0||!String.IsNullOrWhiteSpace(result.Output);
    }
}
}
