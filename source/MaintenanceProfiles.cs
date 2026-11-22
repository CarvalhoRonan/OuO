using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Aurora {
public static class MaintenanceProfiles {
    public static string Folder{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OuO","Profiles");}}
    static string PathFor(string name){if(!Regex.IsMatch(name??"",@"^[\p{L}\p{N}][\p{L}\p{N} -]{0,39}$"))throw new ArgumentException("Invalid profile name");return Path.Combine(Folder,name+".txt");}
    public static string[] List(){if(!Directory.Exists(Folder))return new string[0];return Directory.GetFiles(Folder,"*.txt").Select(Path.GetFileNameWithoutExtension).OrderBy(x=>x).ToArray();}
    public static void Save(string name,IEnumerable<string> ids){string path=PathFor(name);var known=new HashSet<string>(TweakCatalog.All().Select(x=>x.Id).Concat(TweakPreferences.All().SelectMany(x=>x.Variants).Select(x=>x.Definition.Id)),StringComparer.OrdinalIgnoreCase);var lines=ids.Distinct(StringComparer.OrdinalIgnoreCase).Where(known.Contains).ToArray();Directory.CreateDirectory(Folder);File.WriteAllLines(path,lines,Encoding.UTF8);}
    public static string[] Load(string name){string path=PathFor(name);if(!File.Exists(path))throw new FileNotFoundException("Profile not found");var known=new HashSet<string>(TweakCatalog.All().Select(x=>x.Id).Concat(TweakPreferences.All().SelectMany(x=>x.Variants).Select(x=>x.Definition.Id)),StringComparer.OrdinalIgnoreCase);return File.ReadAllLines(path,Encoding.UTF8).Where(known.Contains).ToArray();}
}
}
