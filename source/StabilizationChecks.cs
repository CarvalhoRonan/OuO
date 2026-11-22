using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace Aurora {
public static class StabilizationChecks {
    public static void Run(Action<bool,string> check,string folder){
        const string product="{12345678-1234-1234-1234-123456789ABC}",other="{22345678-1234-1234-1234-123456789ABC}";
        string native=Path.Combine(folder,"Vendor App","uninstall.exe");
        check(!MsiRemoval.IsMsiProduct(product,null,Args.Q(native)+" /S")&&!MsiRemoval.IsMsiProduct("not-a-product",1,""),"GUID-shaped EXE uninstall entries are not mistaken for MSI products");
        check(MsiRemoval.IsMsiProduct(product,1,"")&&MsiRemoval.IsMsiProduct(product,null,Args.Q(Path.Combine(Environment.SystemDirectory,"MsiExec.exe"))+" /I"+product)&&MsiRemoval.IsMsiProduct(product,0,"MsiExec.exe /X "+Args.Q(product)),"MSI detection accepts its installer marker and matching quoted or compact product commands");
        check(!MsiRemoval.IsMsiProduct(product,null,"MsiExec.exe /X "+other)&&!MsiRemoval.IsMsiProduct(product,null,"not-msiexec.exe /X "+product),"MSI fallback requires the same product code and the real executable name");
        var app=new InstalledProgram{Name="Owned vendor fixture",KeyName=product,Uninstall=Args.Q(native)+" /S"};string args;
        string file=ProgramRemoval.UninstallCommand(app,false,out args);check(file==native&&args=="/S","normal uninstall preserves a GUID-shaped EXE program's vendor command");
        file=ProgramRemoval.UninstallCommand(app,true,out args);check(file=="winget.exe"&&args.Contains("--name \"Owned vendor fixture\"")&&args.Contains("--exact")&&!args.Contains(product),"alternate removal of an EXE program does not invoke Windows Installer using its registry GUID");
        app.WindowsInstaller=true;file=ProgramRemoval.UninstallCommand(app,true,out args);check(Path.GetFileName(file)=="msiexec.exe"&&args=="/x "+product+" /qn /norestart","confirmed MSI product uses its validated product code for silent removal");
        check(ProgramRemoval.RemovalResult("fixture",0,true).StartsWith("ERROR"),"uninstaller exit zero cannot confirm removal while the program remains registered");
        check(ProgramRemoval.RemovalResult("fixture",1603,false).StartsWith("ERROR")&&ProgramRemoval.RemovalResult("fixture",-1,false).StartsWith("ERROR"),"failed or timed-out removal cannot become success merely because an inventory entry disappeared");
        check(ProgramRemoval.RemovalResult("fixture",0,false).StartsWith("OK")&&ProgramRemoval.RemovalResult("fixture",3010,false).StartsWith("OK"),"successful removal requires a successful exit and an absent installed entry");
        bool language=L.Pt;try{foreach(bool pt in new[]{true,false}){L.Pt=pt;check(ResultTone.Classify(ProgramRemoval.RemovalResult("fixture",3010,true))==0&&ResultTone.Classify(ProgramRemoval.RemovalResult("fixture",1602,true))==0,"pending restart and user cancellation stay neutral in "+(pt?"Portuguese":"English"));}}finally{L.Pt=language;}
        check(OperationLanes.Lane("Recursos opcionais")=="servicing"&&OperationLanes.Lane("Optional features")=="servicing"&&OperationLanes.Lane("Reparo do sistema")=="servicing"&&OperationLanes.Lane("System repair")=="servicing"&&OperationLanes.Lane("Corrigir WinGet")=="installers"&&OperationLanes.Lane("Desinstalação forçada")=="installers","localized repairs, optional features and forced uninstall participate in their operation queues");
        check(OperationLanes.Lane("DHCP renew")=="network"&&OperationLanes.Lane("Printer preferences")=="printing"&&OperationLanes.Lane("Reinstall printer queue")=="printing"&&OperationLanes.Lane("Check features")==""&&OperationLanes.Lane("Print inventory")=="","network and printer changes serialize while independent read-only queries stay concurrent");
        VerifyConcurrency(check);
        VerifyRepairResults(check);
    }
    static void VerifyRepairResults(Action<bool,string> check){
        string captured="OK\nA Proteção de Recursos do Windows encontrou arquivos corrompidos, mas não conseguiu corrigir alguns deles.";check(ResultTone.Classify(captured)<0&&ResultTone.Completion(captured)<0,"captured Portuguese SFC partial-repair wording remains a failure despite process exit zero");
        foreach(string failure in new[]{"A Proteção de Recursos do Windows não pôde executar a operação solicitada.","A Proteção de Recursos do Windows não pode executar a operação solicitada.","A Proteção de Recursos do Windows não pôde iniciar o serviço de reparo.","Windows Resource Protection could not perform the requested operation."})check(ResultTone.Classify("OK\n"+failure)<0&&ResultTone.Completion("OK\n"+failure)<0,"SFC blocked-operation message cannot appear as successful: "+failure);
        string pending="OK\nThere is a system repair pending which requires reboot to complete.";check(ResultTone.Classify(pending)==0&&ResultTone.Completion(pending)==0&&ResultTone.Classify("OK\nExiste um reparo do sistema pendente que requer reinicialização.")==0,"pending system repair stays neutral instead of becoming a green completion");
        string pendingPt="Há um reparo de sistema pendente; é preciso reinicializar o computador para concluir a operação.";
        foreach(string raw in new[]{pendingPt,"There is a system repair pending which requires reboot to complete."}){string shown=RepairPlans.Present(new RepairCommand{File="sfc.exe"},new CommandResult{Code=1,Output=raw},"ERROR 1\n"+raw);check(ResultTone.Classify(shown)==0&&ResultTone.Completion(shown)==0&&shown.Contains("(1)")&&shown.Contains(raw),"native SFC pending result preserves exit 1 and remains neutral in the displayed report");}
        check(ResultTone.Classify(RepairPlans.Present(new RepairCommand{File="other.exe"},new CommandResult{Code=1,Output=pendingPt},"ERROR 1\n"+pendingPt))<0&&ResultTone.Classify(RepairPlans.Present(new RepairCommand{File="sfc.exe"},new CommandResult{Code=5,Output=pendingPt},"ERROR 5\n"+pendingPt))<0,"only the known SFC pending exit code can become a deferred result");
        string mixed="ERROR: access is denied\n"+pendingPt;check(ResultTone.Classify(RepairPlans.Present(new RepairCommand{File="sfc.exe"},new CommandResult{Code=1,Output=mixed},"ERROR 1\n"+mixed))<0,"a pending message cannot hide an additional repair failure");
        string localizedDism="OK\nFerramenta de Gerenciamento e Manutenção de Imagens de Implantação\nO repositório de componentes é reparável.\nA operação foi concluída com êxito.";check(ResultTone.Classify(localizedDism)<0&&ResultTone.Completion(localizedDism)<0,"captured localized DISM CheckHealth cannot report a corrupted store as a positive completion");
        check(ResultTone.Classify("OK\nThere is no system repair pending.")>0,"negative pending-repair wording does not become a pending operation");
        bool refused=false;try{RepairPlans.Dism("RestoreHealth",null,"","1",true);}catch(ArgumentException){refused=true;}check(refused&&!RepairPlans.Dism("RestoreHealth",null,"","1",false).Arguments.Contains("/LimitAccess"),"repair plan rejects missing local source for LimitAccess while preserving the automatic-source mode");
        bool language=L.Pt;try{foreach(bool pt in new[]{true,false}){L.Pt=pt;var command=new RepairCommand{File="dism.exe"};string source=RepairPlans.Guidance(command,new CommandResult{Code=unchecked((int)0x800f081f)}),network=RepairPlans.Guidance(command,new CommandResult{Code=unchecked((int)0x800f0906)});check(source.Length>40&&network.Length>40&&ResultTone.Classify(source)<0&&ResultTone.Classify(network)<0&&RepairPlans.Guidance(new RepairCommand{File="sfc.exe"},new CommandResult{Code=unchecked((int)0x800f081f)})=="","DISM source/download explanations stay negative and tool-specific in "+(pt?"Portuguese":"English"));}}finally{L.Pt=language;}
        string rollback=RepairPlans.Present(new RepairCommand{File="dism.exe"},new CommandResult{Code=3017,Output="The requested operation failed. A system reboot is required to roll back changes made."},"ERROR 3017");check(rollback.Contains("3017")&&ResultTone.Classify(rollback)<0,"DISM failed rollback requiring reboot is never reported as a successful restart request");
    }
    static void VerifyConcurrency(Action<bool,string> check){
        using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())using(var queued=new ManualResetEventSlim())using(var secondEntered=new ManualResetEventSlim())using(var queried=new ManualResetEventSlim()){
            Task first=Task.Factory.StartNew(()=>OperationLanes.Execute("Recursos opcionais",()=>{entered.Set();if(!release.Wait(10000))throw new TimeoutException("Owned queue fixture release timed out");return "first";},null));
            Task second=null,reader=null;bool firstReady=false,waiting=false,readDone=false,secondBlocked=false;
            try{
                firstReady=entered.Wait(5000);
                if(firstReady){second=Task.Factory.StartNew(()=>OperationLanes.Execute("WinGet install",()=>{secondEntered.Set();return "second";},()=>queued.Set()));reader=Task.Factory.StartNew(()=>OperationLanes.Execute("Check features",()=>{queried.Set();return "read";},null));waiting=queued.Wait(5000);readDone=queried.Wait(5000);secondBlocked=!secondEntered.IsSet;}
            }finally{release.Set();first.Wait(15000);if(second!=null)second.Wait(15000);if(reader!=null)reader.Wait(15000);}
            check(firstReady&&waiting&&readDone&&secondBlocked&&secondEntered.IsSet,"real concurrent delegates prove servicing blocks a conflicting installer but not an independent query, then releases the installer");
        }
    }
}
}
