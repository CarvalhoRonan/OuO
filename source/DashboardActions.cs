using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Aurora {
public static partial class MemoryReclaim {
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int id);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
    [DllImport("psapi.dll",SetLastError=true)] static extern bool EmptyWorkingSet(IntPtr handle);
    public static string TrimVisibleApps(){
        var before=Native.ReadMemory();int inspected=0,trimmed=0;
        int session=Process.GetCurrentProcess().SessionId,own=Process.GetCurrentProcess().Id;
        foreach(Process process in Process.GetProcesses())using(process)try{
            if(process.Id==own||process.SessionId!=session||process.MainWindowHandle==IntPtr.Zero)continue;
            string name=process.ProcessName.ToLowerInvariant();
            if(new[]{"explorer","dwm","taskmgr","sihost","searchhost","startmenuexperiencehost","applicationframehost"}.Contains(name))continue;
            inspected++;IntPtr handle=OpenProcess(0x0500,false,process.Id);
            if(handle==IntPtr.Zero)continue;
            try{if(EmptyWorkingSet(handle))trimmed++;}finally{CloseHandle(handle);}
        }catch{}
        Thread.Sleep(500);var after=Native.ReadMemory();
        string values=before!=null&&after!=null?" • "+L.T("RAM disponível: ","Available RAM: ")+(before.Free/1048576.0).ToString("0")+" → "+(after.Free/1048576.0).ToString("0")+" MB":"";
        return "OK • "+trimmed+"/"+inspected+L.T(" aplicativos visíveis tiveram a memória residente reduzida"," visible apps had resident memory trimmed")+values+". "+L.T("O Windows pode recarregar esses dados quando os aplicativos forem usados.","Windows may reload those pages when the apps are used.");
    }
}
public static class DisplayModes {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    public struct Mode {
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string DeviceName;
        public short SpecVersion,DriverVersion,Size,DriverExtra;
        public int Fields,PositionX,PositionY,Orientation,FixedOutput;
        public short Color,Duplex,YResolution,TTOption,Collate;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string FormName;
        public short LogPixels;
        public int BitsPerPel,Width,Height,Flags,Frequency,IcmMethod,IcmIntent,MediaType,DitherType,Reserved1,Reserved2,PanningWidth,PanningHeight;
        public override string ToString(){return Width+" × "+Height+" • "+Frequency+" Hz";}
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool EnumDisplaySettings(string device,int mode,ref Mode result);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int ChangeDisplaySettings(ref Mode mode,int flags);
    static Mode Empty(){return new Mode{Size=(short)Marshal.SizeOf(typeof(Mode))};}
    public static Mode Current(){Mode value=Empty();if(!EnumDisplaySettings(null,-1,ref value))throw new InvalidOperationException(L.T("Não foi possível ler a resolução atual.","Could not read current display resolution."));return value;}
    public static Mode[] Available(){
        var list=new List<Mode>();var keys=new HashSet<string>();
        for(int index=0;index<300;index++){Mode value=Empty();if(!EnumDisplaySettings(null,index,ref value))break;
            if(value.Width<640||value.Height<480||value.BitsPerPel<32)continue;
            string key=value.Width+"x"+value.Height+"@"+value.Frequency;
            if(keys.Add(key)&&ChangeDisplaySettings(ref value,2)==0)list.Add(value);
        }
        return list.OrderByDescending(x=>x.Width*x.Height).ThenByDescending(x=>x.Frequency).ToArray();
    }
    public static int Apply(Mode mode,bool persist){return ChangeDisplaySettings(ref mode,persist?1:0);}
    public static int Test(Mode mode){return ChangeDisplaySettings(ref mode,2);}
}
public partial class MainWindow {
    DisplayModes.Mode originalResolution,pendingResolution,baselineResolution;
    bool resolutionPending,resolutionCloseHook,baselineSaved;
    DispatcherTimer resolutionTimer;
    void RevertResolution(){
        if(!resolutionPending)return;
        resolutionPending=false;if(resolutionTimer!=null)resolutionTimer.Stop();
        int code=DisplayModes.Apply(originalResolution,false);
        ShowToast(code==0?T("Resolução anterior restaurada.","Previous resolution restored."):T("ERRO • Abra as configurações de vídeo do Windows para restaurar a resolução.","ERROR • Open Windows display settings to restore the resolution."));
    }
    void RestoreOriginalResolution(){
        if(!baselineSaved){ShowToast(T("Nenhuma resolução foi alterada nesta sessão.","No resolution has been changed in this session."));return;}
        if(resolutionTimer!=null)resolutionTimer.Stop();resolutionPending=false;
        int code=DisplayModes.Apply(baselineResolution,true);
        ShowToast(code==0?T("Resolução original restaurada e salva.","Original resolution restored and saved."):T("ERROR • O driver recusou a resolução original. Abra as configurações de vídeo.","ERROR • The driver rejected the original resolution. Open display settings."));
    }
    void DashboardQuickActions(){
        var memory=Card(T("Memória e desempenho","Memory & performance"),T("Ações manuais com resultado visível. Reduzir memória residente não aumenta a RAM física.","Manual actions with visible results. Trimming resident memory does not increase physical RAM."));
        Row(memory,Button(T("Liberar memória dos aplicativos","Trim app memory"),()=>{
            if(!Confirm(T("Solicitar que aplicativos visíveis liberem páginas de memória residentes? Eles podem ficar mais lentos ao recarregar os dados.","Ask visible apps to trim resident memory pages? They may be slower while reloading data.")))return;
            Work(T("Revisar memória","Review memory"),()=>MemoryReclaim.TrimVisibleApps(),r=>ShowToast(r));
        }));
        var boost=Card(T("Boost de desempenho","Performance boost"),T("Plano de energia reversível e revisão de tarefas em segundo plano. Não altera frequências de CPU ou GPU.","Reversible power profile and review of background tasks. Does not change CPU or GPU clocks."));
        Row(boost,Button(T("Ativar Alto desempenho","Enable High performance"),()=>Power("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c")),Button(T("Restaurar plano anterior","Restore previous plan"),()=>RestorePower()));
        var display=Card(T("Resolução da tela","Display resolution"),T("Escolha um modo aceito pelo monitor e driver. Uma mudança sem confirmação será revertida em 15 segundos.","Choose a mode accepted by the monitor and driver. An unconfirmed change reverts after 15 seconds."));
        var choices=new ComboBox{Margin=new Thickness(0,0,0,8),MaxWidth=360};display.Children.Add(choices);
        Action load=()=>{choices.Items.Clear();try{var currentMode=DisplayModes.Current();foreach(var mode in DisplayModes.Available())choices.Items.Add(mode);choices.SelectedItem=choices.Items.Cast<DisplayModes.Mode>().FirstOrDefault(x=>x.Width==currentMode.Width&&x.Height==currentMode.Height&&x.Frequency==currentMode.Frequency);}catch(Exception e){ShowToast("ERROR • "+e.Message);}};
        if(!preview)load();
        Row(display,Button(T("Aplicar temporariamente","Apply temporarily"),()=>{
            if(choices.SelectedItem==null||resolutionPending)return;
            var target=(DisplayModes.Mode)choices.SelectedItem;
            try{originalResolution=DisplayModes.Current();if(DisplayModes.Test(target)!=0){ShowToast(T("ERROR • Modo recusado pelo driver.","ERROR • Driver rejected this mode."));return;}
                if(!Confirm(T("Aplicar esta resolução por 15 segundos? Se a tela ficar ilegível, aguarde a reversão automática.","Apply this resolution for 15 seconds? If the screen becomes unreadable, wait for automatic rollback.")))return;
                if(DisplayModes.Apply(target,false)!=0){ShowToast(T("ERROR • Não foi possível mudar a resolução.","ERROR • Could not change resolution."));return;}
                if(!baselineSaved){baselineResolution=originalResolution;baselineSaved=true;}pendingResolution=target;
                resolutionPending=true;resolutionTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(15)};resolutionTimer.Tick+=(s,e)=>RevertResolution();resolutionTimer.Start();
                ShowToast(T("Confirme a resolução antes de 15 segundos.","Confirm this resolution within 15 seconds."));
                if(!resolutionCloseHook){resolutionCloseHook=true;Closing+=(s,e)=>{if(resolutionPending)RevertResolution();};}
            }catch(Exception e){ShowToast("ERROR • "+e.Message);}
        }),Button(T("Manter resolução","Keep resolution"),()=>{
            if(!resolutionPending)return;int code=DisplayModes.Apply(pendingResolution,true);if(code!=0){RevertResolution();return;}resolutionPending=false;resolutionTimer.Stop();ShowToast(T("Resolução confirmada.","Resolution confirmed."));
        }),Button(T("Reverter alteração pendente","Revert pending change"),()=>RevertResolution()),Button(T("Restaurar resolução original","Restore original resolution"),()=>RestoreOriginalResolution()),Button(T("Atualizar modos","Refresh modes"),()=>load()));
        var restore=Card(T("Ponto de restauração","Restore point"),T("Cria uma opção de retorno do sistema quando a Proteção do Sistema está disponível.","Creates a system rollback option when System Protection is available."));
        Row(restore,Button(T("Criar ponto agora","Create point now"),()=>{
            if(!RequireAdmin()||!Confirm(T("Criar um ponto de restauração agora? O Windows pode limitar a frequência.","Create a restore point now? Windows may limit the frequency.")))return;
            string sid=WindowsIdentity.GetCurrent().User.Value;
            Work(T("Criar ponto de restauração","Create restore point"),()=>{DailyMaintenance.ProtectRoot();return Describe(Runner.PS(DailyMaintenance.Script("Restore",sid),120000));},r=>ShowToast(r));
        }),Button(T("Configurar proteção","Configure protection"),()=>Open("SystemPropertiesProtection.exe")));
    }
}
}
