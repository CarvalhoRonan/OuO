using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
namespace Aurora {
public sealed class CardColumns:Panel {
    const double Gap=12;public int MaxColumns=3;public double ThreeColumnWidth=1150;bool independentColumns;public bool IndependentColumns{get{return independentColumns;}set{if(independentColumns==value)return;independentColumns=value;InvalidateMeasure();InvalidateArrange();}}
    int Count(double width){int visible=InternalChildren.Cast<UIElement>().Count(x=>x.Visibility!=Visibility.Collapsed);return Math.Max(1,Math.Min(Math.Max(1,visible),Math.Min(MaxColumns,width>=ThreeColumnWidth?3:width>=640?2:1)));}
    static double WidthFor(double width,int count){return Math.Max(1,(width-Gap*(count-1))/count);}
    protected override Size MeasureOverride(Size available){double width=Double.IsInfinity(available.Width)?800:Math.Max(1,available.Width);int columns=Count(width),column=0;double row=0,height=0;var heights=new double[columns];foreach(UIElement item in InternalChildren){item.Measure(new Size(WidthFor(width,columns),Double.PositiveInfinity));if(item.Visibility==Visibility.Collapsed)continue;if(IndependentColumns){heights[column%columns]+=item.DesiredSize.Height+Gap;column++;continue;}row=Math.Max(row,item.DesiredSize.Height);if(++column==columns){height+=row+Gap;row=0;column=0;}}if(IndependentColumns)return new Size(width,Math.Max(0,heights.Max()-Gap));if(column>0)height+=row+Gap;return new Size(width,Math.Max(0,height-Gap));}
    protected override Size ArrangeOverride(Size final){int columns=Count(final.Width);double width=WidthFor(final.Width,columns),top=0;var items=InternalChildren.Cast<UIElement>().Where(x=>x.Visibility!=Visibility.Collapsed).ToArray();foreach(UIElement hidden in InternalChildren.Cast<UIElement>().Where(x=>x.Visibility==Visibility.Collapsed))hidden.Arrange(new Rect(0,0,0,0));if(IndependentColumns){var heights=new double[columns];for(int i=0;i<items.Length;i++){int c=i%columns;items[i].Arrange(new Rect(c*(width+Gap),heights[c],width,items[i].DesiredSize.Height));heights[c]+=items[i].DesiredSize.Height+Gap;}return final;}for(int i=0;i<items.Length;i+=columns){double row=items.Skip(i).Take(columns).Max(x=>x.DesiredSize.Height);for(int c=0;c<columns&&i+c<items.Length;c++)items[i+c].Arrange(new Rect(c*(width+Gap),top,width,items[i+c].DesiredSize.Height));top+=row+Gap;}return final;}
}
public static class Args {
    public static string Q(string value){StringBuilder b=new StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1);b.Append('"');}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}b.Append('\\',slashes*2);b.Append('"');return b.ToString();}
    public static string LocalPath(string path){if(String.IsNullOrWhiteSpace(path)||path.IndexOfAny(new[]{'"','\r','\n','\0'})>=0||path.Trim().StartsWith(@"\\"))throw new ArgumentException(L.T("Informe um caminho local válido.","Enter a valid local path."));return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));}
}
public partial class MainWindow {
    CardColumns cardHost;
    void BeginColumns(int maxColumns=3){CardColumns host=new CardColumns{HorizontalAlignment=HorizontalAlignment.Stretch,MaxWidth=1450,MaxColumns=maxColumns,IndependentColumns=true};body.Children.Add(host);cardHost=host;}
    void ResizeCards(CardColumns host){host.InvalidateMeasure();host.InvalidateArrange();}
    CheckBox Choice(Panel parent,string title,bool enabled,bool selected){CheckBox b=new CheckBox{Foreground=Ink,Margin=new Thickness(0,6,0,10),IsEnabled=enabled,IsChecked=selected,Content=new TextBlock{Text=title,TextWrapping=TextWrapping.Wrap,MaxWidth=340}};System.Windows.Automation.AutomationProperties.SetName(b,title);parent.Children.Add(b);return b;}
    TextBox Field(Panel p,string label,string initial){p.Children.Add(Text(label,12,Muted));TextBox box=Input(initial);System.Windows.Automation.AutomationProperties.SetName(box,label);p.Children.Add(box);return box;}
    string PickFile(string filter){var dialog=new Microsoft.Win32.OpenFileDialog{Filter=filter,CheckFileExists=true};return dialog.ShowDialog(this)==true?dialog.FileName:null;}
    void BrowseFile(Panel p,TextBox target,string filter){Row(p,Button(T("Selecionar arquivo","Select file"),()=>{string file=PickFile(filter);if(file!=null)target.Text=file;}));}
    void BrowseFolder(Panel p,TextBox target){Row(p,Button(T("Selecionar pasta","Select folder"),()=>{using(var dialog=new System.Windows.Forms.FolderBrowserDialog()){dialog.Description=T("Selecione a pasta","Select folder");if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)target.Text=dialog.SelectedPath;}}));}
    void AttachResult(Panel parent,TextBox box){
        if(parent.Children.Contains(box))parent.Children.Remove(box);
        TextBlock summary=Text("",13,Muted);summary.Visibility=Visibility.Collapsed;parent.Children.Add(summary);
        var details=new StackPanel();details.Children.Add(box);var tools=new WrapPanel();details.Children.Add(tools);string reportTitle=parent.Children.OfType<TextBlock>().Select(x=>x.Text).FirstOrDefault()??"OuO";var clear=Button(T("Limpar log exibido","Clear displayed log"),()=>box.Clear());tools.Children.Add(clear);var problem=Button(T("Relatar problema","Report a problem"),()=>ReportProblem(reportTitle,box.Text));tools.Children.Add(problem);Expander result=new Expander{Header=T("Ver detalhes técnicos","View technical details"),Foreground=Ink,Content=details,Margin=new Thickness(0,4,0,8),IsExpanded=false,Visibility=Visibility.Collapsed};
        box.TextChanged+=(s,e)=>{
            string value=(box.Text??"").Trim();bool has=value.Length>0;
            summary.Visibility=result.Visibility=has?Visibility.Visible:Visibility.Collapsed;
            if(!has)return;
            bool error=ResultTone.IsFailure(value);
            summary.Foreground=OutcomeBrush(value);
            string first=value.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"";
            summary.Text=ResultSymbol(first)+first;problem.Visibility=error?Visibility.Visible:Visibility.Collapsed;System.Windows.Automation.AutomationProperties.SetName(result,T("Detalhes de ","Details for ")+reportTitle);box.Foreground=OutcomeBrush(value);
        };
        parent.Children.Add(result);
    }
}
}
