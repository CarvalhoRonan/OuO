using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace Aurora {
public static class DiskMigration {
    public static string ValidateTarget(string target){
        if(!Regex.IsMatch(target??"",@"^[A-Za-z]:$"))throw new ArgumentException(L.T("Escolha uma unidade de destino válida.","Choose a valid target drive."));
        string source=Path.GetPathRoot(Environment.SystemDirectory).Substring(0,2).ToUpperInvariant();target=target.ToUpperInvariant();
        if(target==source)throw new InvalidOperationException(L.T("Origem e destino não podem ser iguais.","Source and target must differ."));
        var disks=StorageInventory.Read();var sourceDisk=disks.FirstOrDefault(x=>x.Volumes.Contains(source,StringComparer.OrdinalIgnoreCase));var targetDisk=disks.FirstOrDefault(x=>x.Volumes.Contains(target,StringComparer.OrdinalIgnoreCase));
        if(sourceDisk==null||targetDisk==null||Object.ReferenceEquals(sourceDisk,targetDisk))throw new InvalidOperationException(L.T("Não foi possível confirmar que o destino é outro disco físico.","Could not confirm the target is a different physical disk."));
        var drive=new DriveInfo(target+"\\");if(!drive.IsReady||drive.DriveFormat!="NTFS")throw new InvalidOperationException(L.T("O destino precisa estar pronto e formatado em NTFS.","The destination must be ready and formatted as NTFS."));
        var sourceDrive=new DriveInfo(source+"\\");long used=sourceDrive.TotalSize-sourceDrive.AvailableFreeSpace;
        if(drive.AvailableFreeSpace<used+5L*1024*1024*1024)throw new InvalidOperationException(L.T("O destino parece não ter espaço suficiente para a imagem do sistema.","The target appears to lack space for the system image."));
        return target;
    }
    public static string ImageBackup(string target){
        target=ValidateTarget(target);
        var result=Runner.Run("wbadmin.exe","start backup -backupTarget:"+target+" -include:"+Path.GetPathRoot(Environment.SystemDirectory).Substring(0,2)+" -allCritical -vssCopy -quiet",Int32.MaxValue);
        return (result.Code==0?"OK":"ERROR")+" • "+L.T("Imagem do sistema em ","System image on ")+target+"\n"+result.Output;
    }
}
public partial class MainWindow {
    void DiskMigrationCard(){
        var card=Card(T("Imagem do sistema","System image"),T("Crie uma imagem de recuperação em outro disco físico com a ferramenta do Windows. A clonagem direta está no painel próprio abaixo.","Create a recovery image on a different physical drive with Windows tooling. Direct cloning is in its own panel below."));
        var drives=new ComboBox{Margin=new System.Windows.Thickness(0,0,0,10)};card.Children.Add(drives);
        Action refresh=()=>{drives.Items.Clear();foreach(var drive in DriveInfo.GetDrives())try{if(drive.IsReady&&drive.DriveType==DriveType.Fixed&&drive.Name!=Path.GetPathRoot(Environment.SystemDirectory))drives.Items.Add(drive.Name.Substring(0,2));}catch{}if(drives.Items.Count>0)drives.SelectedIndex=0;};refresh();
        TextBlock state=Text("",13,Muted);card.Children.Add(state);
        Row(card,Button(T("Checar destino","Check target"),()=>{string selected=Convert.ToString(drives.SelectedItem);Work(T("Checar destino","Check target"),()=>{string target=DiskMigration.ValidateTarget(selected);return "OK • "+T("Destino em outro disco físico, NTFS e com espaço estimado suficiente: ","Destination on another physical drive, NTFS, with estimated free space: ")+target;},r=>{state.Text=r;state.Foreground=OutcomeBrush(r);});}),Button(T("Criar imagem do sistema","Create system image"),()=>{
            if(!RequireAdmin()||drives.SelectedItem==null)return;string selected=Convert.ToString(drives.SelectedItem);
            if(!Confirm(T("Criar uma imagem do sistema em ","Create a system image on ")+selected+T("? O backup pode demorar e ocupar muito espaço. O OuO verificará se é outro disco antes de iniciar.","? Backup may take time and use substantial space. OuO will verify it is a different physical drive first.")))return;
            Work(T("Criar imagem do sistema","Create system image"),()=>DiskMigration.ImageBackup(selected),r=>{state.Text=r;state.Foreground=OutcomeBrush(r);});
        }));
        Note(card,"A imagem do sistema permite recuperação, mas não torna o disco de destino inicializável por si só. Clonagem direta deve ser feita fora do Windows em uso, com confirmação visual de origem e destino.","A system image supports recovery but does not make the target drive bootable by itself. Direct cloning should run outside the active Windows installation, with source and target visually confirmed.");
    }
}
}
