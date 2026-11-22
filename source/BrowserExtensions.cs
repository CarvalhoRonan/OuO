using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Aurora {
public sealed class BrowserInfo {
    public string Id,Name,Exe,PolicyPath;
    public bool Installed;
}
public static class BrowserExtensions {
    public const string ChromeUpdate="https://clients2.google.com/service/update2/crx";
    public const string EdgeUpdate="https://edge.microsoft.com/extensionwebstorebase/v1/crx";
    public const string FirefoxUblock="https://addons.mozilla.org/firefox/downloads/latest/ublock-origin/latest.xpi";
    public const string FirefoxWot="https://addons.mozilla.org/firefox/downloads/latest/wot-safe-browsing-tool/latest.xpi";
    public const string ChromeUblock="ddkjiahejlhfcafbddmgiahcphecmpfh";
    public const string ChromeWot="bhmmomiinigofkjcapegjjndpbikblnp";
    public const string EdgeUblock="cimighlppcgcoapaliogpjjdehbnofhn";
    public const string EdgeWot="iiclaphjclecagpkkaacljnpcppnoibi";
    public static BrowserInfo[] Detect(){
        var all=new[]{new BrowserInfo{Id="chrome",Name="Google Chrome",Exe="chrome.exe",PolicyPath=@"Software\Policies\Google\Chrome"},new BrowserInfo{Id="edge",Name="Microsoft Edge",Exe="msedge.exe",PolicyPath=@"Software\Policies\Microsoft\Edge"},new BrowserInfo{Id="brave",Name="Brave",Exe="brave.exe",PolicyPath=@"Software\Policies\BraveSoftware\Brave"},new BrowserInfo{Id="firefox",Name="Mozilla Firefox",Exe="firefox.exe",PolicyPath=@"Software\Policies\Mozilla\Firefox"}};
        foreach(var item in all)item.Installed=Find(item.Exe);
        return all;
    }
    static bool Find(string exe){
        foreach(RegistryView view in new[]{RegistryView.Registry64,RegistryView.Registry32}){
            foreach(RegistryHive hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser}){
                try{using(var root=RegistryKey.OpenBaseKey(hive,view))using(var key=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+exe))if(key!=null){string path=Convert.ToString(key.GetValue("", ""));if(File.Exists(path))return true;}}catch{}
            }
        }
        string[] bases={Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)};
        string[] paths=exe=="chrome.exe"?new[]{@"Google\Chrome\Application\chrome.exe"}:exe=="msedge.exe"?new[]{@"Microsoft\Edge\Application\msedge.exe"}:exe=="brave.exe"?new[]{@"BraveSoftware\Brave-Browser\Application\brave.exe"}:new[]{@"Mozilla Firefox\firefox.exe",@"Programs\Mozilla Firefox\firefox.exe"};
        return bases.Where(x=>!String.IsNullOrEmpty(x)).Any(root=>paths.Any(path=>File.Exists(Path.Combine(root,path))));
    }
    static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=1024*1024};}
    public static string MergeChromium(string existing,string browser,bool ublock,bool wot,bool protect=false){
        if(!ublock&&!wot)throw new ArgumentException("No extensions selected");
        if(browser!="chrome"&&browser!="edge"&&browser!="brave")throw new ArgumentException("Unsupported browser");
        Dictionary<string,object> document;
        if(String.IsNullOrWhiteSpace(existing))document=new Dictionary<string,object>();
        else {try{document=Json().Deserialize<Dictionary<string,object>>(existing);}catch(Exception e){throw new InvalidDataException("Existing browser policy is not valid JSON",e);}if(document==null)throw new InvalidDataException("Invalid browser policy");}
        string update=browser=="edge"?EdgeUpdate:ChromeUpdate;
        string[] ids=browser=="edge"?new[]{EdgeUblock,EdgeWot}:new[]{ChromeUblock,ChromeWot};
        for(int i=0;i<2;i++){
            if(i==0&&!ublock||i==1&&!wot)continue;
            var setting=new Dictionary<string,object>{{"installation_mode",protect?"force_installed":"normal_installed"},{"update_url",update}};
            if(document.ContainsKey(ids[i])){
                var prior=document[ids[i]] as Dictionary<string,object>;
                string mode=prior!=null&&prior.ContainsKey("installation_mode")?Convert.ToString(prior["installation_mode"]):"";
                if(prior==null||(mode!="normal_installed"&&!(protect&&mode=="force_installed"))||!prior.ContainsKey("update_url")||Convert.ToString(prior["update_url"])!=update)throw new InvalidOperationException("Existing policy for extension "+ids[i]+" must be reviewed manually");
                if(protect)prior["installation_mode"]="force_installed";
            }else document.Add(ids[i],setting);
        }
        return Json().Serialize(document);
    }
    static string FirefoxValue(IRegistryStore registry,string path,string url){
        for(int n=1;n<=100;n++){RegistrySnapshot current=registry.Read("HKCU",path,n.ToString());if(current.Exists&&Convert.ToString(current.Value)==url)return null;}
        for(int n=1;n<=100;n++)if(!registry.Read("HKCU",path,n.ToString()).Exists)return n.ToString();
        throw new InvalidOperationException("Firefox policy list is full");
    }
    public static string Apply(BrowserInfo[] browsers,bool ublock,bool wot,IRegistryStore registry,Journal journal,bool protect=false){
        if(!ublock&&!wot)throw new ArgumentException(L.T("Selecione pelo menos uma extensão.","Select at least one extension."));
        if(browsers==null||!browsers.Any(x=>x.Installed))throw new InvalidOperationException(L.T("Nenhum navegador compatível foi encontrado.","No supported browser was found."));
        StringBuilder result=new StringBuilder();
        foreach(var browser in browsers.Where(x=>x.Installed)){
            string title="BrowserExtensions:"+browser.Id;
            try{
                if(browser.Id=="firefox"){
                    string path=browser.PolicyPath+@"\Extensions\Install";
                    foreach(string url in new[]{ublock?FirefoxUblock:null,wot?FirefoxWot:null}.Where(x=>x!=null)){
                        string slot=FirefoxValue(registry,path,url);if(slot!=null)journal.RegistryChange(title,"HKCU",path,slot,url,RegistryValueKind.String);
                    }
                }else{
                    string path=browser.PolicyPath;RegistrySnapshot existing=registry.Read("HKCU",path,"ExtensionSettings");
                    if(existing.Exists&&existing.Kind!=RegistryValueKind.String)throw new InvalidOperationException("Existing ExtensionSettings type must be reviewed manually");
                    string old=existing.Exists?Convert.ToString(existing.Value):null;
                    string next=MergeChromium(old,browser.Id,ublock,wot,protect);
                    if(next!=old)journal.RegistryChange(title,"HKCU",path,"ExtensionSettings",next,RegistryValueKind.String);
                }
                result.AppendLine(browser.Name+": "+L.T("política preparada; reinicie o navegador e confira as extensões.","policy prepared; restart the browser and check extensions."));
            }catch(Exception error){result.AppendLine(browser.Name+": ERROR "+error.Message);}
        }
        return result.ToString();
    }
}
}
