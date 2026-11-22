using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace Aurora {
public static class ExitCountdown {public static int Remaining(double seconds){return Math.Max(0,15-(int)Math.Floor(Math.Max(0,seconds)));}}
public partial class MainWindow {
    bool startupAccepted,exitFinalized,exitDialogOpen;
    public void exitForSessionEnd(){exitFinalized=true;StopAutoMemory();}
    Window LifecycleDialog(bool goodbye,bool autoClose=true){
        double availableWidth=Math.Max(300,SystemParameters.WorkArea.Width-40),availableHeight=Math.Max(260,SystemParameters.WorkArea.Height-70);
        bool visibleOwner=IsVisible&&WindowState!=WindowState.Minimized;var dialog=new Window{Owner=visibleOwner?this:null,Icon=Icon,Title=goodbye?T("Obrigado por usar o OuO","Thank you for using OuO"):T("Antes de começar","Before you start"),Width=Math.Min(goodbye?920:620,availableWidth),MaxHeight=availableHeight,SizeToContent=SizeToContent.Height,WindowStartupLocation=visibleOwner?WindowStartupLocation.CenterOwner:WindowStartupLocation.CenterScreen,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Background=Surface,Foreground=Ink,ShowInTaskbar=!visibleOwner};
        var root=new StackPanel{Margin=new Thickness(22)};
        var heading=Text(goodbye?T("Obrigado por usar o OuO!","Thank you for using OuO!"):T("Antes de começar","Before you start"),25,Accent);root.Children.Add(heading);
        heading.MouseLeftButtonDown+=(s,e)=>{if(e.ButtonState==System.Windows.Input.MouseButtonState.Pressed)try{dialog.DragMove();}catch{}};
        if(!goodbye){
            root.Children.Add(Text(T("O OuO permite alterar configurações importantes do Windows. Leia a descrição de cada função e aplique apenas os ajustes que você compreende. Em caso de dúvida, mantenha a configuração atual e procure orientação.","OuO can change important Windows settings. Read each feature's description and apply only changes you understand. If you are unsure, keep the current setting and seek guidance."),15,Ink));
            root.Children.Add(Text(T("As funções com o aviso ‘Recurso em fase de testes’ ainda precisam de validação em mais cenários. A compatibilidade com outras versões do Windows, hardware e telas está descrita em Sobre e no relatório de validação.","Features marked ‘Feature in testing’ still need validation in more scenarios. Compatibility with other Windows releases, hardware and displays is described in About and the validation report."),13,Muted));
            var leave=Button(T("Sair","Exit"),()=>dialog.DialogResult=false);leave.IsCancel=true;
            var proceed=Button(T("Entendi, continuar","I understand, continue"),()=>dialog.DialogResult=true);proceed.IsDefault=true;Row(root,leave,proceed);
        }else{
            root.Children.Add(Text(T("Desenvolvido por Overclock Informática. Acompanhe o projeto e compartilhe sua experiência. Doações são opcionais; todas as funcionalidades permanecem gratuitas.","Developed by Overclock Informática. Follow the project and share your experience. Donations are optional; every feature remains free."),14,Ink));
            var tiles=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};
            var pix=FarewellTile(tiles,T("Pix • Brasil","Pix • Brazil"),"pix.png");var feedback=Text("",12,StateGreen);pix.Children.Add(feedback);
            pix.Children.Add(Button(T("Copiar código Pix","Copy Pix code"),()=>AboutCopy(AboutPixCode,feedback,"Código Pix copiado.","Pix code copied.")));pix.Children.Add(Button(T("Copiar chave Pix","Copy Pix key"),()=>AboutCopy(AboutPixKey,feedback,"Chave Pix copiada.","Pix key copied.")));
            var kofi=FarewellTile(tiles,"Ko-fi","kofi.png");kofi.Children.Add(Button(T("Abrir Ko-fi ↗","Open Ko-fi ↗"),()=>Open(AboutKofi)));
            var instagram=FarewellTile(tiles,"Instagram","instagram.png");instagram.Children.Add(Button("@oc.informatica ↗",()=>Open(AboutInstagram)));
            var links=FarewellTile(tiles,T("Página de links","Links page"),"links.png");links.Children.Add(Button("Linkme.bio ↗",()=>Open(AboutLinks)));
            root.Children.Add(new ScrollViewer{Content=tiles,MaxHeight=Math.Max(140,availableHeight-220),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var countdown=Text(T("Fechando em 15 segundos…","Closing in 15 seconds…"),13,Muted);root.Children.Add(countdown);
            var close=Button(T("Fechar","Close"),()=>dialog.Close());close.IsDefault=true;close.IsCancel=true;Row(root,close);
            var watch=new Stopwatch();var tick=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};
            tick.Tick+=(s,e)=>{int remaining=ExitCountdown.Remaining(watch.Elapsed.TotalSeconds);countdown.Text=T("Fechando em ","Closing in ")+remaining+T(" segundos…"," seconds…");if(remaining==0)dialog.Close();};
            dialog.ContentRendered+=(s,e)=>{if(autoClose){watch.Start();tick.Start();}};dialog.Closed+=(s,e)=>{tick.Stop();watch.Stop();};
        }
        UIElement layout=root;if(goodbye){var items=root.Children.Cast<UIElement>().ToArray();root.Children.Clear();var grid=new Grid{Margin=root.Margin};grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var header=new StackPanel();header.Children.Add(items[0]);header.Children.Add(items[1]);grid.Children.Add(header);Grid.SetRow(items[2],1);grid.Children.Add(items[2]);var footer=new StackPanel();foreach(var item in items.Skip(3))footer.Children.Add(item);Grid.SetRow(footer,2);grid.Children.Add(footer);layout=grid;}
        if(!goodbye){var items=root.Children.Cast<UIElement>().ToArray();root.Children.Clear();var grid=new Grid{Margin=root.Margin};grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.Children.Add(items[0]);var paragraphs=new StackPanel();foreach(var item in items.Skip(1).Take(items.Length-2))paragraphs.Children.Add(item);var scroll=new ScrollViewer{Content=paragraphs,MaxHeight=Math.Max(60,availableHeight-160),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,1);grid.Children.Add(scroll);Grid.SetRow(items.Last(),2);grid.Children.Add(items.Last());layout=grid;}
        dialog.Content=new Border{Background=Surface,BorderBrush=Accent,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Child=layout};return dialog;
    }
    StackPanel FarewellTile(Panel parent,string title,string image){var tile=new StackPanel{Width=195,Margin=new Thickness(4,6,4,8)};var label=Text(title,15,Ink);label.TextAlignment=TextAlignment.Center;tile.Children.Add(label);AboutQr(tile,image,160);parent.Children.Add(tile);return tile;}
    void HandleExit(object sender,CancelEventArgs e){
        if(preview||exitFinalized||!startupAccepted)return;
        if(activeJobs.Count>0){e.Cancel=true;UpdateWorkUi();status.Text=T("Aguarde as tarefas terminarem antes de fechar. Downloads podem ser cancelados na própria tela.","Wait for running tasks before closing. Downloads can be canceled on their page.");status.Foreground=Muted;return;}
        if(exitDialogOpen){e.Cancel=true;return;}
        exitDialogOpen=true;StopAutoMemory();try{LifecycleDialog(true).ShowDialog();exitFinalized=true;}finally{exitDialogOpen=false;}
    }
}
}
