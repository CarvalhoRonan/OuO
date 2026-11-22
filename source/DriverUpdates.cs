using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Aurora {
public sealed class DriverOffer {
    public string Id,Title,Provider="",Model="",Date="",HardwareId="";public int Revision;public DriverRecord[] Installed=new DriverRecord[0];
    public string CurrentVersion{get{return Installed.Length==0?L.T("não identificada","not identified"):String.Join(", ",Installed.Select(x=>x.Version).Where(x=>!String.IsNullOrWhiteSpace(x)).Distinct().ToArray());}}
    public string Details{get{return L.T("Instalado: ","Installed: ")+CurrentVersion+"\n"+L.T("Candidato: ","Candidate: ")+Title+"\n"+Provider+(Date.Length>0?" • "+Date:"")+"\n"+L.T("Origem: Windows Update • correspondência validada pelo Windows","Source: Windows Update • applicability verified by Windows")+(HardwareId.Length>0?"\n"+HardwareId:"");}}
    public override string ToString(){return Title;}
}
public static class DriverUpdates {
    static readonly object gate=new object();static List<DriverOffer> cached;static DateTime cacheAt;
    public static List<DriverOffer> Search(bool force=false){
        lock(gate)if(!force&&cached!=null&&(DateTime.UtcNow-cacheAt).TotalMinutes<15)return new List<DriverOffer>(cached);
        const string script=@"$session=New-Object -ComObject Microsoft.Update.Session; $searcher=$session.CreateUpdateSearcher(); $result=$searcher.Search('IsInstalled=0 and Type=''Driver'''); foreach($u in $result.Updates){ if(-not $u.IsHidden){ $values=@($u.Identity.UpdateID,$u.Identity.RevisionNumber,$u.Title);foreach($property in @('DriverProvider','DriverModel','DriverVerDate','DriverHardwareID')){try{$value=$u.$property;if($value -is [DateTime]){$value=$value.ToString('yyyy-MM-dd')};$values+=([string]$value)}catch{$values+=''}};Write-Output (($values | ForEach-Object {([string]$_) -replace '[\r\n|]',' '}) -join '|') } }";
        var result=Runner.PS(script,180000);if(result.Code!=0)throw new InvalidOperationException(result.Output);
        var found=Parse(result.Output);List<DriverRecord> installed=null;try{installed=DriverInventory.Read();}catch{}
        if(installed!=null)foreach(var offer in found)offer.Installed=DriverInventory.Match(installed,offer.HardwareId);
        lock(gate){cached=new List<DriverOffer>(found);cacheAt=DateTime.UtcNow;}return found;
    }
    public static List<DriverOffer> Parse(string output){
        var found=new List<DriverOffer>();
        foreach(string line in (output??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){
            string[] parts=line.Split('|');Guid id;int rev;
            if(parts.Length<3||!Guid.TryParse(parts[0],out id)||!Int32.TryParse(parts[1],out rev)||rev<0)continue;
            found.Add(new DriverOffer{Id=id.ToString(),Revision=rev,Title=parts[2],Provider=parts.Length>3?parts[3]:"",Model=parts.Length>4?parts[4]:"",Date=parts.Length>5?parts[5]:"",HardwareId=parts.Length>6?parts[6]:""});
        }
        return found;
    }
    public static string Install(DriverOffer offer){
        Guid id;if(offer==null||!Guid.TryParse(offer.Id,out id)||offer.Revision<0)throw new ArgumentException("Invalid update identity");
        string script=@"$session=New-Object -ComObject Microsoft.Update.Session; $searcher=$session.CreateUpdateSearcher(); $result=$searcher.Search('IsInstalled=0 and Type=''Driver'''); $found=$null; foreach($u in $result.Updates){if($u.Identity.UpdateID -eq '"+id.ToString()+@"' -and $u.Identity.RevisionNumber -eq "+offer.Revision+@"){$found=$u;break}}; if($null -eq $found){throw 'The selected driver is no longer offered.'}; $found.AcceptEula(); $items=New-Object -ComObject Microsoft.Update.UpdateColl; [void]$items.Add($found); $downloader=$session.CreateUpdateDownloader(); $downloader.Updates=$items; $download=$downloader.Download(); if($download.ResultCode -ne 2){throw ('Download failed: '+$download.ResultCode)}; $installer=$session.CreateUpdateInstaller(); $installer.Updates=$items; $install=$installer.Install(); Write-Output ('Installation result: '+$install.ResultCode); Write-Output ('Reboot required: '+$install.RebootRequired); if($install.ResultCode -ne 2){exit 1}";
        var result=Runner.PS(script,900000);return (result.Code==0?"OK\n":"ERROR\n")+result.Output;
    }
    public static string InstallScript(DriverOffer[] selected){
        if(selected==null||selected.Length==0)throw new ArgumentException("Select drivers");
        string[] keys=selected.Select(x=>{Guid id;if(x==null||!Guid.TryParse(x.Id,out id)||x.Revision<0)throw new ArgumentException("Invalid update identity");return "'"+id.ToString()+":"+x.Revision+"'";}).ToArray();
        string script="$wanted=@("+String.Join(",",keys)+");$session=New-Object -ComObject Microsoft.Update.Session;$searcher=$session.CreateUpdateSearcher();$result=$searcher.Search('IsInstalled=0 and Type=''Driver''');$items=New-Object -ComObject Microsoft.Update.UpdateColl;foreach($u in $result.Updates){$key=$u.Identity.UpdateID+':'+$u.Identity.RevisionNumber;if($wanted -contains $key){$u.AcceptEula();[void]$items.Add($u)}};if($items.Count -ne $wanted.Count){throw 'One or more selected drivers are no longer offered.'};$failed=$false;$reboot=$false;foreach($u in $items){try{Write-Output ('OUO-STAGE|Baixando • '+$u.Title);$one=New-Object -ComObject Microsoft.Update.UpdateColl;[void]$one.Add($u);$downloader=$session.CreateUpdateDownloader();$downloader.Updates=$one;$download=$downloader.Download();if($download.ResultCode -ne 2){throw ('Download failed: '+$download.ResultCode)};Write-Output ('OUO-STAGE|Instalando • '+$u.Title);$installer=$session.CreateUpdateInstaller();$installer.Updates=$one;$installer.AllowSourcePrompts=$false;$installer.ForceQuiet=$true;$installed=$installer.Install();$reboot=$reboot -or $installed.RebootRequired;if($installed.ResultCode -ne 2){throw ('Installation failed: '+$installed.ResultCode)};Write-Output ('OK • '+$u.Title)}catch{$failed=$true;Write-Output ('ERROR • '+$u.Title+' • '+$_.Exception.Message)}};Write-Output ('Reboot required: '+$reboot);if($failed){exit 1}";
        return script;
    }
    public static string InstallMany(DriverOffer[] selected,Action<string> stage=null){
        string script=InstallScript(selected);
        var run=Runner.PS(script,1800000,line=>{if(stage!=null&&line.StartsWith("OUO-STAGE|",StringComparison.Ordinal))stage(line.Substring(10));});
        lock(gate){cached=null;cacheAt=DateTime.MinValue;}DriverInventory.Invalidate();
        return (run.Code==0?"OK":"ERROR")+"\n"+run.Output;
    }
}
}
