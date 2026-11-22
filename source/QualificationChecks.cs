using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
namespace Aurora {
public static class ReleaseChecks {
    public static void Run(Action<bool,string> check,string folder){
        string xmlFolder=Path.Combine(folder,new string('d',Math.Max(1,200-folder.Length-1))),xmlPath=Path.Combine(xmlFolder,"history-"+Guid.NewGuid().ToString("N")+".xml");Directory.CreateDirectory(xmlFolder);
        AtomicFiles.SaveXml(new XElement("fixture",1),xmlPath);AtomicFiles.SaveXml(new XElement("fixture",2),xmlPath);check(xmlPath.Length>=240&&XElement.Load(xmlPath).Value=="2"&&Directory.GetFiles(xmlFolder).Length==1,"atomic XML backup and replacement work near the classic path limit without extending the destination filename");
        bool rejected=false;try{AtomicFiles.SaveXml(new XElement("fixture","\u0001"),xmlPath);}catch(ArgumentException){rejected=true;}catch(System.Xml.XmlException){rejected=true;}check(rejected&&XElement.Load(xmlPath).Value=="2"&&Directory.GetFiles(xmlFolder).Length==1,"failed XML serialization preserves the original backup and removes only its own partial temporary file");
        check(ReleaseQualification.Features.Select(x=>x.Id).Distinct().Count()==ReleaseQualification.Features.Length&&ReleaseQualification.Features.All(x=>!String.IsNullOrWhiteSpace(x.ReasonPt)&&!String.IsNullOrWhiteSpace(x.ReasonEn)),"experimental groups have unique IDs and explicit bilingual reasons");
        var office=OfficeConfiguration.Build(OfficeEdition.All()[0],"64","en-us","",new[]{"Word"},null,true);OfficeAutomatic.ConfigureUnattended(office);check((string)office.Root.Element("Display").Attribute("Level")=="None"&&office.Root.Elements("Property").Any(x=>(string)x.Attribute("Name")=="FORCEAPPSHUTDOWN"&&(string)x.Attribute("Value")=="FALSE"),"automatic Office install is unattended and cannot force-close user apps");
        check(ResultTone.Classify("OK\nThe component store is repairable.\nThe operation completed successfully.")<0&&ResultTone.Classify("OK\nNo component store corruption detected.")>0,"a completed health scan with detected corruption cannot be classified as a positive result");
        check(ResultTone.Classify("OK\nVerificação 100% concluída.\nA Proteção de Recursos do Windows encontrou violações de integridade.")<0&&ResultTone.Completion("OK\nWindows Resource Protection found integrity violations.")<0&&ResultTone.Classify("OK\nA Proteção de Recursos do Windows encontrou arquivos corrompidos, mas não pôde corrigir alguns deles.")<0&&ResultTone.Classify("OK\nWindows Resource Protection found corrupt files and successfully repaired them.")>0,"SFC integrity violations remain red in Portuguese and English even when verification completes successfully");
        string fragment="OuO-label-"+Guid.NewGuid().ToString("N"),composite=fragment+" • fixture";check(UiLanguage.English(composite)==composite,"unknown UI fragment is preserved");UiLanguage.Remember(fragment,"translated-owned-label");check(UiLanguage.English(composite)=="translated-owned-label • fixture","new UI translations invalidate cached composite labels");
        var sfc=Runner.Run("sfc.exe","/?",10000);check(sfc.Output.Length>20&&sfc.Output.IndexOf('\0')<0&&sfc.Output.IndexOf('\uFFFD')<0,"native SFC output is decoded as UTF-16 without interleaved nulls or replacement characters");
        var watch=Stopwatch.StartNew();var background=Runner.Run(System.Reflection.Assembly.GetExecutingAssembly().Location,"--test-background-parent",5000,allowBackground:true);int child;check(background.Code==0&&watch.Elapsed.TotalSeconds<3&&Int32.TryParse(background.Output.Trim().Replace("BACKGROUND|",""),out child),"completed installer-style command can leave a background worker when its output streams are closed");
        if(!ExtendedChecks.Allowed()){var denied=Runner.Run(System.Reflection.Assembly.GetExecutingAssembly().Location,"--extended-test",10000);check(denied.Code==80,"administrative extended lab refuses to run on the user's Windows");}
    }
}
public partial class MainWindow {
    [MethodImpl(MethodImplOptions.NoInlining)]WeakReference DiscardedResultLabel(){return new WeakReference(Text("OK",13,StateGreen));}
    void CollectUnusedUI(){Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
    void VerifyQualificationUI(){
        var weak=DiscardedResultLabel();CollectUnusedUI();if(weak.IsAlive)throw new InvalidOperationException("Discarded semantic label is rooted by a global listener.");
        var result=Text("OK",13,StateGreen);result.Text="ERROR • fixture";if(((SolidColorBrush)result.Foreground).Color!=((SolidColorBrush)OutcomeBrush("ERROR")).Color)throw new InvalidOperationException("Semantic label did not change to error red.");result.Text="OK • Aplicado";if(!Object.ReferenceEquals(result.Foreground,StateGreen))throw new InvalidOperationException("Semantic label did not return to success green.");
        ShowToast("OK • Configuração desativada",true);var toast=(TextBlock)((StackPanel)toastHost.Child).Children[0];if(!Object.ReferenceEquals(toast.Foreground,StateGreen))throw new InvalidOperationException("Successful disabling action is incorrectly red.");if(ResultTone.Classify("OK\nThe component store is repairable.\nThe operation completed successfully.")>=0||ResultTone.Classify("OK\nNo component store corruption detected.")<=0)throw new InvalidOperationException("DISM health state is incorrectly classified.");
        L.Pt=true;dark=true;BuildShell();var found=new System.Collections.Generic.HashSet<string>();
        foreach(string page in new[]{"dashboard","performance","processes","tweaks","repair","repair-advanced","protection","cleanup","storage","cloud-storage","apps","apps-updates","bloat","office","browser","network","drivers","recovery","upgrade","about"}){ShowPage(page);foreach(var badge in AppearanceTree(body).OfType<Border>().Where(x=>Convert.ToString(x.Tag).StartsWith("OuO.TestPhase:")))found.Add(Convert.ToString(badge.Tag).Substring("OuO.TestPhase:".Length));}
        string[] absent=ReleaseQualification.Features.Select(x=>x.Id).Where(x=>!found.Contains(x)).ToArray();if(absent.Length>0)throw new InvalidOperationException("Missing visible qualification notices: "+String.Join(",",absent));
        ShowPage("storage");Capture("diagnostics/storage-qualification-pt.png");dark=false;BuildShell();ShowPage("office");Capture("diagnostics/office-qualification-light.png");
        var intro=LifecycleDialog(false);intro.Width=440;intro.MaxHeight=300;intro.ContentRendered+=(s,e)=>{CaptureDialog(intro,"startup-compact-qualification.png");var proceed=AppearanceTree((DependencyObject)intro.Content).OfType<Button>().Single(x=>x.IsDefault);if(proceed.TranslatePoint(new Point(0,proceed.ActualHeight),intro).Y>intro.ActualHeight)throw new InvalidOperationException("Compact startup dialog clipped its Continue button.");proceed.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));};if(intro.ShowDialog()!=true)throw new InvalidOperationException("Compact startup dialog did not preserve consent.");
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"qualification-results.txt"),"PASS • Semantic labels are collectable; state colors update without global property-descriptor listeners.\nPASS • "+found.Count+" experimental feature groups show notices beside their controls, with theme-aware colors.\n"+String.Join("\n",ReleaseQualification.Features.Select(x=>x.Id+" • "+x.ReasonPt)),new UTF8Encoding(true));
    }
    void VerifyNavigationSoak(){
        // Use the normal event loop. Nested synchronous Dispatcher.Invoke waits
        // can starve below rendering and do not model real navigation.
        var pages=new[]{"dashboard","recommendations","performance","processes","stability","tweaks","repair","repair-advanced","protection","dns","network","storage","cloud-storage","cleanup","startup","apps","apps-updates","office","bloat","browser","recovery","drivers","upgrade","settings","history","about"};
        string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"soak-results.txt");int index=0,warm=0,transitions=0,cycles=0,handlesBefore=0;long before=0;var watch=new Stopwatch();var pulse=new DispatcherTimer(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(35)};
        Action collect=()=>{GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();};
        pulse.Tick+=(s,e)=>{try{
            if(index==0&&warm<4)ChangeAppearance(()=>{L.Pt=warm<2;dark=warm%2==0;});
            ShowPage(pages[index++]);UpdateLayout();
            if(index<pages.Length)return;index=0;
            if(warm<4){warm++;if(warm==4){collect();before=GC.GetTotalMemory(true);using(var p=Process.GetCurrentProcess())handlesBefore=p.HandleCount;watch.Start();}return;}
            transitions+=pages.Length;cycles++;if(cycles%4==0)ChangeAppearance(()=>{dark=!dark;L.Pt=!L.Pt;});
            File.WriteAllText(report,"RUNNING • "+watch.Elapsed.TotalSeconds.ToString("0")+" s • "+transitions+" page transitions",Encoding.UTF8);
            if(watch.Elapsed.TotalSeconds<600)return;pulse.Stop();collect();long after=GC.GetTotalMemory(true);int handlesAfter;using(var p=Process.GetCurrentProcess())handlesAfter=p.HandleCount;
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-lifetime.txt"),UiLifetimeProbe.Report(),Encoding.UTF8);if(after-before>24L*1048576||handlesAfter-handlesBefore>80)throw new InvalidOperationException("Navigation soak exceeded retained-memory/handle budget: "+(after-before)+" bytes; "+(handlesAfter-handlesBefore)+" handles.");
            File.WriteAllText(report,"PASS • "+watch.Elapsed.TotalSeconds.ToString("0.0")+" s; "+transitions+" transitions through "+pages.Length+" views; "+cycles+" cycles; dark/light and Portuguese/English changes.\nManaged heap after collection: "+before+" -> "+after+" bytes; retained growth "+(after-before)+" bytes.\nProcess handles: "+handlesBefore+" -> "+handlesAfter+".\nTen-minute navigation stress test through the normal event loop; not long-term qualification with personal applications.",new UTF8Encoding(true));Close();
        }catch(Exception ex){pulse.Stop();File.WriteAllText(report,"FAIL • "+ex,new UTF8Encoding(true));Close();}};pulse.Start();
    }
}
}
