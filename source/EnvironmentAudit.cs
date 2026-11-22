using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
namespace Aurora {
public static class EnvironmentAudit {
    [DllImport("kernel32.dll")]static extern bool IsProcessorFeaturePresent(uint feature);
    public static int Run(){
        var report=new StringBuilder();Action<string,Func<string>> read=(name,action)=>{try{report.AppendLine(name+": "+action());}catch(Exception ex){report.AppendLine(name+": INDISPONÍVEL • "+ex.Message);}};
        report.AppendLine("OuO • auditoria de ambiente somente leitura • "+DateTime.Now.ToString("s"));
        report.AppendLine("Nenhuma configuração, disco, conta ou tarefa agendada é modificada por esta auditoria.");
        read("Administrador",()=>new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator).ToString());
        read("Windows",()=>{using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))return Convert.ToString(key.GetValue("ProductName"))+" • "+Convert.ToString(key.GetValue("DisplayVersion"))+" • build "+Convert.ToString(key.GetValue("CurrentBuildNumber"))+" • "+Convert.ToString(key.GetValue("EditionID"));});
        read("Memória",()=>{var m=Native.ReadMemory();return m==null?"indisponível":(m.Total/1073741824.0).ToString("0.0")+" GB físicos • "+(m.Free/1073741824.0).ToString("0.0")+" GB livres";});
        read("Virtualização no firmware",()=>IsProcessorFeaturePresent(21).ToString());
        read("SLAT exposta",()=>IsProcessorFeaturePresent(20).ToString());
        foreach(var name in new[]{"vmms","vmcompute","HvHost","hyperkbd","PawnIO"})read("Serviço "+name,()=>{using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\"+name))return key==null?"não registrado":"registrado • Start="+Convert.ToString(key.GetValue("Start"));});
        foreach(var drive in DriveInfo.GetDrives().Where(x=>x.DriveType==DriveType.Fixed))read("Disco "+drive.Name,()=>drive.DriveFormat+" • "+(drive.AvailableFreeSpace/1073741824.0).ToString("0.0")+" GB livres");
        read("Hyper-V / recursos",()=>{var r=Runner.Run("dism.exe","/Online /Get-FeatureInfo /FeatureName:Microsoft-Hyper-V-All /English",30000);return r.Code==0?r.Output:"código "+r.Code+" • "+r.Output;});
        read("Agendador / validar XML sem registrar",()=>{string auditXml=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics","scheduler-validate.xml");Directory.CreateDirectory(Path.GetDirectoryName(auditXml));File.WriteAllText(auditXml,DailyMaintenance.TaskXml("Registry","09:00",WindowsIdentity.GetCurrent().User.Value).ToString(System.Xml.Linq.SaveOptions.DisableFormatting),new UTF8Encoding(false));var r=Runner.PS("$ErrorActionPreference='Stop';$s=New-Object -ComObject Schedule.Service;$s.Connect();$f=$s.GetFolder('\\');$null=$f.RegisterTask('OuO_Validate_Only',[IO.File]::ReadAllText("+Runner.QuotePS(auditXml)+"),1,'SYSTEM',$null,5,$null);Write-Output 'XML válido; nenhuma tarefa registrada.'",30000);return "código "+r.Code+" • "+r.Output;});
        read("F8 / BCD somente leitura",()=>{var r=Runner.Run("bcdedit.exe","/enum "+Args.Q("{current}"),30000);return "código "+r.Code+" • "+r.Output;});
        read("Ambiente de recuperação somente leitura",()=>{var r=Runner.Run("reagentc.exe","/info",30000);return "código "+r.Code+" • "+r.Output;});
        read("Proteção do Sistema / consultar disponibilidade",()=>{var r=Runner.PS("$ErrorActionPreference='Stop';@(Get-ComputerRestorePoint | Select-Object -Last 1 Description,SequenceNumber,CreationTime) | Format-List | Out-String",30000);return "código "+r.Code+" • "+r.Output;});
        read("Mapa de disco / índice MFT somente leitura",()=>{var watch=Stopwatch.StartNew();long files=0,bytes=0;string why;System.Collections.Generic.List<DiskBucket> buckets;using(var cancel=new System.Threading.CancellationTokenSource(60000)){try{bool ok=NtfsIndex.TryScan(Path.GetPathRoot(Environment.SystemDirectory),cancel.Token,(f,b)=>{files=f;bytes=b;},null,out buckets,out why);return (ok?"OK":"INDISPONÍVEL • "+why)+" • "+watch.Elapsed.TotalSeconds.ToString("0.00")+" s • "+files+" arquivos • "+bytes+" bytes lógicos";}catch(OperationCanceledException){return "PARCIAL • limite de 60 s • "+files+" arquivos • "+bytes+" bytes lógicos";}}});
        read("Sensores",()=>{var exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Sensors","Aurora.Sensors.exe");var r=Runner.Run(exe,"--once",45000);return "código "+r.Code+" • "+r.Output;});
        string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics");Directory.CreateDirectory(folder);string output=Path.Combine(folder,"environment-audit.txt");File.WriteAllText(output,report.ToString(),new UTF8Encoding(true));return 0;
    }
}
}
