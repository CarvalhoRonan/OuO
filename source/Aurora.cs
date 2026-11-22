using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

[assembly:System.Reflection.AssemblyTitle("OuO — Overclock's Utility Optimizer")]
[assembly:System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly:System.Reflection.AssemblyFileVersion("1.1.0.0")]

namespace Aurora {
public static class Program {
    public static OfficeAutoJob PendingOffice;
    [STAThread] public static int Main(string[] args) {
        if(args.Contains("--self-test")) return Tests.Run();
        if(args.Length==2&&args[0]=="--disk-benchmark")return AcceptanceChecks.Benchmark(Args.LocalPath(args[1]));
        if(args.Contains("--acceptance-test"))return AcceptanceChecks.Run();
        if(args.Contains("--sensor-test")) return SensorChecks.Run();
        if(args.Contains("--process-test")) return ProcessChecks.Run();
        if(args.Contains("--environment-audit")) return EnvironmentAudit.Run();
        if(args.Contains("--isolated-test")) return IsolatedChecks.Run();
        if(args.Contains("--extended-test")) return ExtendedChecks.Run();
        if(args.Contains("--installer-lab-test")) return InstallerFixtureCheck.Standalone();
        if(args.Contains("--office-lab-worker")) return ExtendedChecks.OfficeWorker();
        if(args.Length==2&&args[0]=="--hydrate-onedrive")return CloudFiles.HydrateChild(args[1]);
        if(args.Length==2&&args[0]=="--validate-offline-hive")return OfflineAccountRecovery.ValidateHiveInChild(args[1]);
        if(args.Contains("--test-command-child")){Console.WriteLine("child output");System.Threading.Thread.Sleep(8000);return 0;}
        if(args.Contains("--memory-test-child")){var pages=new byte[33554432];for(int i=0;i<pages.Length;i+=4096)pages[i]=1;Console.WriteLine("READY");System.Threading.Thread.Sleep(8000);GC.KeepAlive(pages);return 0;}
        if(args.Contains("--test-command-parent")){var child=Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--test-command-child"){UseShellExecute=false,CreateNoWindow=true});Console.WriteLine("CHILD|"+child.Id);return 0;}
        if(args.Contains("--test-background-parent")){using(var child=Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--test-command-child"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){child.StandardOutput.ReadLine();Console.WriteLine("BACKGROUND|"+child.Id);}return 0;}
        try {
            int officeArg=Array.IndexOf(args,"--office-auto");
            if(officeArg>=0){if(officeArg+1>=args.Length)throw new ArgumentException("Office request missing");PendingOffice=OfficeAutoJob.Decode(args[officeArg+1]);}
            Application app = new Application();
            app.DispatcherUnhandledException += (s,e) => {if(args.Contains("--verify-ui")||args.Contains("--soak-ui")){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-unhandled.txt"),e.Exception.ToString());e.Handled=true;app.Shutdown(2);return;}MainWindow owner=app.MainWindow as MainWindow;if(owner!=null)owner.ShowNotice("ERROR • "+e.Exception.Message,"OuO");else MessageBox.Show(e.Exception.Message,"OuO"); e.Handled=true; };
            MainWindow window = new MainWindow(args.Contains("--preview") || args.Contains("--verify-ui") || args.Contains("--soak-ui"),args.Contains("--tray"));
            app.SessionEnding+=(s,e)=>window.exitForSessionEnd();
            int appCode=app.Run(window);if(args.Contains("--verify-ui")){string uiReport=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-results.txt");return appCode==0&&File.Exists(uiReport)&&File.ReadAllText(uiReport).StartsWith("PASS",StringComparison.Ordinal)?0:2;}return appCode;
        } catch(Exception e) { File.WriteAllText(Path.Combine(Path.GetTempPath(),"Aurora-error.txt"),e.ToString()); return 1; }
    }
}
public static class L {
    public static bool Pt = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";
    public static string T(string pt,string en) { UiLanguage.Remember(pt,en); return Pt ? pt : en; }
}
public sealed class Machine {
    public string Caption="Windows", Version="", Arch="", Cpu="", Gpu="", Board="", BootTime="";
    public int Build; public ulong Total, Free; public bool Admin, Winget;
    public static IEnumerable<ManagementObject> Query(string path,string query) {
        using(ManagementObjectSearcher search = new ManagementObjectSearcher(path,query))
        {search.Options.Timeout=TimeSpan.FromSeconds(15);search.Options.ReturnImmediately=true;using(ManagementObjectCollection result = search.Get()) {
            foreach(ManagementObject obj in result) yield return obj;
        }}
    }
    public static string Value(ManagementBaseObject o,string key) { try { return Convert.ToString(o[key],CultureInfo.InvariantCulture); } catch { return ""; } }
    public static string First(string query,string key) {
        try { foreach(var o in Query("root\\cimv2",query)) using(o) return Value(o,key); } catch { } return "";
    }
    public static Machine Detect() {
        Machine m=new Machine();
        try { foreach(var o in Query("root\\cimv2","SELECT Caption,Version,BuildNumber,OSArchitecture,TotalVisibleMemorySize,FreePhysicalMemory,LastBootUpTime FROM Win32_OperatingSystem")) using(o) {
            m.BootTime=Value(o,"LastBootUpTime");m.Caption=Value(o,"Caption");m.Version=Value(o,"Version");m.Arch=Value(o,"OSArchitecture");
            Int32.TryParse(Value(o,"BuildNumber"),out m.Build);UInt64.TryParse(Value(o,"TotalVisibleMemorySize"),out m.Total); UInt64.TryParse(Value(o,"FreePhysicalMemory"),out m.Free);
        }} catch { m.Version=Environment.OSVersion.Version.ToString(); }
        if(m.Build==0) {m.Build=Environment.OSVersion.Version.Build;m.Version=Environment.OSVersion.Version.ToString();m.Caption=m.Build>=22000?"Windows 11":m.Build>=10240?"Windows 10":m.Build>=9600?"Windows 8.1":m.Build>=9200?"Windows 8":m.Build>=7600?"Windows 7":"Windows";}
        if(m.Arch=="")m.Arch=Environment.Is64BitOperatingSystem?"64-bit":"32-bit";
        Native.Memory mem=Native.ReadMemory();if(m.Total==0&&mem!=null){m.Total=mem.Total/1024;m.Free=mem.Free/1024;}
        m.Cpu=First("SELECT Name FROM Win32_Processor","Name");
        m.Gpu=First("SELECT Name FROM Win32_VideoController","Name");
        m.Board=First("SELECT Product FROM Win32_BaseBoard","Product");
        if(m.Cpu=="")m.Cpu=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")??L.T("Processador não identificado","Processor unavailable");
        if(m.Gpu=="")m.Gpu=L.T("GPU: consulta WMI indisponível","GPU: WMI query unavailable");
        if(m.Board=="")m.Board=L.T("Placa-mãe: consulta WMI indisponível","Motherboard: WMI query unavailable");
        m.Admin=new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        m.Winget=m.Build>=17763 && File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft\\WindowsApps\\winget.exe"));
        return m;
    }
}
public static class Native {
    [StructLayout(LayoutKind.Sequential)] public class Memory {public uint Length=(uint)Marshal.SizeOf(typeof(Memory));public uint Load;public ulong Total,Free,TotalPage,FreePage,TotalVirtual,FreeVirtual,Extended;}
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GlobalMemoryStatusEx([In,Out] Memory memory);
    [DllImport("kernel32.dll",SetLastError=true)] public static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
    public static Memory ReadMemory(){Memory m=new Memory();return GlobalMemoryStatusEx(m)?m:null;}
}
public sealed class CommandResult { public int Code; public string Output; }
public static class Runner {
    public static CommandResult Run(string file,string arguments,int timeout,string workingDirectory=null,Action<string> received=null,System.Threading.CancellationToken cancellation=default(System.Threading.CancellationToken),bool allowBackground=false) {
        Action<WorkProgress> commandProgress=WorkReporting.Sink;StringBuilder output=new StringBuilder(); object gate=new object();int stdoutEnded=0,stderrEnded=0;
        ProcessStartInfo info=new ProcessStartInfo(file,arguments); info.UseShellExecute=false;info.CreateNoWindow=true;
        if(!String.IsNullOrEmpty(workingDirectory))info.WorkingDirectory=workingDirectory;
        info.RedirectStandardOutput=true;info.RedirectStandardError=true;
        if(Path.GetFileName(file).Equals("powershell.exe",StringComparison.OrdinalIgnoreCase)){info.StandardOutputEncoding=Encoding.UTF8;info.StandardErrorEncoding=Encoding.UTF8;}
        // SFC writes UTF-16 when redirected, including its error messages.
        if(Path.GetFileName(file).Equals("sfc.exe",StringComparison.OrdinalIgnoreCase)){info.StandardOutputEncoding=Encoding.Unicode;info.StandardErrorEncoding=Encoding.Unicode;}
        using(Process p=new Process()) {
            p.StartInfo=info;
            p.OutputDataReceived+=(s,e)=>{if(e.Data==null)System.Threading.Interlocked.Exchange(ref stdoutEnded,1);else{lock(gate)output.AppendLine(e.Data);if(commandProgress!=null){var update=WorkReporting.FromToolLine(e.Data,file);if(update!=null)try{commandProgress(update);}catch{}}if(received!=null)try{received(e.Data);}catch{}}};
            p.ErrorDataReceived+=(s,e)=>{if(e.Data==null)System.Threading.Interlocked.Exchange(ref stderrEnded,1);else{lock(gate)output.AppendLine(e.Data);if(commandProgress!=null){var update=WorkReporting.FromToolLine(e.Data,file);if(update!=null)try{commandProgress(update);}catch{}}if(received!=null)try{received(e.Data);}catch{}}};
            p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();
            using(var job=new OwnedCommandJob(p)){var watch=Stopwatch.StartNew();bool ended=false;while(!cancellation.IsCancellationRequested&&watch.ElapsedMilliseconds<timeout){if(p.WaitForExit((int)Math.Min(200,Math.Max(1,timeout-watch.ElapsedMilliseconds)))){ended=true;break;}}
                while(ended&&!allowBackground&&job.Active>0&&(stdoutEnded==0||stderrEnded==0)&&watch.ElapsedMilliseconds<timeout&&!cancellation.IsCancellationRequested)System.Threading.Thread.Sleep(20);
                int childrenBeforeTermination=job.Active;bool timedOut=!ended||(!allowBackground&&job.Active>0&&(stdoutEnded==0||stderrEnded==0))||cancellation.IsCancellationRequested;
                if(timedOut){job.Terminate();try{if(!p.HasExited)p.Kill();}catch{}p.WaitForExit(3000);}
                var drain=Stopwatch.StartNew();while((System.Threading.Interlocked.CompareExchange(ref stdoutEnded,0,0)==0||System.Threading.Interlocked.CompareExchange(ref stderrEnded,0,0)==0)&&drain.ElapsedMilliseconds<2000)System.Threading.Thread.Sleep(10);
                bool complete=stdoutEnded!=0&&stderrEnded!=0;try{p.CancelOutputRead();}catch{}try{p.CancelErrorRead();}catch{}
                string text;lock(gate)text=output.ToString();if(cancellation.IsCancellationRequested)return new CommandResult{Code=-3,Output=text+L.T("\nCancelado pelo usuário.","\nCanceled by user.")};if(timedOut)return new CommandResult{Code=-1,Output=text+L.T("\nTempo limite excedido. Os subprocessos associados foram interrompidos quando permitido. Verifique o resultado antes de repetir.","\nTimed out. Associated subprocesses were stopped where permitted. Check the result before retrying.")+" [rootExited="+ended+", ownedProcesses="+childrenBeforeTermination+"]"};
                if(!complete&&!allowBackground)return new CommandResult{Code=-2,Output=text+L.T("\nERROR • A saída de um subprocesso não foi encerrada. Ele pode continuar em execução; confira antes de repetir.","\nERROR • A subprocess output pipe did not close. It may still be running; check before retrying.")};
                return new CommandResult{Code=p.ExitCode,Output=text};
            }
        }
    }
    public static CommandResult PS(string script,int timeout,Action<string> received=null) {
        string encoded=Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=New-Object System.Text.UTF8Encoding; try { "+script+" } catch { Write-Output $_.Exception.Message; exit 1 }"));
        return Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell\\v1.0\\powershell.exe"),"-NoProfile -NonInteractive -OutputFormat Text -EncodedCommand "+encoded,timeout,null,received);
    }
    public static string QuotePS(string value) {return "'"+value.Replace("'","''")+"'";}
    public static bool ValidId(string id) {return Regex.IsMatch(id??"","^[A-Za-z0-9][A-Za-z0-9._+-]{1,160}$");}
}
public sealed class RegistrySnapshot {
    public bool Exists; public object Value; public RegistryValueKind Kind;
}
public interface IRegistryStore {
    RegistrySnapshot Read(string hive,string path,string name);
    void Set(string hive,string path,string name,object value,RegistryValueKind kind);
    void Delete(string hive,string path,string name);
}
public sealed partial class WindowsRegistryStore:IRegistryStore {
    RegistryKey Root(string hive){switch(hive){case "HKLM":return Registry.LocalMachine;case "HKCU":return Registry.CurrentUser;case "HKU":return Registry.Users;case "HKCR":return Registry.ClassesRoot;default:throw new ArgumentException("Unknown registry hive");}}
    public RegistrySnapshot Read(string hive,string path,string name){using(var key=Root(hive).OpenSubKey(path)){
        if(key==null||!key.GetValueNames().Contains(name))return new RegistrySnapshot{Kind=RegistryValueKind.String};
        return new RegistrySnapshot{Exists=true,Value=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames),Kind=key.GetValueKind(name)};
    }}
    public void Set(string hive,string path,string name,object value,RegistryValueKind kind){using(var key=Root(hive).CreateSubKey(path))key.SetValue(name,value,kind);}
    public void Delete(string hive,string path,string name){using(var key=Root(hive).OpenSubKey(path,true))if(key!=null)key.DeleteValue(name,false);}
}
public sealed partial class MemoryRegistryStore:IRegistryStore {
    Dictionary<string,RegistrySnapshot> values=new Dictionary<string,RegistrySnapshot>();
    string Key(string h,string p,string n){return h+"\\"+p+"\\"+n;}
    public RegistrySnapshot Read(string h,string p,string n){RegistrySnapshot v;return values.TryGetValue(Key(h,p,n),out v)?v:new RegistrySnapshot{Kind=RegistryValueKind.String};}
    public void Set(string h,string p,string n,object v,RegistryValueKind k){values[Key(h,p,n)]=new RegistrySnapshot{Exists=true,Value=v,Kind=k};}
    public void Delete(string h,string p,string n){values.Remove(Key(h,p,n));}
}
public sealed partial class Journal {
    public readonly string Folder;
    readonly IRegistryStore store;
    public Journal(string folder):this(folder,new WindowsRegistryStore()){}
    public Journal(string folder,IRegistryStore registry) {Folder=folder;store=registry;Directory.CreateDirectory(folder);}
    public void Log(string title,string result) {lock(this){
        File.AppendAllText(Path.Combine(Folder,"activity.log"),DateTime.Now.ToString("s")+" | "+title+Environment.NewLine+result+Environment.NewLine,Encoding.UTF8);
    }
    }
    public bool HasPrefixHive(string prefix,string hive){lock(this){foreach(string file in Directory.GetFiles(Folder,"*.xml")){XElement record;try{record=XElement.Load(file);}catch{continue;}if((record.Name=="change"||record.Name=="tree-change")&&(string)record.Attribute("state")=="applied"&&((string)record.Attribute("title")??"").StartsWith(prefix,StringComparison.Ordinal)&&(string)record.Attribute("hive")==hive)return true;}return false;}}
    public string UndoPrefix(string prefix) {lock(this){int count=0;var errors=new List<string>();foreach(string file in Directory.GetFiles(Folder,"*.xml").OrderByDescending(x=>Path.GetFileName(x))){XElement record;try{record=XElement.Load(file);}catch{continue;}if((record.Name!="change"&&record.Name!="tree-change")||(string)record.Attribute("state")!="applied")continue;string title=(string)record.Attribute("title");if(title==null||!title.StartsWith(prefix,StringComparison.Ordinal))continue;try{UndoRecord(record,file);count++;}catch(Exception ex){errors.Add("ERROR • "+title+" • "+ex.Message);}}return count+" "+L.T("ajuste(s) restaurado(s).","setting(s) restored.")+(errors.Count>0?"\n"+String.Join("\n",errors):"");}}
    public string History() {string f=Path.Combine(Folder,"activity.log");return File.Exists(f)?File.ReadAllText(f):L.T("Nenhuma operação registrada.","No operations recorded.");}
    public void RegistryChange(string title,string hive,string path,string name,object value,RegistryValueKind kind) {lock(this){
        RegistrySnapshot snapshot=store.Read(hive,path,name);
        object previous=snapshot.Value;RegistryValueKind oldKind=snapshot.Kind;bool existed=snapshot.Exists;
        XElement record=new XElement("change",new XAttribute("title",title),new XAttribute("hive",hive),new XAttribute("path",path),new XAttribute("name",name),new XAttribute("existed",existed),new XAttribute("kind",oldKind),new XAttribute("state","prepared"),new XElement("previous",previous==null?"":Convert.ToString(previous,CultureInfo.InvariantCulture)));
        if(existed)record.Add(RegistryValueCodec.Encode(previous,oldKind));TagChange(record);record.Add(new XElement("after",new XAttribute("exists",true),new XAttribute("kind",kind),RegistryValueCodec.Encode(value,kind)));
        string file=Path.Combine(Folder,DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff")+"-"+Guid.NewGuid().ToString("N")+".xml");AtomicFiles.SaveXml(record,file);
        store.Set(hive,path,name,value,kind);
        if(!RegistryValueCodec.Equal(store.Read(hive,path,name).Value,value)) throw new IOException("Registry verification failed");
        record.SetAttributeValue("state","applied");AtomicFiles.SaveXml(record,file);Log(title,"OK / backup: "+Path.GetFileName(file));
    }
    }
    public string Undo(string titleFilter=null) {lock(this){
        foreach(string file in Directory.GetFiles(Folder,"*.xml").OrderByDescending(x=>Path.GetFileName(x))) {
            XElement r;try{r=XElement.Load(file);}catch{continue;}if((r.Name!="change"&&r.Name!="tree-change")||(string)r.Attribute("state")=="undone"||(titleFilter!=null&&(string)r.Attribute("title")!=titleFilter))continue;
            return UndoRecord(r,file);
        }
        return L.T("Nada para restaurar.","Nothing to restore.");
    }}
    string UndoRecord(XElement r,string file){
            string hive=(string)r.Attribute("hive"),path=(string)r.Attribute("path"),name=(string)r.Attribute("name");
            if(r.Name=="tree-change"){RestoreTree(r);}else{
                if((bool)r.Attribute("existed")) {
                    RegistryValueKind kind=(RegistryValueKind)Enum.Parse(typeof(RegistryValueKind),(string)r.Attribute("kind"));
                    object value=r.Element("encoded")!=null?RegistryValueCodec.Decode(r.Element("encoded"),kind):kind==RegistryValueKind.DWord?(object)Int32.Parse(r.Element("previous").Value,CultureInfo.InvariantCulture):r.Element("previous").Value;
                    store.Set(hive,path,name,value,kind);
                } else store.Delete(hive,path,name);
            }
            r.SetAttributeValue("state","undone");AtomicFiles.SaveXml(r,file);Log("Undo",(string)r.Attribute("title"));return (string)r.Attribute("title");
}
}
public sealed class StartupItem {
    public string Name{get;set;} public string Command{get;set;} public string Scope{get;set;} public string Path{get;set;}
    public override string ToString(){return Scope+"  |  "+Name+"  |  "+Command;}
    public ImageSource Icon{get{return AppIcons.FromCommand(Command);}}
    public string Detail{get{return Scope+" • "+Command;}}
}
public partial class MainWindow:Window {
    const string RunPath="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    readonly Journal journal=new Journal(Environment.GetCommandLineArgs().Any(x=>x=="--preview"||x=="--verify-ui"||x=="--soak-ui")?Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics","History"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AuroraUtility","History"));
    Machine machine; bool dark=true,busy=false,preview,compact; string current="dashboard",accentName="menta";
    Grid shell; StackPanel nav,body; TextBlock status,metricCpu,metricRam,metricGpu,metricTemp,tempCpu,tempGpu,tempVrm,hardwareIdentity;ProgressBar workProgress;TextBlock workNotice;DispatcherTimer activityTimer;long previousIdle,previousKernel,previousUser;
    readonly SensorClient sensors=new SensorClient();string[] lastSample;DateTime lastExternalSensorRead=DateTime.MinValue;
    TextBox console; DispatcherTimer timer; bool sampling;
    Brush Bg,Surface,Ink,Muted,Accent;SolidColorBrush positiveStateBrush;bool positiveStateDark;Brush StateGreen{get{if(positiveStateBrush==null||positiveStateDark!=dark){positiveStateDark=dark;positiveStateBrush=new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dark?"#71DCC3":"#087963"));positiveStateBrush.Freeze();}return positiveStateBrush;}}
    public MainWindow(bool previewMode,bool startInTray=false) {
        preview=previewMode;Title="OuO • Overclock's Utility Optimizer";Width=1140;Height=800;MinWidth=920;MinHeight=650;WindowStyle=WindowStyle.None;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this,new System.Windows.Shell.WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(7),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false});
        try{string iconPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","branding","ouo-circuito.ico");if(File.Exists(iconPath)){BitmapImage appIcon=new BitmapImage(new Uri(iconPath));appIcon.Freeze();Icon=appIcon;}}catch{}
        Width=Math.Min(1320,SystemParameters.WorkArea.Width);Height=Math.Min(900,SystemParameters.WorkArea.Height);MinWidth=Math.Min(760,SystemParameters.WorkArea.Width);MinHeight=Math.Min(560,SystemParameters.WorkArea.Height);WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new FontFamily("Segoe UI");FontSize=14;
        try {using(var k=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize")) if(k!=null)dark=Convert.ToInt32(k.GetValue("AppsUseLightTheme",0))==0;}catch{}
        try {using(var k=Registry.CurrentUser.OpenSubKey("Software\\OuO\\Appearance")) if(k!=null)accentName=Convert.ToString(k.GetValue("Accent","menta"));}catch{}
        InitializeWindowBounds();LoadPreferences();BuildShell();if(startInTray&&!preview)InitTray();Loaded+=OnLoaded;Closed+=(s,e)=>{StopAutoMemory();if(timer!=null)timer.Stop();sensors.Dispose();DisposeTray();};
        Closing+=HandleExit;
    }
    string T(string p,string e){return L.T(p,e);}
    Brush Color(string hex){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));}
    void BuildShell(bool preservePages=false){
        if(pageToolbarHost!=null)pageToolbarHost.Content=null;UiLifetimeProbe.Record(shell,"shell");if(UiLifetimeProbe.Enabled)foreach(object value in Application.Current.Resources.Values)if(value is Style)UiLifetimeProbe.Record(value,"style");
        if(preservePages){PageView saved;if(pageScroll!=null){if(pageCache.TryGetValue(current,out saved))saved.Offset=pageScroll.VerticalOffset;pageScroll.Content=null;}}
        else{pageToolbars.Clear();foreach(var view in pageCache)UiLifetimeProbe.Record(view.Value.Body,"page:"+view.Key);pageCache.Clear();pageRefreshActions.Clear();}
        Bg=Color(dark?"#10141D":"#F2F5FA");Surface=Color(dark?"#1B2230":"#FFFFFF");Ink=Color(dark?"#EDF3FF":"#17243A");Muted=Color(dark?"#A5B2C7":"#53637B");Accent=Color(AccentHex());
        ApplyScrollTheme();ApplyComboTheme();ApplyButtonTheme();ApplyControlTheme();ApplyTabTheme();ApplyQualificationTheme();Background=Bg;Foreground=Ink;FontSize=compact?13:14; shell=new Grid();shell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(compact?210:238)});shell.ColumnDefinitions.Add(new ColumnDefinition());shell.RowDefinitions.Add(new RowDefinition{Height=new GridLength(42)});shell.RowDefinitions.Add(new RowDefinition());shell.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=shell;
        Grid titleBar=new Grid{Background=Surface};titleBar.ColumnDefinitions.Add(new ColumnDefinition());titleBar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var titleDrag=new Border{Background=Surface,Padding=new Thickness(16,7,0,0),Child=Text("OuO  •  Overclock's Utility Optimizer",13,Ink)};
        titleDrag.MouseLeftButtonDown+=(sender,e)=>{if(e.ClickCount==2)WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;else try{DragMove();}catch{}};
        titleBar.Children.Add(titleDrag);
        var controls=new StackPanel{Orientation=Orientation.Horizontal};
        var minimize=Button("—",()=>WindowState=WindowState.Minimized);
        var maximize=Button("□",()=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized);
        var close=Button("✕",()=>Close());
        foreach(var control in new[]{minimize,maximize,close}){control.Width=44;control.Height=36;control.Padding=new Thickness(0);control.Margin=new Thickness(0);control.BorderThickness=new Thickness(0);control.Background=Surface;}
        maximize.ToolTip=T("Maximizar ou restaurar","Maximize or restore");close.ToolTip=T("Fechar","Close");minimize.ToolTip=T("Minimizar","Minimize");
        System.Windows.Automation.AutomationProperties.SetName(minimize,T("Minimizar","Minimize"));System.Windows.Automation.AutomationProperties.SetName(maximize,T("Maximizar ou restaurar","Maximize or restore"));System.Windows.Automation.AutomationProperties.SetName(close,T("Fechar","Close"));controls.Children.Add(minimize);controls.Children.Add(maximize);controls.Children.Add(close);Grid.SetColumn(controls,1);titleBar.Children.Add(controls);
        Grid.SetRow(titleBar,0);Grid.SetColumnSpan(titleBar,2);shell.Children.Add(titleBar);
        Grid sidebar=new Grid{Background=Surface};sidebar.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});sidebar.RowDefinitions.Add(new RowDefinition());Grid.SetColumn(sidebar,0);Grid.SetRow(sidebar,1);shell.Children.Add(sidebar);
        StackPanel brand=new StackPanel{Margin=new Thickness(20,24,14,18)};StackPanel brandRow=new StackPanel{Orientation=Orientation.Horizontal};try{string markPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","branding","ouo-circuito.png");if(File.Exists(markPath)){BitmapImage mark=new BitmapImage(new Uri(markPath));mark.Freeze();brandRow.Children.Add(new Image{Source=mark,Width=38,Height=38,Margin=new Thickness(0,0,10,0)});}}catch{}brandRow.Children.Add(Text("OuO",28,Accent));brand.Children.Add(brandRow);brand.Children.Add(Text("Overclock's Utility Optimizer",11,Muted));sidebar.Children.Add(brand);
        nav=new StackPanel{Margin=new Thickness(12,0,8,15)};ScrollViewer tabScroll=new ScrollViewer{Content=nav,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(tabScroll,1);sidebar.Children.Add(tabScroll);
        InitializeNavigation();Nav("dashboard",T("Visão geral","Overview"));Nav("recommendations",T("Recomendações","Recommendations"));Nav("performance",T("Memória e desempenho","Memory & performance"));Nav("processes",T("Análise de processos","Process analysis"));Nav("stability",T("Estabilidade e eventos","Stability & events"));Nav("tweaks","Tweaks");Nav("repair",T("Reparo do sistema","System repair"));Nav("protection",T("Proteção e recursos","Protection & features"));Nav("browser",T("Navegação segura","Safe browsing"));Nav("network",T("Impressão","Printing"));Nav("storage",T("Armazenamento","Storage"));Nav("cloud-storage",T("Armazenamento em nuvem","Cloud storage"));Nav("cleanup",T("Limpeza","Cleanup"));Nav("startup",T("Inicialização","Startup"));Nav("apps",T("Instalar programas","Install apps"));Nav("office",T("Instalar Office","Install Office"));Nav("bloat",T("Remover Programas","Remove Programs"));Nav("recovery",T("Componentes e bibliotecas","Components & libraries"));Nav("drivers",T("Drivers","Drivers"));Nav("upgrade",T("Atualizar Windows","Upgrade Windows"));Nav("settings",T("Idioma e aparência","Language & appearance"));Nav("history",T("Histórico e restauração","History & restore"));Nav("about",T("Sobre","About"));
        ScrollViewer scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};pageScroll=scroll;body=new StackPanel{Margin=compact?new Thickness(15):new Thickness(25,25,30,25)};scroll.Content=body;var pageArea=new DockPanel{LastChildFill=true};pageToolbarHost=new ContentControl{Visibility=Visibility.Collapsed,Margin=new Thickness(16,8,16,0)};DockPanel.SetDock(pageToolbarHost,Dock.Top);pageArea.Children.Add(pageToolbarHost);pageArea.Children.Add(scroll);Grid.SetColumn(pageArea,1);Grid.SetRow(pageArea,1);shell.Children.Add(pageArea);
        StackPanel footer=new StackPanel();status=Text(machine==null?T("Detectando o computador…","Detecting your computer…"):machine.Caption+" • "+machine.Arch+" • "+(machine.Admin?T("Administrador","Administrator"):T("Usuário padrão","Standard user")),12,Muted);status.Margin=new Thickness(20,8,20,5);footer.Children.Add(status);workNotice=Text("",12,Accent);workNotice.Margin=new Thickness(20,0,20,4);workNotice.Visibility=Visibility.Collapsed;footer.Children.Add(workNotice);workProgress=new ProgressBar{Height=4,Margin=new Thickness(0),Foreground=Accent,Background=Surface,IsIndeterminate=busy,Visibility=busy?Visibility.Visible:Visibility.Collapsed};AddActivityControls(footer);footer.Children.Add(workProgress);Grid.SetRow(footer,2);Grid.SetColumnSpan(footer,2);shell.Children.Add(footer);
        toastHost=new Border{Background=Surface,BorderBrush=Accent,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),MaxWidth=480,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(15),Visibility=Visibility.Collapsed};Grid.SetColumn(toastHost,1);Grid.SetRow(toastHost,1);Panel.SetZIndex(toastHost,100);shell.Children.Add(toastHost);
        rebuildingAppearance=preservePages;try{ShowPage(current);}finally{rebuildingAppearance=false;}UpdateWorkUi();
    }
    void Nav(string id,string label){BuildNavigationEntry(id,label);}
    TextBlock Text(string text,double size,Brush color){TextBlock block=Object.ReferenceEquals(color,StateGreen)?new ResultTextBlock{ResolveTone=OutcomeBrush}:new TextBlock();block.Text=text;block.FontSize=compact&&size>=13?size-1:size;block.Foreground=Object.ReferenceEquals(color,StateGreen)?OutcomeBrush(text):color;block.TextWrapping=TextWrapping.Wrap;block.Margin=new Thickness(0,0,0,compact?6:10);return block;}
    Button Button(string label,Action click){Button b=new Button{Content=label,Background=Surface,Foreground=Ink,BorderBrush=Color(dark?"#344057":"#D8E0ED"),BorderThickness=new Thickness(1),Padding=compact?new Thickness(10,6,10,6):new Thickness(14,10,14,10),Margin=new Thickness(0,0,compact?5:8,compact?5:8),Cursor=System.Windows.Input.Cursors.Hand};b.Click+=(s,e)=>click();System.Windows.Automation.AutomationProperties.SetName(b,label);return b;}
    void Heading(string title,string subtitle){body.Children.Add(Text(title,30,Ink));body.Children.Add(Text(subtitle,14,Muted));if(current=="dashboard")BeginColumns(2);else if(new[]{"performance","recovery","browser","settings","storage"}.Contains(current))BeginColumns();}
    StackPanel Card(string title,string detail){StackPanel p=new StackPanel();p.Children.Add(Text(title,18,Ink));AddTestPhaseNotice(p,title);if(detail!="")p.Children.Add(Text(detail,13,Muted));Border border=new Border{Tag="OuO.Card",Background=Surface,CornerRadius=new CornerRadius(12),Padding=compact?new Thickness(12):new Thickness(18),Margin=compact?new Thickness(0,5,0,3):new Thickness(0,9,0,6),Child=p};border.SetBinding(UIElement.VisibilityProperty,new System.Windows.Data.Binding("Visibility"){Source=p});Panel host=(Panel)cardHost??body;host.Children.Add(border);RegisterTweakExtra(border);if(cardHost!=null)ResizeCards(cardHost);return p;}
    void Row(Panel parent,params Button[] buttons){WrapPanel wrap=new WrapPanel();foreach(Button b in buttons)wrap.Children.Add(b);parent.Children.Add(wrap);}
    void Note(Panel p,string pt,string en){p.Children.Add(Text(T(pt,en),13,Muted));}
    TextBox Input(string value){return new TextBox{Text=value,Padding=new Thickness(10),Margin=new Thickness(0,0,8,10),Background=Bg,Foreground=Ink,BorderBrush=Muted};}
    TextBox Output(Panel p){var report=Input("");report.IsReadOnly=true;report.Foreground=StateGreen;report.TextChanged+=(sender,args)=>report.Foreground=OutcomeBrush(report.Text);report.AcceptsReturn=true;report.TextWrapping=TextWrapping.Wrap;report.Height=190;report.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;AttachResult(p,report);console=report;return report;}
    bool Confirm(string text){return ConfirmDialog(text);}
    void Open(string url){try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception e){ShowNotice(e.Message);}}
    bool RequireAdmin(){if(machine!=null&&machine.Admin)return true;ShowNotice(T("Abra novamente usando o botão Administrador no painel inicial.","Reopen using the Administrator button on the overview."));return false;}
    readonly Dictionary<int,string> activeJobKeys=new Dictionary<int,string>();
    readonly Dictionary<int,Tuple<string,DateTime>> activeJobs=new Dictionary<int,Tuple<string,DateTime>>();
    int nextJobId;[ThreadStatic]static int workerJobId;
    void UpdateWorkUi(){
        busy=activeJobs.Count>0;
        if(workNotice!=null){workNotice.Visibility=busy?Visibility.Visible:Visibility.Collapsed;
            if(busy)workNotice.Text=String.Join("  |  ",activeJobs.Select(x=>{WorkProgress p;return jobProgress.TryGetValue(x.Key,out p)&&p.Total>0?p.Label:UiLanguage.Current(x.Value.Item1)+" • "+(DateTime.Now-x.Value.Item2).ToString(@"mm\:ss");}).ToArray());}
        if(workProgress!=null){workProgress.Visibility=busy?Visibility.Visible:Visibility.Collapsed;WorkProgress measured=null;if(activeJobs.Count>0)jobProgress.TryGetValue(activeJobs.Keys.OrderBy(x=>x).First(),out measured);if(measured!=null&&measured.Total<=0)measured=null;workProgress.IsIndeterminate=busy&&measured==null;workProgress.Value=measured==null?0:measured.Percent;workProgress.ToolTip=measured==null?(activeJobs.Count==0?"":activeJobs.Values.First().Item1):measured.Label;}
        if(activityButton!=null)activityButton.Content=T("Atividades e resultados","Activity and results")+(busy?" ("+activeJobs.Count+")":"");if(busy){if(activityTimer==null){activityTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};activityTimer.Tick+=(s,e)=>UpdateWorkUi();}activityTimer.Start();}
        else if(activityTimer!=null)activityTimer.Stop();
    }
    void Work(string title,Func<string> action,Action<string> finished){StartTrackedWork(title,action,finished);}
    void ReportStage(string title,int jobId=0){
        int id=jobId==0?workerJobId:jobId;Dispatcher.BeginInvoke(new Action(()=>{if(id!=0&&activeJobs.ContainsKey(id)){activeJobs[id]=Tuple.Create(title,activeJobs[id].Item2);WorkProgress p;if(jobProgress.TryGetValue(id,out p))p.Stage=title;UpdateWorkUi();}}));
    }    string Describe(CommandResult r){return (r.Code==0?"OK":r.Code==3010||r.Code==1641?T("OK • Reinicialização necessária (","OK • Restart required (")+r.Code+")":"ERROR "+r.Code)+Environment.NewLine+r.Output;}
    void Command(string title,string file,string args,bool admin,int timeout){
        if(admin&&!RequireAdmin())return;if(!Confirm(title+"\n\n"+file+" "+args))return;TextBox target=console;
        Work(title,()=>Describe(Runner.Run(file,args,timeout)),r=>{if(target!=null)target.Text=r;else ShowNotice(r);});
    }
    void Reg(string title,string hive,string path,string name,int value){
        if(hive=="HKLM"&&!RequireAdmin())return;if(!Confirm(title+T("\nA configuração anterior será salva no histórico.","\nThe previous setting will be saved in history.")))return;
        Work(title,()=>{journal.RegistryChange(title,hive,path,name,value,RegistryValueKind.DWord);return T("Aplicado e verificado. Pode exigir reinicialização ou reabrir o aplicativo.","Applied and verified. Restart Windows or the affected application if needed.");},r=>ShowNotice(r));
    }
    bool initializedOnce;
    void OnLoaded(object sender,RoutedEventArgs args){
        UiLifetimeProbe.Stage("OnLoaded; initialized="+initializedOnce);if(initializedOnce)return;initializedOnce=true;Loaded-=OnLoaded;
        if(!preview){if(LifecycleDialog(false).ShowDialog()!=true){exitFinalized=true;Dispatcher.BeginInvoke(new Action(Close),DispatcherPriority.Background);return;}startupAccepted=true;InitAutoMemory();if(TrayActive)Dispatcher.BeginInvoke(new Action(HideToTray),DispatcherPriority.Background);}
        Work(T("Detectar sistema","Detect system"),()=>{machine=Machine.Detect();return machine.Caption+" "+machine.Version;},r=>{
            pageCache.Clear();pageRefreshActions.Clear();ShowPage(current);status.Text=r+" • "+(machine.Admin?T("Administrador","Administrator"):T("Usuário padrão","Standard user"));
            // CPU load uses native counters; avoid loading performance-counter libraries during startup.
            Native.GetSystemTimes(out previousIdle,out previousKernel,out previousUser);
            if(!preview)sensors.Start();InitializeNextExperience();
            timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};timer.Tick+=(s,e)=>{timer.Interval=TimeSpan.FromSeconds(current=="dashboard"||current=="performance"?3:TrayActive?8:15);Sample();};timer.Start();Sample();
            if(Program.PendingOffice!=null&&!preview){OfficeAutoJob pending=Program.PendingOffice;Program.PendingOffice=null;Dispatcher.BeginInvoke(new Action(()=>StartOfficeAuto(pending,true)));}
            if(preview){DispatcherTimer shot=new DispatcherTimer{Interval=TimeSpan.FromSeconds(4)};shot.Tick+=(s,e)=>{shot.Stop();if(Environment.GetCommandLineArgs().Contains("--soak-ui")){VerifyNavigationSoak();return;}try{
                if(Environment.GetCommandLineArgs().Contains("--verify-ui")) {
                    L.Pt=true;dark=true;BuildShell();
                    foreach(string page in new[]{"dashboard","recommendations","performance","processes","stability","tweaks","repair","repair-advanced","protection","dns","network","storage","cloud-storage","cleanup","startup","apps","apps-updates","office","bloat","browser","recovery","drivers","upgrade","settings","history","about"}) {ShowPage(page);Capture("diagnostics/"+page+"-pt.png");}
                    ShowPage("storage");pageScroll.ScrollToVerticalOffset(400);Capture("diagnostics/storage-explorer-pt.png");pageScroll.ScrollToTop();ShowPage("storage");pageScroll.ScrollToBottom();Capture("diagnostics/direct-clone-pt.png");pageScroll.ScrollToTop();ShowPage("cleanup");pageScroll.ScrollToBottom();Capture("diagnostics/registry-expanded-pt.png");pageScroll.ScrollToTop();ShowPage("drivers");pageScroll.ScrollToBottom();Capture("diagnostics/driver-catalogs-pt.png");pageScroll.ScrollToTop();
                    ShowPage("browser");var browserTabs=body.Children.OfType<WrapPanel>().First();foreach(var button in browserTabs.Children.OfType<Button>()){button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));int shown=body.Children.OfType<CardColumns>().Count(x=>x.Visibility==Visibility.Visible);if(shown!=1)throw new InvalidOperationException("Safe browsing must show exactly one internal section.");Capture("diagnostics/browser-tab-"+browserTabs.Children.IndexOf(button)+".png");}
                    ShowPage("apps");AddQueue("Mozilla.Firefox");AddQueue("Google.Chrome");AddQueue("7zip.7zip");Capture("diagnostics/apps-queue-pt.png");
                    ShowPage("performance");foreach(var pane in AppearanceTree(body).OfType<Expander>().ToArray()){var reportBox=AppearanceTree(pane).OfType<TextBox>().FirstOrDefault();if(reportBox!=null)reportBox.Text="OK • relatório de teste";pane.IsExpanded=true;}Capture("diagnostics/performance-expanded-pt.png");Border[] performanceCards=cardHost.Children.OfType<Border>().ToArray();if(performanceCards.Length>=3&&VisualTreeHelper.GetOffset(performanceCards[2]).Y>VisualTreeHelper.GetOffset(performanceCards[0]).Y+performanceCards[0].ActualHeight+40)throw new InvalidOperationException("Expanded card left a full-row gap in Memory & performance.");
                    ShowPage("drivers");var retainedBody=body;Panel retainedHost=cardHost==null?(Panel)body:cardHost;var retainedText=retainedHost.Children.OfType<Border>().SelectMany(b=>((StackPanel)b.Child).Children.OfType<TextBlock>()).First();retainedText.Text="retained-state-check";ShowPage("dns");ShowPage("drivers");if(!Object.ReferenceEquals(retainedBody,body)||retainedText.Text!="retained-state-check")throw new InvalidOperationException("Page state was reset on navigation.");
                    Width=820;Height=610;ShowPage("dns");Capture("diagnostics/dns-compact.png");Width=1320;Height=900;
                    Width=1720;ShowPage("tweaks");Capture("diagnostics/tweaks-wide.png");Width=1320;
                    VerifyRevision5UI();
                    VerifyStableUI();
                    VerifyRevision7UI();VerifyQualificationUI();VerifyNextReleaseUI();VerifyCloudStorageUI();VerifyOneDriveCloudUI();
                    dark=false;current="dashboard";BuildShell();Capture("diagnostics/dashboard-light.png");L.Pt=false;BuildShell();Capture("diagnostics/dashboard-en.png");
                    foreach(string page in new[]{"repair","repair-advanced","processes","protection","dns","office","cloud-storage"}){ShowPage(page);Capture("diagnostics/"+page+"-en.png");}
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-results.txt"),"PASS: 26 views, cloud storage ranking, direct clone, expanded registry, driver catalogs and 4 safe-browsing sections rendered; retained page state and compact window checked; expanded-card layout checked; disk explorer fixture rendered; RC5 selectors, temporary selection, synchronized folder tree and cached Back navigation checked; RC6 grouped navigation, category filtering, offline recovery and state retained through theme, accent, density, language and a running background callback; RC7 component queries across navigation, explicit OneDrive choice, automatic-memory controls, tab contrast, semantic colors, startup consent and fifteen-second farewell; light/dark themes; Portuguese/English. No system changes executed.");
                } else if(Environment.GetCommandLineArgs().Contains("--soak-ui")) VerifyNavigationSoak();else Capture("preview.png");
            }catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,Environment.GetCommandLineArgs().Contains("--soak-ui")?"soak-results.txt":"ui-results.txt"),"FAIL • "+ex.ToString());}finally{if(Environment.GetCommandLineArgs().Contains("--verify-ui")){activeJobs.Clear();activeJobKeys.Clear();}Close();}};shot.Start();}
        });
    }
    void Capture(string relative){Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));UpdateLayout();RenderTargetBitmap bitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);PngBitmapEncoder enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bitmap));string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));using(var f=File.Create(path))enc.Save(f);}
    void Sample(){if(sampling||machine==null||(!IsVisible&&!TrayActive))return;sampling=true;
        Task.Factory.StartNew(()=>{
            string cpu="—",gpu="—",temp=T("Não disponível","Not available");ulong free=0;
            long idle,kernel,user;if(Native.GetSystemTimes(out idle,out kernel,out user)){long delta=(kernel-previousKernel)+(user-previousUser);if(delta>0)cpu=(Math.Max(0,Math.Min(100,100.0*(delta-(idle-previousIdle))/delta))).ToString("0")+"%";previousIdle=idle;previousKernel=kernel;previousUser=user;}
            
            if(cpu=="—"){string load=Machine.First("SELECT LoadPercentage FROM Win32_Processor","LoadPercentage");if(load!="")cpu=load+"%";}
            Native.Memory memory=Native.ReadMemory();if(memory!=null)free=memory.Free/1024;
            try{double max=0;bool found=false;if(current=="dashboard"||TrayActive)foreach(var o in Machine.Query("root\\cimv2","SELECT UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine"))using(o){double v;if(Double.TryParse(Machine.Value(o,"UtilizationPercentage"),out v)){max=Math.Max(max,v);found=true;}}if(found)gpu=max.ToString("0")+"%*";}catch{}
            SensorSnapshot snapshot=sensors.Snapshot;if(!preview&&snapshot.Status!="ready"&&(DateTime.UtcNow-lastExternalSensorRead).TotalSeconds>=60){lastExternalSensorRead=DateTime.UtcNow;SensorSnapshot external=SensorSnapshot.ReadWmi();if(external!=null)snapshot=external;}if(snapshot.GpuLoad!="")gpu=snapshot.GpuLoad;
            if(snapshot.CpuName!="")machine.Cpu=snapshot.CpuName;if(snapshot.GpuName!="")machine.Gpu=snapshot.GpuName;if(snapshot.BoardName!="")machine.Board=snapshot.BoardName;
            if(snapshot.Status=="ready")temp=snapshot.Source+(snapshot.Cpu=="—"&&machine!=null&&!machine.Admin?T(" • abra como administrador para consultar os sensores de temperatura."," • run as administrator to query temperature sensors."):T(" • leitura ativa. Sensores não expostos pelo equipamento aparecem como indisponíveis."," • readings active. Sensors not exposed by the device are shown as unavailable."));else if(preview)temp=T("Motor de sensores não iniciado neste modo de verificação.","Sensor engine is not started in this verification mode.");else if(snapshot.Status=="framework-required")temp=T("Sensores integrados exigem .NET Framework 4.7.2 ou posterior.","Integrated sensors require .NET Framework 4.7.2 or later.");else temp=T("Motor integrado: ","Integrated engine: ")+snapshot.Status;
            return new string[]{cpu,gpu,free.ToString(CultureInfo.InvariantCulture),snapshot.Cpu,snapshot.Gpu,snapshot.Vrm,temp};
        }).ContinueWith(task=>Dispatcher.BeginInvoke(new Action(()=>{sampling=false;if(!task.IsFaulted){
            lastSample=task.Result;if(current=="dashboard")RenderMetrics();UpdateTrayStatus();
        }})));
    }
    void ShowPage(string id){ShowPage(id,false);}
    void ShowPage(string id,bool refresh){if(pageScroll!=null&&pageCache.ContainsKey(current))pageCache[current].Offset=pageScroll.VerticalOffset;current=id;SelectNavigation(id);DisplayPageToolbar(id);
        PageView cached;if(!refresh&&id!="history"&&pageCache.TryGetValue(id,out cached)){body=cached.Body;console=cached.Console;cardHost=cached.Columns;pageScroll.Content=body;pageScroll.ScrollToVerticalOffset(cached.Offset);if(id=="dashboard")RenderMetrics();Action reload;if(!preview&&!rebuildingAppearance&&pageRefreshActions.TryGetValue(id,out reload))Dispatcher.BeginInvoke(reload);return;}
        if(id=="history"&&pageCache.ContainsKey(id))UiLifetimeProbe.Record(pageCache[id].Body,"replaced-history");cardHost=null;console=null;body=new StackPanel{Margin=compact?new Thickness(15):new Thickness(25,25,30,25),MaxWidth=1450,HorizontalAlignment=HorizontalAlignment.Stretch};pageScroll.Content=body;
        switch(id){case "dashboard":Dashboard();break;case "recommendations":RecommendationsPage();break;case "processes":ProcessesPage();break;case "stability":StabilityPage();break;case "repair":Repair(false);break;case "repair-advanced":Repair(true);break;case "protection":Protection();break;case "dns":DnsPage();break;case "network":PrintingPage();break;case "office":Office();break;case "performance":Performance();break;case "tweaks":Tweaks();break;case "storage":Storage();break;case "cloud-storage":CloudStoragePage();break;case "cleanup":CleanupPage();break;case "startup":Startup();break;case "apps":Apps();break;case "apps-updates":AppUpdates();break;case "bloat":Bloat();break;case "browser":Browser();break;case "privacy":Privacy();break;case "recovery":Recovery();break;case "drivers":DriversPage();break;case "upgrade":Upgrade();break;case "settings":Settings();break;case "history":History();break;case "about":AboutPage();break;}
        pageCache[id]=new PageView{Body=body,Console=console,Columns=cardHost};pageScroll.ScrollToTop();
    }
    void Dashboard(){
        Heading(T("Um novo olhar para o seu PC","A fresh look at your PC"),T("Diagnóstico real. Ajustes sob seu controle.","Real diagnostics. Changes under your control."));
        var p=Card(machine==null?T("Conhecendo seu computador…","Getting to know your PC…"):machine.Caption,machine==null?"":machine.Version+" • "+machine.Arch+" • Build "+machine.Build);
        hardwareIdentity=Text(machine==null?T("Aguarde a leitura do hardware.","Please wait for hardware detection."):machine.Cpu+"\n"+machine.Gpu+"\n"+machine.Board,13,Muted);p.Children.Add(hardwareIdentity);
        WrapPanel metrics=new WrapPanel();p.Children.Add(metrics);metricCpu=Metric(metrics,"CPU");metricRam=Metric(metrics,"RAM");metricGpu=Metric(metrics,"GPU");
        tempCpu=Metric(metrics,T("TEMPERATURA CPU","CPU TEMPERATURE"));tempGpu=Metric(metrics,T("TEMPERATURA GPU","GPU TEMPERATURE"));tempVrm=Metric(metrics,T("TEMPERATURA VRM","VRM TEMPERATURE"));
        Note(p,"* GPU: maior utilização entre mecanismos detectados; não representa uma soma de todos os mecanismos.","* GPU: highest utilization among detected engines; not the sum of all engines.");
        var sensorPanel=Card(T("Motor de sensores","Sensor engine"),T("Libre Hardware Monitor 0.9.6 incluído no pacote.","Libre Hardware Monitor 0.9.6 included in the package."));metricTemp=Text(T("Iniciando motor de sensores…","Starting sensor engine…"),12,Muted);sensorPanel.Children.Add(metricTemp);
        Note(sensorPanel,"Leitura direta integrada. CPU e VRM podem exigir administrador e o driver PawnIO. Exibe o máximo válido por grupo; leituras zeradas ou não expostas aparecem como —. O WMI externo continua como alternativa quando disponível.","Integrated direct readings. CPU and VRM may require administrator and the PawnIO driver. Displays the valid maximum per group; zero or unexposed readings appear as —. External WMI remains an alternative when available.");
        Row(sensorPanel,Button(T("Reiniciar sensores","Restart sensors"),()=>{if(!preview)Work(T("Reiniciar sensores","Restart sensors"),()=>{sensors.Restart();return T("Motor reiniciado. Aguarde a próxima leitura.","Engine restarted. Wait for the next reading.");},r=>metricTemp.Text=r);}),Button(T("Preparar driver de sensores","Set up sensor driver"),()=>InstallSensorDriver()));
        Row(p,Button(T("Atualizar diagnóstico","Refresh diagnostics"),()=>Work(T("Detectar sistema","Detect system"),()=>{machine=Machine.Detect();return machine.Caption;},r=>ShowPage("dashboard",true))),Button(T("Abrir como administrador","Open as administrator"),()=>{
            if(busy)return;try{Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName){UseShellExecute=true,Verb="runas"});exitFinalized=true;Close();}catch(Exception e){ShowNotice(e.Message);}
        }));
        DashboardStorage();
        DashboardQuickActions();
        RenderMetrics();
    }
    TextBlock Metric(Panel p,string name){var stack=new StackPanel{Width=190,Margin=new Thickness(0,12,5,12)};stack.Children.Add(Text(name,12,Muted));TextBlock value=Text("—",26,Accent);stack.Children.Add(value);p.Children.Add(stack);return value;}
    void Performance(){
        Heading(T("Memória e desempenho","Memory & performance"),T("Energia, compactação e liberação de memória sob seu controle.","Power, compression and memory reclaim under your control."));
        if(cardHost!=null)cardHost.MaxColumns=2;
        var p=Card(T("Perfil para pouca memória","Low-memory profile"),machine!=null&&machine.Total>0&&machine.Total<=4194304?T("Este computador possui 4 GB de RAM ou menos.","This computer has 4 GB RAM or less."):T("Revise cada ajuste conforme seu uso.","Review each setting for your needs."));
        Note(p,"O perfil orienta a revisão da inicialização, efeitos visuais e memória virtual. O OuO não encerra processos nem desativa o arquivo de paginação.","The profile guides review of startup, visual effects and virtual memory. OuO does not terminate processes or disable paging.");
        Row(p,Button(T("Efeitos visuais e paginação","Visual effects & paging"),()=>Open("SystemPropertiesPerformance.exe")),Button(T("Gerenciador de tarefas","Task Manager"),()=>Open("taskmgr.exe")));
        var power=Card(T("Desbloquear desempenho","Unlock performance"),T("Ativa o plano Alto desempenho. Pode aumentar temperatura, consumo e uso de bateria. Disponibilidade depende do dispositivo.","Enables High performance. May increase temperature, energy use and battery drain. Availability depends on the device."));
        Row(power,Button(T("Ver planos de energia","View power plans"),()=>ReadCommand("powercfg.exe","/list")),Button(T("Ativar Alto desempenho","Enable High performance"),()=>Power("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")),Button(T("Ativar Equilibrado","Enable Balanced"),()=>Power("381b4222-f694-41f0-9685-ff5bb260df2e")),Button(T("Restaurar plano anterior","Restore previous plan"),()=>RestorePower()));
        Row(p,Button(T("Limpeza de disco do Windows","Windows Disk Cleanup"),()=>Open("cleanmgr.exe")));MemoryCompression();AutoMemoryCard();
    }
    void ReadCommand(string file,string args){TextBox target=console;Work(T("Consultar","Query"),()=>Describe(Runner.Run(file,args,30000)),r=>{if(target!=null)target.Text=r;else ShowNotice(r);});}
    void Power(string guid){if(!RequireAdmin()||!Confirm(T("Alterar o plano de energia? O plano atual será salvo.","Change power plan? The current plan will be saved.")))return;TextBox target=console;
        Work("Power plan",()=>{var old=Runner.Run("powercfg.exe","/getactivescheme",15000);Match m=Regex.Match(old.Output,"[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}");if(old.Code!=0||!m.Success)throw new IOException("Cannot read current plan");string f=Path.Combine(journal.Folder,"power-previous.txt");if(!File.Exists(f))File.WriteAllText(f,m.Value);return Describe(Runner.Run("powercfg.exe","/setactive "+guid,15000));},r=>{if(target!=null)target.Text=r;else ShowToast(r);});
    }
    void RestorePower(){string f=Path.Combine(journal.Folder,"power-previous.txt");if(!File.Exists(f)){ShowNotice(T("Nenhum plano anterior salvo.","No previous plan saved."));return;}string guid=File.ReadAllText(f).Trim();if(!Regex.IsMatch(guid,"^[0-9a-fA-F-]{36}$"))return;if(!RequireAdmin()||!Confirm(T("Restaurar plano de energia salvo?","Restore saved power plan?")))return;TextBox target=console;Work("Restore power",()=>{var r=Runner.Run("powercfg.exe","/setactive "+guid,15000);if(r.Code==0)File.Delete(f);return Describe(r);},r=>{if(target!=null)target.Text=r;else ShowToast(r);});}
    void Storage(){
        Heading(T("Armazenamento","Storage"),T("Saúde, espaço e manutenção adequada à unidade.","Health, space and drive-appropriate maintenance."));
        cardHost=null;
        StorageDashboard();
        DiskMapPage();
        BeginColumns(2);
        var p=Card(T("Unidades e diagnóstico","Drives & diagnostics"),T("SMART é um indicador, não uma garantia. Alguns controladores USB e RAID não expõem todos os dados.","SMART is an indicator, not a guarantee. Some USB and RAID controllers do not expose all data."));
        Row(p,Button(T("Consultar unidades e SMART","Read drives & SMART"),()=>{TextBox target=console;Work("Storage / SMART",()=>{
            StringBuilder b=new StringBuilder();foreach(DriveInfo d in DriveInfo.GetDrives())if(d.IsReady)b.AppendLine(d.Name+"  "+d.DriveFormat+"  "+(d.AvailableFreeSpace/1073741824.0).ToString("0.0")+" GB free / "+(d.TotalSize/1073741824.0).ToString("0.0")+" GB");
            foreach(var o in Machine.Query("root\\cimv2","SELECT Model,Status,InterfaceType,Size FROM Win32_DiskDrive"))using(o)b.AppendLine(Machine.Value(o,"Model")+" | "+Machine.Value(o,"InterfaceType")+" | "+Machine.Value(o,"Status"));
            try{foreach(var o in Machine.Query("root\\wmi","SELECT InstanceName,PredictFailure FROM MSStorageDriver_FailurePredictStatus"))using(o)b.AppendLine(Machine.Value(o,"InstanceName")+" PredictFailure="+Machine.Value(o,"PredictFailure"));}catch(Exception e){b.AppendLine("SMART: "+e.Message);}
            if(machine!=null&&machine.Build>=9200)b.AppendLine(Describe(Runner.PS("Get-PhysicalDisk | Select-Object FriendlyName,MediaType,HealthStatus,OperationalStatus | Format-Table -AutoSize",30000)));
            return b.ToString();},r=>target.Text=r);
        }),Button(T("Consultar TRIM","Query TRIM"),()=>ReadCommand("fsutil.exe","behavior query DisableDeleteNotify")),Button(T("Ativar TRIM","Enable TRIM"),()=>Command("TRIM ON","fsutil.exe","behavior set DisableDeleteNotify 0",true,15000)),Button(T("Desativar TRIM","Disable TRIM"),()=>Command(T("Desativar TRIM globalmente; pode reduzir o desempenho dos SSDs","Disable TRIM globally; SSD performance may decrease"),"fsutil.exe","behavior set DisableDeleteNotify 1",true,15000)));
        Note(p,"TRIM: DisableDeleteNotify = 0 significa habilitado; 1 significa desabilitado. Consulte e registre o estado antes de alterá-lo.","TRIM: DisableDeleteNotify = 0 means enabled; 1 means disabled. Query and record the state before changing it.");
        var maintenance=Card(T("Otimização e agendamento","Optimization & scheduling"),T("No Windows 8 ou posterior, o Windows escolhe a manutenção adequada ao tipo da unidade. No Windows 7, use a ferramenta nativa para revisar a unidade e a agenda.","On Windows 8 or later, Windows chooses maintenance appropriate to the drive type. On Windows 7, review drives and schedules in the native tool."));
        Row(maintenance,Button(T("Abrir Otimizar Unidades / agenda","Open Optimize Drives / schedule"),()=>Open("dfrgui.exe")));
        if(machine!=null&&machine.Build>=9200){
            ComboBox drives=new ComboBox{Margin=new Thickness(0,0,0,10)};foreach(DriveInfo d in DriveInfo.GetDrives())if(d.DriveType==DriveType.Fixed)drives.Items.Add(d.Name.Substring(0,2));drives.SelectedIndex=0;maintenance.Children.Add(drives);
            Row(maintenance,Button(T("Otimizar unidade selecionada","Optimize selected drive"),()=>{if(drives.SelectedItem!=null)Command(T("Otimizar unidade","Optimize drive"),"defrag.exe",drives.SelectedItem+" /O /U /V",true,1800000);}));
            Note(maintenance,"Agendas diária, semanal e mensal ficam disponíveis no botão da ferramenta nativa. A execução pode demorar; aguarde o resultado.","Daily, weekly and monthly schedules are available through the native tool button. Execution may take time; wait for the result.");
        }Output(p);DiskMigrationCard();cardHost=null;DirectCloneCard();
    }
    void Startup(){
        Heading(T("Inicialização mais leve","A lighter startup"),T("Gerencie entradas do seu usuário. Entradas de máquina são exibidas para consulta.","Manage current-user entries. Machine entries are shown for reference."));
        BeginColumns(2);
        var p=Card(T("Aplicativos ao entrar no Windows","Apps at Windows sign-in"),T("A lista cobre as chaves Run e as entradas desativadas pelo OuO. Os atalhos da pasta Inicializar aparecem ao lado.","This list covers Run keys and entries disabled by OuO. Startup folder shortcuts appear beside it."));
        ListBox list=new ListBox{Height=270,Margin=new Thickness(0,0,0,10),Background=Surface,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};p.Children.Add(list);
        TextBlock startupState=Text("",13,Muted);p.Children.Add(startupState);
        Action refresh=null;Action<StartupItem,bool> setEnabled=(item,enabled)=>{
            bool wasEnabled=item.Path==RunPath;if(enabled==wasEnabled)return;
            if(!Confirm(T("Alterar a inicialização de ","Change startup for ")+item.Name+"?\n"+item.Command)){refresh();return;}
            string destination=enabled?RunPath:RunPath+"-AuroraDisabled";
            try{
                using(var dest=Registry.CurrentUser.OpenSubKey(destination))if(dest!=null&&dest.GetValueNames().Contains(item.Name))throw new IOException(T("Já existe uma entrada com esse nome no destino.","An entry with this name already exists at the destination."));
                RegistryValueKind kind;object value;using(var src=Registry.CurrentUser.OpenSubKey(item.Path)){kind=src.GetValueKind(item.Name);value=src.GetValue(item.Name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);}
                string backup=Path.Combine(journal.Folder,"Startup-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff")+".backup");
                new XElement("startup",new XAttribute("name",item.Name),new XAttribute("source",item.Path),new XAttribute("destination",destination),new XAttribute("kind",kind),Convert.ToString(value)).Save(backup);
                using(var dest=Registry.CurrentUser.CreateSubKey(destination))dest.SetValue(item.Name,value,kind);
                using(var dest=Registry.CurrentUser.OpenSubKey(destination))if(!object.Equals(dest.GetValue(item.Name,null,RegistryValueOptions.DoNotExpandEnvironmentNames),value))throw new IOException("Startup copy verification failed");
                using(var src=Registry.CurrentUser.OpenSubKey(item.Path,true))src.DeleteValue(item.Name);journal.Log("Startup move",item.Path+" -> "+destination+" | "+item.Name);
            }catch(Exception e){ShowNotice(e.Message);}finally{refresh();}
        };
        refresh=()=>{list.Items.Clear();var issues=new List<string>();foreach(string scope in new[]{"HKCU","HKLM"})foreach(string path in new[]{RunPath,RunPath+"-AuroraDisabled"}){
            try{RegistryKey root=scope=="HKCU"?Registry.CurrentUser:Registry.LocalMachine;using(var key=root.OpenSubKey(path))if(key!=null)foreach(string n in key.GetValueNames()){
                StartupItem item=new StartupItem{Name=n,Command=Convert.ToString(key.GetValue(n)),Scope=scope+(path.EndsWith("Disabled")?" OFF":" ON"),Path=path};
                CheckBox check=new CheckBox{IsChecked=path==RunPath,IsEnabled=scope=="HKCU",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4,0,10,0),ToolTip=scope=="HKCU"?T("Marque para ativar; desmarque para desativar.","Check to enable; uncheck to disable."):T("Entrada do sistema: somente leitura.","System entry: read-only.")};
                check.Checked+=(s,e)=>setEnabled(item,true);check.Unchecked+=(s,e)=>setEnabled(item,false);
                StackPanel labels=new StackPanel();labels.Children.Add(new TextBlock{Text=item.Name,FontSize=14,Foreground=Ink});labels.Children.Add(new TextBlock{Text=item.Detail,FontSize=11,Foreground=Muted,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=650});
                StackPanel row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(4,5,4,5)};row.Children.Add(check);row.Children.Add(new Image{Source=item.Icon,Width=28,Height=28,Margin=new Thickness(0,0,12,0)});row.Children.Add(labels);list.Items.Add(row);
            }}catch(Exception e){issues.Add(scope+": "+e.Message);}}list.Visibility=list.Items.Count>0?Visibility.Visible:Visibility.Collapsed;startupState.Text=issues.Count>0?"ERROR • "+T("Algumas entradas não puderam ser lidas. Confira as permissões do Registro.","Some entries could not be read. Check Registry permissions."):list.Items.Count+T(" entrada(s) encontrada(s)."," entry/entries found.");startupState.Foreground=issues.Count>0?OutcomeBrush(startupState.Text):StateGreen;startupState.ToolTip=String.Join("\n",issues.ToArray());};
        refresh();Row(p,Button(T("Atualizar lista","Refresh list"),()=>refresh()),Button(T("Ferramenta nativa","Native tool"),()=>Open(machine!=null&&machine.Build>=9200?"taskmgr.exe":"msconfig.exe")));
        Note(p,"Marque para ativar e desmarque para desativar entradas do usuário atual. Entradas do sistema são somente leitura. Cada alteração é confirmada e registrada para revisão.","Check to enable and uncheck to disable current-user entries. System entries are read-only. Each change is confirmed and logged for review.");
        Action refreshShortcuts=StartupFolderCard();pageRefreshActions["startup"]=()=>{refresh();refreshShortcuts();};
    }
    void Apps(){
        packageChoices.Clear();
        Heading(T("Seus programas, em um lugar","Your apps, in one place"),T("Pesquise no WinGet ou escolha entre os programas mais baixados.","Search WinGet or choose from popular downloads."));
        var tabs=Card(T("Programas","Programs"),"");Row(tabs,TabButton(T("Instalar programas","Install programs"),()=>{},true),TabButton(T("Atualizar instalados","Update installed"),()=>ShowPage("apps-updates")),Button(T("Instalar ou corrigir o WinGet","Install or repair WinGet"),()=>{
            if(machine==null||machine.Build<17763){ShowNotice(T("O WinGet requer uma versão compatível do Windows 10 ou 11.","WinGet requires a compatible Windows 10 or 11 release."));return;}
            if(!Confirm(T("Instalar ou corrigir o WinGet usando o módulo oficial da Microsoft? Isso baixará componentes da PowerShell Gallery.","Install or repair WinGet using Microsoft's official module? This downloads components from PowerShell Gallery.")))return;
            Work(T("Corrigir WinGet","Repair WinGet"),()=>WingetSetup.Repair(),r=>{ShowNotice(r);if(!ResultTone.IsFailure(r)){machine.Winget=true;ShowPage("apps",true);}});
        }));
        BeginColumns(2);bool available=machine!=null&&machine.Winget;
        var p=Card(T("Instalar com o WinGet","Install with WinGet"),available?T("Disponível para este usuário. Instalações podem abrir o instalador ou solicitar elevação.","Available for this user. Installers may open or request elevation."):T("Não detectado. Requer Windows 10 1809 ou posterior e o Instalador de Aplicativo. Nos sistemas antigos, use os sites oficiais e verifique a compatibilidade.","Not detected. Requires Windows 10 1809 or later and App Installer. On older systems use official websites and check compatibility."));
        TextBox search=Input("");p.Children.Add(Text(T("Buscar programa","Search apps"),13,Muted));p.Children.Add(search);
        ListBox results=IconList(160,true,false);results.Visibility=Visibility.Collapsed;p.Children.Add(results);
        results.SelectionChanged+=(s,e)=>{var item=results.SelectedItem as PackageItem;if(item!=null)AddQueue(item.Id);};
        CheckBox sourceAgreement=new CheckBox{Content=T("Aceitar automaticamente os termos da fonte WinGet ao consultar.","Automatically accept WinGet source terms when searching."),Foreground=Ink,Margin=new Thickness(0,4,0,12),IsChecked=true};p.Children.Add(sourceAgreement);
        results.ToolTipOpening+=(s,e)=>{var source=e.OriginalSource as FrameworkElement;var item=source==null?null:source.DataContext as PackageItem;if(item==null||item.Summary!=null||!available||sourceAgreement.IsChecked!=true)return;if(PopularCatalog.Description(item.Id)!=item.Id){item.Summary=PopularCatalog.Description(item.Id);return;}item.Summary=T("Buscando descrição do programa…","Loading app description…");Work(T("Descrição de ","Description of ")+item.Id,()=>PackageMetadata.Get(item.Id),r=>item.Summary=r);};
        Button searchButton=Button(T("Pesquisar","Search"),()=>{
            if(!available||String.IsNullOrWhiteSpace(search.Text))return;string term=search.Text.Trim();if(!Regex.IsMatch(term,"^[\\p{L}\\p{N} ._+-]{1,80}$")){ShowNotice(T("Use letras, números, espaços, ponto, +, - ou _.","Use letters, numbers, spaces, dot, +, - or _."));return;}
            string consent=sourceAgreement.IsChecked==true?" --accept-source-agreements":"";
            TextBox target=console;Work("WinGet search",()=>Describe(Runner.Run("winget.exe","search --query \""+term+"\" --source winget --disable-interactivity"+consent,120000)),r=>{
                if(term!=search.Text.Trim())return;target.Text=r;results.Items.Clear();foreach(string line in r.Split('\n')){string[] columns=Regex.Split(line.Trim(),"\\s{2,}");if(columns.Length>=3&&Runner.ValidId(columns[1])&&columns[1].Contains("."))results.Items.Add(AppIcons.Package(columns[1],columns[0]));}results.Visibility=results.Items.Count>0?Visibility.Visible:Visibility.Collapsed;
            });
        });Row(p,searchButton,Button(T("Detalhes e licença","Details & license"),()=>{var item=results.SelectedItem as PackageItem;string id=item==null?queue.FirstOrDefault():item.Id;if(id==null||!available)return;string consent=sourceAgreement.IsChecked==true?" --accept-source-agreements":"";ReadCommand("winget.exe","show --id "+id+" --exact --source winget --disable-interactivity"+consent);}),Button(T("Instalador de Aplicativo","App Installer"),()=>Open("https://apps.microsoft.com/detail/9nblggh4nns1")));
        DispatcherTimer liveSearch=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(600)};
        liveSearch.Tick+=(s,e)=>{liveSearch.Stop();if(current!="apps"||search.Text.Trim().Length<2||sourceAgreement.IsChecked!=true)return;if(activeJobs.Values.Any(x=>x.Item1=="WinGet search")){liveSearch.Start();return;}searchButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));};
        search.TextChanged+=(s,e)=>{liveSearch.Stop();if(search.Text.Trim().Length>=2)liveSearch.Start();};
        sourceAgreement.Checked+=(s,e)=>{if(search.Text.Trim().Length>=2){liveSearch.Stop();liveSearch.Start();}};        var q=Card(T("Fila de instalação","Install queue"),T("Confira os identificadores. Contratos que exigirem aceite devem ser revisados antes de instalar.","Review package IDs. Agreements requiring acceptance must be reviewed before installation."));queueEmpty=Text(T("Fila vazia. Escolha um programa na busca ou nos mais baixados.","Queue is empty. Choose an app from search or popular apps."),13,StateGreen);q.Children.Add(queueEmpty);queueList=IconList(150,true,false);queueList.Visibility=queue.Count>0?Visibility.Visible:Visibility.Collapsed;foreach(string id in queue)queueList.Items.Add(AppIcons.Package(id));q.Children.Add(queueList);
        CheckBox packageAgreement=new CheckBox{Content=T("Aceitar automaticamente os termos dos pacotes e da fonte WinGet.","Automatically accept WinGet package and source terms."),Foreground=Ink,Margin=new Thickness(0,4,0,12),IsChecked=true};q.Children.Add(packageAgreement);
        Row(q,Button(T("Remover selecionados","Remove selected"),()=>{foreach(string id in AppIcons.Selected(queueList))queue.Remove(id);RefreshQueue();}),Button(T("Instalar fila","Install queue"),()=>{
            if(!available){ShowNotice(T("WinGet indisponível neste sistema.","WinGet unavailable on this system."));return;}string[] ids=queue.ToArray();if(ids.Length==0||!Confirm(T("Instalar os seguintes programas?\n","Install these apps?\n")+String.Join("\n",ids)))return;
            string consent=packageAgreement.IsChecked==true?" --accept-package-agreements --accept-source-agreements":"";
            TextBox target=console;var completed=new List<string>();var names=ids.ToDictionary(id=>id,id=>AppIcons.Package(id).Name);Work("WinGet install",()=>{StringBuilder output=new StringBuilder();int completedCount=0;foreach(string id in ids){WorkReporting.Report(completedCount++,ids.Length,T("Instalando ","Installing ")+names[id]);if(!Runner.ValidId(id))throw new ArgumentException("Invalid package ID");ReportStage(T("Instalando ","Installing ")+names[id]);output.AppendLine(names[id]);var installed=Runner.Run("winget.exe","install --id "+id+" --exact --source winget --silent --disable-interactivity"+consent,1800000);output.AppendLine(Describe(installed));if(installed.Code==0||installed.Code==3010||installed.Code==1641)completed.Add(id);}WorkReporting.Report(ids.Length,ids.Length,T("Fila processada","Queue processed"));return output.ToString();},r=>{target.Text=r;foreach(string id in completed)queue.Remove(id);RefreshQueue();});
        }));Output(q);
        Row(q,Button(T("Salvar lista","Save list"),()=>{if(queue.Count==0)return;var dialog=new Microsoft.Win32.SaveFileDialog{Filter="Lista OuO (*.xml)|*.xml",FileName="Meus-programas.xml"};if(dialog.ShowDialog(this)!=true)return;try{PackageProfiles.Save(dialog.FileName,queue);}catch(Exception error){ShowNotice(error.Message);}}),Button(T("Importar lista","Import list"),()=>{string file=PickFile("Lista OuO (*.xml)|*.xml");if(file==null)return;try{foreach(string id in PackageProfiles.Load(file))AddQueue(id);}catch(Exception error){ShowNotice(error.Message);}}));        Row(q,Button(T("Sites oficiais","Official websites"),()=>Open("https://learn.microsoft.com/windows/package-manager/winget/")));
        cardHost=null;
        var catalog=Card(T("Mais baixados","Most downloaded"),T("Sugestões por categoria. Ranking de popularidade do repositório Chocolatey, quando disponível; a instalação usa WinGet.","Suggestions by category. Chocolatey repository popularity ranking when available; installation uses WinGet."));
        TextBlock ranking=Text(PopularCatalog.Status,12,Muted);catalog.Children.Add(ranking);
        var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=4};catalog.Children.Add(grid);
        Action fill=()=>{grid.Children.Clear();packageChoices.Clear();foreach(var app in PopularCatalog.Current())grid.Children.Add(PackageButton(app.Id));ranking.Text=PopularCatalog.Status;};
        fill();catalog.SizeChanged+=(s,e)=>{double width=catalog.ActualWidth;int columns=width>=1150?6:width>=900?5:width>=680?4:width>=480?3:width>=320?2:1;if(grid.Columns!=columns)grid.Columns=columns;};
        pageRefreshActions["apps"]=()=>Work(T("Atualizar ranking de programas","Refresh app ranking"),()=>PopularCatalog.Refresh(),r=>{ranking.Text=r;fill();});
        if(!preview)pageRefreshActions["apps"]();
    }
    List<string> queue=new List<string>();ListBox queueList;TextBlock queueEmpty;
    void AddQueue(string id){if(!Runner.ValidId(id))return;if(!queue.Contains(id))queue.Add(id);RefreshQueue();}
    void RefreshQueue(){if(queueList!=null){queueList.Items.Clear();foreach(string id in queue)queueList.Items.Add(AppIcons.Package(id));queueList.Visibility=queue.Count>0?Visibility.Visible:Visibility.Collapsed;if(queueEmpty!=null)queueEmpty.Visibility=queue.Count==0?Visibility.Visible:Visibility.Collapsed;}syncingPackages=true;try{foreach(var pair in packageChoices)pair.Value.IsChecked=queue.Contains(pair.Key);}finally{syncingPackages=false;}}
    void LegacyBloat(){
        Heading(T("Remover aplicativos","Remove applications"),T("Remoção seletiva, sem listas agressivas de componentes do Windows.","Selective removal, without aggressive lists of Windows components."));
        BeginColumns();
        var p=Card(T("Aplicativos opcionais do usuário","Optional current-user apps"),T("A lista é limitada a aplicativos de consumo conhecidos. A remoção pode apagar dados locais e não possui reversão pelo histórico. Reinstale pela loja quando disponível.","The list is limited to known consumer apps. Removal may delete local data and cannot be reversed through history. Reinstall from the Store when available."));
        if(machine==null||machine.Build<9200)Note(p,"Este Windows não oferece aplicativos AppX. Use Programas e Recursos.","This Windows version does not support AppX apps. Use Programs and Features.");
        ListBox list=new ListBox{Height=150,SelectionMode=SelectionMode.Multiple,Background=Bg,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};p.Children.Add(list);var removedPackages=new List<string>();
        Row(p,Button(T("Listar aplicativos opcionais","List optional apps"),()=>{if(machine==null||machine.Build<9200)return;Work("AppX inventory",()=>Describe(Runner.PS("Get-AppxPackage | Where-Object { $_.Name -match '^(Microsoft\\.(BingNews|BingWeather|MicrosoftSolitaireCollection|GetHelp|Getstarted|ZuneMusic|ZuneVideo)|Clipchamp\\.Clipchamp)$' } | ForEach-Object { $_.PackageFullName }",60000)),r=>{list.Items.Clear();foreach(string line in r.Split('\n')){string s=line.Trim();if(Regex.IsMatch(s,"^[A-Za-z0-9.]+_[A-Za-z0-9._~-]+$"))list.Items.Add(s);}list.Visibility=list.Items.Count>0?Visibility.Visible:Visibility.Collapsed;console.Text=r;});}),Button(T("Remover selecionados","Remove selected"),()=>{
            string[] ids=list.SelectedItems.Cast<string>().ToArray();if(ids.Length==0||!Confirm(T("Remover do usuário atual? Dados locais podem ser perdidos.\n","Remove from current user? Local data may be lost.\n")+String.Join("\n",ids)))return;TextBox target=console;
            Work("AppX removal",()=>{StringBuilder b=new StringBuilder();foreach(string id in ids){b.AppendLine(id);var result=Runner.PS("Remove-AppxPackage -Package "+Runner.QuotePS(id)+" -ErrorAction Stop",120000);if(result.Code==0)lock(removedPackages)removedPackages.Add(id);b.AppendLine(Describe(result));}return b.ToString();},r=>target.Text=r);
        }),Button(T("Programas e Recursos","Programs and Features"),()=>Open("appwiz.cpl")));Output(p);
        var remnants=Card(T("Rastros dos pacotes removidos","Remnants of removed packages"),T("Busca apenas dados locais de pacotes removidos nesta sessão. Pastas do Windows e outros aplicativos não são oferecidos.","Searches only local data for packages removed in this session. Windows folders and other applications are excluded."));
        ListBox found=new ListBox{Height=90,SelectionMode=SelectionMode.Multiple,Background=Bg,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};remnants.Children.Add(found);
        TextBox remnantReport=Input("");remnantReport.IsReadOnly=true;remnantReport.Foreground=StateGreen;remnantReport.TextChanged+=(sender,args)=>remnantReport.Foreground=OutcomeBrush(remnantReport.Text);remnantReport.AcceptsReturn=true;remnantReport.Height=100;remnantReport.Visibility=Visibility.Collapsed;remnants.Children.Add(remnantReport);
        Row(remnants,Button(T("Buscar rastros","Find remnants"),()=>{
            string[] candidates;lock(removedPackages)candidates=removedPackages.ToArray();found.Items.Clear();
            foreach(string id in candidates){string path=AppxRemnants.SafePath(id);if(path!=null&&Directory.Exists(path)&&!AppxRemnants.Installed(id))found.Items.Add(path);}
            found.Visibility=found.Items.Count>0?Visibility.Visible:Visibility.Collapsed;remnantReport.Visibility=Visibility.Visible;remnantReport.Text=found.Items.Count+T(" pasta(s) verificadas disponíveis para revisão."," verified folder(s) available for review.");
        }),Button(T("Mover selecionados para quarentena","Quarantine selected"),()=>{
            string[] selected=found.SelectedItems.Cast<string>().ToArray();if(selected.Length==0)return;
            if(!Confirm(T("Mover estes dados de aplicativos já removidos para quarentena? Revise os caminhos:\n","Move these data from uninstalled apps into quarantine? Review paths:\n")+String.Join("\n",selected)))return;
            Work("AppX remnants quarantine",()=>{var report=new StringBuilder();foreach(string path in selected){string id;lock(removedPackages)id=removedPackages.FirstOrDefault(x=>String.Equals(AppxRemnants.SafePath(x),path,StringComparison.OrdinalIgnoreCase));
                if(id==null||AppxRemnants.Installed(id)||!AppxRemnants.ValidPath(path)) {report.AppendLine(path+" • blocked");continue;}
                string quarantine=Path.Combine(journal.Folder,"Quarantine",DateTime.Now.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.GetDirectoryName(quarantine));Directory.Move(path,quarantine);File.WriteAllText(quarantine+".origin",path);report.AppendLine(path+" → "+quarantine);
            }return report.ToString();},r=>{remnantReport.Visibility=Visibility.Visible;remnantReport.Text=r;found.Items.Clear();found.Visibility=Visibility.Collapsed;});
        }),Button(T("Restaurar último rastro","Restore latest remnant"),()=>{
            string root=Path.Combine(journal.Folder,"Quarantine");if(!Directory.Exists(root))return;
            string manifest=Directory.GetFiles(root,"*.origin").OrderByDescending(x=>x).FirstOrDefault();if(manifest==null)return;
            string original=File.ReadAllText(manifest),saved=manifest.Substring(0,manifest.Length-7);
            if(!AppxRemnants.ValidPath(original)||Directory.Exists(original)||!Directory.Exists(saved))return;
            if(!Confirm(T("Restaurar dados para ","Restore data to ")+original+"?"))return;
            Work("Restore AppX remnant",()=>{Directory.Move(saved,original);File.Delete(manifest);return original;},r=>{remnantReport.Visibility=Visibility.Visible;remnantReport.Text=r;});
        }));
        var msi=Card(T("Programas MSI instalados","Installed MSI programs"),T("Marque vários programas para desinstalar em sequência. Cada desinstalador pode pedir atenção e retornar um resultado próprio.","Select multiple programs to uninstall in sequence. Each uninstaller may need attention and returns its own result."));
        ListBox msiList=new ListBox{Height=180,SelectionMode=SelectionMode.Multiple,Background=Bg,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};msi.Children.Add(msiList);
        TextBox msiReport=Input("");msiReport.IsReadOnly=true;msiReport.Foreground=StateGreen;msiReport.TextChanged+=(sender,args)=>msiReport.Foreground=OutcomeBrush(msiReport.Text);msiReport.AcceptsReturn=true;msiReport.Height=120;msiReport.Visibility=Visibility.Collapsed;msi.Children.Add(msiReport);
        var removedMsi=new List<MsiApp>();
        Row(msi,Button(T("Checar programas MSI","Check MSI programs"),()=>{msiList.Items.Clear();foreach(var app in MsiRemoval.Inventory())msiList.Items.Add(app);msiList.Visibility=msiList.Items.Count>0?Visibility.Visible:Visibility.Collapsed;}),Button(T("Desinstalar selecionados","Uninstall selected"),()=>{
            var selected=msiList.SelectedItems.Cast<MsiApp>().ToArray();if(selected.Length==0)return;if(selected.Any(x=>x.Scope=="Machine")&&!RequireAdmin())return;
            if(!Confirm(T("Desinstalar estes programas? Alguns poderão remover dados do usuário.\n","Uninstall these programs? Some may remove user data.\n")+String.Join("\n",selected.Select(x=>x.ToString()).ToArray())))return;
            Work("MSI batch uninstall",()=>{var result=new StringBuilder();foreach(var app in selected){var action=MsiRemoval.Uninstall(app);result.AppendLine(app.Name+" • "+Describe(action));if(action.Code==0||action.Code==3010)lock(removedMsi)removedMsi.Add(app);}return result.ToString();},r=>{msiReport.Visibility=Visibility.Visible;msiReport.Text=r;msiList.Items.Clear();msiList.Visibility=Visibility.Collapsed;});
        }));
        var msiRemainders=Card(T("Rastros MSI verificáveis","Verifiable MSI remnants"),T("Oferece apenas uma pasta de instalação exclusiva, com nome correspondente e sem processos ativos. Itens ambíguos permanecem bloqueados.","Offers only an exclusive install folder with matching name and no active processes. Ambiguous items stay blocked."));
        ListBox msiTraces=new ListBox{Height=100,SelectionMode=SelectionMode.Multiple,Background=Bg,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};msiRemainders.Children.Add(msiTraces);
        TextBox traceReport=Input("");traceReport.IsReadOnly=true;traceReport.Foreground=StateGreen;traceReport.TextChanged+=(sender,args)=>traceReport.Foreground=OutcomeBrush(traceReport.Text);traceReport.AcceptsReturn=true;traceReport.Height=100;traceReport.Visibility=Visibility.Collapsed;msiRemainders.Children.Add(traceReport);
        Row(msiRemainders,Button(T("Buscar rastros MSI","Find MSI remnants"),()=>{msiTraces.Items.Clear();MsiApp[] removed;lock(removedMsi)removed=removedMsi.ToArray();var installed=MsiRemoval.Inventory();foreach(var app in removed)if(!installed.Any(x=>x.ProductCode==app.ProductCode&&x.Scope==app.Scope)&&MsiRemoval.SafeRemainder(app))msiTraces.Items.Add(app);msiTraces.Visibility=msiTraces.Items.Count>0?Visibility.Visible:Visibility.Collapsed;traceReport.Visibility=Visibility.Visible;traceReport.Text=msiTraces.Items.Count+T(" pasta(s) elegíveis."," eligible folder(s).");}),Button(T("Mover para quarentena","Move to quarantine"),()=>{
            MsiApp[] chosen=msiTraces.SelectedItems.Cast<MsiApp>().ToArray();if(chosen.Length==0||!Confirm(T("Mover estas pastas para quarentena?\n","Move these folders into quarantine?\n")+String.Join("\n",chosen.Select(x=>x.Location).ToArray())))return;
            Work("MSI remnants quarantine",()=>{var result=new StringBuilder();foreach(var app in chosen){if(MsiRemoval.Inventory().Any(x=>x.ProductCode==app.ProductCode&&x.Scope==app.Scope)||!MsiRemoval.SafeRemainder(app)){result.AppendLine(app.Name+" • blocked");continue;}
                string dest=Path.Combine(journal.Folder,"Quarantine","MSI-"+DateTime.Now.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.GetDirectoryName(dest));Directory.Move(app.Location,dest);File.WriteAllText(dest+".msi-origin",app.Location);result.AppendLine(app.Location+" → "+dest);
            }return result.ToString();},r=>{traceReport.Visibility=Visibility.Visible;traceReport.Text=r;msiTraces.Items.Clear();msiTraces.Visibility=Visibility.Collapsed;});
        }),Button(T("Restaurar último rastro MSI","Restore latest MSI remnant"),()=>{string root=Path.Combine(journal.Folder,"Quarantine");if(!Directory.Exists(root))return;string manifest=Directory.GetFiles(root,"*.msi-origin").OrderByDescending(x=>x).FirstOrDefault();if(manifest==null)return;string original=File.ReadAllText(manifest),saved=manifest.Substring(0,manifest.Length-11);if(!MsiRemoval.ValidRestorePath(original)||!String.Equals(Path.GetDirectoryName(Path.GetFullPath(saved)),Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase)||Directory.Exists(original)||!Directory.Exists(saved))return;if(!Confirm(T("Restaurar pasta em ","Restore folder at ")+original+"?"))return;Work("Restore MSI remnant",()=>{Directory.Move(saved,original);File.Delete(manifest);return original;},r=>{traceReport.Visibility=Visibility.Visible;traceReport.Text=r;}); }));
        var registryTraces=Card(T("Rastros de Registro identificáveis","Identifiable registry remnants"),T("Somente chaves diretas HKCU\\Software com nome exatamente igual ao programa MSI removido nesta sessão. Faz cópia .reg antes de excluir; chaves compartilhadas ou ambíguas não aparecem.","Only direct HKCU\\Software keys exactly matching an MSI app removed this session. Makes a .reg backup before deletion; shared or ambiguous keys do not appear."));
        ListBox regList=new ListBox{Height=95,SelectionMode=SelectionMode.Multiple,Background=Bg,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};registryTraces.Children.Add(regList);
        TextBox regReport=Input("");regReport.IsReadOnly=true;regReport.Foreground=StateGreen;regReport.TextChanged+=(sender,args)=>regReport.Foreground=OutcomeBrush(regReport.Text);regReport.AcceptsReturn=true;regReport.Height=90;regReport.Visibility=Visibility.Collapsed;registryTraces.Children.Add(regReport);
        Row(registryTraces,Button(T("Buscar chaves exatas","Find exact keys"),()=>{regList.Items.Clear();MsiApp[] removed;lock(removedMsi)removed=removedMsi.ToArray();var installed=MsiRemoval.Inventory();foreach(var app in removed)if(!installed.Any(x=>String.Equals(x.Name,app.Name,StringComparison.OrdinalIgnoreCase))&&RegistryTraces.Exists(app.Name))regList.Items.Add(app);regList.Visibility=regList.Items.Count>0?Visibility.Visible:Visibility.Collapsed;regReport.Visibility=Visibility.Visible;regReport.Text=regList.Items.Count+T(" chave(s) candidatas."," candidate key(s).");}),Button(T("Exportar e remover selecionadas","Back up and remove selected"),()=>{
            var chosen=regList.SelectedItems.Cast<MsiApp>().ToArray();if(chosen.Length==0||!Confirm(T("Exportar e remover estas chaves exatas do usuário atual? Preferências antigas poderão ser perdidas.\n","Export and remove these exact current-user keys? Old preferences may be lost.\n")+String.Join("\n",chosen.Select(x=>x.Name).ToArray())))return;
            Work("Registry remnants",()=>{var result=new StringBuilder();foreach(var app in chosen){if(MsiRemoval.Inventory().Any(x=>String.Equals(x.Name,app.Name,StringComparison.OrdinalIgnoreCase))||!RegistryTraces.Exists(app.Name)){result.AppendLine(app.Name+" • blocked");continue;}try{result.AppendLine(app.Name+" → "+RegistryTraces.Quarantine(app.Name,Path.Combine(journal.Folder,"Quarantine")));}catch(Exception e){result.AppendLine(app.Name+" • "+e.Message);}}return result.ToString();},r=>{regReport.Visibility=Visibility.Visible;regReport.Text=r;regList.Items.Clear();regList.Visibility=Visibility.Collapsed;});
        }),Button(T("Restaurar última chave","Restore latest key"),()=>{string root=Path.Combine(journal.Folder,"Quarantine");if(!Directory.Exists(root))return;string file=Directory.GetFiles(root,"RegistryTrace-*.reg").OrderByDescending(x=>x).FirstOrDefault();if(file==null||!Confirm(T("Importar novamente o backup ","Import backup ")+file+"?"))return;Work("Restore registry trace",()=>RegistryTraces.Restore(file,root),r=>{regReport.Visibility=Visibility.Visible;regReport.Text=r;});}));
    }
    void Browser(){
        Heading(T("Navegação segura","Safe browsing"),T("Conexão, DNS, bloqueios e proteções dos navegadores em um só lugar.","Connection, DNS, site blocking and browser protection in one place."));
        CardColumns extensionHost=cardHost;var sectionsNav=new WrapPanel{Margin=new Thickness(0,0,0,8)};body.Children.Insert(2,sectionsNav);
        BrowserAutomatic();
        var p=Card(T("Bloqueadores e reputação","Blockers & reputation"),T("Escolha o navegador e confirme a instalação na loja oficial. A disponibilidade depende do navegador e de sua versão.","Choose your browser and confirm installation in the official store. Availability depends on browser and version."));
        Row(p,Button("uBlock Origin • Firefox",()=>Open("https://addons.mozilla.org/firefox/addon/ublock-origin/")),Button("uBlock Origin Lite • Edge",()=>Open("https://microsoftedge.microsoft.com/addons/detail/cimighlppcgcoapaliogpjjdehbnofhn/")),Button("uBlock Origin Lite • Chrome/Brave",()=>Open("https://chromewebstore.google.com/detail/ublock-origin-lite/ddkjiahejlhfcafbddmgiahcphecmpfh")),Button("WOT • Edge",()=>Open("https://microsoftedge.microsoft.com/addons/detail/iiclaphjclecagpkkaacljnpcppnoibi/")));
        Note(p,"WOT é opcional: revise as permissões e a política de privacidade antes de instalar. A proteção contra remoção por política depende do navegador e do gerenciamento do computador.","WOT is optional: review permissions and privacy policy before installing. Policy-based removal protection depends on the browser and computer management.");
        BeginColumns(2);CardColumns notificationHost=cardHost;
        var n=Card(T("Notificações de sites","Website notifications"),T("Bloqueia solicitações por políticas do usuário no Chrome e Edge. Pode aparecer a indicação de navegador gerenciado. Reinicie o navegador. Políticas corporativas podem prevalecer.","Blocks requests using user policies in Chrome and Edge. A managed-browser indicator may appear. Restart the browser. Organization policies may take precedence."));
        Row(n,Button(T("Bloquear no Chrome","Block in Chrome"),()=>Reg("Chrome notifications: block","HKCU","Software\\Policies\\Google\\Chrome","DefaultNotificationsSetting",2)),Button(T("Bloquear no Edge","Block in Edge"),()=>Reg("Edge notifications: block","HKCU","Software\\Policies\\Microsoft\\Edge","DefaultNotificationsSetting",2)),Button(T("Bloquear no Brave","Block in Brave"),()=>Reg("Brave notifications: block","HKCU","Software\\Policies\\BraveSoftware\\Brave","DefaultNotificationsSetting",2)),Button(T("Bloquear no Firefox","Block in Firefox"),()=>Reg("Firefox notifications: block","HKCU","Software\\Policies\\Mozilla\\Firefox\\Permissions\\Notifications","BlockNewRequests",1)));
        Note(n,"As regras impedem novas solicitações nos navegadores compatíveis. Revogue permissões já concedidas nas configurações de sites. Outros navegadores podem exigir ajuste manual.","Rules prevent new requests in compatible browsers. Revoke previously granted permissions in site settings. Other browsers may require manual adjustment.");
        Row(n,Button(T("Instruções do Firefox","Firefox instructions"),()=>Open("https://support.mozilla.org/kb/push-notifications-firefox")));
        var dnsSections=DnsSections();CardColumns[] hosts={extensionHost,dnsSections.Item1,dnsSections.Item2,notificationHost};var tabButtons=new List<Button>();
        Action<int> select=selected=>{for(int index=0;index<hosts.Length;index++){hosts[index].Visibility=index==selected?Visibility.Visible:Visibility.Collapsed;if(index<tabButtons.Count)SelectTab(tabButtons[index],index==selected);}cardHost=hosts[selected];};
        string[] labels={T("Extensões","Extensions"),T("Conexão e DNS","Connection & DNS"),T("Sites bloqueados","Blocked sites"),T("Notificações","Notifications")};
        for(int index=0;index<labels.Length;index++){int target=index;Button button=TabButton(labels[index],()=>select(target));tabButtons.Add(button);sectionsNav.Children.Add(button);}select(0);
    }
    void Privacy(){
        Heading(T("Privacidade, com contexto","Privacy, with context"),T("Ajustes individuais e restauráveis. A edição do Windows pode limitar o efeito de políticas.","Individual, restorable settings. Windows edition may limit policy effects."));
        if(machine==null||machine.Build<10240){Card(T("Compatibilidade","Compatibility"),T("Os ajustes desta tela requerem Windows 10 ou 11.","Settings on this page require Windows 10 or 11."));return;}
        var p=Card(T("Diagnósticos opcionais","Optional diagnostics"),T("Solicita o nível mínimo de dados de diagnóstico permitido pela edição. Não elimina toda a comunicação com a Microsoft.","Requests the lowest diagnostic data level allowed by the edition. Does not eliminate all communication with Microsoft."));
        Row(p,Button(T("Reduzir telemetria","Reduce telemetry"),()=>Reg(T("Reduzir telemetria","Reduce telemetry"),"HKLM","Software\\Policies\\Microsoft\\Windows\\DataCollection","AllowTelemetry",0)));
        var suggestions=Card(T("Sugestões e publicidade","Suggestions & advertising"),T("Desativa sugestões de aplicativos e o identificador de publicidade do usuário quando respeitados pelo Windows.","Disables app suggestions and the user's advertising identifier where respected by Windows."));
        Row(suggestions,Button(T("Desativar sugestões","Disable suggestions"),()=>Reg("App suggestions","HKCU","Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager","SubscribedContent-338388Enabled",0)),Button(T("Desativar ID de publicidade","Disable advertising ID"),()=>Reg("Advertising ID","HKCU","Software\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo","Enabled",0)));
    }
    void Recovery(){
        Heading(T("Bibliotecas e componentes","Libraries and components"),T("Confira apenas os componentes necessários para seus jogos e programas.","Check only the components your games and apps need."));
        RuntimeLibraries();OptionalFeatureCard();
        var p=Card(".NET Framework",T("Consulte os componentes instalados. .NET Framework 3.5 é um recurso do Windows em sistemas modernos; outras versões possuem instaladores e requisitos próprios.","Inspect installed components. .NET Framework 3.5 is a Windows feature on modern systems; other versions have their own installers and requirements."));
        TextBox netReport=Output(p);netReport.Height=145;
        Row(p,Button(T("Consultar .NET Framework","Inspect .NET Framework"),()=>{Work(T("Consultar .NET Framework","Inspect .NET Framework"),()=>Describe(Runner.PS("Get-ChildItem 'HKLM:\\SOFTWARE\\Microsoft\\NET Framework Setup\\NDP' -Recurse | Get-ItemProperty | Select-Object PSChildName,Version,Release,Install | Format-Table -AutoSize",30000)),r=>netReport.Text=r);}),Button(T("Recursos do Windows","Windows Features"),()=>Open("optionalfeatures.exe")),Button(T("Downloads oficiais .NET","Official .NET downloads"),()=>Open("https://dotnet.microsoft.com/download/dotnet-framework")));
        if(machine!=null&&machine.Build>=9200)Row(p,Button(T("Habilitar .NET Framework 3.5","Enable .NET Framework 3.5"),()=>{console=netReport;Command(T("Habilitar .NET 3.5; pode baixar arquivos e exigir reinicialização","Enable .NET 3.5; may download files and require reboot"),"dism.exe","/Online /Enable-Feature /FeatureName:NetFx3 /All /NoRestart",true,1800000);}));

    }
    void Boot(string policy){if(!RequireAdmin()||!Confirm(T("Alterar política do menu de inicialização para ","Change boot menu policy to ")+policy+T("?\nUm backup BCD será exportado antes. A restauração do BCD exige recuperação manual; o backup não salva a chave BitLocker.","?\nA BCD backup will be exported first. BCD restoration requires manual recovery; the backup does not save the BitLocker key.")))return;TextBox target=console;
        Work("Boot policy",()=>{string file=Path.Combine(journal.Folder,"BCD-"+DateTime.Now.ToString("yyyyMMddHHmmss")+".bak");var backup=Runner.Run("bcdedit.exe","/export \""+file+"\"",30000);if(backup.Code!=0)return Describe(backup);return "Backup: "+file+"\n"+Describe(Runner.Run("bcdedit.exe","/set "+Args.Q("{current}")+" bootmenupolicy "+policy,30000));},r=>target.Text=r);
    }
    void Upgrade(){
        Heading(T("Atualizar Windows","Upgrade Windows"),T("Escolha a versão, revise a compatibilidade e consulte a oferta do Windows Update.","Choose a release, review compatibility and check the Windows Update offer."));BeginColumns(2);
        var p=Card(T("Selecione a versão de referência","Select a reference release"),T("Inclui versões recentes e históricas para consulta. Algumas já encerraram suporte e podem não ser oferecidas pelo Windows Update. 26H1 é destinada a novos dispositivos específicos.","Includes recent and historical releases for reference. Some are out of support and may not be offered by Windows Update. 26H1 targets specific new devices."));
        ComboBox versions=new ComboBox{Margin=new Thickness(0,0,0,12)};foreach(string v in ReleaseCatalog.Current())versions.Items.Add(v);versions.SelectedIndex=0;p.Children.Add(versions);
        TextBlock releaseStatus=Text(ReleaseCatalog.Status,12,Muted);p.Children.Add(releaseStatus);
        Action fillReleases=()=>{string selected=Convert.ToString(versions.SelectedItem);versions.Items.Clear();foreach(string v in ReleaseCatalog.Current())versions.Items.Add(v);versions.SelectedItem=versions.Items.Cast<string>().FirstOrDefault(x=>x==selected)??versions.Items[0];releaseStatus.Text=ReleaseCatalog.Status;};
        Action<string> showReleaseResult=r=>{releaseStatus.Text=r;releaseStatus.Foreground=r.StartsWith("AVISO",StringComparison.Ordinal)||r.StartsWith("WARNING",StringComparison.Ordinal)?Color(dark?"#F5C976":"#895710"):ResultTone.IsFailure(r)?OutcomeBrush(r):StateGreen;if(!ResultTone.IsFailure(r)&&!r.StartsWith("AVISO",StringComparison.Ordinal)&&!r.StartsWith("WARNING",StringComparison.Ordinal))fillReleases();};
        Row(p,Button(T("Atualizar lista de versões","Refresh release list"),()=>Work(T("Conferir versões Windows","Check Windows releases"),()=>ReleaseCatalog.RefreshOrFallback(),showReleaseResult)));
        if(!preview)Work(T("Conferir versões Windows","Check Windows releases"),()=>ReleaseCatalog.RefreshOrFallback(),showReleaseResult);
        CheckBox useBypass=Choice(p,T("Avançado: preparar contorno limitado de TPM/CPU","Advanced: prepare limited TPM/CPU bypass"),true,false);
        TextBlock releaseHint=Text("",13,Muted);p.Children.Add(releaseHint);Action releaseLabel=()=>{string target=Convert.ToString(versions.SelectedItem)??"";releaseHint.Text=target.EndsWith("26H1",StringComparison.Ordinal)?T("26H1 foi criada para dispositivos novos específicos; confira a rota de atualização antes de escolhê-la.","26H1 was made for specific new devices; check the upgrade route before selecting it."):target.StartsWith("Windows 10")?T("Requer hardware e mídia compatíveis; o suporte geral do Windows 10 terminou.","Requires compatible hardware and media; general Windows 10 support ended."):useBypass.IsChecked==true?T("Com ajustes: tentativa não oficial; a oferta e a instalação dependem do Windows e da mídia.","With adjustments: unofficial attempt; offer and installation depend on Windows and media."):T("Oficial: a compatibilidade ainda precisa ser comprovada pelo instalador.","Official: compatibility still needs confirmation by Setup.");releaseHint.Foreground=Muted;};versions.SelectionChanged+=(s,e)=>releaseLabel();useBypass.Checked+=(s,e)=>releaseLabel();useBypass.Unchecked+=(s,e)=>releaseLabel();releaseLabel();
        Row(p,Button(T("Preparar versão e verificar no Windows Update","Set target and check Windows Update"),()=>{
            if(machine==null||machine.Build<10240){if(console!=null)console.Text=T("No Windows 7/8, use a mídia oficial e confira o caminho de atualização.","On Windows 7/8, use official media and check the upgrade path.");return;}
            if(!RequireAdmin())return;
            string selected=Convert.ToString(versions.SelectedItem);string release=selected.Substring(selected.LastIndexOf(' ')+1);string product=selected.StartsWith("Windows 10")?"Windows 10":"Windows 11";if(product=="Windows 10"&&machine.Build>=22000){if(console!=null)console.Text=T("O Windows Update não faz downgrade do Windows 11 para o Windows 10.","Windows Update cannot downgrade Windows 11 to Windows 10.");return;}
            bool bypassSelected=product=="Windows 11"&&useBypass.IsChecked==true;
            if(!Confirm(T("Definir ","Target ")+product+" "+release+T(" como versão desejada? O Windows Update pode não oferecê-la. As alterações serão registradas para restauração."," as the desired release? Windows Update may not offer it. Changes will be backed up for restoration.")+(bypassSelected?T("\nO contorno de TPM/CPU será aplicado.","\nThe TPM/CPU bypass will be applied."):"")))return;
            TextBox target=console;
            Work(T("Preparar atualização","Prepare upgrade"),()=>{string applied=UpgradePolicy.Apply(journal,product,release,bypassSelected);return applied+"\n\n"+UpgradePolicy.Offered(product,release);},r=>{if(target!=null)target.Text=r;if(!r.StartsWith("ERROR",StringComparison.Ordinal))Open("ms-settings:windowsupdate");});
        }));        Row(p,Button(T("Ver diagnóstico de atualização","View upgrade diagnostics"),()=>{TextBox target=console;string selected=Convert.ToString(versions.SelectedItem);Work("Upgrade diagnostics",()=>{
            StringBuilder b=new StringBuilder(selected+"\n");b.AppendLine(machine==null?"Unknown OS":machine.Caption+" | "+machine.Arch);b.AppendLine("RAM: "+(machine==null?"?":(machine.Total/1048576.0).ToString("0.0"))+" GB");b.AppendLine("CPU: "+(machine==null?"?":machine.Cpu));
            foreach(DriveInfo d in DriveInfo.GetDrives())if(d.IsReady&&d.Name==Path.GetPathRoot(Environment.SystemDirectory))b.AppendLine("System free: "+(d.AvailableFreeSpace/1073741824.0).ToString("0.0")+" GB");
            try{foreach(var o in Machine.Query("root\\CIMV2\\Security\\MicrosoftTpm","SELECT SpecVersion,IsEnabled_InitialValue FROM Win32_Tpm"))using(o)b.AppendLine("TPM: "+Machine.Value(o,"SpecVersion")+" enabled="+Machine.Value(o,"IsEnabled_InitialValue"));}catch(Exception e){b.AppendLine("TPM: "+e.Message);}
            b.AppendLine(T("Este diagnóstico é parcial. Compatibilidade da CPU, instruções, UEFI, Secure Boot, drivers e preservação de aplicativos devem ser validados pelo instalador oficial. A versão selecionada apenas identifica o relatório; não baixa nem direciona o instalador.","This diagnosis is partial. CPU support, instructions, UEFI, Secure Boot, drivers and app preservation must be validated by the official installer. The selected release labels this report only; it does not download or target an installer."));
            return b.ToString();},r=>target.Text=r);
        }),Button(T("Baixar mídia oficial","Download official media"),()=>Open("https://www.microsoft.com/software-download/windows11")),Button(T("Conferir versões e suporte","Check releases & support"),()=>Open("https://learn.microsoft.com/windows/release-health/windows11-release-information")));
        Note(p,"Versões antigas podem estar fora do suporte e não ser oferecidas pelo Windows Update. Windows 7/8/8.1: não há atualização direta garantida para Windows 11 preservando aplicativos. A seleção acima não altera a versão entregue pelo site da Microsoft.","Windows 7/8/8.1: no guaranteed direct upgrade to Windows 11 preserving apps. The selection above does not change the version delivered by Microsoft's website.");
        var advanced=Card(T("Avançado • contorno de TPM/CPU","Advanced • TPM/CPU bypass"),T("Grava AllowUpgradesWithUnsupportedTPMOrCPU em MoSetup. É uma configuração de compatibilidade limitada: pode ser ignorada pelo instalador atual, não remove requisitos de instruções da CPU e não garante atualizações, suporte ou instalação. Não inicia a atualização.","Writes AllowUpgradesWithUnsupportedTPMOrCPU in MoSetup. This is a limited compatibility setting: current installers may ignore it, it does not remove CPU instruction requirements and it does not guarantee updates, support or installation. It does not start an upgrade."));
        Button bypass=Button(T("Preparar contorno limitado","Prepare limited bypass"),()=>Reg(T("Preparar contorno limitado de CPU/TPM","Prepare limited CPU/TPM bypass"),"HKLM","SYSTEM\\Setup\\MoSetup","AllowUpgradesWithUnsupportedTPMOrCPU",1));bypass.IsEnabled=machine!=null&&machine.Build>=10240&&Environment.Is64BitOperatingSystem;Row(advanced,bypass);
        Note(advanced,"Faça backup dos arquivos antes de qualquer atualização. Um ponto de restauração não substitui uma cópia dos dados. Esta versão não modifica ISOs, não substitui binários e não executa instalações sem supervisão.","Back up files before any upgrade. A restore point does not replace a data backup. This version does not modify ISOs, replace binaries or run unattended installations.");Output(p);
        var readiness=Card(T("Compatibilidade deste computador","Compatibility of this computer"),T("A checagem inicial mostra os requisitos conhecidos. A mídia oficial pode fazer uma verificação mais completa sem iniciar a instalação.","The initial check shows known requirements. Official media can perform a fuller scan without starting installation."));
        TextBox readinessResult=Input("");readinessResult.IsReadOnly=true;readinessResult.AcceptsReturn=true;readinessResult.TextWrapping=TextWrapping.Wrap;readinessResult.Height=180;readinessResult.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        Row(readiness,Button(T("Checar requisitos","Check requirements"),()=>{string target=Convert.ToString(versions.SelectedItem);Work(T("Checar compatibilidade","Check compatibility"),()=>WindowsReadiness.Explain(machine,target),r=>readinessResult.Text=r);}),
            Button(T("Verificar com mídia oficial","Scan with official media"),()=>{string setup=PickFile("Windows setup|setup.exe|Executable|*.exe");if(setup==null)return;if(!Confirm(T("Verificar a compatibilidade desta mídia sem instalar o Windows? Salve seu trabalho; a análise pode demorar.","Scan this media for compatibility without installing Windows? Save your work; the scan may take time.")))return;
                Work(T("Verificar mídia Windows","Scan Windows media"),()=>WindowsReadiness.ScanMedia(setup,journal.Folder,machine==null?0:machine.Build),r=>readinessResult.Text=r);
            }));AttachResult(readiness,readinessResult);
        if(!preview){string initialTarget=Convert.ToString(versions.SelectedItem);Work(T("Pré-checar Windows","Precheck Windows"),()=>WindowsReadiness.Explain(machine,initialTarget),r=>readinessResult.Text=r);}
        LegacyUpgradeCard();
    }
    void Settings(){
        Heading(T("Do seu jeito","Make it yours"),T("Tema do aplicativo independente do tema do Windows.","App theme is independent from Windows theme."));
        var p=Card(T("Aparência do OuO","OuO appearance"),T("Na abertura, segue a preferência do Windows quando disponível.","On startup, follows the Windows preference when available."));
        Row(p,ThemeButton(false,()=>SetAppTheme(false)),ThemeButton(true,()=>SetAppTheme(true)));CheckBox compactMode=Choice(p,T("Tema compacto: cartões e controles menores","Compact theme: smaller cards and controls"),true,compact);compactMode.Checked+=(s,e)=>SetCompact(true);compactMode.Unchecked+=(s,e)=>SetCompact(false);
        var accents=Card(T("Cor dos ícones e destaques","Icon and accent color"),T("A preferência fica salva somente para o seu usuário.","The preference is saved only for your user account."));
        Row(accents,Button(T("Menta","Mint"),()=>SetAccent("menta")),Button(T("Azul","Blue"),()=>SetAccent("azul")),Button(T("Violeta","Violet"),()=>SetAccent("violeta")),Button(T("Âmbar","Amber"),()=>SetAccent("ambar")),Button(T("Cinza","Gray"),()=>SetAccent("cinza")));
        var language=Card(T("Idioma","Language"),T("Português em sistemas em português; inglês como alternativa. A troca manual vale para esta sessão.","Portuguese on Portuguese systems; English as fallback. Manual choice applies to this session."));Row(language,Button("Português",()=>SetLanguage(true)),Button("English",()=>SetLanguage(false)));
        var win=Card(T("Tema do sistema","System theme"),T("Disponível para Windows 10 e 11. A alteração é salva no histórico e pode exigir reabrir os aplicativos.","Available on Windows 10 and 11. Changes are backed up in history and may require reopening apps."));
        Button light=Button(T("Aplicativos claros","Light apps"),()=>Reg("Windows app theme: light","HKCU","Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize","AppsUseLightTheme",1));Button dk=Button(T("Aplicativos escuros","Dark apps"),()=>Reg("Windows app theme: dark","HKCU","Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize","AppsUseLightTheme",0));light.IsEnabled=dk.IsEnabled=machine!=null&&machine.Build>=14393;
        Button sysLight=Button(T("Sistema claro","Light system"),()=>Reg("Windows system theme: light","HKCU","Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize","SystemUsesLightTheme",1));Button sysDark=Button(T("Sistema escuro","Dark system"),()=>Reg("Windows system theme: dark","HKCU","Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize","SystemUsesLightTheme",0));sysLight.IsEnabled=sysDark.IsEnabled=machine!=null&&machine.Build>=18362;Row(win,light,dk,sysLight,sysDark);
    }
    void History(){
        Heading(T("Histórico e restauração","History & restore"),T("O histórico contém detalhes do computador e das operações. Compartilhe apenas quando necessário.","History contains computer and operation details. Share only when needed."));
        var export=Card(T("Diagnóstico exportável","Export diagnostics"),T("Gera um resumo local sem nome de usuário, número de série nem caminhos de arquivos. Revise o texto antes de compartilhar.","Creates a local summary without username, serial numbers or file paths. Review it before sharing."));
        Row(export,Button(T("Gerar relatório","Generate report"),()=>ExportDiagnostic()));OperationHistoryCard();ChangeHistoryCard();
        var p=Card(T("Alterações de Registro","Registry changes"),T("Restaura um valor por vez, do mais recente ao mais antigo. Operações de programas, TRIM, BCD e movimentações de inicialização possuem procedimentos próprios.","Restores one value at a time, newest first. App operations, TRIM, BCD and startup moves have separate procedures."));
        Row(p,Button(T("Restaurar último valor","Restore latest value"),()=>{if(!Confirm(T("Restaurar o valor de Registro mais recente? Alterações de máquina exigem administrador.","Restore the latest registry value? Machine changes require administrator.")))return;Work("Registry undo",()=>journal.Undo(),r=>{ShowNotice(r);ShowPage("history");});}),Button(T("Abrir pasta do histórico","Open history folder"),()=>Open(journal.Folder)),Button(T("Atualizar histórico","Refresh history"),()=>ShowPage("history",true)));
        Output(p);console.Height=300;console.Text=journal.History();
    }
}
public static class Tests {
    public static int Run(){string folder=Path.Combine(Path.GetTempPath(),"OuO-Test-"+Guid.NewGuid().ToString("N"));string key="Software\\AuroraUtilityTests\\"+Guid.NewGuid().ToString("N");int count=0;try{
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);count++;};
        check(Runner.ValidId("Mozilla.Firefox"),"valid ID");check(!Runner.ValidId("x & calc"),"reject injection");check(!Runner.ValidId("--id"),"reject option");check(Runner.QuotePS("a'b")=="'a''b'","PowerShell quoting");
        MemoryRegistryStore store=new MemoryRegistryStore();Journal j=new Journal(folder,store);j.RegistryChange("test","HKCU",key,"Value",2,RegistryValueKind.DWord);
        check((int)store.Read("HKCU",key,"Value").Value==2,"write verified");j.Undo();
        check(!store.Read("HKCU",key,"Value").Exists,"restore absent value");
        store.Set("HKCU",key,"Value",7,RegistryValueKind.DWord);
        j.RegistryChange("test2","HKCU",key,"Value",0,RegistryValueKind.DWord);j.Undo();check((int)store.Read("HKCU",key,"Value").Value==7,"restore existing value");
        store.Set("HKCU",key,"Expand","%TEMP%\\test",RegistryValueKind.ExpandString);
        j.RegistryChange("test3","HKCU",key,"Expand","new",RegistryValueKind.String);j.Undo();check((string)store.Read("HKCU",key,"Expand").Value=="%TEMP%\\test"&&store.Read("HKCU",key,"Expand").Kind==RegistryValueKind.ExpandString,"restore raw expandable value");
        CommandResult result=Runner.Run("cmd.exe","/d /c exit 9",5000);check(result.Code==9,"exit code propagation");
        check(!String.IsNullOrEmpty(Machine.Detect().Version),"OS detection fallback");L.Pt=true;check(L.T("pt","en")=="pt","Portuguese");L.Pt=false;check(L.T("pt","en")=="en","English");
        j.RegistryChange("Tweak:A","HKCU",key,"A",1,RegistryValueKind.DWord);j.RegistryChange("Tweak:B","HKCU",key,"B",2,RegistryValueKind.DWord);j.Undo("Tweak:A");check(!store.Read("HKCU",key,"A").Exists&&(int)store.Read("HKCU",key,"B").Value==2,"targeted tweak restoration preserves unrelated tweak");
        check(TweakCatalog.All().Select(x=>x.Id).Distinct().Count()==TweakCatalog.All().Count,"unique tweak IDs");check(!TweakCatalog.All().First(x=>x.Id=="background-apps").Supported(26300),"Windows 10-only tweak disabled on Windows 11");
        check(MainWindow.ParseMemory("AURORA-MEMORY:True")&&!MainWindow.ParseMemory("AURORA-MEMORY:False"),"memory compression state parsing");
        bool rejected=false;try{MainWindow.ParseMemory("access denied");}catch(InvalidDataException){rejected=true;}check(rejected,"reject unavailable memory state");
        foreach(bool setting in new[]{true,false}){string script=MainWindow.MemoryScript(setting);var parsed=Runner.PS("$errors=$null; [System.Management.Automation.PSParser]::Tokenize("+Runner.QuotePS(script)+",[ref]$errors) | Out-Null; if ($errors.Count -gt 0) { throw ($errors | Out-String) }; Write-Output 'PARSED'",15000);check(parsed.Code==0&&parsed.Output.Contains("PARSED"),"memory PowerShell syntax "+setting);}
        SensorSnapshot sensor=SensorSnapshot.Parse("<snapshot status='ready'><sensor hardware='Cpu' type='Temperature' name='CPU Package' value='54.5'/><sensor hardware='GpuNvidia' type='Temperature' name='GPU Core' value='61'/><sensor hardware='SuperIO' type='Temperature' name='VRM MOS' value='70'/><sensor hardware='SuperIO' type='Temperature' name='System' value='95'/><sensor hardware='GpuNvidia' type='Load' name='GPU Core' value='32'/></snapshot>");
        check(sensor.Cpu.Contains("54")&&sensor.Gpu.Contains("61")&&sensor.Vrm.Contains("70")&&sensor.GpuLoad=="32%","sensor CPU/GPU/VRM routing");
        sensor=SensorSnapshot.Parse("<snapshot><sensor hardware='SuperIO' type='Temperature' name='System' value='60'/><sensor hardware='Cpu' type='Temperature' name='Package' value='NaN'/><sensor hardware='Cpu' type='Temperature' name='Core' value='0'/></snapshot>");check(sensor.Vrm=="—"&&sensor.Cpu=="—","unknown motherboard sensor is not mislabeled as VRM; zero readings unavailable");
        check(AppIcons.ExecutablePath(@"\\server\share\app.exe /run")=="","skip network executable icon lookup");check(AppIcons.ExecutablePath("https://example.com/a.exe")=="","skip URL icon lookup");
        string localCommand="\""+Path.Combine(Environment.SystemDirectory,"cmd.exe")+"\" /d";check(AppIcons.ExecutablePath(localCommand)==Path.Combine(Environment.SystemDirectory,"cmd.exe"),"quoted startup command icon resolution");check(!Object.ReferenceEquals(AppIcons.FromCommand(localCommand),AppIcons.Generic()),"native executable icon extraction");
        foreach(string id in new[]{"Mozilla.Firefox","Google.Chrome","Brave.Brave","TheDocumentFoundation.LibreOffice","SumatraPDF.SumatraPDF","7zip.7zip","Microsoft.PowerToys","Notepad++.Notepad++","VideoLAN.VLC","Spotify.Spotify","Discord.Discord","Microsoft.VisualStudioCode","Git.Git"})check(!Object.ReferenceEquals(AppIcons.ForPackage(id),AppIcons.Generic()),"bundled icon "+id);
        ListBox testList=new ListBox{SelectionMode=SelectionMode.Multiple};testList.Items.Add(AppIcons.Package("Mozilla.Firefox"));testList.Items.Add(AppIcons.Package("7zip.7zip"));testList.SelectAll();check(AppIcons.Selected(testList).SequenceEqual(new[]{"Mozilla.Firefox","7zip.7zip"}),"icon rows retain package selection IDs");
        FeatureChecks.Run(check,folder);
        CompletionChecks.Run(check,folder);
        Revision5Checks.Run(check,folder);
        StableChecks.Run(check,folder);
        Revision7Checks.Run(check,folder);
        ReleaseChecks.Run(check,folder);
        StabilizationChecks.Run(check,folder);
        CloudStorageChecks.Run(check,folder);NextReleaseChecks.Run(check,folder);
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),"PASS: "+count+" checks\n"+DateTime.Now.ToString("s"));return 0;
    }catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),"FAIL after "+count+": "+e);return 1;}}
}
}
