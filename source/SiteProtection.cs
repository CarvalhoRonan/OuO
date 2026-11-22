using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;
namespace Aurora {
public static class SiteProtection {
    static readonly object gate=new object();
    public static string Normalize(string input){
        string text=(input??"").Trim();Uri uri;
        if(text.IndexOf("://",StringComparison.Ordinal)>=0){
            if(!Uri.TryCreate(text,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https")||!String.IsNullOrEmpty(uri.UserInfo))throw new ArgumentException("URL inválida.");
            text=uri.IdnHost;
        }
        return HostsBlock.Domain(text);
    }
    public static string BrowserPattern(string domain,bool firefox){return firefox?"*://*."+HostsBlock.Domain(domain)+"/*":HostsBlock.Domain(domain);}
    public static string SyncBrowsers(IEnumerable<string> domains,string folder){
        lock(gate){
            string manifest=Path.Combine(folder,"Browser-site-blocks.xml");
            XElement old=File.Exists(manifest)?XElement.Load(manifest):new XElement("blocks"),next=new XElement("blocks");
            var changes=new List<Tuple<string,string,object>>();
            var allowed=new HashSet<string>(BrowserExtensions.Detect().Select(x=>x.PolicyPath+(x.Id=="firefox"?@"\WebsiteFilter\Block":@"\URLBlocklist")),StringComparer.OrdinalIgnoreCase);
            foreach(var entry in old.Elements("entry")){int number;if(!allowed.Contains((string)entry.Attribute("path")??"")||!Int32.TryParse((string)entry.Attribute("name"),out number)||number<1||number>1000)throw new InvalidDataException("Invalid browser block manifest");}
            try{
                // Remove only values created by OuO and left unmodified since then.
                foreach(var entry in old.Elements("entry")){
                    string path=(string)entry.Attribute("path"),name=(string)entry.Attribute("name"),value=entry.Value;
                    using(var key=Registry.LocalMachine.OpenSubKey(path,true))if(key!=null&&String.Equals(Convert.ToString(key.GetValue(name)),value,StringComparison.Ordinal)){
                        changes.Add(Tuple.Create(path,name,(object)value));key.DeleteValue(name,false);
                    }
                }
                int count=0;
                foreach(var browser in BrowserExtensions.Detect().Where(x=>x.Installed)){
                    string path=browser.PolicyPath+(browser.Id=="firefox"?@"\WebsiteFilter\Block":@"\URLBlocklist");
                    using(var key=Registry.LocalMachine.CreateSubKey(path)){
                        foreach(string domain in domains.Distinct(StringComparer.OrdinalIgnoreCase)){
                            string value=BrowserPattern(domain,browser.Id=="firefox");
                            if(key.GetValueNames().Any(x=>String.Equals(Convert.ToString(key.GetValue(x)),value,StringComparison.Ordinal)))continue;
                            int number=1;while(key.GetValue(number.ToString())!=null)number++;
                            if(number>1000)throw new InvalidOperationException("Limite de bloqueios do navegador atingido.");
                            string name=number.ToString();changes.Add(Tuple.Create(path,name,(object)null));
                            key.SetValue(name,value,RegistryValueKind.String);
                            if(Convert.ToString(key.GetValue(name))!=value)throw new IOException("Falha ao conferir a regra do navegador.");
                            next.Add(new XElement("entry",new XAttribute("path",path),new XAttribute("name",name),value));count++;
                        }
                    }
                }
                AtomicFiles.SaveXml(old,manifest+".previous");AtomicFiles.SaveXml(next,manifest);
                return count+L.T(" regra(s) gravada(s) nos navegadores detectados. Feche todas as janelas do navegador e abra novamente para carregar as políticas."," rule(s) saved in detected browsers. Close all browser windows and reopen to load policies.");
            }catch{
                foreach(var change in changes.AsEnumerable().Reverse())try{using(var key=Registry.LocalMachine.CreateSubKey(change.Item1)){if(change.Item3==null)key.DeleteValue(change.Item2,false);else key.SetValue(change.Item2,change.Item3,RegistryValueKind.String);}}catch{}
                throw;
            }
        }
    }
    public static string Verify(string domain){
        domain=Normalize(domain);
        IPAddress[] addresses;
        try{addresses=Dns.GetHostAddresses(domain);}catch(System.Net.Sockets.SocketException){return L.T("Não confirmado • não foi possível resolver ","Not confirmed • could not resolve ")+domain+L.T(". Verifique a conexão antes de repetir o teste.",". Check your connection before repeating the test.");}
        bool blocked=addresses.Length>0&&addresses.All(x=>x.Equals(IPAddress.Any)||x.Equals(IPAddress.IPv6Any)||IPAddress.IsLoopback(x));
        return (blocked?"OK • ":"ERROR • ")+domain+(blocked?L.T(" bloqueado na resolução do Windows."," blocked in Windows name resolution."):L.T(" continua resolvendo para um endereço externo. Revise VPN, cache ou o arquivo hosts."," still resolves to an external address. Check VPN, cache or hosts."))+"\n"+String.Join(", ",addresses.Select(x=>x.ToString()).ToArray());
    }
}
}