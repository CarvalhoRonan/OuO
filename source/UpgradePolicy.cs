using System;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace Aurora {
public static class UpgradePolicy {
    const string PathName=@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
    static void Validate(string product,string version){
        if((product!="Windows 11"&&product!="Windows 10")||!Regex.IsMatch(version??"",@"^\d{2}H[12]$"))throw new ArgumentException("Invalid release");
    }
    public static string Offered(string version){return Offered("Windows 11",version);}
    public static string Offered(string product,string version){
        Validate(product,version);
        string script="$session=New-Object -ComObject Microsoft.Update.Session; $search=$session.CreateUpdateSearcher(); $result=$search.Search('IsInstalled=0 and Type=''Software'''); foreach($u in $result.Updates){if($u.Title -match '"+product+"' -and $u.Title -match '"+version+"'){Write-Output $u.Title}}";
        var result=Runner.PS(script,180000);
        if(result.Code!=0)return L.T("Não foi possível conferir a oferta agora: ","Could not check the offer now: ")+result.Output;
        if(String.IsNullOrWhiteSpace(result.Output))return L.T("O Windows Update não ofereceu esta versão neste PC. Consulte a mídia oficial quando apropriado.","Windows Update has not offered this release on this PC. Check official media when appropriate.");
        return L.T("Oferta encontrada pelo Windows Update:\n","Windows Update offer found:\n")+result.Output;
    }
    public static string Apply(Journal journal,string version,bool bypass){return Apply(journal,"Windows 11",version,bypass);}
    public static string Apply(Journal journal,string product,string version,bool bypass){
        Validate(product,version);if(journal==null)throw new ArgumentNullException("journal");
        string title=product+" target "+version;int written=0;
        try{
            journal.RegistryChange(title,"HKLM",PathName,"ProductVersion",product,RegistryValueKind.String);written++;
            journal.RegistryChange(title,"HKLM",PathName,"TargetReleaseVersion",1,RegistryValueKind.DWord);written++;
            journal.RegistryChange(title,"HKLM",PathName,"TargetReleaseVersionInfo",version,RegistryValueKind.String);written++;
            if(bypass&&product=="Windows 11"){journal.RegistryChange(title,"HKLM",@"SYSTEM\Setup\MoSetup","AllowUpgradesWithUnsupportedTPMOrCPU",1,RegistryValueKind.DWord);written++;}
            return L.T("Preferência salva com backup. O Windows Update ainda decide se oferece a atualização. Ajustes registrados: ","Preference saved with backup. Windows Update still decides whether to offer it. Settings recorded: ")+written;
        }catch{for(int i=0;i<written;i++)try{journal.Undo(title);}catch{}throw;}
    }
}
}