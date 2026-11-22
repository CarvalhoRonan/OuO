using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
namespace Aurora {
public sealed class AutoMemoryPolicy {
    public bool Enabled;public int Threshold=85,IntervalMinutes=15;public DateTime LastAttempt=DateTime.MinValue;DateTime pressure=DateTime.MinValue;
    public bool ShouldRun(DateTime now,uint load,bool busy){
        if(!Enabled||busy||load<Threshold){pressure=DateTime.MinValue;return false;}
        if(pressure==DateTime.MinValue||now<pressure){pressure=now;return false;}
        if((now-pressure).TotalSeconds<30||(now-LastAttempt).TotalMinutes<IntervalMinutes)return false;
        LastAttempt=now;pressure=DateTime.MinValue;return true;
    }
    public void ResetPressure(){pressure=DateTime.MinValue;}
}
public static partial class MemoryReclaim {
    [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("advapi32.dll",SetLastError=true)]static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool IsProcessCritical(IntPtr process,out bool critical);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetProcessTimes(IntPtr process,out long creation,out long exit,out long kernel,out long user);
    public static bool Eligible(bool sameUser,bool sameSession,bool minimized,bool foreground,bool critical,bool systemPath,long workingSet,double cpuMilliseconds){return sameUser&&sameSession&&minimized&&!foreground&&!critical&&!systemPath&&workingSet>=33554432&&cpuMilliseconds>=0&&cpuMilliseconds<=35;}
    internal static bool ValidateOwnedWorker(){
        var info=new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--memory-test-child"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true};
        using(var process=Process.Start(info))using(var job=new OwnedCommandJob(process))try{if(process.StandardOutput.ReadLine()!="READY")return false;process.Refresh();long before=process.WorkingSet64;bool trimmed=EmptyWorkingSet(process.Handle);Thread.Sleep(100);process.Refresh();return trimmed&&process.WorkingSet64<before;}finally{job.Terminate();if(!process.HasExited)process.Kill();process.WaitForExit(3000);}
    }
    public static string TrimIdleApps(){
        using(var mutex=new Mutex(false,"Local\\OuO-AutoMemory-"+WindowsIdentity.GetCurrent().User.Value)){
            bool entered=false;try{try{entered=mutex.WaitOne(0);}catch(AbandonedMutexException){entered=true;}if(!entered)return L.T("AVISO • Outra liberação automática está em andamento.","WARNING • Another automatic reclaim is running.");
                var before=Native.ReadMemory();int trimmed=0;var candidates=new List<Tuple<Process,TimeSpan,DateTime>>();int own=Process.GetCurrentProcess().Id,session=Process.GetCurrentProcess().SessionId;uint foreground;GetWindowThreadProcessId(GetForegroundWindow(),out foreground);
                try{
                    foreach(var process in Process.GetProcesses()){bool keep=false;try{if(process.Id==own||process.Id==foreground||process.SessionId!=session||process.MainWindowHandle==IntPtr.Zero||!IsIconic(process.MainWindowHandle)||process.WorkingSet64<33554432)continue;string path=process.MainModule.FileName;if(path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))continue;candidates.Add(Tuple.Create(process,process.TotalProcessorTime,process.StartTime.ToUniversalTime()));keep=true;}catch{}finally{if(!keep)process.Dispose();}}
                    if(candidates.Count>0)Thread.Sleep(700);
                    foreach(var candidate in candidates.Take(12)){var process=candidate.Item1;IntPtr handle=IntPtr.Zero,token=IntPtr.Zero;try{
                        GetWindowThreadProcessId(GetForegroundWindow(),out foreground);if(process.HasExited||!IsIconic(process.MainWindowHandle)||process.Id==foreground||(process.TotalProcessorTime-candidate.Item2).TotalMilliseconds>35)continue;
                        handle=OpenProcess(0x0500,false,process.Id);if(handle==IntPtr.Zero||!OpenProcessToken(handle,8,out token))continue;bool owner;using(var identity=new WindowsIdentity(token))owner=identity.User.Equals(WindowsIdentity.GetCurrent().User);
                        bool critical=false;try{if(!IsProcessCritical(handle,out critical))continue;}catch(EntryPointNotFoundException){}
                        long created,exited,kernel,user;if(!GetProcessTimes(handle,out created,out exited,out kernel,out user)||DateTime.FromFileTimeUtc(created)!=candidate.Item3)continue;
                        if(Eligible(owner,process.SessionId==session,IsIconic(process.MainWindowHandle),process.Id==foreground,critical,false,process.WorkingSet64,(process.TotalProcessorTime-candidate.Item2).TotalMilliseconds)&&EmptyWorkingSet(handle))trimmed++;
                    }catch{}finally{if(token!=IntPtr.Zero)CloseHandle(token);if(handle!=IntPtr.Zero)CloseHandle(handle);}}
                }finally{foreach(var item in candidates)item.Item1.Dispose();}
                var after=Native.ReadMemory();return "OK • "+trimmed+L.T(" aplicativo(s) minimizado(s) e ocioso(s) tratado(s)."," minimized idle app(s) processed.")+(before==null||after==null?"":"\n"+L.T("RAM disponível observada: ","Observed available RAM: ")+(before.Free/1048576.0).ToString("0")+" → "+(after.Free/1048576.0).ToString("0")+" MB")+"\n"+L.T("O Windows pode recarregar páginas ao usar esses aplicativos novamente.","Windows may reload pages when those apps are used again.");
            }finally{if(entered)mutex.ReleaseMutex();}
        }
    }
}
public partial class MainWindow {
    AutoMemoryPolicy autoMemory=new AutoMemoryPolicy();DispatcherTimer autoMemoryTimer;TextBlock autoMemoryStatus;string autoMemoryLast="";
    void InitAutoMemory(){
        try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\OuO\Memory"))if(key!=null){autoMemory.Enabled=Convert.ToString(key.GetValue("Enabled","0"))=="1";int value;if(Int32.TryParse(Convert.ToString(key.GetValue("Threshold",85)),out value)&&new[]{80,85,90,95}.Contains(value))autoMemory.Threshold=value;if(Int32.TryParse(Convert.ToString(key.GetValue("Interval",15)),out value)&&new[]{5,10,15,30,60}.Contains(value))autoMemory.IntervalMinutes=value;DateTime last;if(DateTime.TryParse(Convert.ToString(key.GetValue("LastAttempt","")),null,System.Globalization.DateTimeStyles.RoundtripKind,out last))autoMemory.LastAttempt=last.ToUniversalTime();autoMemoryLast=Convert.ToString(key.GetValue("LastResult",""));}}catch{}
        if(preview)return;autoMemoryTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(10)};autoMemoryTimer.Tick+=(s,e)=>{
            var memory=Native.ReadMemory();if(memory==null||!startupAccepted||exitDialogOpen||exitFinalized)return;
            if(autoMemory.ShouldRun(DateTime.UtcNow,memory.Load,activeJobs.Count>0))try{SaveAutoMemory();Work(T("Liberação automática de memória","Automatic memory reclaim"),()=>MemoryReclaim.TrimIdleApps(),r=>{autoMemoryLast=DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")+"\n"+r;SaveAutoMemory();UpdateAutoMemoryStatus();});}catch(Exception error){autoMemory.Enabled=false;autoMemoryLast="ERROR • "+error.Message;UpdateAutoMemoryStatus();}
        };autoMemoryTimer.Start();UpdateAutoMemoryStatus();
    }
    void StopAutoMemory(){if(autoMemoryTimer!=null)autoMemoryTimer.Stop();}
    void SaveAutoMemory(){if(preview)return;using(var key=Registry.CurrentUser.CreateSubKey(@"Software\OuO\Memory")){key.SetValue("Enabled",autoMemory.Enabled?"1":"0");key.SetValue("Threshold",autoMemory.Threshold);key.SetValue("Interval",autoMemory.IntervalMinutes);key.SetValue("LastAttempt",autoMemory.LastAttempt.ToString("o"));key.SetValue("LastResult",autoMemoryLast);}}
    void UpdateAutoMemoryStatus(){if(autoMemoryStatus==null)return;autoMemoryStatus.Text=(autoMemory.Enabled?T("Ativada","Enabled"):T("Desativada","Disabled"))+(String.IsNullOrEmpty(autoMemoryLast)?T(" • Ainda não executada."," • Not run yet."):"\n"+autoMemoryLast);autoMemoryStatus.Foreground=OutcomeBrush(autoMemoryStatus.Text);}
    void AutoMemoryCard(){
        var card=Card(T("Liberação automática de memória","Automatic memory reclaim"),T("Reduz a memória residente de aplicativos minimizados e ociosos quando o uso da RAM permanece alto por 30 segundos. O Windows pode recarregar essas páginas depois; não é garantia de ganho de desempenho.","Trims resident memory of minimized idle apps when RAM use stays high for 30 seconds. Windows may reload those pages later; this does not guarantee better performance."));
        var enabled=Choice(card,T("Ativar liberação automática","Enable automatic reclaim"),true,autoMemory.Enabled);var threshold=new ComboBox{Margin=new Thickness(0,0,0,8)};foreach(int value in new[]{80,85,90,95})threshold.Items.Add(value);threshold.SelectedItem=autoMemory.Threshold;card.Children.Add(Text(T("Acionar quando o uso da RAM alcançar (%)","Run when RAM use reaches (%)"),12,Muted));card.Children.Add(threshold);
        var interval=new ComboBox{Margin=new Thickness(0,0,0,8)};foreach(int value in new[]{5,10,15,30,60})interval.Items.Add(value);interval.SelectedItem=autoMemory.IntervalMinutes;card.Children.Add(Text(T("Intervalo mínimo entre execuções (minutos)","Minimum time between runs (minutes)"),12,Muted));card.Children.Add(interval);
        autoMemoryStatus=Text("",13,Muted);card.Children.Add(autoMemoryStatus);UpdateAutoMemoryStatus();
        Row(card,Button(T("Salvar configuração","Save settings"),()=>{bool active=enabled.IsChecked==true;if(active&&!autoMemory.Enabled&&!Confirm(T("Ativar a liberação automática enquanto o OuO estiver aberto, inclusive na bandeja? Somente aplicativos minimizados e ociosos desta conta serão considerados.","Enable automatic reclaim while OuO is open, including in the tray? Only minimized idle apps from this account will be considered.")))return;autoMemory.Enabled=active;autoMemory.Threshold=(int)threshold.SelectedItem;autoMemory.IntervalMinutes=(int)interval.SelectedItem;autoMemory.ResetPressure();try{SaveAutoMemory();UpdateAutoMemoryStatus();ShowToast(T("Configuração de memória salva.","Memory settings saved."),true);}catch(Exception error){ShowNotice("ERROR • "+error.Message);}}));
    }
}
}
