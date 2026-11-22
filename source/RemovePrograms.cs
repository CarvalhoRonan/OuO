using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;

namespace Aurora {
public sealed class InstalledProgram {
    public string Name{get;set;} public string Publisher{get;set;} public string Version{get;set;} public string Location{get;set;} public string Uninstall{get;set;} public string KeyName{get;set;} public string Scope{get;set;} public string Package{get;set;} public string DisplayIcon{get;set;}
    public bool Store,WindowsInstaller;public ImageSource StoreIcon;
    public bool IsMsi{get{return !Store&&MsiRemoval.IsMsiProduct(KeyName,WindowsInstaller?1:0,Uninstall);}}
    public string Description{get{return Name+"\n"+Id+"\n"+(Store?"Pacote: "+Package+"\n":"")+Location;}}
    public string Id{get{return (Publisher??"")+" • "+(Version??"")+" • "+(Scope??"");}}
    ImageSource cachedIcon;
    public ImageSource Icon{get{if(cachedIcon!=null)return cachedIcon;if(Store)return cachedIcon=StoreIcon??AppIcons.FromInstallLocation(Location,Name);ImageSource icon=AppIcons.FromCommand(String.IsNullOrWhiteSpace(DisplayIcon)?Uninstall:DisplayIcon);return cachedIcon=Object.ReferenceEquals(icon,AppIcons.Generic())?AppIcons.FromInstallLocation(Location,Name):icon;}}
    public override string ToString(){return Name+(String.IsNullOrWhiteSpace(Version)?"":" • "+Version);}
}
public sealed class ProgramTrace {
    public string Kind,Path,Reason;public InstalledProgram Owner;
    public override string ToString(){return Kind+" • "+Path;}
}
public static class ProgramRemoval {
    public static string StoreInventoryError="";
    static readonly string UninstallRoot=@"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    public static List<InstalledProgram> Inventory(bool includeStore){
        StoreInventoryError="";var list=new List<InstalledProgram>();
        foreach(RegistryHive hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})foreach(RegistryView view in new[]{RegistryView.Registry64,RegistryView.Registry32})try{
            using(var baseKey=RegistryKey.OpenBaseKey(hive,view))using(var root=baseKey.OpenSubKey(UninstallRoot)){
                if(root==null)continue;
                foreach(string name in root.GetSubKeyNames())try{using(var key=root.OpenSubKey(name)){
                    string title=Convert.ToString(key.GetValue("DisplayName"));
                    if(String.IsNullOrWhiteSpace(title)||Convert.ToString(key.GetValue("SystemComponent"))=="1"||key.GetValue("ParentKeyName")!=null)continue;
                    string command=Convert.ToString(key.GetValue("UninstallString"));
                    bool isMsi=MsiRemoval.IsMsiProduct(name,key.GetValue("WindowsInstaller"),command);
                    if(String.IsNullOrWhiteSpace(command)&&!isMsi)continue;
                    list.Add(new InstalledProgram{Name=title,Publisher=Convert.ToString(key.GetValue("Publisher")),Version=Convert.ToString(key.GetValue("DisplayVersion")),Location=Convert.ToString(key.GetValue("InstallLocation")),Uninstall=command,DisplayIcon=Convert.ToString(key.GetValue("DisplayIcon")),KeyName=name,Scope=hive==RegistryHive.CurrentUser?"User":"Machine",WindowsInstaller=isMsi});
                }}catch{}
            }
        }catch{}
        if(includeStore)try{list.AddRange(StoreApps.Inventory());}catch(Exception ex){StoreInventoryError=ex.Message;}
        return list.GroupBy(x=>x.Store?"Store|"+x.Package:x.Scope+"|"+x.KeyName,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    public static bool StillInstalled(InstalledProgram item){
        return Inventory(false).Any(x=>!x.Store&&x.Scope==item.Scope&&x.KeyName==item.KeyName);
    }
    static string ParseExecutable(string command,out string args){
        args="";string value=Environment.ExpandEnvironmentVariables((command??"").Trim());
        if(value.Length==0)throw new ArgumentException("No uninstall command");
        if(value.StartsWith("\"")){int end=value.IndexOf('"',1);if(end<2)throw new ArgumentException("Invalid uninstall command");args=value.Substring(end+1).Trim();return value.Substring(1,end-1);}
        Match match=Regex.Match(value,@"^(.+?\.(?:exe|com))(?=\s|$)",RegexOptions.IgnoreCase);
        if(!match.Success)throw new ArgumentException("Uninstaller must be an executable");
        args=value.Substring(match.Length).Trim();return match.Groups[1].Value;
    }
    public static string UninstallCommand(InstalledProgram item,bool force,out string args){
        if(item==null||item.Store)throw new ArgumentException("Select a desktop program");
        if(item.IsMsi){args="/x "+item.KeyName+(force?" /qn /norestart":" /passive /norestart");return Path.Combine(Environment.SystemDirectory,"msiexec.exe");}
        if(force){if(String.IsNullOrWhiteSpace(item.Name)||item.Name.Length>120||item.Name.IndexOfAny(new[]{'\r','\n'})>=0)throw new ArgumentException("Invalid package name");args="uninstall --name "+Args.Q(item.Name)+" --exact --silent --disable-interactivity --accept-source-agreements";return "winget.exe";}
        string exe=ParseExecutable(item.Uninstall,out args);
        if(Path.GetFileName(exe).Equals("msiexec.exe",StringComparison.OrdinalIgnoreCase))args=Regex.Replace(args,@"(?i)(?:^|\s)/i(?=\s|\{)"," /x");
        if(!Path.IsPathRooted(exe)&&!new[]{"msiexec.exe","rundll32.exe"}.Contains(Path.GetFileName(exe),StringComparer.OrdinalIgnoreCase))throw new ArgumentException("Uninstaller path is not absolute");
        return exe;
    }
    public static string RemovalResult(string name,int code,bool stillInstalled){
        string prefix=name+" • ";
        if(code==1602)return "AVISO • "+prefix+L.T("desinstalação cancelada pelo usuário.","uninstallation canceled by user.");
        if(code!=0&&code!=3010&&code!=1641)return "ERROR • "+prefix+L.T("código ","exit code ")+code+(code==1618?L.T(" • outro instalador está em execução; aguarde antes de tentar novamente."," • another installer is running; wait before retrying."):"");
        bool restart=code==3010||code==1641;
        if(stillInstalled)return (restart?"AVISO • ":"ERROR • ")+prefix+(restart?L.T("a entrada ainda existe; reinicialização necessária. Consulte a lista após reiniciar; os rastros permanecem protegidos.","the entry remains; restart required. Check the list after reboot; remnants remain protected."):L.T("a entrada ainda existe; remoção não confirmada. Confira o desinstalador antes de repetir.","the entry remains; removal not confirmed. Check the uninstaller before retrying."));
        return "OK • "+prefix+L.T("remoção confirmada na lista instalada.","removal confirmed in the installed list.")+(restart?L.T(" Reinicialização necessária."," Restart required."):"");
    }
    public static string Remove(InstalledProgram item){
        if(item==null)throw new ArgumentException("No program selected");
        if(item.Store){if(!Regex.IsMatch(item.Package??"",@"^[A-Za-z0-9._-]+$"))throw new ArgumentException("Invalid package");var r=Runner.PS("Remove-AppxPackage -Package "+Runner.QuotePS(item.Package)+" -ErrorAction Stop; Write-Output 'Removed'",900000);return r.Code==0?"OK • "+item.Name+"\n"+r.Output:"ERROR • "+item.Name+"\n"+r.Output;}
        string args,exe=UninstallCommand(item,false,out args);
        if(Path.IsPathRooted(exe)&&!File.Exists(exe))throw new FileNotFoundException("Uninstaller not found",exe);
        using(Process process=new Process()){
            process.StartInfo=new ProcessStartInfo(exe,args){UseShellExecute=true};
            process.Start();if(!process.WaitForExit(1800000))return "ERROR • "+item.Name+" • "+L.T("desinstalador ainda em execução; confira antes de pesquisar rastros.","uninstaller is still running; check before scanning traces.");
            int code=process.ExitCode;
            return RemovalResult(item.Name,code,StillInstalled(item));
        }
    }
    public static string ForceUninstall(InstalledProgram item){
        if(item==null||item.Store)throw new ArgumentException(L.T("Selecione um programa de desktop.","Select a desktop program."));
        string args,exe=UninstallCommand(item,true,out args);
        CommandResult result=Runner.Run(exe,args,1800000,allowBackground:true);
        return RemovalResult(item.Name,result.Code,StillInstalled(item))+"\n"+result.Output;
    }
    static bool CandidateDirectory(string candidate,string root){
        try{
            string full=Path.GetFullPath(candidate).TrimEnd('\\'),basePath=Path.GetFullPath(root).TrimEnd('\\');
            if(!String.Equals(Path.GetDirectoryName(full),basePath,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(full))return false;
            if((new DirectoryInfo(full).Attributes&FileAttributes.ReparsePoint)!=0)return false;
            return true;
        }catch{return false;}
    }
    static bool SafeTraceDirectory(string path,InstalledProgram owner,List<InstalledProgram> installed,string[] runningPaths){
        try{
            string full=Path.GetFullPath(path).TrimEnd((char)92);
            if(!Directory.Exists(full)||(new DirectoryInfo(full).Attributes&FileAttributes.ReparsePoint)!=0)return false;
            string leaf=Path.GetFileName(full),name=Regex.Replace(owner.Name??"","[^A-Za-z0-9]","").ToLowerInvariant();
            string normalized=Regex.Replace(leaf,"[^A-Za-z0-9]","").ToLowerInvariant();
            if(normalized.Length<5||!name.Contains(normalized)||Regex.IsMatch(leaf,@"^(Microsoft|Windows|Common Files)(?:$|[ ._-])",RegexOptions.IgnoreCase))return false;
            foreach(var other in installed){
                if(other.Scope==owner.Scope&&other.KeyName==owner.KeyName)continue;
                if(String.IsNullOrWhiteSpace(other.Location))continue;
                string occupied=Path.GetFullPath(other.Location).TrimEnd((char)92);
                if(occupied.Equals(full,StringComparison.OrdinalIgnoreCase)||occupied.StartsWith(full+((char)92),StringComparison.OrdinalIgnoreCase))return false;
            }
            foreach(string running in runningPaths)if(running.StartsWith(full+((char)92),StringComparison.OrdinalIgnoreCase))return false;
            return true;
        }catch{return false;}
    }
    public static List<ProgramTrace> FindTraces(InstalledProgram app){
        var result=new List<ProgramTrace>();if(app==null)return result;
        if(app.Store){
            string path=AppxRemnants.SafePath(app.Package);
            if(path!=null&&!AppxRemnants.Installed(app.Package)&&Directory.Exists(path))result.Add(new ProgramTrace{Kind="Pasta",Path=path,Reason="Dados locais do pacote removido",Owner=app});
            return result;
        }
        List<InstalledProgram> installed=Inventory(false);
        if(installed.Any(x=>!x.Store&&x.Scope==app.Scope&&x.KeyName==app.KeyName))return result;
        var runningPaths=new List<string>();foreach(Process process in Process.GetProcesses())using(process)try{runningPaths.Add(process.MainModule.FileName);}catch{}
        string simple=Regex.Replace(app.Name??"","[^A-Za-z0-9 ._-]","").Trim();
        if(simple.Length<5||Regex.IsMatch(simple,@"^(Microsoft|Windows|Common Files|Program Files)$",RegexOptions.IgnoreCase))return result;
        var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)};
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string root in roots.Where(x=>!String.IsNullOrWhiteSpace(x))){
            string path=Path.Combine(root,simple);
            if(CandidateDirectory(path,root)&&SafeTraceDirectory(path,app,installed,runningPaths.ToArray())&&paths.Add(path))result.Add(new ProgramTrace{Kind="Pasta",Path=path,Reason="Nome exato do programa em local comum",Owner=app});
        }
        if(!String.IsNullOrWhiteSpace(app.Location)&&MsiRemoval.ValidRestorePath(app.Location)&&SafeTraceDirectory(app.Location,app,installed,runningPaths.ToArray())&&paths.Add(Path.GetFullPath(app.Location)))
            result.Add(new ProgramTrace{Kind="Pasta",Path=Path.GetFullPath(app.Location),Reason="Local de instalação registrado",Owner=app});
        if(RegistryTraces.Exists(simple))result.Add(new ProgramTrace{Kind="Registro",Path=RegistryTraces.PathFor(simple),Reason="Chave do usuário com nome exato do programa",Owner=app});
        return result;
    }
    public static string Quarantine(ProgramTrace trace,string folder){
        if(trace==null||trace.Owner==null)throw new ArgumentException("Invalid trace");
        if(trace.Kind=="Registro"){if(!FindTraces(trace.Owner).Any(x=>x.Kind=="Registro"&&String.Equals(x.Path,trace.Path,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Trace no longer eligible");return RegistryTraces.Quarantine(Regex.Replace(trace.Owner.Name??"","[^A-Za-z0-9 ._-]","").Trim(),folder);}
        if(trace.Kind!="Pasta"||!FindTraces(trace.Owner).Any(x=>x.Kind=="Pasta"&&String.Equals(x.Path,trace.Path,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Trace no longer eligible");
        string target=Path.Combine(folder,"RemovedProgram-"+Guid.NewGuid().ToString("N"));Directory.Move(trace.Path,target);
        File.WriteAllText(target+".original",trace.Path);return target;
    }
    public static string DeleteTrace(ProgramTrace trace,string backupFolder){
        if(trace==null||trace.Owner==null)throw new ArgumentException("Invalid trace");
        if(!FindTraces(trace.Owner).Any(x=>x.Kind==trace.Kind&&String.Equals(x.Path,trace.Path,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException(L.T("Rastro não confirmado após nova checagem.","Trace not confirmed by the latest check."));
        if(trace.Kind=="Registro")return L.T("Excluído do Registro • cópia em ","Deleted from Registry • backup at ")+RegistryTraces.Quarantine(Regex.Replace(trace.Owner.Name??"","[^A-Za-z0-9 ._-]","").Trim(),backupFolder);
        if(trace.Kind!="Pasta"||!Directory.Exists(trace.Path))throw new InvalidOperationException("Invalid trace path");
        FileSystem.DeleteDirectory(trace.Path,UIOption.OnlyErrorDialogs,RecycleOption.SendToRecycleBin);
        return L.T("Excluído • enviado à Lixeira","Deleted • sent to Recycle Bin");
    }
}
public partial class MainWindow {
    void Bloat(){
        Heading(T("Remover Programas","Remove Programs"),T("Desinstale primeiro; depois revise os rastros encontrados.","Uninstall first; then review any remaining traces."));
        var card=Card(T("Programas instalados","Installed programs"),T("Pesquise pelo nome e selecione um ou mais programas. O desinstalador do fabricante pode abrir uma janela própria.","Search by name and select one or more programs. The vendor uninstaller may open its own window."));
        TextBox search=Input("");search.ToolTip=T("Pesquisar programas","Search programs");card.Children.Add(search);
        ListBox programs=new ListBox{Height=350,Background=Surface,Foreground=Ink,BorderBrush=Muted,Margin=new Thickness(0,0,0,10)};card.Children.Add(programs);
        TextBlock statusLine=Text(T("Clique em Atualizar lista.","Click Refresh list."),13,Muted);card.Children.Add(statusLine);
        List<InstalledProgram> inventory=new List<InstalledProgram>();
        List<InstalledProgram> recentUninstalled=new List<InstalledProgram>();
        var checkedPrograms=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Func<InstalledProgram,string> programKey=app=>app.Store?"Store|"+app.Package:app.Scope+"|"+app.KeyName;
        Action filter=()=>{string term=search.Text.Trim();programs.Items.Clear();foreach(var app in inventory.Where(x=>term.Length==0||x.Name.IndexOf(term,StringComparison.CurrentCultureIgnoreCase)>=0)){
            string key=programKey(app);var choice=new CheckBox{IsChecked=checkedPrograms.Contains(key),Foreground=Ink,Margin=new Thickness(5,6,5,6),ToolTip=app.Description};
            var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new Image{Source=app.Icon,Width=30,Height=30,Margin=new Thickness(0,0,10,0)});
            var labels=new StackPanel();labels.Children.Add(Text(app.Name,14,Ink));labels.Children.Add(Text(app.Id,11,Muted));row.Children.Add(labels);choice.Content=row;
            choice.Checked+=(s,e)=>checkedPrograms.Add(key);choice.Unchecked+=(s,e)=>checkedPrograms.Remove(key);programs.Items.Add(choice);
        }};
        search.TextChanged+=(s,e)=>filter();
        var tracesCard=Card(T("Rastros para revisar","Traces to review"),T("Após a desinstalação, o OuO mostra pastas e entradas do Registro associadas ao programa. Selecione apenas os rastros que deseja excluir; pastas vão para a Lixeira e entradas do Registro recebem uma cópia.","After removal, OuO shows related folders and registry entries. Select only traces to delete; folders go to Recycle Bin and Registry entries are backed up."));
        tracesCard.Visibility=Visibility.Collapsed;
        ListBox traces=new ListBox{Height=230,SelectionMode=SelectionMode.Multiple,Background=Surface,Foreground=Ink,BorderBrush=Muted};tracesCard.Children.Add(traces);
        TextBlock traceNote=Text("",13,StateGreen);tracesCard.Children.Add(traceNote);
        Row(tracesCard,Button(T("Procurar rastros novamente","Search traces again"),()=>{var owners=recentUninstalled.ToArray();if(owners.Length==0)return;List<ProgramTrace> reviewed=null;Work(T("Procurar rastros","Search traces"),()=>{reviewed=owners.SelectMany(ProgramRemoval.FindTraces).ToList();return "OK • "+reviewed.Count+T(" rastro(s) verificado(s)."," verified trace(s).");},r=>{traces.Items.Clear();foreach(var trace in reviewed??new List<ProgramTrace>())traces.Items.Add(trace);traceNote.Text=r;traceNote.Foreground=OutcomeBrush(r);});}),Button(T("Excluir rastros selecionados","Delete selected traces"),()=>{
            ProgramTrace[] chosen=traces.SelectedItems.OfType<ProgramTrace>().ToArray();if(chosen.Length==0||!RequireAdmin())return;
            if(!Confirm(T("Excluir estes rastros? Revise os caminhos:\n","Delete these traces? Review paths:\n")+String.Join("\n",chosen.Select(x=>x.Path).ToArray())))return;
            var removed=new List<ProgramTrace>();Work(T("Excluir rastros","Delete traces"),()=>{var report=new StringBuilder();foreach(var trace in chosen)try{report.AppendLine(trace.Path+" → "+ProgramRemoval.DeleteTrace(trace,journal.Folder));removed.Add(trace);}catch(Exception ex){report.AppendLine("ERROR • "+trace.Path+" • "+ex.Message);}return report.ToString();},r=>{traceNote.Text=r;traceNote.Foreground=OutcomeBrush(r);foreach(var t in removed)traces.Items.Remove(t);});
        }));
        Button refresh=Button(T("Atualizar lista","Refresh list"),()=>{
            List<InstalledProgram> found=null;Work(T("Listar programas","List programs"),()=>{found=ProgramRemoval.Inventory(machine!=null&&machine.Build>=9200);return found.Count.ToString()+(ProgramRemoval.StoreInventoryError.Length>0?"\nERROR • Store: "+ProgramRemoval.StoreInventoryError:"");},r=>{if(found!=null){inventory=found;filter();statusLine.Text=found.Count+T(" programas encontrados."," programs found.")+(ProgramRemoval.StoreInventoryError.Length>0?"\nERROR • Store: "+ProgramRemoval.StoreInventoryError:"");}else statusLine.Text=r;statusLine.Foreground=found!=null&&ProgramRemoval.StoreInventoryError.Length==0?StateGreen:OutcomeBrush("ERROR • "+r);});
        }); Button uninstall=Button(T("Desinstalar selecionados","Uninstall selected"),()=>{
            InstalledProgram[] selected=inventory.Where(x=>checkedPrograms.Contains(programKey(x))).ToArray();if(selected.Length==0)return;
            if(selected.Any(x=>x.Scope=="Machine")&&!RequireAdmin())return;
            if(!Confirm(T("Desinstalar estes programas? Ao terminar, o OuO procurará rastros para sua revisão:\n","Uninstall these programs? Afterward OuO will search for traces for your review:\n")+String.Join("\n",selected.Select(x=>x.Name).ToArray())))return;
            List<ProgramTrace> found=null;List<InstalledProgram> after=null;List<InstalledProgram> successful=null;
            Work(T("Desinstalar programas","Uninstall programs"),()=>{
                var report=new StringBuilder();var candidates=new List<ProgramTrace>();successful=new List<InstalledProgram>();
                foreach(var app in selected){string outcome;try{outcome=ProgramRemoval.Remove(app);}catch(Exception ex){outcome="ERROR • "+app.Name+" • "+ex.Message;}report.AppendLine(outcome);
                    if(outcome.StartsWith("OK",StringComparison.Ordinal)){successful.Add(app);try{candidates.AddRange(ProgramRemoval.FindTraces(app));}catch(Exception ex){report.AppendLine("ERROR • busca de rastros: "+ex.Message);}}
                }found=candidates;after=ProgramRemoval.Inventory(machine!=null&&machine.Build>=9200);return report.ToString();
            },r=>{statusLine.Text=r;statusLine.Foreground=OutcomeBrush(r);if(successful!=null)recentUninstalled=successful;if(after!=null){inventory=after;checkedPrograms.IntersectWith(new HashSet<string>(inventory.Select(programKey),StringComparer.OrdinalIgnoreCase));filter();}traces.Items.Clear();foreach(var item in found??new List<ProgramTrace>())traces.Items.Add(item);
                tracesCard.Visibility=Visibility.Visible;traceNote.Text=traces.Items.Count==0?T("Nenhum rastro elegível encontrado. Se o programa ainda estiver listado, conclua a desinstalação antes da limpeza.","No eligible trace found. If the app remains listed, complete uninstallation before cleanup."):traces.Items.Count+T(" rastro(s) encontrados. Revise antes de excluir."," trace(s) found. Review before deletion.");
            });
        });Row(card,refresh,uninstall,Button(T("Desinstalação forçada","Force uninstall"),()=>{
            InstalledProgram[] selected=inventory.Where(x=>checkedPrograms.Contains(programKey(x))).ToArray();
            if(selected.Length!=1||selected[0].Store){ShowNotice(T("Selecione um único programa de desktop. Aplicativos da Store usam a remoção normal.","Select one desktop program. Store apps use normal removal."));return;}
            if(selected[0].Scope=="Machine"&&!RequireAdmin())return;
            if(!Confirm(T("Tentar a remoção alternativa de ","Try an alternate uninstaller for ")+selected[0].Name+T("? O OuO usará o Windows Installer para MSI ou WinGet para outros programas. Isto não apaga pastas do sistema nem simula que a remoção funcionou.","? OuO uses Windows Installer for MSI or WinGet for other apps. This does not delete system folders or pretend removal succeeded.")))return;
            InstalledProgram target=selected[0];List<InstalledProgram> after=null;List<ProgramTrace> found=null;
            Work(T("Desinstalação forçada","Force uninstall"),()=>{string result=ProgramRemoval.ForceUninstall(target);if(result.StartsWith("OK",StringComparison.Ordinal))found=ProgramRemoval.FindTraces(target);after=ProgramRemoval.Inventory(machine!=null&&machine.Build>=9200);return result;},r=>{
                statusLine.Text=r;statusLine.Foreground=OutcomeBrush(r);if(after!=null){inventory=after;checkedPrograms.Remove(programKey(target));filter();}
                if(found!=null){recentUninstalled=new List<InstalledProgram>{target};traces.Items.Clear();foreach(var trace in found)traces.Items.Add(trace);tracesCard.Visibility=Visibility.Visible;traceNote.Text=found.Count+T(" rastro(s) para revisar."," trace(s) to review.");}
            });
        }));pageRefreshActions["bloat"]=()=>refresh.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));if(!preview)Dispatcher.BeginInvoke(pageRefreshActions["bloat"]);
    }
}
}
