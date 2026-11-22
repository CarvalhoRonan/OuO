using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.VisualBasic.FileIO;
namespace Aurora {
public sealed class CleanChoice:INotifyPropertyChanged {
    readonly Action<CleanChoice,bool> changed;bool selected;public readonly CleanItem Item;
    public CleanChoice(CleanItem value,Action<CleanChoice,bool> callback){Item=value;changed=callback;}
    public string Label{get{return Item.ToString();}}public string Path{get{return Item.Path;}}
    public bool Selected{get{return selected;}set{if(value==selected)return;selected=value;if(changed!=null)changed(this,value);if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Selected"));}}
    public event PropertyChangedEventHandler PropertyChanged;
}
public sealed class CleanSelection {
    public readonly List<CleanChoice> Items=new List<CleanChoice>();public int Count{get;private set;}public long Bytes{get;private set;}bool batch;public event Action Changed;
    public CleanSelection(IEnumerable<CleanItem> items){foreach(var item in items.GroupBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()))Items.Add(new CleanChoice(item,(entry,on)=>{Count+=on?1:-1;Bytes+=on?entry.Item.Bytes:-entry.Item.Bytes;if(!batch&&Changed!=null)Changed();}));}
    public void RestoreSelection(CleanSelection previous){if(previous==null)return;var saved=previous.SelectedItems().ToDictionary(x=>x.Path,StringComparer.OrdinalIgnoreCase);batch=true;try{foreach(var choice in Items){CleanItem prior;if(saved.TryGetValue(choice.Path,out prior)&&prior.Bytes==choice.Item.Bytes&&prior.Modified.ToUniversalTime()==choice.Item.Modified.ToUniversalTime())choice.Selected=true;}}finally{batch=false;}if(Changed!=null)Changed();}
    public void SetAll(bool selected){batch=true;try{foreach(var item in Items)item.Selected=selected;}finally{batch=false;}if(Changed!=null)Changed();}
    public CleanItem[] SelectedItems(){return Items.Where(x=>x.Selected).Select(x=>x.Item).ToArray();}
    public static string Size(long bytes){return bytes>=1073741824?(bytes/1073741824.0).ToString("0.00")+" GB":bytes>=1048576?(bytes/1048576.0).ToString("0.0")+" MB":bytes>=1024?(bytes/1024.0).ToString("0.0")+" KB":bytes+" B";}
}
public sealed class CleanupBatchResult {
    public int Removed,Skipped,Failed;public long Bytes;public readonly List<string> Details=new List<string>();
    public string Report(){return (Failed>0?"ERROR":Skipped>0?L.T("AVISO","NOTICE"):"OK")+" • "+Removed+L.T(" excluído(s) • "," deleted • ")+CleanSelection.Size(Bytes)+" • "+Skipped+L.T(" ignorado(s) • "," skipped • ")+Failed+L.T(" falha(s)."," failed.")+(Details.Count>0?"\n"+String.Join("\n",Details):"");}
}
public static class CleanupBatch {
    internal static CleanupBatchResult Execute(CleanItem[] selected,List<UserCleanup.CleanRoot> roots,bool permanent,Action<int,int,string> progress){var result=new CleanupBatchResult();var items=(selected??new CleanItem[0]).Where(x=>x!=null).GroupBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).ToArray();int processed=0;foreach(var item in items){try{
            UserCleanup.CleanRoot matched;if(!UserCleanup.EligibleIn(item.Path,DateTime.UtcNow,roots,out matched)||!Unchanged(item)){result.Skipped++;if(result.Details.Count<100)result.Details.Add(L.T("Ignorado: arquivo alterado, indisponível ou fora das regras • ","Skipped: changed, unavailable or outside cleanup rules • ")+item.Path);continue;}
            if(permanent){using(var file=new FileStream(item.Path,FileMode.Open,FileAccess.Read,FileShare.Delete)){if(file.Length!=item.Bytes||!Unchanged(item))throw new IOException(L.T("O arquivo mudou após a análise.","File changed after scanning."));File.Delete(item.Path);}}
            else{using(var file=new FileStream(item.Path,FileMode.Open,FileAccess.Read,FileShare.Delete)){}FileSystem.DeleteFile(item.Path,UIOption.OnlyErrorDialogs,RecycleOption.SendToRecycleBin);}
            result.Removed++;result.Bytes+=item.Bytes;
        }catch(IOException error){result.Skipped++;if(result.Details.Count<100)result.Details.Add(L.T("Ignorado: em uso ou indisponível • ","Skipped: in use or unavailable • ")+item.Path+" • "+error.Message);}catch(Exception error){result.Failed++;if(result.Details.Count<100)result.Details.Add("ERROR • "+item.Path+" • "+error.Message);}finally{processed++;if(progress!=null)progress(processed,items.Length,item.Path);}}return result;}
    static bool Unchanged(CleanItem item){var info=new FileInfo(item.Path);return info.Exists&&info.Length==item.Bytes&&info.LastWriteTimeUtc==item.Modified.ToUniversalTime();}
}
public partial class MainWindow {
    CleanSelection temporarySelection;TextBlock temporarySelectionState;
    Action TemporaryCleanupCard(){
        var card=Card(T("Arquivos temporários e caches","Temporary files and caches"),T("Analise os temporários conhecidos, selecione os arquivos e escolha como excluir. Arquivos recentes, alterados ou em uso são preservados.","Scan known temporary locations, select files and choose how to delete. Recent, changed or busy files are preserved."));
        var files=new ListBox{MaxHeight=300,Background=Surface,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};ScrollViewer.SetHorizontalScrollBarVisibility(files,ScrollBarVisibility.Disabled);ScrollViewer.SetCanContentScroll(files,true);VirtualizingStackPanel.SetIsVirtualizing(files,true);VirtualizingStackPanel.SetVirtualizationMode(files,System.Windows.Controls.VirtualizationMode.Recycling);
        var factory=new FrameworkElementFactory(typeof(CheckBox));factory.SetBinding(CheckBox.IsCheckedProperty,new Binding("Selected"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});var rowLabel=new FrameworkElementFactory(typeof(TextBlock));rowLabel.SetBinding(TextBlock.TextProperty,new Binding("Label"));rowLabel.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);factory.SetValue(CheckBox.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch);factory.AppendChild(rowLabel);factory.SetBinding(CheckBox.ToolTipProperty,new Binding("Path"));factory.SetResourceReference(CheckBox.ForegroundProperty,"OuOComboInk");factory.SetValue(CheckBox.MarginProperty,new Thickness(3));files.ItemTemplate=new DataTemplate{VisualTree=factory};card.Children.Add(files);
        var state=Text(T("Clique em Analisar para procurar arquivos temporários.","Select Analyze to find temporary files."),13,Muted);card.Children.Add(state);var selectionState=Text("",13,Accent);card.Children.Add(selectionState);temporarySelectionState=selectionState;bool running=false;Action scan=null;
        Action<IEnumerable<CleanItem>> populate=items=>{var previous=temporarySelection;temporarySelection=new CleanSelection(items);temporarySelection.Changed+=()=>selectionState.Text=temporarySelection.Count+T(" selecionado(s) • "," selected • ")+CleanSelection.Size(temporarySelection.Bytes);files.ItemsSource=temporarySelection.Items;files.Visibility=temporarySelection.Items.Count>0?Visibility.Visible:Visibility.Collapsed;temporarySelection.RestoreSelection(previous);if(previous==null)temporarySelection.SetAll(false);};populate(new CleanItem[0]);
        scan=()=>{if(running||preview)return;running=true;List<CleanItem> found=null;bool limited=false;Work(T("Analisar temporários","Scan temporary files"),()=>{found=UserCleanup.Scan(out limited);return (limited?T("AVISO • análise parcial • ","NOTICE • partial scan • "):"OK • ")+found.Count+T(" arquivo(s) encontrado(s) • "," file(s) found • ")+CleanSelection.Size(found.Sum(x=>x.Bytes))+(limited?T(". Limite de 300.000 itens examinados; analise novamente após limpar.",". 300,000 examined-item limit; scan again after cleanup."):"");},r=>{running=false;if(found!=null)populate(found);state.Text=r;state.Foreground=OutcomeBrush(r);});};
        Action open=()=>{var choice=files.SelectedItem as CleanChoice;if(choice==null)return;try{ProcessStartFileLocation(choice.Item.Path);}catch(Exception error){ShowToast("ERROR • "+error.Message);}};files.MouseDoubleClick+=(s,e)=>open();files.PreviewMouseLeftButtonDown+=(s,e)=>{var target=e.OriginalSource as DependencyObject;var item=target==null?null:ItemsControl.ContainerFromElement(files,target) as ListBoxItem;if(item!=null)item.IsSelected=true;};
        Action<bool> delete=permanent=>{if(running||preview)return;var chosen=temporarySelection.SelectedItems();if(chosen.Length==0){ShowToast(T("Selecione os arquivos para excluir.","Select files to delete."));return;}string amount=chosen.Length+T(" arquivo(s) • "," file(s) • ")+CleanSelection.Size(chosen.Sum(x=>x.Bytes));if(!Confirm((permanent?T("Excluir permanentemente os arquivos selecionados?\n","Permanently delete selected files?\n"):T("Enviar os arquivos selecionados à Lixeira?\n","Send selected files to Recycle Bin?\n"))+amount+(permanent?T("\n\nA exclusão será definitiva, sem Lixeira.","\n\nDeletion is permanent and bypasses the Recycle Bin."):"")))return;running=true;Work(permanent?T("Excluir temporários permanentemente","Permanently delete temporary files"):T("Enviar temporários à Lixeira","Recycle temporary files"),()=>{int job=workerJobId;return UserCleanup.DeleteMany(chosen,permanent,(done,total,path)=>{if(done%25==0||done==total)ReportStage(done+" / "+total+" • "+Path.GetFileName(path),job);});},r=>{running=false;populate(temporarySelection.Items.Select(x=>x.Item).Where(x=>File.Exists(x.Path)).ToArray());state.Text=r.Split('\n')[0];state.Foreground=OutcomeBrush(r);ShowToast(r.Split('\n')[0]);});};
        Row(card,Button(T("Analisar","Analyze"),()=>scan()),Button(T("Selecionar todos","Select all"),()=>{if(!running)temporarySelection.SetAll(true);}),Button(T("Limpar seleção","Clear selection"),()=>{if(!running)temporarySelection.SetAll(false);}),Button(T("Abrir pasta do arquivo","Open file location"),()=>open()));
        Row(card,Button(T("Enviar à Lixeira","Move to Recycle Bin"),()=>delete(false)),Button(T("Excluir permanentemente","Delete permanently"),()=>delete(true)),Button(T("Limpeza do Windows","Windows cleanup"),()=>Open("cleanmgr.exe")));
        if(preview){populate(new[]{new CleanItem{Path=@"C:\TEMP\cache-de-exemplo.tmp",Source=T("Cache de exemplo","Sample cache"),Bytes=10485760,Modified=DateTime.Now.AddDays(-3)},new CleanItem{Path=@"C:\TEMP\temporario-de-exemplo.tmp",Source=T("Temporário de exemplo","Sample temporary file"),Bytes=5242880,Modified=DateTime.Now.AddDays(-2)}});state.Text=T("Demonstração • arquivos fictícios","Preview • fixture files");}
        return scan;
    }
    void ProcessStartFileLocation(string path){System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe","/select,"+Args.Q(path)){UseShellExecute=true});}
}
}
