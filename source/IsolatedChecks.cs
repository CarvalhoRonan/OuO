using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Xml.Linq;
namespace Aurora {
public static class IsolatedChecks {
    const string GuardRoot=@"C:\OuOLab";static readonly string Root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"lab-data");static readonly List<string> report=new List<string>();static int failures;
    static void Test(string label,Action action){report.Add("RUNNING • "+label);Save();try{action();report.Add("PASS • "+label);}catch(Exception ex){failures++;report.Add("FAIL • "+label+" • "+ex.Message);report.Add("DETAIL • "+ex);}Save();}
    static void Save(){Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics"));File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics","isolated-results.txt"),report,new UTF8Encoding(true));if(Directory.Exists(@"C:\OuOResults"))File.WriteAllLines(@"C:\OuOResults\isolated-results.txt",report,new UTF8Encoding(true));}
    static void Must(bool value,string message){if(!value)throw new InvalidDataException(message);}
    static void Step(string message){report.Add("STEP • "+message);Save();}
    static void Cmd(string exe,string arguments,int timeout=45000){var r=Runner.Run(exe,arguments,timeout);if(r.Code!=0)throw new IOException(exe+" • "+r.Code+" • "+r.Output);}
    public static int Run(){
        // This entry point is deliberately unusable on the user's working Windows.
        if(!Environment.UserName.Equals("WDAGUtilityAccount",StringComparison.OrdinalIgnoreCase)||!File.Exists(Path.Combine(GuardRoot,"isolated.marker"))||!AppDomain.CurrentDomain.BaseDirectory.StartsWith(GuardRoot+"\\",StringComparison.OrdinalIgnoreCase))return 80;
        if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))return 81;
        Directory.CreateDirectory(Root);
        report.Add("Windows Sandbox • "+Environment.OSVersion.Version+" • "+DateTime.Now.ToString("s"));report.Add("Actions below run only in the disposable guest. Host input is read-only; only reports are exported.");
        Test("Task Scheduler accepts the exact Registry and Restore XML without registering",()=>{
            foreach(string mode in new[]{"Registry","Restore"}){string xml=DailyMaintenance.TaskXml(mode,"09:00",WindowsIdentity.GetCurrent().User.Value).ToString(SaveOptions.DisableFormatting);string file=Path.Combine(Root,"validate-"+mode+".xml");File.WriteAllText(file,xml,new UTF8Encoding(false));var r=Runner.PS("$s=New-Object -ComObject Schedule.Service;$s.Connect();$null=$s.GetFolder('\').RegisterTask('OuO-Validate-Only',[IO.File]::ReadAllText("+Runner.QuotePS(file)+"),1,'SYSTEM',$null,5,$null);'VALID'",30000);Must(r.Code==0&&r.Output.Contains("VALID"),r.Output);}
        });
        Test("Register and read back both daily schedules, then run Registry backup as SYSTEM",()=>{
            try{string sid=WindowsIdentity.GetCurrent().User.Value;DailyMaintenance.Register("Registry","09:00",sid);DailyMaintenance.Register("Restore","09:15",sid);var states=ProtectionStatus.Schedules();Must(states.Count==2&&states.All(x=>x.Exists&&x.Enabled)&&states.Single(x=>x.Mode=="Registry").Time=="09:00"&&states.Single(x=>x.Mode=="Restore").Time=="09:15","Schedule readback differs");
                DateTime backupStarted=DateTime.UtcNow.AddSeconds(-2);
                var run=Runner.PS("$s=New-Object -ComObject Schedule.Service;$s.Connect();$folder=$s.GetFolder('\\');$task=$folder.GetTask('Aurora-Registry-Daily');$started=[DateTime]::Now.AddSeconds(-2);$null=$task.Run($null);$limit=[DateTime]::UtcNow.AddSeconds(90);do{Start-Sleep -Milliseconds 500;$task=$folder.GetTask('Aurora-Registry-Daily');$completed=$task.LastRunTime -ge $started -and $task.State -ne 2 -and $task.State -ne 4}while(-not $completed -and [DateTime]::UtcNow -lt $limit);if(-not $completed){$task.Stop(0);throw 'Backup did not finish'};if($task.LastTaskResult -ne 0){throw ('Backup result '+$task.LastTaskResult)};'BACKUP_OK'",110000);Must(run.Code==0&&run.Output.Contains("BACKUP_OK"),run.Output);Must(Directory.GetFiles(DailyMaintenance.Root,"SYSTEM.hiv",SearchOption.AllDirectories).Any(x=>File.GetLastWriteTimeUtc(x)>=backupStarted),"Fresh SYSTEM backup not found");
            }finally{DailyMaintenance.Remove("Registry");DailyMaintenance.Remove("Restore");}
        });
        Test("Hosts blocking applies, resolves locally, then restores the guest hosts file",()=>{
            string previous=File.ReadAllText(HostsBlock.FilePath);try{HostsBlock.Apply(new[]{"ouo-check.invalid"},Path.Combine(Root,"hosts-journal"));Cmd("ipconfig.exe","/flushdns");string check=SiteProtection.Verify("ouo-check.invalid");Must(!ResultTone.IsFailure(check),check);}finally{File.WriteAllText(HostsBlock.FilePath,previous,Encoding.UTF8);Runner.Run("ipconfig.exe","/flushdns",15000);}
        });
        Test("Apply DNS, read actual adapter servers, then restore the guest network settings",()=>{
            var adapter=AdapterInfo.List().FirstOrDefault(x=>x.V4Index>0&&!String.IsNullOrEmpty(x.LocalV4));if(adapter==null)throw new IOException("Guest has no IPv4 adapter");var before=DnsManager.Capture(adapter,false);try{var profile=DnsProfile.All().Single(x=>x.Name=="Quad9 • Secure");DnsManager.ApplyProfile(adapter,profile,false,Path.Combine(Root,"dns-journal"));Must(profile.V4.All(x=>DnsManager.Effective(adapter).Contains(x)),"Applied DNS not returned by adapter");}finally{DnsManager.Restore(before);}
        });
        Test("Offline recovery edits only an exported disposable SAM, with full backup and active-Windows guard",()=>{
            string user="OuOLabAccount";Step("Create disposable local account");Cmd("net.exe","user "+user+" OuO-Lab9281! /add");try{string windows=Path.Combine(Root,"Offline","Windows"),config=Path.Combine(windows,"System32","config");Directory.CreateDirectory(config);foreach(string hive in new[]{"SAM","SYSTEM","SECURITY"}){Step("Export guest-only "+hive);Cmd("reg.exe","save HKLM\\"+hive+" "+Args.Q(Path.Combine(config,hive))+" /y");}Step("Open exported SAM copy");var session=OfflineAccountRecovery.Open(windows);var account=session.Accounts.Single(x=>x.Name==user);string before=OfflineAccountRecovery.Hash(session.SamPath);Step("Clear selected account in the exported SAM copy");OfflineAccountRecovery.Apply(session,account.Rid,true);Must(OfflineAccountRecovery.Hash(session.SamPath)!=before,"SAM copy was not changed");Must(Directory.GetDirectories(session.Folder,"Backup-*").Any(),"Recovery backup was not created");OfflineAccountRecovery.ValidateHive(session.SamPath);Must(OfflineAccountRecovery.Open(windows).Accounts.Any(x=>x.Name==user),"Changed SAM could not be reopened");}finally{Runner.Run("net.exe","user "+user+" /delete",30000);}
        });
        Test("Direct MFT scan on a disposable NTFS VHD matches generated file sizes",()=>{
            string vhd=Path.Combine(Root,"mft-test.vhd");try{
                string script="create vdisk file=\""+vhd+"\" maximum=128 type=expandable\r\nselect vdisk file=\""+vhd+"\"\r\nattach vdisk\r\ncreate partition primary\r\nformat fs=ntfs quick label=OuO-Lab\r\nassign letter=R\r\nexit\r\n";string file=Path.Combine(Root,"mft-create.txt");File.WriteAllText(file,script,Encoding.ASCII);Cmd("diskpart.exe","/s "+Args.Q(file),60000);Must(Directory.Exists(@"R:\"),"Test VHD did not mount");
                Directory.CreateDirectory(@"R:\Pastas\Subpasta");using(var data=File.Create(@"R:\Pastas\Subpasta\sample.dat"))data.SetLength(1234567);File.WriteAllText(@"R:\root.txt","OuO",Encoding.ASCII);List<DiskBucket> found;string why;Must(NtfsIndex.TryScan(@"R:\",CancellationToken.None,null,null,out found,out why),"MFT index unavailable: "+why);Must(found.Single(x=>x.Path.Equals(@"R:\Pastas",StringComparison.OrdinalIgnoreCase)).Bytes==1234567&&found.Single(x=>x.Path.Equals(@"R:\root.txt",StringComparison.OrdinalIgnoreCase)).Bytes==3,"MFT sizes differ from generated fixture");
            }finally{string detach=Path.Combine(Root,"mft-detach.txt");File.WriteAllText(detach,"select vdisk file=\""+vhd+"\"\r\ndetach vdisk\r\nexit\r\n",Encoding.ASCII);Runner.Run("diskpart.exe","/s "+Args.Q(detach),30000);}
        });
        Test("Physical-drive cloning between two guest-only VHDs verifies sectors and relocates GPT",()=>{
            string source=Path.Combine(Root,"clone-source.vhd"),target=Path.Combine(Root,"clone-target.vhd");try{
                foreach(var pair in new[]{Tuple.Create(source,64),Tuple.Create(target,96)}){string script="create vdisk file=\""+pair.Item1+"\" maximum="+pair.Item2+" type=expandable\r\nselect vdisk file=\""+pair.Item1+"\"\r\nattach vdisk\r\n"+(pair.Item1==source?"convert gpt\r\ncreate partition primary\r\nformat fs=ntfs quick label=OuO-Clone\r\nassign letter=S\r\n":"")+"exit\r\n";string plan=Path.Combine(Root,pair.Item2+".txt");File.WriteAllText(plan,script,Encoding.ASCII);Cmd("diskpart.exe","/s "+Args.Q(plan),60000);}
                Must(Directory.Exists(@"S:\"),"Source test VHD not mounted");File.WriteAllText(@"S:\OuO-test.txt","Disposable clone validation");Step("Inventory guest VHDs");var disks=DirectClone.Inventory();CloneDisk a=disks.Single(x=>x.Volumes.Contains(@"S:\")),b=disks.Single(x=>!x.Protected&&x.Size==96L*1048576);Step("Take source VHD offline");DirectClone.SetOffline(a,true);Step("Take target VHD offline");DirectClone.SetOffline(b,true);disks=DirectClone.Inventory();a=disks.Single(x=>x.Number==a.Number);b=disks.Single(x=>x.Number==b.Number);Step("Copy sectors and verify the larger target");var phases=new HashSet<string>();string result=DirectClone.Copy(a,b,1,true,CancellationToken.None,p=>{string key=p.Phase+(p.Done==p.Total?" complete":" started");if(phases.Add(key))Step(key);});Must(!ResultTone.IsFailure(result),result);Must(result.Contains("GPT"),"Larger target did not report backup GPT relocation");
            }finally{foreach(string path in new[]{target,source}){string script=Path.Combine(Root,"clone-detach.txt");File.WriteAllText(script,"select vdisk file=\""+path+"\"\r\ndetach vdisk\r\nexit\r\n",Encoding.ASCII);Runner.Run("diskpart.exe","/s "+Args.Q(script),30000);}}
        });
        report.Add("RESULT • "+failures+" failure(s). No host OS or host physical disk was modified.");Save();return failures==0?0:1;
    }
}
}
