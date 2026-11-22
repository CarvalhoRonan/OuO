using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
namespace Aurora {
public sealed class CardColumns:Panel {
    const double Gap=12; public int MaxColumns=3; public double ThreeColumnWidth=1150;
    static int Shortest(double[] heights){int result=0;for(int i=1;i<heights.Length;i++)if(heights[i]<heights[result])result=i;return result;}
    int Count(double width){return Math.Max(1,Math.Min(InternalChildren.Cast<UIElement>().Count(x=>x.Visibility!=Visibility.Collapsed),Math.Min(MaxColumns,width>=ThreeColumnWidth?3:width>=600?2:1)));}
    static double WidthFor(double width,int columns){return Math.Max(260,(width-Gap*(columns-1))/columns);}
    protected override Size MeasureOverride(Size available){
        double width=Double.IsInfinity(available.Width)?800:Math.Max(300,available.Width);int columns=Count(width);double itemWidth=WidthFor(width,columns);double[] heights=new double[columns];
        for(int i=0;i<InternalChildren.Count;i++){UIElement item=InternalChildren[i];item.Measure(new Size(itemWidth,Double.PositiveInfinity));if(item.Visibility==Visibility.Collapsed)continue;int column=Shortest(heights);heights[column]+=item.DesiredSize.Height+Gap;}
        return new Size(width,heights.Length==0?0:Math.Max(0,heights.Max()-Gap));
    }
    protected override Size ArrangeOverride(Size final){
        int columns=Count(final.Width);double itemWidth=WidthFor(final.Width,columns);double[] tops=new double[columns];
        for(int i=0;i<InternalChildren.Count;i++){UIElement item=InternalChildren[i];if(item.Visibility==Visibility.Collapsed){item.Arrange(new Rect(0,0,0,0));continue;}int column=Shortest(tops);double height=item.DesiredSize.Height;item.Arrange(new Rect(column*(itemWidth+Gap),tops[column],itemWidth,height));tops[column]+=height+Gap;}
        return final;
    }
}
public static class Args {
    public static string Q(string value){StringBuilder b=new StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1);b.Append('"');}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}b.Append('\\',slashes*2);b.Append('"');return b.ToString();}
    public static string LocalPath(string path){if(String.IsNullOrWhiteSpace(path)||path.IndexOfAny(new[]{'"','\r','\n','\0'})>=0||path.Trim().StartsWith(@"\\"))throw new ArgumentException(L.T("Informe um caminho local válido.","Enter a valid local path."));return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));}
}
public partial class MainWindow {
    CardColumns cardHost;
    void BeginColumns(int maxColumns=3){CardColumns host=new CardColumns{HorizontalAlignment=HorizontalAlignment.Stretch,MaxWidth=1240,MaxColumns=maxColumns};body.Children.Add(host);cardHost=host;}
    void ResizeCards(CardColumns host){host.InvalidateMeasure();host.InvalidateArrange();}
    CheckBox Choice(Panel parent,string title,bool enabled,bool selected){CheckBox b=new CheckBox{Foreground=Ink,Margin=new Thickness(0,6,0,10),IsEnabled=enabled,IsChecked=selected,Content=new TextBlock{Text=title,TextWrapping=TextWrapping.Wrap,MaxWidth=340}};parent.Children.Add(b);return b;}
    TextBox Field(Panel p,string label,string initial){p.Children.Add(Text(label,12,Muted));TextBox box=Input(initial);p.Children.Add(box);return box;}
    string PickFile(string filter){var dialog=new Microsoft.Win32.OpenFileDialog{Filter=filter,CheckFileExists=true};return dialog.ShowDialog(this)==true?dialog.FileName:null;}
    void BrowseFile(Panel p,TextBox target,string filter){Row(p,Button(T("Selecionar arquivo","Select file"),()=>{string file=PickFile(filter);if(file!=null)target.Text=file;}));}
    void BrowseFolder(Panel p,TextBox target){Row(p,Button(T("Selecionar pasta","Select folder"),()=>{using(var dialog=new System.Windows.Forms.FolderBrowserDialog()){dialog.Description=T("Selecione a pasta","Select folder");if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)target.Text=dialog.SelectedPath;}}));}
    void AttachResult(Panel parent,TextBox box){
        if(parent.Children.Contains(box))parent.Children.Remove(box);
        TextBlock summary=Text("",13,Muted);summary.Visibility=Visibility.Collapsed;parent.Children.Add(summary);
        Expander result=new Expander{Header=T("Ver detalhes técnicos","View technical details"),Foreground=Ink,Content=box,Margin=new Thickness(0,4,0,8),IsExpanded=false,Visibility=Visibility.Collapsed};
        box.TextChanged+=(s,e)=>{
            string value=(box.Text??"").Trim();bool has=value.Length>0;
            summary.Visibility=result.Visibility=has?Visibility.Visible:Visibility.Collapsed;
            if(!has)return;
            bool error=ResultTone.IsFailure(value);
            summary.Foreground=OutcomeBrush(value);
            string first=value.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"";
            summary.Text=first;box.Foreground=OutcomeBrush(value);
        };
        parent.Children.Add(result);
    }
}
}
