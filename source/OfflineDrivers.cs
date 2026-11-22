using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Aurora {
public sealed class OfflineDriverCandidate {
    public string Inf,DeviceId,Version,Catalog;
    public override string ToString(){return Path.GetFileName(Inf)+" • "+Version;}
}
public static class OfflineDrivers {
    static string Token(string deviceId){return Regex.Match(deviceId??"",@"(?:VEN_[0-9A-Fa-f]{4}&DEV_[0-9A-Fa-f]{4}|VID_[0-9A-Fa-f]{4}&PID_[0-9A-Fa-f]{4}|^ACPI\\[A-Za-z0-9_]+|^ROOT\\[A-Za-z0-9_]+)",RegexOptions.IgnoreCase).Value;}
    public static bool ExactMatch(string inf,string[] hardwareIds,string instanceId){
        if(!File.Exists(inf))return false;string content=File.ReadAllText(inf);var ids=new HashSet<string>((hardwareIds??new string[0]).Where(x=>!String.IsNullOrWhiteSpace(x)),StringComparer.OrdinalIgnoreCase);
        if(ids.Count==0&&!String.IsNullOrWhiteSpace(instanceId)){string[] parts=instanceId.Split('\\');if(parts.Length>=2)ids.Add(parts[0]+"\\"+parts[1]);}
        string wanted=Environment.Is64BitOperatingSystem?"NTamd64":"NTx86",section="";
        foreach(string raw in content.Split('\n')){string line=raw.Split(';')[0].Trim();if(line.StartsWith("[")&&line.EndsWith("]")){section=line.Substring(1,line.Length-2);continue;}var arch=Regex.Match(section,@"\bNT(?:amd64|x86|arm64)\b",RegexOptions.IgnoreCase);if(arch.Success&&!arch.Value.Equals(wanted,StringComparison.OrdinalIgnoreCase))continue;
            int equals=line.IndexOf('=');if(equals<0)continue;string[] fields=line.Substring(equals+1).Split(',');if(fields.Length<2)continue;foreach(string field in fields.Skip(1)){string id=field.Trim().Trim('"');if(ids.Contains(id))return true;}
        }return false;
    }
    public static OfflineDriverCandidate Read(string inf,string deviceId){
        string token=Token(deviceId);if(token.Length==0||!File.Exists(inf)||Path.GetExtension(inf).ToLowerInvariant()!=".inf")return null;
        string content=File.ReadAllText(inf);if(content.IndexOf(token,StringComparison.OrdinalIgnoreCase)<0)return null;
        string version=Regex.Match(content,@"(?im)^\s*DriverVer\s*=\s*([^\r\n;]+)").Groups[1].Value.Trim();
        string catalog=Regex.Match(content,@"(?im)^\s*CatalogFile(?:\.[A-Za-z0-9.]+)?\s*=\s*([^\r\n;]+)").Groups[1].Value.Trim().Trim('"');
        if(catalog.Length==0||catalog.IndexOfAny(new[]{'\\','/',':'})>=0)return null;
        string path=Path.Combine(Path.GetDirectoryName(inf),catalog);if(!File.Exists(path))return null;
        return new OfflineDriverCandidate{Inf=Path.GetFullPath(inf),DeviceId=deviceId,Version=version,Catalog=path};
    }
    public static List<OfflineDriverCandidate> Scan(string folder,string deviceId){
        string root=Args.LocalPath(folder);if(!Directory.Exists(root))throw new DirectoryNotFoundException(root);
        if((new DirectoryInfo(root).Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException(L.T("Escolha uma pasta real de drivers, não um link.","Choose a real driver folder, not a link."));
        var result=new List<OfflineDriverCandidate>();var queue=new Queue<Tuple<string,int>>();queue.Enqueue(Tuple.Create(root,0));int files=0;
        while(queue.Count>0){var entry=queue.Dequeue();if(entry.Item2>12)continue;
            string[] infs;try{infs=Directory.GetFiles(entry.Item1,"*.inf");}catch(UnauthorizedAccessException){continue;}catch(IOException){continue;}
            foreach(string file in infs){
                if(++files>10000)throw new InvalidDataException("Too many INF files in selected folder");
                try{var item=Read(file,deviceId);if(item!=null)result.Add(item);}catch(UnauthorizedAccessException){}catch(IOException){}
            }
            string[] subfolders;try{subfolders=Directory.GetDirectories(entry.Item1);}catch(UnauthorizedAccessException){continue;}catch(IOException){continue;}
            foreach(string sub in subfolders)try{if(((new DirectoryInfo(sub)).Attributes&FileAttributes.ReparsePoint)==0)queue.Enqueue(Tuple.Create(sub,entry.Item2+1));}catch(UnauthorizedAccessException){}catch(IOException){}
        }
        return result;
    }
    public static string Install(OfflineDriverCandidate item,int build){
        if(item==null||Read(item.Inf,item.DeviceId)==null)throw new InvalidDataException(L.T("O pacote não corresponde mais ao dispositivo selecionado.","The package no longer matches the selected device."));
        var signature=Runner.PS("$s=Get-AuthenticodeSignature -LiteralPath "+Runner.QuotePS(item.Catalog)+"; if($s.Status -ne 'Valid'){throw ('Catálogo sem assinatura válida: '+$s.Status)}; Write-Output $s.SignerCertificate.Subject",30000);
        if(signature.Code!=0)throw new InvalidDataException(signature.Output);
        var result=Runner.Run("pnputil.exe",build>=10240?"/add-driver "+Args.Q(item.Inf)+" /install":"-a "+Args.Q(item.Inf),120000);
        return (result.Code==0||result.Code==3010?"OK":"ERROR "+result.Code)+" • "+Path.GetFileName(item.Inf)+"\n"+signature.Output+"\n"+result.Output;
    }
}
}
