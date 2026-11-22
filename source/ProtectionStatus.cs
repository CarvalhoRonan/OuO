using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;
namespace Aurora {
public sealed class ScheduleState {
    public string Mode,Time,LastResult,NextRun;public bool Exists,Enabled;
    public override string ToString(){return (Mode=="Registry"?L.T("Backup do Registro","Registry backup"):L.T("Ponto de restauração","Restore point"))+": "+(!Exists?L.T("Desativado • sem agenda criada.","Disabled • no schedule created."):(Enabled?L.T("Ativado","Enabled"):L.T("Desativado","Disabled"))+" • "+Time+"\n"+LastResult+(String.IsNullOrWhiteSpace(NextRun)?"":"\n"+L.T("Próxima execução: ","Next run: ")+NextRun));}
}
public static class ProtectionStatus {
    public static List<ScheduleState> Schedules(){
        string script="$service=New-Object -ComObject Schedule.Service;$service.Connect();$folder=$service.GetFolder('\\');$available=@{};foreach($task in $folder.GetTasks(0)){$available[$task.Name]=$task};$xml=New-Object System.Xml.XmlDocument;$root=$xml.CreateElement('tasks');[void]$xml.AppendChild($root);foreach($name in @('Aurora-Registry-Daily','Aurora-RestorePoint-Daily')){$node=$xml.CreateElement('task');$node.SetAttribute('name',$name);if($available.ContainsKey($name)){$task=$available[$name];$node.SetAttribute('exists','true');$node.SetAttribute('enabled',([bool]$task.Enabled).ToString().ToLowerInvariant());$doc=[xml]$task.Xml;$ns=New-Object System.Xml.XmlNamespaceManager($doc.NameTable);$ns.AddNamespace('t','http://schemas.microsoft.com/windows/2004/02/mit/task');$boundary=$doc.SelectSingleNode('//t:StartBoundary',$ns);if($boundary){$node.SetAttribute('time',$boundary.InnerText)};$node.SetAttribute('result',[string]$task.LastTaskResult);$node.SetAttribute('last',$task.LastRunTime.ToString('o',[Globalization.CultureInfo]::InvariantCulture));$node.SetAttribute('next',[string]$task.NextRunTime)}else{$node.SetAttribute('exists','false')};[void]$root.AppendChild($node)};$xml.OuterXml";
        var result=Runner.PS(script,30000);if(result.Code!=0)throw new IOException(result.Output);
        return ParseSchedules(result.Output);
    }
    public static List<ScheduleState> ParseSchedules(string output){
        var xml=XElement.Parse(output.Trim());var states=new List<ScheduleState>();
        foreach(var node in xml.Elements("task")){
            string time=(string)node.Attribute("time")??"";DateTime parsed;if(DateTime.TryParse(time,out parsed))time=parsed.ToString("HH:mm");
            string code=(string)node.Attribute("result");DateTime lastRun;bool neverRun=DateTime.TryParse((string)node.Attribute("last"),out lastRun)&&lastRun.Year<2000;
            string last=code==null?"":neverRun?L.T("Ainda não executada.","Not run yet."):code=="0"?L.T("Última execução concluída.","Last run completed."):code=="267011"?L.T("Ainda não executada.","Not run yet."):code=="267009"?L.T("Em execução.","Running."):"ERROR • "+L.T("Última execução retornou o código ","Last run returned code ")+code;
            string next=(string)node.Attribute("next")??"";DateTime nextDate;if(DateTime.TryParse(next,out nextDate))next=nextDate.Year>2000?nextDate.ToString("dd/MM/yyyy HH:mm"):"";
            states.Add(new ScheduleState{Mode=((string)node.Attribute("name")).Contains("Registry")?"Registry":"Restore",Exists=(string)node.Attribute("exists")=="true",Enabled=(string)node.Attribute("enabled")=="true",Time=time,LastResult=last,NextRun=next});
        }
        return states;
    }
    public static string HyperV(bool admin){
        if(Environment.OSVersion.Version.Build<9200)return L.T("Indisponível • este Windows não oferece o Hyper-V cliente completo.","Unavailable • this Windows release does not offer full client Hyper-V.");
        string edition="";using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))if(key!=null)edition=Convert.ToString(key.GetValue("EditionID"));
        var report=new StringBuilder();report.AppendLine(L.T("Edição do Windows: ","Windows edition: ")+edition);
        bool hypervisor=false;try{hypervisor=Machine.First("SELECT HypervisorPresent FROM Win32_ComputerSystem","HypervisorPresent").Equals("True",StringComparison.OrdinalIgnoreCase);}catch{}
        report.AppendLine(hypervisor?L.T("Ativado • hipervisor já detectado. Algumas capacidades da CPU podem ficar ocultas durante a execução.","Enabled • hypervisor already detected. Some CPU capabilities may be hidden while it runs."):L.T("Hipervisor não detectado nesta consulta.","Hypervisor not detected in this query."));
        try{foreach(var cpu in Machine.Query("root\\cimv2","SELECT Name,VirtualizationFirmwareEnabled,SecondLevelAddressTranslationExtensions,VMMonitorModeExtensions FROM Win32_Processor"))using(cpu){
            report.AppendLine(Machine.Value(cpu,"Name"));
            foreach(var pair in new[]{new[]{"VirtualizationFirmwareEnabled",L.T("Virtualização no firmware","Firmware virtualization")},new[]{"SecondLevelAddressTranslationExtensions","SLAT"},new[]{"VMMonitorModeExtensions",L.T("Suporte de virtualização da CPU","CPU virtualization support")}}){
                string value=Machine.Value(cpu,pair[0]);report.AppendLine(pair[1]+": "+(value=="True"?L.T("Ativado","Enabled"):value=="False"&&!hypervisor?L.T("Desativado","Disabled"):L.T("Não confirmado","Not confirmed")));
            }
        }}catch(Exception ex){report.AppendLine(L.T("Leitura das capacidades indisponível: ","Capability query unavailable: ")+ex.Message);}
        if(Regex.IsMatch(edition,@"^(Core|Starter)",RegexOptions.IgnoreCase))return L.T("Indisponível • Hyper-V completo não está incluído nesta edição.\n","Unavailable • full Hyper-V is not included in this edition.\n")+report;
        if(!admin)return report+L.T("\nAbra o OuO como administrador para checar o estado do recurso Hyper-V.","\nOpen OuO as administrator to check the Hyper-V feature state.");
        var feature=Runner.Run("dism.exe","/Online /Get-FeatureInfo /FeatureName:Microsoft-Hyper-V-All /English",90000);
        if(feature.Code!=0)return "ERROR • "+L.T("Não foi possível consultar o recurso Hyper-V.","Could not query the Hyper-V feature.")+"\n"+report+"\n"+feature.Output;
        var state=Regex.Match(feature.Output,@"(?im)^\s*State\s*:\s*(.+)");
        string raw=state.Success?state.Groups[1].Value.Trim():"Unknown";
        return (raw.Equals("Enabled",StringComparison.OrdinalIgnoreCase)?L.T("Ativado • Hyper-V","Enabled • Hyper-V"):raw.StartsWith("Disabled",StringComparison.OrdinalIgnoreCase)?L.T("Desativado • Hyper-V","Disabled • Hyper-V"):L.T("Estado do Hyper-V: ","Hyper-V state: ")+raw)+"\n"+report;
    }
}
}
