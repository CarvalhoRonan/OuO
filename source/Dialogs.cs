using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Aurora {
public partial class MainWindow {
    bool ConfirmDialog(string message){
        Window dialog=new Window{Owner=this,Title=T("Revisar operação","Review action"),Width=540,MinWidth=420,MaxHeight=620,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,WindowStyle=WindowStyle.None,Background=Surface,Foreground=Ink,ShowInTaskbar=false};
        Border frame=new Border{Background=Surface,BorderBrush=Accent,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(22)};
        Grid grid=new Grid();grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        TextBlock heading=Text(T("Revisar operação","Review action"),20,Accent);heading.Margin=new Thickness(0,0,0,12);grid.Children.Add(heading);
        TextBlock content=Text(message,14,Ink);content.Margin=new Thickness(0);ScrollViewer scroll=new ScrollViewer{Content=content,MaxHeight=420,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,1);grid.Children.Add(scroll);
        StackPanel actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,20,0,0)};
        Button cancel=Button(T("Cancelar","Cancel"),()=>dialog.DialogResult=false);Button proceed=Button(T("Confirmar","Confirm"),()=>dialog.DialogResult=true);
        cancel.IsCancel=true;proceed.IsDefault=true;actions.Children.Add(cancel);actions.Children.Add(proceed);Grid.SetRow(actions,2);grid.Children.Add(actions);
        frame.Child=grid;dialog.Content=frame;dialog.KeyDown+=(s,e)=>{if(e.Key==Key.Escape)dialog.DialogResult=false;};heading.MouseLeftButtonDown+=(s,e)=>{if(e.ButtonState==MouseButtonState.Pressed)dialog.DragMove();};
        return dialog.ShowDialog()==true;
    }
}
}
