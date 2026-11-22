using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32.SafeHandles;
namespace Aurora {
public sealed class CloneDisk {
    public int Number,Sector;public long Size;public string Model="",Serial="",Identity="",HandleIdentity="";public bool Protected,Offline,AttributesKnown,ReadOnly;
    public readonly List<string> Volumes=new List<string>();
    public string Fingerprint{get{return Number+"|"+Model.Trim()+"|"+Serial.Trim()+"|"+Size+"|"+Identity+"|"+HandleIdentity;}}
    public string DisplayIdentity{get{return !String.IsNullOrWhiteSpace(Serial)?Serial.Trim():String.IsNullOrWhiteSpace(HandleIdentity)?L.T("Não confirmada","Not confirmed"):HandleIdentity;}}
    public override string ToString(){return L.T("Disco ","Disk ")+Number+" • "+Model+" • "+(Size/1073741824.0).ToString("0.0")+" GB"+(Protected?L.T(" • em uso pelo sistema"," • used by system"):Offline?" • offline":" • online");}
}
public static class DirectClone {
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool DeviceIoControl(SafeFileHandle file,uint control,IntPtr input,int inputSize,byte[] output,int outputSize,out int returned,IntPtr overlapped);
    public static string DescriptorSerial(byte[] descriptor,int returned){if(descriptor==null||returned<28||returned>descriptor.Length)throw new InvalidDataException("Incomplete storage descriptor");int at=BitConverter.ToInt32(descriptor,24);if(at<28||at>=returned)throw new InvalidDataException("Storage descriptor has no hardware serial");int end=at;while(end<returned&&descriptor[end]!=0)end++;if(end==returned)throw new InvalidDataException("Unterminated storage serial");return Encoding.ASCII.GetString(descriptor,at,end-at).Trim();}
    static void ConfirmHandle(SafeFileHandle handle,CloneDisk selected){string identity=DiskBinding.Read(handle,selected.Number);if(String.IsNullOrWhiteSpace(selected.HandleIdentity)||identity!=selected.HandleIdentity)throw new IOException(L.T("A identidade do disco aberto não corresponde à seleção. A cópia não foi iniciada.","Opened disk identity does not match the selection. Copy was not started."));long length=BitConverter.ToInt64(Io(handle,0x7405c,8),0);if(length!=selected.Size)throw new IOException("Opened disk capacity changed");}
    public static string PathFor(int number){if(number<0||number>1023)throw new ArgumentOutOfRangeException("number");return @"\\.\PhysicalDrive"+number;}
    static SafeFileHandle Open(int number,bool write){var handle=CreateFile(PathFor(number),write?0xc0000000u:0x80000000u,3,IntPtr.Zero,3,write?0x80000000u:0,IntPtr.Zero);if(handle.IsInvalid){int code=Marshal.GetLastWin32Error();handle.Dispose();throw new Win32Exception(code);}return handle;}
    static byte[] Io(SafeFileHandle file,uint code,int size){var bytes=new byte[size];int returned;if(!DeviceIoControl(file,code,IntPtr.Zero,0,bytes,size,out returned,IntPtr.Zero))throw new Win32Exception(Marshal.GetLastWin32Error());if(returned<size&&size<=24)throw new IOException("Incomplete disk information");return bytes;}
    static void Inspect(CloneDisk disk){using(var file=Open(disk.Number,false)){
        disk.Size=BitConverter.ToInt64(Io(file,0x7405c,8),0);var geometry=Io(file,0x700a0,256);disk.Sector=BitConverter.ToInt32(geometry,20);
        try{disk.HandleIdentity=DiskBinding.Read(file,disk.Number);}catch{disk.HandleIdentity="";}
        try{ulong attributes=BitConverter.ToUInt64(Io(file,0x700f0,16),8);disk.AttributesKnown=true;disk.Offline=(attributes&1)!=0;disk.ReadOnly=(attributes&2)!=0;}catch{disk.AttributesKnown=false;}
    }}
    public static List<CloneDisk> Inventory(){
        string[] protect={System.IO.Path.GetPathRoot(Environment.SystemDirectory),System.IO.Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory),System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))};
        var result=new List<CloneDisk>();foreach(ManagementObject raw in Machine.Query("root\\cimv2","SELECT Index,Model,SerialNumber,Size,BytesPerSector,DeviceID FROM Win32_DiskDrive"))using(raw){
            int number,sector;long size;if(!Int32.TryParse(Machine.Value(raw,"Index"),out number)||!Int64.TryParse(Machine.Value(raw,"Size"),out size))continue;Int32.TryParse(Machine.Value(raw,"BytesPerSector"),out sector);
            var disk=new CloneDisk{Number=number,Model=Machine.Value(raw,"Model"),Serial=Machine.Value(raw,"SerialNumber"),Size=size,Sector=sector};
            try{foreach(ManagementObject partition in raw.GetRelated("Win32_DiskPartition"))using(partition)foreach(ManagementObject volume in partition.GetRelated("Win32_LogicalDisk"))using(volume){string drive=Machine.Value(volume,"DeviceID")+"\\";disk.Volumes.Add(drive);if(protect.Any(p=>String.Equals(p,drive,StringComparison.OrdinalIgnoreCase)))disk.Protected=true;}}catch{disk.Protected=true;}
            try{Inspect(disk);}catch{}result.Add(disk);
        }
        try{foreach(ManagementObject value in Machine.Query("root\\Microsoft\\Windows\\Storage","SELECT Number,IsBoot,IsSystem,UniqueId FROM MSFT_Disk"))using(value){int number;if(!Int32.TryParse(Machine.Value(value,"Number"),out number))continue;var disk=result.FirstOrDefault(x=>x.Number==number);if(disk==null)continue;disk.Identity=Machine.Value(value,"UniqueId");if(Machine.Value(value,"IsBoot")=="True"||Machine.Value(value,"IsSystem")=="True")disk.Protected=true;}}catch{}
        return result.OrderBy(x=>x.Number).ToList();
    }
    static CloneDisk Recheck(CloneDisk selected){if(selected==null)throw new ArgumentException("Select a disk");var current=Inventory().FirstOrDefault(x=>x.Number==selected.Number);if(current==null||current.Fingerprint!=selected.Fingerprint)throw new IOException(L.T("A identidade do disco mudou. Atualize a lista e selecione novamente.","Disk identity changed. Refresh and select again."));if(String.IsNullOrWhiteSpace(current.HandleIdentity))throw new IOException(L.T("Não foi possível confirmar a identidade da unidade pelo Windows. Atualize a lista como administrador.","Windows could not confirm the drive identity. Refresh as administrator."));return current;}
    public static void Validate(CloneDisk source,CloneDisk target,bool requireOffline){
        if(source==null||target==null||source.Number==target.Number)throw new InvalidOperationException(L.T("Escolha dois discos físicos diferentes.","Choose two different physical disks."));
        if(!String.IsNullOrWhiteSpace(source.HandleIdentity)&&source.HandleIdentity==target.HandleIdentity)throw new InvalidOperationException(L.T("As identidades confirmadas pelo Windows são iguais. Selecione dois discos distintos.","Windows returned the same identity for both selections. Select two distinct disks."));
        if(!String.IsNullOrWhiteSpace(source.Identity)&&String.Equals(source.Identity,target.Identity,StringComparison.OrdinalIgnoreCase)||!String.IsNullOrWhiteSpace(source.Serial)&&String.Equals(source.Serial,target.Serial,StringComparison.OrdinalIgnoreCase)&&String.Equals(source.Model,target.Model,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(L.T("As identidades de hardware são iguais ou ambíguas. Não é possível confirmar dois discos diferentes.","Hardware identities are identical or ambiguous. Two different disks cannot be confirmed."));
        if(source.Protected||target.Protected)throw new InvalidOperationException(L.T("O disco do Windows em uso, do OuO ou do histórico está protegido. Inicie por outro sistema para cloná-lo.","The active Windows, OuO or history disk is protected. Boot another system to clone it."));
        if(source.Size<=0||target.Size<source.Size)throw new InvalidOperationException(L.T("O destino precisa ter capacidade igual ou maior que toda a origem.","Target capacity must be at least the entire source size."));
        if((source.Sector!=512&&source.Sector!=4096)||source.Sector!=target.Sector||source.Size%source.Sector!=0||target.Size%source.Sector!=0)throw new InvalidOperationException(L.T("As unidades precisam usar o mesmo tamanho de setor lógico (512 ou 4096 bytes).","Drives must use the same logical sector size (512 or 4096 bytes)."));
        if(target.ReadOnly)throw new InvalidOperationException(L.T("O destino está protegido contra gravação.","Target is write-protected."));
        if(requireOffline&&(!source.AttributesKnown||!target.AttributesKnown||!source.Offline||!target.Offline))throw new InvalidOperationException(L.T("Prepare os dois discos como offline antes de iniciar.","Prepare both disks offline before starting."));
    }
    public static string SetOffline(CloneDisk selected,bool offline){
        var disk=Recheck(selected);if(disk.Protected)throw new InvalidOperationException(L.T("Disco em uso pelo sistema ou OuO não pode ser desconectado.","Disk used by Windows or OuO cannot be taken offline."));
        DailyMaintenance.ProtectRoot();string script=System.IO.Path.Combine(DailyMaintenance.Root,"clone-"+Guid.NewGuid().ToString("N")+".txt");
        try{File.WriteAllText(script,"select disk "+disk.Number+Environment.NewLine+(offline?"offline disk":"online disk")+Environment.NewLine+"exit"+Environment.NewLine,Encoding.ASCII);var operation=Runner.Run("diskpart.exe","/s "+Args.Q(script),60000);Inspect(disk);if(!disk.AttributesKnown||disk.Offline!=offline)throw new IOException(operation.Output);return "OK • "+disk+"\n"+operation.Output;}finally{try{File.Delete(script);}catch{}}
    }
    public static string Copy(CloneDisk selectedSource,CloneDisk selectedTarget,int bufferMb,bool verify,CancellationToken token,Action<CloneProgress> progress){
        var source=Recheck(selectedSource);var target=Recheck(selectedTarget);Validate(source,target,true);
        using(var sourceHandle=Open(source.Number,false))using(var targetHandle=Open(target.Number,true)){
            ConfirmHandle(sourceHandle,source);ConfirmHandle(targetHandle,target);
            Inspect(source);Inspect(target);Validate(source,target,true);
            // Raw disks reject reads beyond their last sector. A megabyte-sized
            // FileStream read-ahead buffer can overrun it when verifying the
            // backup GPT. Bulk transfer buffers remain controlled by CloneCore.
            using(var input=new FileStream(sourceHandle,FileAccess.Read,source.Sector,false))using(var output=new FileStream(targetHandle,FileAccess.ReadWrite,target.Sector,false)){
                CloneResult result=CloneCore.Copy(input,output,source.Size,target.Size,source.Sector,bufferMb,verify,true,token,progress);
                return "OK • "+L.T("Clonagem concluída. O destino permanece offline.","Cloning completed. Target remains offline.")+"\n"+L.T("Origem: ","Source: ")+source+"\n"+L.T("Destino: ","Target: ")+target+"\nSHA-256: "+result.SourceHash+"\n"+(result.Verified?L.T("Todos os bytes copiados foram conferidos antes do ajuste GPT.","All copied bytes were verified before GPT adjustment."):L.T("Conferência de leitura não selecionada.","Read-back verification was not selected."))+(result.GptRelocated?"\n"+L.T("GPT secundária reposicionada no final do destino maior. As partições mantêm seus tamanhos; o restante fica livre.","Backup GPT relocated to the larger target's end. Partitions retain their sizes; remaining space is unallocated."):"")+"\n"+L.T("Tempo: ","Time: ")+result.Elapsed.ToString(@"hh\:mm\:ss")+"\n"+L.T("Desconecte uma das cópias antes de colocar a outra online, pois seus identificadores de disco e volume são iguais.","Disconnect one copy before bringing the other online because disk and volume identifiers match.");
            }
        }
    }
}
public partial class MainWindow {
    void DirectCloneCard(){
        var card=Card(T("Clonagem direta de disco","Direct disk cloning"),T("Cópia setor a setor entre discos físicos, incluindo partições e boot. Os dados do destino serão perdidos. Os dois discos devem estar offline; o Windows em execução não pode ser a origem.","Sector-by-sector physical disk copy, including partitions and boot. Target data will be lost. Both disks must be offline; running Windows cannot be the source."));
        var pickers=new Grid();pickers.ColumnDefinitions.Add(new ColumnDefinition());pickers.ColumnDefinitions.Add(new ColumnDefinition());card.Children.Add(pickers);
        StackPanel left=new StackPanel{Margin=new Thickness(0,0,12,0)},right=new StackPanel();pickers.Children.Add(left);Grid.SetColumn(right,1);pickers.Children.Add(right);
        left.Children.Add(Text(T("Disco de origem","Source disk"),13,Ink));right.Children.Add(Text(T("Disco de destino — será apagado","Target disk — will be erased"),13,Ink));
        ComboBox origin=new ComboBox{Margin=new Thickness(0,0,0,8)},destination=new ComboBox{Margin=new Thickness(0,0,0,8)};left.Children.Add(origin);right.Children.Add(destination);
        TextBlock sourceInfo=Text("",12,Muted),targetInfo=Text("",12,Muted);left.Children.Add(sourceInfo);right.Children.Add(targetInfo);
        Action<ComboBox,TextBlock> describe=(box,label)=>{var disk=box.SelectedItem as CloneDisk;label.Text=disk==null?"":T("Identificação: ","Identifier: ")+disk.DisplayIdentity+"\n"+T("Setor lógico: ","Logical sector: ")+disk.Sector+" B\n"+String.Join(", ",disk.Volumes);};origin.SelectionChanged+=(s,e)=>describe(origin,sourceInfo);destination.SelectionChanged+=(s,e)=>describe(destination,targetInfo);
        var verify=Choice(card,T("Conferir toda a cópia por SHA-256 (recomendado)","Verify the entire copy with SHA-256 (recommended)"),true,true);
        var buffer=new ComboBox{Width=180,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,8)};foreach(int size in new[]{4,16,64})buffer.Items.Add(size);buffer.SelectedIndex=1;card.Children.Add(Text(T("Buffer de cópia (MB)","Copy buffer (MB)"),12,Muted));card.Children.Add(buffer);
        var typed=Field(card,T("Para confirmar, digite APAGAR DISCO seguido do número do destino","To confirm, type ERASE DISK followed by the target number"),"");
        var state=Text(T("Atualize a lista para selecionar os discos.","Refresh the list to select disks."),13,Muted);card.Children.Add(state);var progress=new ProgressBar{Height=7,Minimum=0,Maximum=100,Foreground=Accent,Background=Bg,Visibility=Visibility.Collapsed};card.Children.Add(progress);
        TextBox report=Input("");report.IsReadOnly=true;report.AcceptsReturn=true;report.Height=160;report.TextWrapping=TextWrapping.Wrap;report.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;AttachResult(card,report);
        CancellationTokenSource cancellation=null;
        Action refresh=()=>{CloneDisk oldSource=origin.SelectedItem as CloneDisk,oldTarget=destination.SelectedItem as CloneDisk;List<CloneDisk> disks=null;Work(T("Listar discos de clonagem","List cloning disks"),()=>{disks=DirectClone.Inventory();return "OK • "+disks.Count+T(" disco(s) identificado(s)."," disk(s) identified.");},r=>{origin.Items.Clear();destination.Items.Clear();foreach(var disk in disks??new List<CloneDisk>()){origin.Items.Add(disk);destination.Items.Add(disk);if(oldSource!=null&&oldSource.Fingerprint==disk.Fingerprint)origin.SelectedItem=disk;if(oldTarget!=null&&oldTarget.Fingerprint==disk.Fingerprint)destination.SelectedItem=disk;}state.Text=r;state.Foreground=OutcomeBrush(r);});};
        Action<bool,bool> prepare=(isSource,offline)=>{var disk=(isSource?origin:destination).SelectedItem as CloneDisk;if(disk==null||cancellation!=null||!RequireAdmin())return;if(!Confirm((offline?T("Colocar este disco offline? Seus volumes ficarão temporariamente inacessíveis.\n","Take this disk offline? Its volumes will be temporarily unavailable.\n"):T("Reativar a origem? Mantenha o disco clonado offline ou desconectado.\n","Bring the source online? Keep its clone offline or disconnected.\n"))+disk))return;Work(T("Preparar disco de clonagem","Prepare cloning disk"),()=>DirectClone.SetOffline(disk,offline),r=>{report.Text=r;refresh();});};
        Row(card,Button(T("Atualizar discos","Refresh disks"),()=>refresh()),Button(T("Preparar origem offline","Prepare source offline"),()=>prepare(true,true)),Button(T("Preparar destino offline","Prepare target offline"),()=>prepare(false,true)),Button(T("Reativar origem","Bring source online"),()=>prepare(true,false)),Button(T("Clonar agora","Clone now"),()=>{
            var source=origin.SelectedItem as CloneDisk;var target=destination.SelectedItem as CloneDisk;if(cancellation!=null||source==null||target==null||!RequireAdmin())return;
            try{DirectClone.Validate(source,target,true);}catch(Exception ex){ShowNotice(ex.Message);return;}
            string phrase=T("APAGAR DISCO ","ERASE DISK ")+target.Number;if(!typed.Text.Trim().Equals(phrase,StringComparison.Ordinal)){ShowNotice(T("Confirme digitando exatamente: ","Confirm by typing exactly: ")+phrase);return;}
            if(!Confirm(T("Sobrescrever integralmente o destino?\nORIGEM: ","Overwrite the entire target?\nSOURCE: ")+source+"\n"+T("Identificação: ","Identifier: ")+source.DisplayIdentity+"\n\n"+T("DESTINO: ","TARGET: ")+target+"\n"+T("Identificação: ","Identifier: ")+target.DisplayIdentity+T("\n\nCancelar não restaura os dados anteriores do destino.","\n\nCanceling cannot restore the target's previous data.")))return;
            bool check=verify.IsChecked==true;int mb=(int)buffer.SelectedItem;cancellation=new CancellationTokenSource();var token=cancellation.Token;progress.Visibility=Visibility.Visible;progress.Value=0;var watch=Stopwatch.StartNew();long previous=-1000;bool finished=false;
            Work(T("Clonar disco","Clone disk"),()=>{int job=workerJobId;string cloned=DirectClone.Copy(source,target,mb,check,token,p=>{if(watch.ElapsedMilliseconds-previous<250&&p.Done<p.Total)return;previous=watch.ElapsedMilliseconds;string title=p.Phase+" • "+p.Percent.ToString("0")+"%";ReportStage(title,job);Dispatcher.BeginInvoke(new Action(()=>{progress.Value=p.Percent;state.Text=title;state.Foreground=Accent;}));});finished=true;return cloned;},r=>{cancellation.Dispose();cancellation=null;report.Text=r;if(token.IsCancellationRequested&&!finished){report.Text=T("Cancelado. O destino pode conter uma cópia parcial e deve continuar offline.","Canceled. Target may contain a partial copy and must remain offline.");}state.Text=report.Text.Split('\n')[0];state.Foreground=OutcomeBrush(report.Text);typed.Clear();});
        }),Button(T("Cancelar clonagem","Cancel cloning"),()=>{if(cancellation!=null)cancellation.Cancel();}));
        if(!preview)refresh();
    }
}
}
