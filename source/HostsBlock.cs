using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Aurora {
public static class HostsBlock {
    public const string Begin="# OuO BLOCK START",End="# OuO BLOCK END";
    public static string FilePath{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"drivers","etc","hosts");}}
    public static string Domain(string input){
        string value=(input??"").Trim().TrimEnd('.');if(value.Length<4||value.Length>253||value.Contains("/")||value.Contains(":")||value.Contains("\\")||value.Contains(" "))throw new ArgumentException("Informe somente um domínio, como exemplo.com.");
        string ascii=new IdnMapping().GetAscii(value).ToLowerInvariant();
        if(!Regex.IsMatch(ascii,@"^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$"))throw new ArgumentException("Domínio inválido.");
        return ascii;
    }
    public static string[] Parse(string text){
        int start=text.IndexOf(Begin,StringComparison.Ordinal),end=text.IndexOf(End,StringComparison.Ordinal);
        if(start<0&&end<0)return new string[0];
        if(start<0||end<start)throw new InvalidDataException("A seção OuO no arquivo hosts está incompleta.");
        string section=text.Substring(start+Begin.Length,end-start-Begin.Length);
        return section.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries)).Where(x=>x.Length>=2&&(x[0]=="0.0.0.0"||x[0]=="127.0.0.1")).Select(x=>Domain(x[1])).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static string Update(string original,IEnumerable<string> domains){
        string[] names=domains.Select(Domain).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        string text=original??"";int start=text.IndexOf(Begin,StringComparison.Ordinal),end=text.IndexOf(End,StringComparison.Ordinal);
        if(start>=0||end>=0){if(start<0||end<start)throw new InvalidDataException("A seção OuO no arquivo hosts está incompleta.");text=text.Remove(start,end+End.Length-start).TrimEnd('\r','\n');}
        if(names.Length==0)return text+Environment.NewLine;
        StringBuilder block=new StringBuilder();block.AppendLine(Begin);foreach(string name in names){block.AppendLine("127.0.0.1 "+name);block.AppendLine("::1 "+name);}block.AppendLine(End);
        return text.TrimEnd('\r','\n')+Environment.NewLine+Environment.NewLine+block.ToString();
    }
    public static string[] Current(){return Parse(File.Exists(FilePath)?File.ReadAllText(FilePath):"");}
    public static string Apply(IEnumerable<string> domains,string backupFolder){
        string path=FilePath,old=File.Exists(path)?File.ReadAllText(path):"";
        string next=Update(old,domains);if(old==next)return "Nenhuma alteração.";
        Directory.CreateDirectory(backupFolder);string backup=Path.Combine(backupFolder,"Hosts-"+DateTime.Now.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N")+".backup");
        if(File.Exists(path))File.Copy(path,backup);else File.WriteAllText(backup,"");
        try{File.WriteAllText(path,next,new UTF8Encoding(false));var flush=Runner.Run("ipconfig.exe","/flushdns",15000);if(!new HashSet<string>(Current(),StringComparer.OrdinalIgnoreCase).SetEquals(domains.Select(Domain)))throw new IOException("Falha ao conferir o arquivo hosts.");return "OK • "+Current().Length+" domínio(s) • Backup: "+backup+"\n"+flush.Output;}
        catch{try{File.Copy(backup,path,true);}catch{}throw;}
    }
}
}
