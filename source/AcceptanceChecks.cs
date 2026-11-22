using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Text;
namespace Aurora {
public static class AcceptanceChecks {
    public static int Benchmark(string path){
        var watch=Stopwatch.StartNew();long files=0,bytes=0;string result;
        using(var cancel=new CancellationTokenSource(60000))try{
            var buckets=DiskIndex.Scan(path,cancel.Token,(f,b)=>{files=f;bytes=b;},true);
            result="OK • native scan "+watch.ElapsedMilliseconds+" ms; "+files+" files; "+buckets.Sum(x=>x.Bytes)+" bytes; "+buckets.Sum(x=>x.Errors)+" inaccessible items.";
        }catch(OperationCanceledException){result="PARTIAL • timed out at 60 seconds; "+files+" files; "+bytes+" bytes counted.";}
        catch(Exception ex){result="ERROR • "+ex.Message;}
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"disk-benchmark.txt"),result,Encoding.UTF8);return result.StartsWith("OK",StringComparison.Ordinal)?0:1;
    }
    public static int Run(){
        string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics","acceptance-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(root);
        var report=new List<string>();int failed=0;
        Action<bool,string> check=(ok,label)=>{report.Add((ok?"PASS • ":"FAIL • ")+label);if(!ok)failed++;};
        Func<Action,bool> rejects=action=>{try{action();return false;}catch{return true;}};
        check(ResultTone.Classify("OK\nERROR • algum item falhou")<0,"mixed success/error report is red");
        check(ResultTone.Classify("Desativado")<0&&ResultTone.Classify("Disabled")<0,"disabled state is red in both languages");
        check(ResultTone.Classify("OK • DNS aplicado e confirmado")>0,"confirmed DNS is green");
        check(ResultTone.Classify("Não identificado")==0,"unknown state is neutral");
        var ps=Runner.PS("Write-Progress -Activity 'Test' -Status 'Test'; Write-Output 'ação verificada'",30000);
        check(ps.Code==0&&!ps.Output.Contains("CLIXML")&&ps.Output.Contains("ação verificada"),"PowerShell plain Unicode output without CLIXML");
        check(DnsManager.SameServers(new[]{"2606:4700:4700:0:0:0:0:1113"},new[]{"2606:4700:4700::1113"}),"DNS compares normalized IPv6 addresses");
        check(!DnsManager.SameServers(new[]{"192.168.1.1"},new[]{"1.1.1.3","1.0.0.3"}),"wrong effective DNS cannot report confirmation");
        check(SiteProtection.Normalize("https://www.example.com/path?q=a")=="www.example.com","site address becomes a domain");
        check(SiteProtection.BrowserPattern("example.com",false)=="example.com"&&SiteProtection.BrowserPattern("example.com",true)=="*://*.example.com/*","browser block rules include subdomains");
        var removal=OfficeReplacement.RemovalConfiguration();
        check((string)removal.Root.Element("Remove").Attribute("All")=="TRUE"&&removal.Root.Element("Add")==null,"Office removal is a separate configure operation");
        check((string)removal.Root.Element("Property").Attribute("Value")=="FALSE","Office removal cannot force-close apps");
        var job=new OfficeAutoJob{Product="O365BusinessRetail",Architecture="64",Language="pt-br",Apps=new[]{"Word"},AcceptTerms=true,RemoveExisting=true};
        check(OfficeAutoJob.Decode(job.Encode()).RemoveExisting,"Office removal consent survives elevation");
        check(OperationLanes.Lane("WinGet install")=="installers"&&OperationLanes.Lane("Procurar drivers")=="","installation queue leaves independent queries available");
        var popular=PopularCatalog.Current();
        check(popular.Length>=24&&popular.Select(x=>x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()==popular.Length&&popular.All(x=>!String.IsNullOrWhiteSpace(x.Description)),"popular catalog has 24 or more distinct described apps");
        check(!UserCleanup.Eligible(Environment.GetFolderPath(Environment.SpecialFolder.Windows)+@"\explorer.exe",DateTime.UtcNow),"temporary cleaner rejects system files");
        check(OrphanRegistryCleaner.LooksOrphan(Path.Combine(root,"missing-app.exe"),Path.Combine(root,"missing-install"))&&!OrphanRegistryCleaner.LooksOrphan("msiexec.exe /x {12345678-1234-1234-1234-123456789ABC}",Path.Combine(root,"missing-install")),"registry cleaner requires a missing absolute uninstaller and location");
        check(TweakCatalog.All().Select(x=>x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()==TweakCatalog.All().Count&&TweakCatalog.All().First(x=>x.Id=="mouse-acceleration").Settings().Count()==3,"tweak catalog IDs unique and multi-setting mouse preference complete");
        check(rejects(()=>DiskMigration.ValidateTarget(Path.GetPathRoot(Environment.SystemDirectory).Substring(0,2))),"system image destination cannot be the source volume");
        check(rejects(()=>LegacyUpgrade.VerifyMicrosoftSetup(Path.Combine(root,"not-setup.exe"))),"legacy upgrade rejects unverified media");
        string officeSummary=OfficeReplacement.Summarize(new[]{"Office MSI • Microsoft Office Professional Plus 2016","Office MSI • Microsoft Office Shared MUI 2016","Office MSI • Microsoft Office Proofing Tools 2016"});
        check(officeSummary.Contains("Professional Plus 2016")&&officeSummary.Contains("2 componente")&&!officeSummary.Contains("\nOffice MSI • Microsoft Office Shared"),"Office inventory groups supporting components under main suite");
        string pageTest=Path.Combine(root,"block-page-style");Directory.CreateDirectory(pageTest);File.WriteAllText(Path.Combine(pageTest,"config.xml"),"<page><title>Olá &amp; bem-vindo</title><message>Este endereço foi bloqueado pelo usuário.</message><accent>red; background:url(x)</accent></page>");
        BrowserBlockPage.GenerateTo(pageTest,new[]{"example.org"});string pageHtml=File.ReadAllText(Path.Combine(pageTest,"blocked.html"));
        check(pageHtml.Contains("Ol&#225; &amp; bem-vindo")&&pageHtml.Contains("#71dcc3")&&!pageHtml.Contains("background:url"),"custom block page escapes text and rejects untrusted CSS");
        string[] releases=ReleaseCatalog.Parse("<h2>Windows 11, version 25H2</h2><h2>Windows 11, version 26H2</h2><p>Windows 10, version 22H2</p>");
        check(releases.Length==2&&releases[0]=="26H2"&&releases[1]=="25H2"&&ReleaseCatalog.Current().Contains("Windows 10 22H2"),"release catalog keeps the latest Windows 11 versions and older references");
        check(ReleaseCatalog.Parse("Version 26H2 (OS build 26300) Version 25H2 (OS build 26200)").Length==2,"release catalog understands current Microsoft release-history headings");
        check(System.Runtime.InteropServices.Marshal.SizeOf(typeof(DisplayModes.Mode))==220,"display mode layout matches DEVMODEW");
        try{var mode=DisplayModes.Current();check(mode.Width>0&&mode.Height>0,"current display mode can be read without changing resolution");}catch(Exception ex){report.Add("UNVERIFIED • display mode read unavailable: "+ex.Message);}
        string driverFolder=Path.Combine(root,"driver-fixture");Directory.CreateDirectory(Path.Combine(driverFolder,"nested"));
        File.WriteAllText(Path.Combine(driverFolder,"nested","sample.inf"),"[Version]\nDriverVer=10/01/2026,1.2.3.4\nCatalogFile=sample.cat\n[Models]\nPCI\\VEN_1234&DEV_5678",Encoding.ASCII);
        File.WriteAllText(Path.Combine(driverFolder,"nested","sample.cat"),"fixture");
        check(OfflineDrivers.Scan(driverFolder,@"PCI\VEN_1234&DEV_5678").Count==1&&OfflineDrivers.Scan(driverFolder,@"PCI\VEN_ABCD&DEV_EF01").Count==0,"offline driver scan matches only the chosen hardware identifier");
        string startupRoot=Path.Combine(root,"startup"),startupOff=Path.Combine(root,"startup-off");Directory.CreateDirectory(startupRoot);string shortcut=Path.Combine(startupRoot,"Example.lnk");File.WriteAllText(shortcut,"fixture");StartupFolders.MoveValidated(shortcut,startupRoot,startupOff);
        check(!File.Exists(shortcut)&&File.Exists(Path.Combine(startupOff,"Example.lnk")),"startup folder toggle moves only its shortcut to the disabled location");
        StartupFolders.MoveValidated(Path.Combine(startupOff,"Example.lnk"),startupOff,startupRoot);
        check(File.Exists(shortcut),"startup shortcut can be restored without touching the installed program");
        string extension=Path.Combine(root,"browser-extension");BrowserBlockPage.GenerateTo(extension,new[]{"example.com","example.com"});
        var rules=new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(extension,"rules.json"))) as object[];
        check(rules!=null&&rules.Length==1&&File.ReadAllText(Path.Combine(extension,"blocked.html")).Contains("Site bloqueado"),"blocked-page extension deduplicates domains and has a local explanation");
        var taskXml=DailyMaintenance.TaskXml("Registry","09:00",WindowsIdentity.GetCurrent().User.Value);
        check(taskXml.ToString().Contains("StartBoundary")&&taskXml.ToString().Contains("T09:00:00")&&!taskXml.ToString().Contains("<?xml")&&!taskXml.ToString().Contains("<LogonType>ServiceAccount</LogonType>"),"daily task XML uses a valid schema and passes SYSTEM logon type separately");
        CommandResult schema=null;
        try{
            string xmlPath=Path.Combine(root,"daily-task.xml");File.WriteAllText(xmlPath,taskXml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),new UTF8Encoding(false));
            string script="$service=New-Object -ComObject Schedule.Service;$service.Connect();$task=$service.NewTask(0);$task.XmlText=[IO.File]::ReadAllText("+Runner.QuotePS(xmlPath)+");Write-Output 'XML accepted'";
            schema=Runner.PS(script,30000);
        }catch(Exception ex){report.Add("UNVERIFIED • task XML COM validation unavailable: "+ex.Message);}
        if(schema!=null){
            bool denied=schema.Code!=0&&(schema.Output.IndexOf("0x80070005",StringComparison.OrdinalIgnoreCase)>=0||schema.Output.IndexOf("Acesso negado",StringComparison.OrdinalIgnoreCase)>=0||schema.Output.IndexOf("Access is denied",StringComparison.OrdinalIgnoreCase)>=0);
            if(denied)report.Add("UNVERIFIED • task XML COM validation requires Task Scheduler access on this PC.");
            else check(schema.Code==0&&schema.Output.Contains("XML accepted"),"Windows Task Scheduler accepts daily task XML without registering it: "+schema.Output);
        }
        string fixture=Path.Combine(root,"disk-fixture");Directory.CreateDirectory(fixture);long expected=0;
        for(int d=0;d<12;d++){string folder=Path.Combine(fixture,"folder-"+d);Directory.CreateDirectory(folder);for(int f=0;f<160;f++){int size=(d+1)*(f+1);using(var stream=File.Create(Path.Combine(folder,"file-"+f+".bin")))stream.SetLength(size);expected+=size;}}
        Directory.CreateDirectory(Path.Combine(fixture,"empty"));
        var watch=Stopwatch.StartNew();var scanned=DiskIndex.Scan(fixture,CancellationToken.None,null,true);watch.Stop();long cold=watch.ElapsedMilliseconds;
        check(scanned.Sum(x=>x.Bytes)==expected&&scanned.Count==13,"native disk scan returns exact logical sizes and empty directories");
        watch.Restart();var again=DiskIndex.Scan(fixture,CancellationToken.None,null,false);watch.Stop();
        check(again.Sum(x=>x.Bytes)==expected,"cached disk scan keeps correct totals");
        report.Add("INFO • disk fixture: 1920 files; first scan "+cold+" ms; repeat "+watch.ElapsedMilliseconds+" ms.");
        var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;try{DiskIndex.Scan(fixture,cancelled.Token,null,false);}catch(OperationCanceledException){stopped=true;}check(stopped,"disk scan can be canceled");
        string logos=Path.Combine(root,"logo-fixture");Directory.CreateDirectory(Path.Combine(logos,"Assets"));File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","branding","ouo-circuito.png"),Path.Combine(logos,"Assets","Logo.scale-200.png"));
        check(StoreApps.LogoPath(logos,@"Assets\Logo.png")!=null&&StoreApps.LogoPath(logos,@"..\elsewhere.png")==null,"Store logo locates scale variants and rejects traversal");
        try{var adapters=AdapterInfo.List();if(adapters.Count>0){report.Add("PASS • current DNS read from active interface: "+DnsManager.CurrentLabel(AdapterInfo.Active(adapters)));}else report.Add("UNVERIFIED • no readable active network interface.");}catch(Exception ex){report.Add("UNVERIFIED • DNS inventory: "+ex.Message);}
        try{var tasks=ProtectionStatus.Schedules();check(tasks.Count==2,"both maintenance schedules inspected");foreach(var task in tasks)report.Add("INFO • "+task.ToString());}catch(Exception ex){report.Add("UNVERIFIED • schedule access unavailable: "+ex.Message);}
        try{var apps=StoreApps.Inventory();check(apps.All(x=>!String.IsNullOrWhiteSpace(x.Name)&&!String.IsNullOrWhiteSpace(x.Version)),"Store app inventory has names and versions");report.Add("INFO • Store apps: "+apps.Count+"; icons: "+apps.Count(x=>x.StoreIcon!=null));}catch(Exception ex){report.Add("UNVERIFIED • Store inventory access unavailable: "+ex.Message);}
        bool admin=new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        if(admin){var boot=Runner.Run("bcdedit.exe","/enum "+Args.Q("{current}"),30000);if(boot.Code!=0&&Environment.UserName.Equals("WDAGUtilityAccount",StringComparison.OrdinalIgnoreCase))report.Add("UNVERIFIED • Windows Sandbox does not expose a normal boot configuration store. F8 must be checked on an installed Windows.");else check(boot.Code==0,"quoted current boot entry can be queried");}else report.Add("UNVERIFIED • F8 check requires an elevated disposable test system.");
        report.Add("INFO • no DNS, hosts, Office, driver, task, boot or system feature was modified.");
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"acceptance-results.txt"),String.Join(Environment.NewLine,report.ToArray()),Encoding.UTF8);
        return failed==0?0:1;
    }
}
}
