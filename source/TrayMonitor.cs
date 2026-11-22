using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace Aurora {
public partial class MainWindow {
    System.Windows.Forms.NotifyIcon trayIcon;
    bool TrayActive{get{return trayIcon!=null;}}
    void InitTray(){
        try{
            trayIcon=new System.Windows.Forms.NotifyIcon();
            trayIcon.Icon=System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
            trayIcon.Text="OuO • Monitorando o computador";
            var menu=new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add(T("Abrir OuO","Open OuO"),null,(s,e)=>Dispatcher.BeginInvoke(new Action(ShowFromTray)));
            menu.Items.Add(T("Encerrar","Exit"),null,(s,e)=>Dispatcher.BeginInvoke(new Action(()=>Close())));
            trayIcon.ContextMenuStrip=menu;
            trayIcon.DoubleClick+=(s,e)=>Dispatcher.BeginInvoke(new Action(ShowFromTray));
            trayIcon.Visible=true;
            Loaded+=(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{if(startupAccepted)HideToTray();}),DispatcherPriority.Background);
            StateChanged+=(s,e)=>{if(WindowState==WindowState.Minimized&&trayIcon!=null){Hide();ShowInTaskbar=false;}};
        }catch{DisposeTray();}
    }
    void ShowFromTray(){ShowInTaskbar=true;Show();WindowState=WindowState.Normal;Activate();}
    void HideToTray(){Hide();ShowInTaskbar=false;}
    void UpdateTrayStatus(){
        if(trayIcon==null||lastSample==null)return;
        string label="OuO • CPU "+lastSample[0]+" • GPU "+lastSample[1]+" • "+lastSample[3];
        if(label.Length>63)label=label.Substring(0,63);
        try{trayIcon.Text=label;}catch{}
    }
    void DisposeTray(){if(trayIcon==null)return;try{trayIcon.Visible=false;trayIcon.Dispose();}catch{}trayIcon=null;}
}
}
