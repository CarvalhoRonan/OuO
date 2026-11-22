using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aurora {
public partial class MainWindow {
    TreeView diskFolderTree;ListBox diskFileList;Action<string> diskBrowserNavigate;Func<string> diskBrowserLocation;Action diskBrowserBack;Func<int> diskBrowserScanCount,diskBrowserCacheHits;
    void DiskMapPage(){
        var card=Card(T("Mapa de espaço em disco","Disk space map"),T("Explore pastas por tamanho. Você pode alternar entre blocos e lista; a exclusão exige confirmação.","Explore folders by size. Switch between blocks and list; deletion requires confirmation."));
        Note(card,"Tamanhos lógicos. Links não são percorridos. Pastas visitadas usam cache; use Atualizar análise para reler os arquivos.","Logical sizes. Links are skipped. Visited folders use cache; choose Refresh scan to read files again.");
        var controls=new WrapPanel{Margin=new Thickness(0,0,0,8)};
        var drive=new ComboBox{Width=130,Margin=new Thickness(0,0,8,0)};
        foreach(var d in DriveInfo.GetDrives())try{if(d.IsReady&&d.DriveType==DriveType.Fixed)drive.Items.Add(d.Name);}catch{}
        if(drive.Items.Count>0)drive.SelectedIndex=0;
        var mode=new ComboBox{Width=205};mode.Items.Add(T("Blocos + lista","Blocks + list"));mode.Items.Add(T("Blocos + pastas","Blocks + folders"));mode.SelectedIndex=0;
        controls.Children.Add(drive);controls.Children.Add(mode);card.Children.Add(controls);
        var navigation=new StackPanel();card.Children.Add(navigation);
        var navButtons=new StackPanel();navigation.Children.Add(navButtons);
        var breadcrumb=new WrapPanel{Margin=new Thickness(0,0,0,8)};navigation.Children.Add(breadcrumb);
        var status=Text(T("Escolha uma unidade para analisar.","Choose a drive to scan."),13,Muted);card.Children.Add(status);
        var blocks=new Canvas{Height=220,Background=Bg,ClipToBounds=true,Visibility=Visibility.Collapsed,Margin=new Thickness(0,4,0,8)};card.Children.Add(blocks);
        var explorer=new Grid{Height=300,Visibility=Visibility.Collapsed};explorer.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(170)});explorer.ColumnDefinitions.Add(new ColumnDefinition());card.Children.Add(explorer);
        var folders=new TreeView{Background=Surface,Foreground=Ink,BorderBrush=Muted,Margin=new Thickness(0,0,7,0)};explorer.Children.Add(folders);
        folders.Resources[SystemColors.HighlightBrushKey]=Color(dark?"#244254":"#DCECF3");folders.Resources[SystemColors.HighlightTextBrushKey]=Ink;folders.Resources[SystemColors.InactiveSelectionHighlightBrushKey]=Color(dark?"#244254":"#DCECF3");folders.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey]=Ink;
        var list=new ListBox{Background=Surface,Foreground=Ink,BorderBrush=Muted};Grid.SetColumn(list,1);explorer.Children.Add(list);
        explorer.SizeChanged+=(s,e)=>{bool narrow=explorer.ActualWidth<700;folders.Visibility=narrow?Visibility.Collapsed:Visibility.Visible;explorer.ColumnDefinitions[0].Width=new GridLength(narrow?0:190);};
        var detail=Text("",12,Muted);detail.Visibility=Visibility.Collapsed;card.Children.Add(detail);
        var history=new DiskNavigationHistory();string current=null,pending=null;bool pendingRecord=false,changingDrive=false;int scanCount=0,cacheHits=0;DiskTreeNavigator folderTree=null;Action goBack=null,goForward=null,goUp=null;
        CancellationTokenSource cancel=null;List<DiskBucket> shown=null;DiskBucket selected=null;int version=0;
        diskFolderTree=folders;diskFileList=list;
        var visited=new Dictionary<string,List<DiskBucket>>(StringComparer.OrdinalIgnoreCase);var marked=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Action<string,bool> scan=null;Action render=null;Action refreshView=null;Action<bool> deleteSelected=null,deleteMarked=null;ContextMenu tileMenu=null;
        refreshView=()=>{blocks.Visibility=explorer.Visibility=shown!=null?Visibility.Visible:Visibility.Collapsed;list.Items.Clear();
            string parentPath=DiskNavigationHistory.Parent(current);if(shown!=null&&parentPath!=null){var parentRow=new ListBoxItem{Tag=parentPath,Content=new TextBlock{Text=T("📁  ..  Voltar à pasta superior","📁  ..  Parent folder"),Foreground=Accent,Margin=new Thickness(5,7,5,7)},ToolTip=parentPath};parentRow.PreviewMouseRightButtonDown+=(sender,args)=>parentRow.IsSelected=true;list.Items.Add(parentRow);}
            if(shown!=null)foreach(var item in shown.Where(x=>mode.SelectedIndex==0||x.Folder).Take(500)){
                bool folder=item.Folder;var row=new Grid{Margin=new Thickness(5,4,5,4)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(29)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(88)});
                row.Children.Add(new TextBlock{Text=folder?"📁":"📄",FontSize=16,Foreground=Accent});
                var labels=new StackPanel();var name=new TextBlock{Text=(marked.Contains(item.Path)?"✓ ":"")+Path.GetFileName(item.Path.TrimEnd('\\')),Foreground=Ink,TextTrimming=TextTrimming.CharacterEllipsis};labels.Children.Add(name);
                labels.Children.Add(new TextBlock{Text=(folder?T("Pasta","Folder"):Path.GetExtension(item.Path).TrimStart('.').ToUpperInvariant()+T(" • arquivo"," • file"))+(item.Modified==DateTime.MinValue?"":" • "+item.Modified.ToString("dd/MM/yyyy HH:mm")),Foreground=Muted,FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis});Grid.SetColumn(labels,1);row.Children.Add(labels);
                var size=new TextBlock{Text=item.SizeLabel,Foreground=Muted,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(size,2);row.Children.Add(size);
                var line=new ListBoxItem{Tag=item,Content=row,ToolTip=item.Path,HorizontalContentAlignment=HorizontalAlignment.Stretch,Background=marked.Contains(item.Path)?Color(dark?"#244254":"#DCECF3"):Surface};
                line.PreviewMouseRightButtonDown+=(s,e)=>{line.IsSelected=true;};
                list.Items.Add(line);
            }render();};
        render=()=>{
            blocks.Children.Clear();if(shown==null)return;
            var items=shown.Take(24).ToArray();if(items.Length==0)return;
            double width=Math.Max(200,blocks.ActualWidth),height=blocks.Height;
            Action<int,int,double,double,double,double> place=null;
            place=(first,last,x,y,w,h)=>{
                if(last<=first||w<=0||h<=0)return;
                if(last-first==1){
                    DiskBucket bucket=items[first];var fill=new SolidColorBrush((Color)ColorConverter.ConvertFromString(new[]{"#58C6AD","#6DA8DE","#A384D4","#E8B65C","#CF758E"}[first%5]));
                    var tile=new Border{Width=Math.Max(0,w-2),Height=Math.Max(0,h-2),Background=fill,BorderBrush=Bg,BorderThickness=new Thickness(2),ToolTip=bucket.ToString()};
                    if(w>88&&h>42){tile.Child=new TextBlock{Text=Path.GetFileName(bucket.Path.TrimEnd('\\'))+"\n"+bucket.SizeLabel,Foreground=Brushes.Black,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(6),FontSize=11};}
                    tile.MouseLeftButtonDown+=(sender,e)=>{selected=bucket;detail.Text=bucket.ToString();detail.Visibility=Visibility.Visible;if(e.ClickCount>1&&Directory.Exists(bucket.Path))scan(bucket.Path,true);else{status.Text=bucket.ToString();status.Foreground=Ink;}e.Handled=true;};
                    tile.ContextMenu=tileMenu;
                    tile.PreviewMouseRightButtonDown+=(sender,e)=>{selected=bucket;detail.Text=bucket.ToString();detail.Visibility=Visibility.Visible;};
                    Canvas.SetLeft(tile,x);Canvas.SetTop(tile,y);blocks.Children.Add(tile);return;
                }
                double total=0;for(int i=first;i<last;i++)total+=Math.Max(1.0,items[i].Bytes);
                double half=0;int cut=first+1;for(int i=first;i<last-1;i++){half+=Math.Max(1.0,items[i].Bytes);cut=i+1;if(half>=total/2)break;}
                double ratio=Math.Max(.08,Math.Min(.92,half/total));
                if(w>=h){double split=w*ratio;place(first,cut,x,y,split,h);place(cut,last,x+split,y,w-split,h);}
                else{double split=h*ratio;place(first,cut,x,y,w,split);place(cut,last,x,y+split,w,h-split);}
            };
            place(0,items.Length,0,0,width,height);
        };
        blocks.SizeChanged+=(s,e)=>render();mode.SelectionChanged+=(s,e)=>{if(refreshView!=null)refreshView();};
        list.SelectionChanged+=(s,e)=>{var row=list.SelectedItem as ListBoxItem;selected=row==null?null:row.Tag as DiskBucket;detail.Text=selected==null?"":selected.ToString();detail.Visibility=selected==null?Visibility.Collapsed:Visibility.Visible;};
        list.MouseDoubleClick+=(s,e)=>{var item=list.SelectedItem as ListBoxItem;var parent=item==null?null:item.Tag as string;if(parent!=null){scan(parent,true);return;}if(selected!=null&&Directory.Exists(selected.Path))scan(selected.Path,true);};
        var menu=new ContextMenu{Background=Surface,Foreground=Ink,BorderBrush=Accent};
        var fileActions=new List<MenuItem>();Func<string,Action,MenuItem> addMenu=(label,action)=>{var item=new MenuItem{Header=label,Foreground=Ink,Background=Surface};item.Click+=(s,e)=>action();menu.Items.Add(item);return item;};
        var backItem=addMenu(T("Voltar","Back"),()=>{if(goBack!=null)goBack();});var forwardItem=addMenu(T("Avançar","Forward"),()=>{if(goForward!=null)goForward();});var upItem=addMenu(T("Pasta superior","Parent folder"),()=>{if(goUp!=null)goUp();});menu.Items.Add(new Separator());
        menu.Opened+=(s,e)=>{backItem.IsEnabled=history.CanBack;forwardItem.IsEnabled=history.CanForward;upItem.IsEnabled=DiskNavigationHistory.Parent(current)!=null;foreach(var item in fileActions)item.IsEnabled=selected!=null;};
        list.PreviewMouseRightButtonDown+=(s,e)=>{var target=e.OriginalSource as DependencyObject;if(target==null||ItemsControl.ContainerFromElement(list,target)==null)list.SelectedIndex=-1;};
        fileActions.Add(addMenu(T("Marcar para exclusão","Mark for deletion"),()=>{if(selected==null)return;if(!marked.Add(selected.Path))marked.Remove(selected.Path);refreshView();}));
        fileActions.Add(addMenu(T("Abrir pasta","Open folder"),()=>{if(selected==null)return;try{Process.Start(new ProcessStartInfo("explorer.exe","/select,"+Args.Q(selected.Path)){UseShellExecute=true});}catch(Exception ex){ShowToast("ERROR • "+ex.Message);}}));
        fileActions.Add(addMenu(T("Enviar à Lixeira","Move to Recycle Bin"),()=>{if(deleteSelected!=null)deleteSelected(false);}));
        fileActions.Add(addMenu(T("Excluir permanentemente","Delete permanently"),()=>{if(deleteSelected!=null)deleteSelected(true);}));
        tileMenu=menu;list.ContextMenu=menu;blocks.ContextMenu=menu;
        folderTree=new DiskTreeNavigator(folders,path=>FastDisk.Entries(path).Where(x=>x.Directory&&!x.Link).Select(x=>x.Path),path=>{if(scan!=null&&!String.Equals(path,current,StringComparison.OrdinalIgnoreCase))scan(path,true);});
        drive.SelectionChanged+=(s,e)=>{if(!changingDrive&&!folderTree.Synchronizing){folderTree.Reset(Convert.ToString(drive.SelectedItem));if(!preview&&scan!=null)scan(Convert.ToString(drive.SelectedItem),true);}};folderTree.Reset(Convert.ToString(drive.SelectedItem));
        scan=(path,record)=>{
            if(String.IsNullOrWhiteSpace(path)||!Directory.Exists(path))return;path=DiskNavigationHistory.Normalize(path);
            if(cancel!=null){pending=path;pendingRecord=record;cancel.Cancel();status.Text=T("Mudando de pasta…","Changing folder…");return;}
            string matchingDrive=drive.Items.Cast<string>().Where(x=>DiskNavigationHistory.Within(path,x)).OrderByDescending(x=>x.Length).FirstOrDefault();if(matchingDrive!=null&&!String.Equals(matchingDrive,Convert.ToString(drive.SelectedItem),StringComparison.OrdinalIgnoreCase)){changingDrive=true;drive.SelectedItem=matchingDrive;changingDrive=false;}history.Visit(path,record);current=history.Current;selected=null;folderTree.Synchronize(path,Convert.ToString(drive.SelectedItem));
            breadcrumb.Children.Clear();string root=Path.GetPathRoot(path),cursor=root;
            breadcrumb.Children.Add(Button(root,()=>scan(root,true)));
            foreach(string segment in path.Substring(root.Length).Split(new[]{'\\'},StringSplitOptions.RemoveEmptyEntries)){
                cursor=Path.Combine(cursor,segment);string jump=cursor;breadcrumb.Children.Add(Button("› "+segment,()=>scan(jump,true)));
            }
            List<DiskBucket> ready;if(visited.TryGetValue(path,out ready)){cacheHits++;shown=ready;status.Text=T("Leitura em cache • ","Cached view • ")+path;status.Foreground=StateGreen;refreshView();return;}
            scanCount++;cancel=new CancellationTokenSource();var token=cancel;int scanId=++version;
            status.Text=T("Analisando ","Scanning ")+path;status.Foreground=Muted;
            List<DiskBucket> found=null;
            Work(T("Mapear disco","Map disk")+": "+path,()=>{int job=workerJobId;found=DiskIndex.Scan(path,token.Token,(files,bytes)=>ReportStage(T("Mapeando disco • ","Mapping disk • ")+files+T(" arquivos • "," files • ")+(bytes/1073741824.0).ToString("0.0")+" GB",job),false);return "OK • "+found.Count+T(" itens analisados."," items scanned.");},r=>{
                if(scanId!=version)return;cancel=null;
                if(pending!=null){string next=pending;bool nextRecord=pendingRecord;pending=null;scan(next,nextRecord);return;}
                shown=found;if(found!=null)visited[path]=found;
                status.Text=path+"\n"+r+(found!=null&&found.Any(x=>x.Errors>0)?T(" • leitura parcial"," • partial scan"):"");status.Foreground=OutcomeBrush(r);
                refreshView();
            });
        };
        goBack=()=>{string target=history.Back();if(target!=null)scan(target,false);};goForward=()=>{string target=history.Forward();if(target!=null)scan(target,false);};goUp=()=>{string parent=DiskNavigationHistory.Parent(current);if(parent!=null)scan(parent,true);};
        Row(navButtons,Button(T("Analisar unidade","Analyze drive"),()=>scan(Convert.ToString(drive.SelectedItem),true)),Button(T("Voltar","Back"),()=>goBack()),Button(T("Avançar","Forward"),()=>goForward()),Button(T("Subir","Up"),()=>goUp()));
        deleteSelected=permanent=>{
            if(cancel!=null){ShowToast(T("Conclua ou cancele a análise antes de excluir.","Finish or cancel the scan before deleting."));return;}
            if(selected==null)return;
            if(!DiskScanner.CanRecycle(selected.Path)){ShowToast(T("ERROR • Caminho protegido ou indisponível.","ERROR • Protected or unavailable path."));return;}
            string path=selected.Path,folder=current;
            if(!Confirm((permanent?T("Excluir permanentemente? Esta ação não usa a Lixeira.\n","Delete permanently? This does not use the Recycle Bin.\n"):T("Enviar à Lixeira?\n","Move to Recycle Bin?\n"))+path))return;
            Work(permanent?T("Exclusão permanente","Permanent deletion"):T("Enviar à Lixeira","Recycle"),()=>{if(permanent)DiskScanner.DeletePermanent(path);else DiskScanner.Recycle(path);return "OK • "+path;},r=>{status.Text=r;status.Foreground=OutcomeBrush(r);visited.Clear();marked.Remove(path);scan(folder,false);});
        };
        deleteMarked=permanent=>{
            if(cancel!=null||marked.Count==0)return;
            string[] paths=marked.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
            if(paths.Length>20){ShowToast(T("Selecione até 20 itens por lote.","Select up to 20 items per batch."));return;}
            if(paths.Any(x=>!DiskScanner.CanRecycle(x))){ShowToast(T("ERROR • Há um caminho protegido ou indisponível na seleção.","ERROR • A protected or unavailable path is selected."));return;}
            if(!Confirm((permanent?T("Excluir permanentemente estes itens? Esta ação não usa a Lixeira.\n","Delete these items permanently? This does not use the Recycle Bin.\n"):T("Enviar estes itens à Lixeira?\n","Move these items to the Recycle Bin?\n"))+String.Join("\n",paths)))return;
            string folder=current;
            Work(permanent?T("Excluir itens marcados","Delete marked items"):T("Reciclar itens marcados","Recycle marked items"),()=>{var result=new List<string>();foreach(string path in paths)try{if(permanent)DiskScanner.DeletePermanent(path);else DiskScanner.Recycle(path);result.Add("OK • "+path);}catch(Exception ex){result.Add("ERROR • "+path+" • "+ex.Message);}return String.Join("\n",result);},r=>{status.Text=r;status.Foreground=OutcomeBrush(r);visited.Clear();marked.Clear();scan(folder,false);});
        };
        Row(card,
            Button(T("Cancelar","Cancel"),()=>{if(cancel!=null){pending=null;cancel.Cancel();status.Text=T("Cancelando análise…","Canceling scan…");}}),
            Button(T("Atualizar análise","Refresh scan"),()=>{visited.Clear();DiskIndex.Invalidate();scan(current??Convert.ToString(drive.SelectedItem),false);}),
            Button(T("Excluir selecionado","Delete selected"),()=>deleteSelected(false)),
            Button(T("Enviar marcados à Lixeira","Recycle marked"),()=>deleteMarked(false)),
            Button(T("Excluir marcados permanentemente","Delete marked permanently"),()=>deleteMarked(true)));
        diskBrowserNavigate=path=>scan(path,true);diskBrowserLocation=()=>current;diskBrowserBack=()=>goBack();diskBrowserScanCount=()=>scanCount;diskBrowserCacheHits=()=>cacheHits;
        if(preview){
            string fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostics","ui-disk-fixture");Directory.CreateDirectory(fixture);
            foreach(string folder in new[]{"Documentos","Imagens","Projetos"}){string child=Path.Combine(fixture,folder);Directory.CreateDirectory(child);string file=Path.Combine(child,"arquivo-de-exemplo.bin");if(!File.Exists(file))using(var stream=File.Create(file))stream.SetLength(folder=="Imagens"?50000:folder=="Projetos"?30000:10000);}
            string readme=Path.Combine(fixture,"Exemplo.txt");if(!File.Exists(readme))File.WriteAllText(readme,"Arquivos fictícios para conferir a interface do OuO.");
            foreach(string child in Directory.GetDirectories(fixture))visited[child]=DiskIndex.Scan(child,CancellationToken.None,null,false);visited[fixture]=DiskIndex.Scan(fixture,CancellationToken.None,null,false);drive.Items.Add(fixture);drive.SelectedItem=fixture;scan(fixture,false);status.Text=T("Demonstração da interface • arquivos fictícios","Interface demonstration • fixture files");
        }
    }
}
}
