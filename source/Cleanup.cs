using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;

namespace Aurora {
public sealed class CleanItem {
    public string Path,Source;public long Bytes;public DateTime Modified;
    public override string ToString(){return Source+" • "+System.IO.Path.GetFileName(Path)+" • "+(Bytes/1048576.0).ToString("0.0")+" MB • "+Modified.ToString("dd/MM/yyyy");}
}
public static class UserCleanup {
    public static string Root{get{return System.IO.Path.GetFullPath(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp")).TrimEnd('\\')+"\\";}}
    public sealed class CleanRoot {public string Path,Name;}
    internal static bool SafeRoot(string path){try{string full=System.IO.Path.GetFullPath(path).TrimEnd('\\');if(full.Equals(System.IO.Path.GetPathRoot(full).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))return false;string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);bool systemTemp=DiskNavigationHistory.Within(full,System.IO.Path.Combine(windows,"Temp"));if(DiskNavigationHistory.Within(full,windows)&&!systemTemp)return false;foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})if(!String.IsNullOrWhiteSpace(root)&&DiskNavigationHistory.Within(full,root))return false;foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)})if(!String.IsNullOrWhiteSpace(root)&&full.Equals(System.IO.Path.GetFullPath(root).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))return false;return true;}catch{return false;}}
    static void Add(List<CleanRoot> roots,string path,string name){
        try{if(String.IsNullOrWhiteSpace(path)||!Directory.Exists(path)||!SafeRoot(path))return;string full=System.IO.Path.GetFullPath(path).TrimEnd('\\')+"\\";
            if(name=="TEMP"&&!full.Split('\\').Any(x=>x.Equals("Temp",StringComparison.OrdinalIgnoreCase)||x.Equals("Tmp",StringComparison.OrdinalIgnoreCase)))return;
            if((File.GetAttributes(full.TrimEnd('\\'))&FileAttributes.ReparsePoint)!=0||roots.Any(x=>full.StartsWith(x.Path,StringComparison.OrdinalIgnoreCase)))return;
            roots.RemoveAll(x=>x.Path.StartsWith(full,StringComparison.OrdinalIgnoreCase));
            roots.Add(new CleanRoot{Path=full,Name=name});
        }catch{}
    }
    public static List<CleanRoot> Sources(){
        var roots=new List<CleanRoot>();string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Add(roots,Path.GetTempPath(),"TEMP");Add(roots,Path.Combine(local,"Temp"),"Windows • usuário");
        Add(roots,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Temp"),"Windows • sistema");
        Add(roots,Path.Combine(local,"D3DSCache"),"Cache gráfico");
        Add(roots,Path.Combine(local,"CrashDumps"),"Relatórios • despejos de falha");
        foreach(string folder in new[]{"ReportArchive","ReportQueue"})Add(roots,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Microsoft","Windows","WER",folder),"Relatórios de erros • "+folder);
        foreach(var browser in new[]{new[]{Path.Combine(local,"Google","Chrome","User Data"),"Chrome"},new[]{Path.Combine(local,"Microsoft","Edge","User Data"),"Edge"},new[]{Path.Combine(local,"BraveSoftware","Brave-Browser","User Data"),"Brave"}}){
            string basePath=browser[0];if(!Directory.Exists(basePath))continue;
            try{foreach(string profile in Directory.GetDirectories(basePath).Where(x=>Path.GetFileName(x)=="Default"||Path.GetFileName(x).StartsWith("Profile ",StringComparison.OrdinalIgnoreCase)))foreach(string folder in new[]{"Cache","Cache\\Cache_Data","Code Cache","GPUCache"})Add(roots,Path.Combine(profile,folder),browser[1]+" • "+folder);}catch(UnauthorizedAccessException){}catch(IOException){}
        }
        string firefox=Path.Combine(local,"Mozilla","Firefox","Profiles");
        if(Directory.Exists(firefox))try{foreach(string profile in Directory.GetDirectories(firefox))Add(roots,Path.Combine(profile,"cache2"),"Firefox • cache");}catch(UnauthorizedAccessException){}catch(IOException){}
        foreach(string folder in new[]{"discord","discordcanary","discordptb"})foreach(string cache in new[]{"Cache","Code Cache","GPUCache"})Add(roots,Path.Combine(roaming,folder,cache),"Discord • "+cache);
        foreach(string cache in new[]{"Cache","Code Cache","GPUCache"}){Add(roots,Path.Combine(roaming,"Slack",cache),"Slack • "+cache);Add(roots,Path.Combine(roaming,"Microsoft","Teams",cache),"Teams clássico • "+cache);}
        foreach(string cache in new[]{"Cache","Code Cache","GPUCache"})Add(roots,Path.Combine(local,"Packages","MSTeams_8wekyb3d8bbwe","LocalCache","Microsoft","MSTeams","EBWebView","Default",cache),"Teams • "+cache);
        foreach(string cache in new[]{"Media Cache","Media Cache Files"})Add(roots,Path.Combine(roaming,"Adobe","Common",cache),"Adobe • "+cache);
        return roots;
    }
    static CleanRoot Containing(string full,List<CleanRoot> roots){return roots.FirstOrDefault(x=>full.StartsWith(x.Path,StringComparison.OrdinalIgnoreCase));}
    internal static bool EligibleIn(string file,DateTime now,List<CleanRoot> roots,out CleanRoot matched){
        matched=null;try{string full=System.IO.Path.GetFullPath(file);matched=Containing(full,roots);if(matched==null||!File.Exists(full))return false;
            if((File.GetAttributes(matched.Path.TrimEnd('\\'))&FileAttributes.ReparsePoint)!=0)return false;
            string parent=System.IO.Path.GetDirectoryName(full);while(parent!=null&&parent.Length>=matched.Path.Length){if((File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)return false;parent=System.IO.Path.GetDirectoryName(parent);}
            FileInfo info=new FileInfo(full);return (info.Attributes&FileAttributes.ReparsePoint)==0&&info.LastWriteTimeUtc<now.AddDays(-1);
        }catch{return false;}
    }
    public static bool Eligible(string file,DateTime now){
        CleanRoot matched;return EligibleIn(file,now,Sources(),out matched);
    }
    public static List<CleanItem> Scan(){bool limited;return Scan(out limited);}
    public static List<CleanItem> Scan(out bool limited){
        var result=new List<CleanItem>();var roots=Sources();int visited=0;
        foreach(var root in roots){var folders=new Stack<string>();folders.Push(root.Path);
            while(folders.Count>0&&visited<300000){string folder=folders.Pop();try{foreach(string path in Directory.EnumerateFileSystemEntries(folder)){
                if(++visited>300000)break;FileAttributes attr;try{attr=File.GetAttributes(path);}catch{continue;}
                if((attr&FileAttributes.ReparsePoint)!=0)continue;
                if((attr&FileAttributes.Directory)!=0){folders.Push(path);continue;}
                CleanRoot matched;if(!EligibleIn(path,DateTime.UtcNow,roots,out matched))continue;
                var info=new FileInfo(path);result.Add(new CleanItem{Path=path,Source=matched.Name,Bytes=info.Length,Modified=info.LastWriteTime});
            }}catch(UnauthorizedAccessException){}catch(IOException){}}
        }
        limited=visited>=300000;return result.GroupBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).OrderByDescending(x=>x.Bytes).ToList();
    }
    public static string Delete(CleanItem item){return DeleteMany(new[]{item});}
    public static string DeleteMany(CleanItem[] selected,bool permanent=false,Action<int,int,string> progress=null){return CleanupBatch.Execute(selected,Sources(),permanent,progress).Report();}

}
public sealed class OrphanRegistryItem {
    public RegistryHive Hive;public RegistryView View;public string Key,Title,Uninstall,Location;
    public override string ToString(){return Title+" • "+(Hive==RegistryHive.CurrentUser?"Usuário":"Computador")+" • "+View;}
}
public static class OrphanRegistryCleaner {
    const string Root=@"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    public static bool LooksOrphan(string command,string location){
        if(String.IsNullOrWhiteSpace(command))return false;
        string exe=command.Trim();if(exe.StartsWith("\"")){int end=exe.IndexOf('"',1);if(end<2)return false;exe=exe.Substring(1,end-1);}else{int index=exe.IndexOf(".exe",StringComparison.OrdinalIgnoreCase);if(index<0)return false;exe=exe.Substring(0,index+4);}
        try{exe=Environment.ExpandEnvironmentVariables(exe);location=Environment.ExpandEnvironmentVariables(location??"");if(!Path.IsPathRooted(exe))return false;
            if(String.IsNullOrWhiteSpace(location))location=Path.GetDirectoryName(exe);
            if(!Path.IsPathRooted(location))return false;
            string appPath=Path.GetFullPath(location).TrimEnd('\\');string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);if(appPath.Length<=3||appPath.Equals(windows,StringComparison.OrdinalIgnoreCase)||appPath.StartsWith(windows+"\\",StringComparison.OrdinalIgnoreCase))return false;
            return !File.Exists(exe)&&!Directory.Exists(appPath);
        }catch{return false;}
    }
    public static List<OrphanRegistryItem> Scan(){var result=new List<OrphanRegistryItem>();foreach(RegistryHive hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(RegistryView view in new[]{RegistryView.Registry64,RegistryView.Registry32})try{
        using(var baseKey=RegistryKey.OpenBaseKey(hive,view))using(var root=baseKey.OpenSubKey(Root)){if(root==null)continue;foreach(string name in root.GetSubKeyNames())try{using(var key=root.OpenSubKey(name)){string title=Convert.ToString(key.GetValue("DisplayName")),command=Convert.ToString(key.GetValue("UninstallString")),location=Convert.ToString(key.GetValue("InstallLocation"));if(String.IsNullOrWhiteSpace(title)||key.GetValue("SystemComponent")!=null||!LooksOrphan(command,location))continue;result.Add(new OrphanRegistryItem{Hive=hive,View=view,Key=name,Title=title,Uninstall=command,Location=location});}}catch{}}
    }catch{}return result.GroupBy(x=>x.Hive+"|"+x.View+"|"+x.Key).Select(x=>x.First()).OrderBy(x=>x.Title).Take(200).ToList();}
    public static string Delete(OrphanRegistryItem item,string backupRoot){
        if(item==null||String.IsNullOrWhiteSpace(item.Key)||item.Key.IndexOfAny(new[]{'\\','/','\r','\n'})>=0)throw new ArgumentException("Invalid registry item");
        using(var baseKey=RegistryKey.OpenBaseKey(item.Hive,item.View))using(var root=baseKey.OpenSubKey(Root,true)){
            if(root==null)throw new IOException("Registry root unavailable");using(var key=root.OpenSubKey(item.Key)){if(key==null||!LooksOrphan(Convert.ToString(key.GetValue("UninstallString")),Convert.ToString(key.GetValue("InstallLocation"))))throw new InvalidOperationException(L.T("A entrada não é mais órfã.","The entry is no longer orphaned."));}
            Directory.CreateDirectory(backupRoot);string file=Path.Combine(backupRoot,"Orphan-"+Guid.NewGuid().ToString("N")+".reg");string hive=item.Hive==RegistryHive.CurrentUser?"HKCU":"HKLM";
            string path=hive+"\\"+Root+"\\"+item.Key;string args="export "+Args.Q(path)+" "+Args.Q(file)+" /y "+(item.View==RegistryView.Registry32?"/reg:32":"/reg:64");var saved=Runner.Run("reg.exe",args,30000);if(saved.Code!=0||!File.Exists(file))throw new IOException(L.T("Não foi possível guardar a cópia do Registro.","Could not back up the Registry key.")+" "+saved.Output);
            root.DeleteSubKeyTree(item.Key,false);return "OK • "+item.Title+L.T(" • cópia: "," • backup: ")+file;
        }
    }
}
public partial class MainWindow {
    void CleanupPage(){
        Heading(T("Limpeza","Cleanup"),T("Revise cada arquivo ou entrada antes de excluir.","Review each file or entry before deleting."));BeginColumns(2);
        Action scan=TemporaryCleanupCard();
        var registry=Card(T("Entradas órfãs do Registro","Orphaned Registry entries"),T("Procura entradas de desinstalação cujo executável e pasta não existem. Quando a pasta não consta no Registro, usa a pasta do desinstalador. Exporta cada chave antes de excluir.","Finds uninstall entries whose executable and folder are missing. When the Registry omits the install location, uses the uninstaller folder. Exports each key before deletion."));
        ListBox keys=new ListBox{Height=240,Background=Surface,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};registry.Children.Add(keys);TextBlock regState=Text(T("Clique em Analisar para procurar entradas órfãs.","Click Analyze to find orphaned entries."),13,Muted);registry.Children.Add(regState);var keyChecks=new Dictionary<OrphanRegistryItem,CheckBox>();
        Action scanRegistry=()=>{List<OrphanRegistryItem> found=null;Work(T("Analisar Registro","Scan Registry"),()=>{found=OrphanRegistryCleaner.Scan();return "OK • "+found.Count+T(" entrada(s) para revisar."," entry/entries to review.");},r=>{keys.Items.Clear();keyChecks.Clear();foreach(var item in found??new List<OrphanRegistryItem>()){var box=new CheckBox{Content=item.ToString(),ToolTip=item.Uninstall+"\n"+item.Location,Foreground=Ink,Margin=new Thickness(3)};keys.Items.Add(box);keyChecks[item]=box;}keys.Visibility=keys.Items.Count>0?Visibility.Visible:Visibility.Collapsed;regState.Text=r;regState.Foreground=OutcomeBrush(r);});};
        Row(registry,Button(T("Analisar","Analyze"),()=>scanRegistry()),Button(T("Excluir selecionados","Delete selected"),()=>{var chosen=keyChecks.Where(x=>x.Value.IsChecked==true).Select(x=>x.Key).ToArray();if(chosen.Length==0||chosen.Any(x=>x.Hive==RegistryHive.LocalMachine)&&!RequireAdmin())return;if(!Confirm(T("Exportar e excluir estas entradas órfãs?\n","Export and delete these orphaned entries?\n")+String.Join("\n",chosen.Select(x=>x.ToString()))))return;Work(T("Limpar Registro","Clean Registry"),()=>{var report=new List<string>();foreach(var item in chosen)try{report.Add(OrphanRegistryCleaner.Delete(item,journal.Folder));}catch(Exception e){report.Add("ERROR • "+item.Title+" • "+e.Message);}return String.Join("\n",report);},r=>{regState.Text=r;regState.Foreground=OutcomeBrush(r);scanRegistry();});}));
        Action expandedRegistry=ExpandedRegistryCard();pageRefreshActions["cleanup"]=()=>{scan();scanRegistry();expandedRegistry();};if(!preview)pageRefreshActions["cleanup"]();
    }
}
}
