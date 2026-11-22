using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
namespace Aurora {
public static class BrowserInventory {
    static readonly object gate=new object();static BrowserInfo[] cached;static DateTime at;
    public static BrowserInfo[] Detect(bool force=false){lock(gate){if(!force&&cached!=null&&(DateTime.UtcNow-at).TotalMinutes<10)return cached;var all=new List<BrowserInfo>();
        Add(all,"chrome","Google Chrome","chrome.exe","chrome",@"Software\Policies\Google\Chrome",@"Google\Chrome\Application\chrome.exe");
        Add(all,"chrome-beta","Google Chrome Beta","chrome.exe","chrome",@"Software\Policies\Google\Chrome",@"Google\Chrome Beta\Application\chrome.exe");
        Add(all,"chrome-dev","Google Chrome Dev","chrome.exe","chrome",@"Software\Policies\Google\Chrome",@"Google\Chrome Dev\Application\chrome.exe");
        Add(all,"chrome-canary","Google Chrome Canary","chrome.exe","chrome",@"Software\Policies\Google\Chrome",@"Google\Chrome SxS\Application\chrome.exe");
        Add(all,"edge","Microsoft Edge","msedge.exe","edge",@"Software\Policies\Microsoft\Edge",@"Microsoft\Edge\Application\msedge.exe");
        Add(all,"edge-beta","Microsoft Edge Beta","msedge.exe","edge",@"Software\Policies\Microsoft\Edge",@"Microsoft\Edge Beta\Application\msedge.exe");
        Add(all,"brave","Brave","brave.exe","brave",@"Software\Policies\BraveSoftware\Brave",@"BraveSoftware\Brave-Browser\Application\brave.exe");
        Add(all,"firefox","Mozilla Firefox","firefox.exe","firefox",@"Software\Policies\Mozilla\Firefox",@"Mozilla Firefox\firefox.exe",@"Programs\Mozilla Firefox\firefox.exe");
        Add(all,"firefox-developer","Firefox Developer Edition","firefox.exe","firefox",@"Software\Policies\Mozilla\Firefox",@"Firefox Developer Edition\firefox.exe");
        Add(all,"chromium","Chromium","chrome.exe","chromium",@"Software\Policies\Chromium",@"Chromium\Application\chrome.exe",@"Chromium\chrome.exe");
        Add(all,"vivaldi","Vivaldi","vivaldi.exe","chromium",null,@"Vivaldi\Application\vivaldi.exe");
        Add(all,"opera","Opera","opera.exe","chromium",null,@"Programs\Opera\opera.exe",@"Opera\opera.exe",@"Opera\launcher.exe");
        Add(all,"opera-gx","Opera GX","opera.exe","chromium",null,@"Programs\Opera GX\opera.exe",@"Opera GX\opera.exe",@"Opera GX\launcher.exe");
        Add(all,"librewolf","LibreWolf","librewolf.exe","firefox",null,@"LibreWolf\librewolf.exe");
        cached=all.ToArray();at=DateTime.UtcNow;return cached;}}
    static void Add(List<BrowserInfo> list,string id,string name,string exe,string family,string policy,params string[] suffixes){string path="",scope="";foreach(var root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}){if(String.IsNullOrWhiteSpace(root))continue;foreach(string suffix in suffixes){string candidate=Path.Combine(root,suffix);if(File.Exists(candidate)){path=candidate;scope=root==Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)?L.T("usuário","user"):L.T("computador","machine");break;}}if(path.Length>0)break;}
        if(path.Length==0)foreach(RegistryView view in new[]{RegistryView.Registry64,RegistryView.Registry32})foreach(RegistryHive hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})try{using(var root=RegistryKey.OpenBaseKey(hive,view))using(var key=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+exe)){string candidate=key==null?"":Convert.ToString(key.GetValue("","")).Trim('"');if(!candidate.StartsWith(@"\\")&&File.Exists(candidate)&&suffixes.Any(s=>candidate.EndsWith(s,StringComparison.OrdinalIgnoreCase))){path=candidate;scope=hive==RegistryHive.CurrentUser?L.T("usuário","user"):L.T("computador","machine");}}}catch{}
        list.Add(new BrowserInfo{Id=id,Name=name,Exe=exe,PolicyPath=policy,Installed=path.Length>0,InstallPath=path,Family=family,Scope=scope});}
}
public partial class MainWindow {
    void StartBrowserDetection(){if(preview)return;System.Threading.Tasks.Task.Factory.StartNew(()=>BrowserInventory.Detect()).ContinueWith(task=>{if(task.IsFaulted||Dispatcher.HasShutdownStarted)return;Dispatcher.BeginInvoke(new Action(()=>{Action refresh;if(current=="browser"&&pageRefreshActions.TryGetValue("browser-extensions",out refresh))refresh();}));});}
    void OpenExtensionStore(BrowserInfo browser,bool wot){string family=String.IsNullOrEmpty(browser.Family)?browser.Id:browser.Family;string address=family=="firefox"?wot?"https://addons.mozilla.org/firefox/addon/wot-safe-browsing-tool/":"https://addons.mozilla.org/firefox/addon/ublock-origin/":family=="edge"?"https://microsoftedge.microsoft.com/addons/detail/"+(wot?BrowserExtensions.EdgeWot:BrowserExtensions.EdgeUblock):"https://chromewebstore.google.com/detail/"+(wot?BrowserExtensions.ChromeWot:BrowserExtensions.ChromeUblock);try{if(browser.Installed&&File.Exists(browser.InstallPath))System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(browser.InstallPath,Args.Q(address)){UseShellExecute=true});else Open(address);}catch(Exception ex){ShowNotice(ex.Message);}}
}
}
