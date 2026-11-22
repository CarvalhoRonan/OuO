using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Net;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace Aurora {
public static class BrowserBlockPage {
    public sealed class PageStyle {public string Title="Site bloqueado",Message="O acesso a este site foi bloqueado por uma regra escolhida no OuO.",Accent="#71dcc3";}
    public static PageStyle Style(string directory=null){var value=new PageStyle();try{string file=Path.Combine(directory??Root,"config.xml");if(!File.Exists(file))return value;var xml=XElement.Load(file);string title=(string)xml.Element("title"),message=(string)xml.Element("message"),accent=(string)xml.Element("accent");if(title!=null&&title.Length>=3&&title.Length<=70)value.Title=title;if(message!=null&&message.Length>=5&&message.Length<=300)value.Message=message;if(Regex.IsMatch(accent??"",@"^#[0-9a-fA-F]{6}$"))value.Accent=accent;}catch{}return value;}
    public static void Configure(string title,string message,string accent){
        title=(title??"").Trim();message=(message??"").Trim();
        if(title.Length<3||title.Length>70||message.Length<5||message.Length>300||!Regex.IsMatch(accent??"",@"^#[0-9a-fA-F]{6}$"))throw new InvalidDataException(L.T("Informe título, mensagem e cor válidos.","Enter a valid title, message and color."));
        Directory.CreateDirectory(Root);AtomicFiles.SaveXml(new XElement("page",new XElement("title",title),new XElement("message",message),new XElement("accent",accent)),Path.Combine(Root,"config.xml"));
    }
    public static string Root{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OuO","Pagina de bloqueio");}}
    public static bool Exists{get{return File.Exists(Path.Combine(Root,"manifest.json"));}}
    public static bool Active{get{return File.Exists(Path.Combine(Root,"active.flag"));}}
    public static string Prepare(string[] domains,string journalFolder){
        string rules=Path.Combine(Root,"rules.json"),flag=Path.Combine(Root,"active.flag");
        byte[] previous=File.Exists(rules)?File.ReadAllBytes(rules):null;bool wasActive=File.Exists(flag);
        string result=Generate(domains);
        try{return result+"\n"+SiteProtection.SyncBrowsers(new string[0],journalFolder);}
        catch{
            if(previous==null){if(File.Exists(rules))File.Delete(rules);}else File.WriteAllBytes(rules,previous);
            if(!wasActive&&File.Exists(flag))File.Delete(flag);
            throw;
        }
    }
    public static string Generate(string[] domains){return GenerateTo(Root,domains);}
    public static string GenerateTo(string destination,string[] domains){
        string[] names=domains.Select(HostsBlock.Domain).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(names.Length>1000)throw new InvalidDataException("Browser extension rule limit exceeded");
        Directory.CreateDirectory(destination);var json=new JavaScriptSerializer();var utf8=new UTF8Encoding(false);
        var manifest=new{manifest_version=3,name="OuO - Pagina de bloqueio",version="1.0.1",description="Mostra uma explicacao quando um site escolhido no OuO esta bloqueado.",permissions=new[]{"declarativeNetRequestWithHostAccess"},host_permissions=new[]{"<all_urls>"},declarative_net_request=new{rule_resources=new[]{new{id="blocked",enabled=true,path="rules.json"}}},web_accessible_resources=new[]{new{resources=new[]{"blocked.html"},matches=new[]{"<all_urls>"}}}};
        var rules=names.Select((domain,index)=>new{id=index+1,priority=1,action=new{type="redirect",redirect=new{extensionPath="/blocked.html"}},condition=new{urlFilter="||"+domain+"^",resourceTypes=new[]{"main_frame"}}}).ToArray();
        File.WriteAllText(Path.Combine(destination,"manifest.json"),json.Serialize(manifest),utf8);
        File.WriteAllText(Path.Combine(destination,"rules.json"),json.Serialize(rules),utf8);
        PageStyle page=Style(destination);string title=WebUtility.HtmlEncode(page.Title),message=WebUtility.HtmlEncode(page.Message);
        File.WriteAllText(Path.Combine(destination,"blocked.html"),"<!doctype html><html lang='pt-BR'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>"+title+" • OuO</title><style>body{background:#10151e;color:#eaf2fa;font:18px Segoe UI,Arial,sans-serif;min-height:100vh;display:grid;place-items:center;margin:0}main{max-width:560px;padding:32px;background:#1b2636;border:1px solid #4e756f;border-radius:16px}h1{color:"+page.Accent+"}p{line-height:1.5;color:#c2d4e7}</style><main><h1>"+title+"</h1><p>"+message+"</p><p>Para revisar ou remover a regra, abra <b>OuO → Navegação segura → Sites bloqueados</b>.</p></main></html>",utf8);
        File.WriteAllText(Path.Combine(destination,"LEIA-ME.txt"),"OuO - pagina de bloqueio para Chrome, Edge e Brave\r\n\r\nAbra a pagina de extensoes do navegador, ative o modo de desenvolvedor e escolha Carregar sem compactacao. Selecione esta pasta. Ao mudar a lista de sites no OuO, recarregue a extensao e as abas abertas.\r\n\r\nA extensao precisa de permissao para os sites que voce escolheu bloquear. Ela nao e instalada automaticamente. No Firefox, utilize o bloqueio nativo do navegador ate existir uma extensao assinada.\r\n",utf8);
        File.WriteAllText(Path.Combine(destination,"active.flag"),"OuO",utf8);
        return "OK • "+names.Length+L.T(" site(s) preparados para a página de bloqueio. Carregue e recarregue a extensão no navegador."," site(s) prepared for the blocked page. Load and reload the extension in the browser.");
    }
    public static string Deactivate(){if(!Exists)return "OK • "+L.T("Nenhuma extensão preparada.","No prepared extension.");File.WriteAllText(Path.Combine(Root,"rules.json"),"[]",new UTF8Encoding(false));string flag=Path.Combine(Root,"active.flag");if(File.Exists(flag))File.Delete(flag);return "OK • "+L.T("Página personalizada desativada. Recarregue ou remova a extensão no navegador.","Custom page disabled. Reload or remove the extension in the browser.");}
}
}
