using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Aurora {
public static class ReleaseCatalog {
    const string Source="https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information";
    static readonly string[] Fallback={"26H2","26H1","25H2","24H2","23H2","22H2"};
    static string[] live;static DateTime updated;
    static string CachePath{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OuO","windows-releases.xml");}}
    static ReleaseCatalog(){try{var xml=XElement.Load(CachePath);var values=xml.Elements("release").Select(x=>x.Value).Where(x=>Regex.IsMatch(x,@"^2[0-9]H[12]$")).Distinct().ToArray();if(values.Length>=2){live=values;DateTime.TryParse((string)xml.Attribute("updated"),out updated);}}catch{}}
    public static string[] Parse(string html){
        string plain=WebUtility.HtmlDecode(Regex.Replace(html??"",@"<[^>]*>"," "));
        plain=Regex.Replace(plain,@"\s+"," ");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(Match match in Regex.Matches(plain,@"Windows\s*11\s*(?:,\s*)?(?:version\s*)?(2[0-9]H[12])\b",RegexOptions.IgnoreCase))names.Add(match.Groups[1].Value.ToUpperInvariant());
        foreach(Match match in Regex.Matches(plain,@"\bVersion\s+(2[0-9]H[12])\s*\(OS build\b",RegexOptions.IgnoreCase))names.Add(match.Groups[1].Value.ToUpperInvariant());
        return names.OrderByDescending(x=>Int32.Parse(x.Substring(0,2))).ThenByDescending(x=>x[3]).ToArray();
    }
    public static string[] Current(){return (live??Fallback).Concat(Fallback).Distinct(StringComparer.OrdinalIgnoreCase).Select(x=>"Windows 11 "+x).Concat(new[]{"Windows 10 22H2","Windows 10 21H2","Windows 10 20H2"}).ToArray();}
    public static string Status{get{return live==null?L.T("Lista local. Consulte o suporte de cada versão antes de escolher.","Local list. Check support for each release before selecting."):L.T("Versões conferidas na Microsoft em ","Releases checked at Microsoft on ")+updated.ToString("dd/MM/yyyy HH:mm");}}
    public static string Refresh(){
        // Older .NET Framework installations can default to TLS 1.0.
        ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;
        var request=(HttpWebRequest)WebRequest.Create(Source);request.Timeout=12000;request.ReadWriteTimeout=12000;request.UserAgent="OuO/1.1 (Windows release check)";
        string html;using(var response=(HttpWebResponse)request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))html=reader.ReadToEnd();
        string[] releases=Parse(html);if(releases.Length<2)throw new InvalidDataException(L.T("A página oficial mudou de formato; a lista local foi preservada.","The official page changed format; the local list was preserved."));
        live=releases;updated=DateTime.Now;
        try{Directory.CreateDirectory(Path.GetDirectoryName(CachePath));AtomicFiles.SaveXml(new XElement("windows",new XAttribute("updated",updated.ToString("o")),live.Select(x=>new XElement("release",x))),CachePath);}catch{}
        return Status;
    }
    public static string RefreshOrFallback(){
        try{return Refresh();}
        catch(WebException ex){return L.T("AVISO • Não foi possível consultar a Microsoft agora. A lista local continua disponível. Verifique a conexão, o relógio e o TLS do Windows. Detalhe: ","WARNING • Could not check Microsoft now. The local list remains available. Check the connection, clock and Windows TLS. Detail: ")+ex.Message;}
        catch(System.Security.Authentication.AuthenticationException ex){return L.T("AVISO • A conexão segura com a Microsoft falhou. A lista local continua disponível. Detalhe: ","WARNING • Secure connection to Microsoft failed. The local list remains available. Detail: ")+ex.Message;}
        catch(InvalidDataException ex){return L.T("AVISO • A lista online não pôde ser interpretada. A lista local continua disponível. Detalhe: ","WARNING • Online list could not be parsed. The local list remains available. Detail: ")+ex.Message;}
    }
}
}
