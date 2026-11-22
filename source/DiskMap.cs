using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualBasic.FileIO;
using Path=System.IO.Path;

namespace Aurora {
public sealed class DiskBucket {
    public string Path; public long Bytes; public int Errors;public bool Folder;public DateTime Modified;
    public string SizeLabel{get{return Bytes>=1073741824?(Bytes/1073741824.0).ToString("0.00")+" GB":Bytes>=1048576?(Bytes/1048576.0).ToString("0.0")+" MB":Bytes>=1024?(Bytes/1024.0).ToString("0.0")+" KB":Bytes+" B";}}
    public override string ToString(){return Path+" • "+SizeLabel+(Errors>0?" • "+Errors+L.T(" inacessíveis"," inaccessible"):"");}
}
public static class DiskScanner {
    public static List<DiskBucket> Scan(string directory){return DiskIndex.Scan(directory,CancellationToken.None,null,false);}
    public static bool ReservedPath(string path){string full=Path.GetFullPath(path).TrimEnd('\\'),root=Path.GetPathRoot(full);foreach(string name in new[]{"Recovery","Boot","EFI","$Recycle.Bin","$Extend","System Volume Information","Config.Msi","Windows.old","$WINDOWS.~BT","$WINDOWS.~WS"}){string protectedPath=Path.Combine(root,name);if(full.Equals(protectedPath,StringComparison.OrdinalIgnoreCase)||full.StartsWith(protectedPath+"\\",StringComparison.OrdinalIgnoreCase))return true;}string relative=full.Substring(root.Length);return relative.StartsWith("$",StringComparison.Ordinal);}
    public static bool CanRecycle(string path){
        try{
            if(ReservedPath(path))return false;
            string full=Path.GetFullPath(path).TrimEnd((char)92),root=Path.GetPathRoot(full).TrimEnd((char)92);
            if(full.Equals(root,StringComparison.OrdinalIgnoreCase)||full.Equals(Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).TrimEnd((char)92),StringComparison.OrdinalIgnoreCase)||(!File.Exists(full)&&!Directory.Exists(full)))return false;
            string[] protectedRoots={Environment.GetFolderPath(Environment.SpecialFolder.Windows),Environment.GetFolderPath(Environment.SpecialFolder.System),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)};
            foreach(string candidate in protectedRoots.Where(x=>!String.IsNullOrWhiteSpace(x))){
                string p=Path.GetFullPath(candidate).TrimEnd((char)92);
                if(full.Equals(p,StringComparison.OrdinalIgnoreCase)||full.StartsWith(p+((char)92),StringComparison.OrdinalIgnoreCase)||p.StartsWith(full+((char)92),StringComparison.OrdinalIgnoreCase))return false;
            }
            string profile=Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).TrimEnd((char)92);
            if(profile.StartsWith(full+((char)92),StringComparison.OrdinalIgnoreCase)||String.Equals(Path.GetDirectoryName(full),Path.GetDirectoryName(profile),StringComparison.OrdinalIgnoreCase))return false;
            string leaf=Path.GetFileName(full);
            if(String.Equals(Path.GetDirectoryName(full),root+((char)92),StringComparison.OrdinalIgnoreCase)&&new[]{"Recovery","Boot","$Recycle.Bin","System Volume Information","Config.Msi"}.Contains(leaf,StringComparer.OrdinalIgnoreCase))return false;
            if((File.GetAttributes(full)&FileAttributes.System)!=0)return false;
            string cursor=Directory.Exists(full)?full:Path.GetDirectoryName(full);
            while(!String.IsNullOrWhiteSpace(cursor)&&cursor.Length>root.Length){
                if((new DirectoryInfo(cursor).Attributes&FileAttributes.ReparsePoint)!=0)return false;
                cursor=Path.GetDirectoryName(cursor);
            }
            return true;
        }catch{return false;}
    }
    public static void Recycle(string path){
        if(!CanRecycle(path))throw new InvalidOperationException(L.T("Caminho protegido ou indisponível.","Protected or unavailable path."));
        DiskIndex.Invalidate();if(Directory.Exists(path))FileSystem.DeleteDirectory(path,UIOption.OnlyErrorDialogs,RecycleOption.SendToRecycleBin);
        else FileSystem.DeleteFile(path,UIOption.OnlyErrorDialogs,RecycleOption.SendToRecycleBin);
    }
    public static void DeletePermanent(string path){
        if(!CanRecycle(path))throw new InvalidOperationException(L.T("Caminho protegido ou indisponível.","Protected or unavailable path."));
        string full=Path.GetFullPath(path);
        if(Directory.Exists(full)){CheckTree(full);DeleteTree(full,full);}else{if((File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new IOException("Links are not deleted permanently");File.Delete(full);}
        DiskIndex.Invalidate();
    }
    static void CheckTree(string root){
        var pending=new Stack<string>();pending.Push(root);int visited=0;
        while(pending.Count>0){string folder=pending.Pop();if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)throw new IOException("Folder contains a link; permanent deletion stopped");
            foreach(string entry in Directory.EnumerateFileSystemEntries(folder)){if(++visited>300000)throw new IOException("Folder is too large for guarded permanent deletion");
                var attr=File.GetAttributes(entry);if((attr&FileAttributes.ReparsePoint)!=0)throw new IOException("Folder contains a link; permanent deletion stopped");
                if((attr&FileAttributes.Directory)!=0)pending.Push(entry);
            }
        }
    }
    static void DeleteTree(string path,string root){
        string full=Path.GetFullPath(path);
        if(!full.Equals(root,StringComparison.OrdinalIgnoreCase)&&!full.StartsWith(root.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))throw new IOException("Path escaped selected folder");
        if((File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new IOException("Links are not deleted permanently");
        foreach(string entry in Directory.EnumerateFileSystemEntries(full)){
            FileAttributes attr=File.GetAttributes(entry);if((attr&FileAttributes.ReparsePoint)!=0)throw new IOException("Folder contains a link; permanent deletion stopped");
            if((attr&FileAttributes.Directory)!=0)DeleteTree(entry,root);else File.Delete(entry);
        }
        Directory.Delete(full,false);
    }
}
public partial class MainWindow {
    void LegacyDiskMapPage(){
        var card=Card(T("Mapa de espaço em disco","Disk space map"),T("Explore por tamanho. A exclusão envia o item escolhido à Lixeira após confirmação. Pastas do sistema ficam bloqueadas.","Browse by size. Deletion sends the chosen item to the Recycle Bin after confirmation. System folders are blocked."));
        Note(card,"A análise ignora links e mostra o tamanho lógico. Ao abrir subpastas, reutiliza a leitura por até dez minutos. Atualizar análise força uma nova leitura.","Scan skips links and shows logical sizes. Opening subfolders reuses data for up to ten minutes. Refresh scan forces a new read.");
        ComboBox drive=new ComboBox{Margin=new Thickness(0,0,0,8)};
        foreach(var d in DriveInfo.GetDrives())if(d.IsReady&&d.DriveType==DriveType.Fixed)drive.Items.Add(d.Name);
        if(drive.Items.Count>0)drive.SelectedIndex=0;card.Children.Add(drive);
        TextBlock location=Text("",13,StateGreen);card.Children.Add(location);
        Canvas map=new Canvas{Height=120,Background=Bg,Margin=new Thickness(0,0,0,8),ClipToBounds=true,Visibility=Visibility.Collapsed};card.Children.Add(map);
        ListBox biggest=new ListBox{Height=230,Background=Surface,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};card.Children.Add(biggest);
        string current=null;Action<string> analyze=null;CancellationTokenSource scanCancel=null;int scanVersion=0;string pendingPath=null;
        analyze=(path)=>{
            if(String.IsNullOrWhiteSpace(path))return;if(scanCancel!=null){pendingPath=path;scanCancel.Cancel();location.Text=T("Reiniciando a análise…","Restarting scan…");return;}scanCancel=new CancellationTokenSource();var cancel=scanCancel;int version=++scanVersion;current=path;location.Text=T("Analisando… ","Scanning… ")+path;
            List<DiskBucket> buckets=null;
            Work(T("Mapear disco","Map disk")+": "+path,()=>{int job=workerJobId;buckets=DiskIndex.Scan(path,cancel.Token,(files,bytes)=>ReportStage(T("Mapeando disco • ","Mapping disk • ")+files+T(" arquivos • "," files • ")+(bytes/1073741824.0).ToString("0.0")+" GB",job),false);return "OK • "+buckets.Count+T(" itens neste nível. Tamanhos lógicos; links não são percorridos."," items at this level. Logical sizes; links are not followed.");},r=>{
                if(version!=scanVersion)return;scanCancel=null;if(pendingPath!=null){string next=pendingPath;pendingPath=null;analyze(next);return;}location.Text=path+"\n"+r;biggest.Items.Clear();map.Children.Clear();biggest.Visibility=map.Visibility=buckets==null?Visibility.Collapsed:Visibility.Visible;
                if(buckets==null){location.Text=r;return;}
                foreach(var item in buckets.Take(100))biggest.Items.Add(item);
                double total=Math.Max(1,buckets.Sum(x=>(double)x.Bytes)),offset=0,width=Math.Max(200,map.ActualWidth);int index=0;
                foreach(var item in buckets.Take(18)){if(offset>=width)break;double w=Math.Max(2,Math.Min(width-offset,width*item.Bytes/total));
                    var color=(Color)ColorConverter.ConvertFromString(new[]{"#71DCC3","#83BBFF","#C4A4FF","#F5C976","#E78C9E"}[index++%5]);
                    var rect=new Rectangle{Width=w,Height=120,Fill=new SolidColorBrush(color),Stroke=Bg,StrokeThickness=2,ToolTip=item.ToString()};
                    Canvas.SetLeft(rect,offset);map.Children.Add(rect);offset+=w;
                }
                if(buckets.Any(x=>x.Errors>0))location.Text+=T(" • leitura parcial"," • partial scan");
            });
        };
        biggest.MouseDoubleClick+=(s,e)=>{var item=biggest.SelectedItem as DiskBucket;if(item!=null&&Directory.Exists(item.Path))analyze(item.Path);};
        Row(card,Button(T("Analisar unidade","Analyze drive"),()=>analyze(Convert.ToString(drive.SelectedItem))),
            Button(T("Cancelar análise","Cancel scan"),()=>{if(scanCancel!=null){pendingPath=null;scanCancel.Cancel();location.Text=T("Cancelando a leitura…","Canceling scan…");}}),
            Button(T("Atualizar análise","Refresh scan"),()=>{DiskIndex.Invalidate();analyze(current??Convert.ToString(drive.SelectedItem));}),
            Button(T("Voltar uma pasta","Up one folder"),()=>{if(current==null)return;string parent=Path.GetDirectoryName(current.TrimEnd((char)92));if(parent!=null)analyze(parent);}),
            Button(T("Enviar selecionado à Lixeira","Recycle selected"),()=>{if(scanCancel!=null){location.Text=T("Conclua ou cancele a análise antes de excluir um item.","Finish or cancel the scan before deleting an item.");return;}var item=biggest.SelectedItem as DiskBucket;if(item==null)return;if(!DiskScanner.CanRecycle(item.Path)){location.Text=T("ERROR • Este caminho está protegido ou não está disponível.","ERROR • This path is protected or unavailable.");return;}
                if(!Confirm(T("Enviar à Lixeira?","Send to Recycle Bin?")+"\n"+item.Path))return;
                string path=item.Path,folder=current;
                Work(T("Enviar à Lixeira","Move to Recycle Bin")+": "+path,()=>{DiskScanner.Recycle(path);return "OK • "+path;},r=>{location.Text=r;if(folder!=null)analyze(folder);});
            }));
    }
}
}
