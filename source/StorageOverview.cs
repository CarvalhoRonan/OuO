using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Aurora {
public sealed class StorageDevice {
    public string Model="",Manufacturer="",Firmware="",Interface="",Status="",Temperature="",Wear="",PowerOnHours="",ReadErrors="",WriteErrors="",PhysicalNumber="",CapacityBytes="",Serial="";
    public readonly List<string> Volumes=new List<string>();
}
public static class StorageInventory {
    public static List<StorageDevice> Read(){
        var items=new List<StorageDevice>();
        foreach(ManagementObject disk in Machine.Query("root\\cimv2","SELECT Model,Manufacturer,FirmwareRevision,InterfaceType,Status,DeviceID,Size,SerialNumber FROM Win32_DiskDrive"))using(disk){
            var item=new StorageDevice{Model=Machine.Value(disk,"Model"),Manufacturer=Machine.Value(disk,"Manufacturer"),Firmware=Machine.Value(disk,"FirmwareRevision"),Interface=Machine.Value(disk,"InterfaceType"),Status=Machine.Value(disk,"Status"),CapacityBytes=Machine.Value(disk,"Size"),Serial=Machine.Value(disk,"SerialNumber")};
            var number=System.Text.RegularExpressions.Regex.Match(Machine.Value(disk,"DeviceID"),@"PHYSICALDRIVE(\d+)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if(number.Success)item.PhysicalNumber=number.Groups[1].Value;
            try{foreach(ManagementObject partition in disk.GetRelated("Win32_DiskPartition"))using(partition)
                foreach(ManagementObject volume in partition.GetRelated("Win32_LogicalDisk"))using(volume){string letter=Machine.Value(volume,"DeviceID");if(letter.Length>0&&!item.Volumes.Contains(letter))item.Volumes.Add(letter);}}
            catch{}
            items.Add(item);
        }
        try{
            foreach(ManagementObject counter in Machine.Query("root\\Microsoft\\Windows\\Storage","SELECT DeviceId,Temperature,Wear,PowerOnHours,ReadErrorsUncorrected,WriteErrorsUncorrected FROM MSFT_StorageReliabilityCounter"))using(counter){
                var item=items.FirstOrDefault(x=>x.PhysicalNumber==Machine.Value(counter,"DeviceId"));if(item==null)continue;
                item.Temperature=Machine.Value(counter,"Temperature");
                item.Wear=Machine.Value(counter,"Wear");
                item.PowerOnHours=Machine.Value(counter,"PowerOnHours");
                item.ReadErrors=Machine.Value(counter,"ReadErrorsUncorrected");
                item.WriteErrors=Machine.Value(counter,"WriteErrorsUncorrected");
            }
        }catch{}
        return items;
    }
    public static string Trim(){
        try{var result=Runner.Run("fsutil.exe","behavior query DisableDeleteNotify",15000);if(result.Code!=0)return L.T("Indisponível","Unavailable");
            if(System.Text.RegularExpressions.Regex.IsMatch(result.Output,@"DisableDeleteNotify\s*=\s*0"))return L.T("Ativado (global)","Enabled (global)");
            if(System.Text.RegularExpressions.Regex.IsMatch(result.Output,@"DisableDeleteNotify\s*=\s*1"))return L.T("Desativado (global)","Disabled (global)");
        }catch{}return L.T("Não identificado","Unknown");
    }
    public static string MatchTemperature(StorageDevice disk,SensorSnapshot sample,int diskCount){
        if(sample==null||sample.StorageTemperatures.Count==0)return L.T("Sensor indisponível","Sensor unavailable");
        string model=(disk.Model??"").Trim();
        foreach(var pair in sample.StorageTemperatures)if(model.Length>4&&(pair.Key.IndexOf(model,StringComparison.OrdinalIgnoreCase)>=0||model.IndexOf(pair.Key,StringComparison.OrdinalIgnoreCase)>=0))return pair.Value;
        return diskCount==1&&sample.StorageTemperatures.Count==1?sample.StorageTemperatures.Values.First():L.T("Sem correspondência confiável","No reliable match");
    }
}
public partial class MainWindow {
    Grid StorageDonut(double used){
        used=Math.Max(0,Math.Min(100,used));
        var ring=new Grid{Width=86,Height=86,ToolTip=used.ToString("0")+T("% do volume utilizado","% of volume used")};
        ring.Children.Add(new Ellipse{Width=76,Height=76,Stroke=Color(dark?"#354257":"#D8E0ED"),StrokeThickness=10});
        var active=new Ellipse{Width=76,Height=76,Stroke=Accent,StrokeThickness=10,StrokeDashArray=new DoubleCollection{Math.Max(.01,23.88*used/100),Math.Max(.01,23.88*(100-used)/100)},RenderTransform=new RotateTransform(-90,38,38)};
        ring.Children.Add(active);
        ring.Children.Add(new TextBlock{Text=used.ToString("0")+"%",Foreground=Ink,FontSize=16,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});
        return ring;
    }
    void StorageDashboard(){
        var card=Card(T("Estado das unidades","Drive status"),T("Identificação do hardware e uso dos volumes. Saúde e temperatura dependem dos sensores expostos pelo dispositivo.","Hardware identity and volume usage. Health and temperature depend on sensors exposed by the device."));
        var content=new CardColumns{MaxColumns=2};card.Children.Add(content);
        TextBlock state=Text(T("Carregando unidades…","Loading drives…"),13,Muted);content.Children.Add(state);
        Action refresh=()=>{
            List<StorageDevice> disks=null;string trim=null;
            Work(T("Consultar armazenamento","Inspect storage"),()=>{disks=StorageInventory.Read();trim=StorageInventory.Trim();return "OK • "+disks.Count+T(" unidade(s) encontrada(s)."," drive(s) found.");},result=>{
                content.Children.Clear();if(disks==null){content.Children.Add(Text(result,13,OutcomeBrush(result)));return;}
                SensorSnapshot sample=sensors.Snapshot;
                foreach(StorageDevice disk in disks){
                    Border frame=new Border{Background=Bg,CornerRadius=new CornerRadius(7),Padding=new Thickness(12),Margin=new Thickness(0,3,0,10)};
                    StackPanel details=new StackPanel();frame.Child=details;content.Children.Add(frame);
                    details.Children.Add(Text(String.IsNullOrWhiteSpace(disk.Model)?T("Modelo não identificado","Model unknown"):disk.Model,16,Ink));
                    details.Children.Add(Text(T("Fabricante: ","Manufacturer: ")+(String.IsNullOrWhiteSpace(disk.Manufacturer)?"—":disk.Manufacturer)+"  •  "+T("Firmware: ","Firmware: ")+(String.IsNullOrWhiteSpace(disk.Firmware)?"—":disk.Firmware),12,Muted));
                    double capacity;if(Double.TryParse(disk.CapacityBytes,System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out capacity))details.Children.Add(Text(T("Capacidade física: ","Physical capacity: ")+(capacity/1073741824.0).ToString("0.0")+" GB",12,Muted));
                    if(!String.IsNullOrWhiteSpace(disk.Serial))details.Children.Add(Text(T("Número de série: ","Serial number: ")+disk.Serial.Trim(),12,Muted));
                    details.Children.Add(Text(T("Interface: ","Interface: ")+(String.IsNullOrWhiteSpace(disk.Interface)?"—":disk.Interface)+"  •  "+T("Estado informado pelo Windows: ","Windows reported state: ")+(String.IsNullOrWhiteSpace(disk.Status)?"—":disk.Status),12,disk.Status.Equals("OK",StringComparison.OrdinalIgnoreCase)?StateGreen:Muted));
                    string sensor=StorageInventory.MatchTemperature(disk,sample,disks.Count);
                    string temperature=sensor.Contains("°C")?sensor:!String.IsNullOrWhiteSpace(disk.Temperature)&&disk.Temperature!="0"?disk.Temperature+" °C":sensor;
                    details.Children.Add(Text(T("Temperatura: ","Temperature: ")+temperature,12,Muted));details.Children.Add(Text("TRIM: "+trim,12,OutcomeBrush(trim)));
                    if(!String.IsNullOrWhiteSpace(disk.PowerOnHours))details.Children.Add(Text(T("Horas ligadas: ","Power-on hours: ")+disk.PowerOnHours,12,Muted));
                    int wear;if(Int32.TryParse(disk.Wear,out wear)&&wear>=0&&wear<=100)details.Children.Add(Text(T("Desgaste informado: ","Reported wear: ")+wear+"%",12,Muted));
                    ulong readErrors,writeErrors;bool hasErrors=(UInt64.TryParse(disk.ReadErrors,out readErrors)&&readErrors>0)|(UInt64.TryParse(disk.WriteErrors,out writeErrors)&&writeErrors>0);
                    if(!String.IsNullOrWhiteSpace(disk.ReadErrors)||!String.IsNullOrWhiteSpace(disk.WriteErrors))details.Children.Add(Text(T("Erros não corrigidos • leitura: ","Uncorrected errors • read: ")+(disk.ReadErrors==""?"—":disk.ReadErrors)+T(" • gravação: "," • write: ")+(disk.WriteErrors==""?"—":disk.WriteErrors),12,hasErrors?OutcomeBrush("ERROR"):Muted));
                    foreach(string letter in disk.Volumes){try{var volume=new DriveInfo(letter);if(!volume.IsReady||volume.TotalSize<=0)continue;double used=100.0*(volume.TotalSize-volume.AvailableFreeSpace)/volume.TotalSize;
                        var volumeRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,3,0,12)};
                        volumeRow.Children.Add(StorageDonut(used));
                        var volumeInfo=new StackPanel{Margin=new Thickness(12,12,0,0)};
                        volumeInfo.Children.Add(Text(letter+"  "+used.ToString("0")+T("% usado","% used"),13,Ink));
                        volumeInfo.Children.Add(Text((volume.AvailableFreeSpace/1073741824.0).ToString("0.0")+T(" GB livres de "," GB free of ")+(volume.TotalSize/1073741824.0).ToString("0.0")+" GB",12,Muted));
                        try{volumeInfo.Children.Add(Text(T("Sistema de arquivos: ","File system: ")+volume.DriveFormat,12,Muted));}catch{}
                        volumeRow.Children.Add(volumeInfo);details.Children.Add(volumeRow);
                    }catch{}}
                }
                if(disks.Count==0)content.Children.Add(Text(T("Nenhuma unidade física foi identificada.","No physical drive identified."),13,Muted));
            });
        };
        Row(card,Button(T("Atualizar estado","Refresh status"),()=>refresh()));pageRefreshActions["storage"]=refresh;
        if(!preview)refresh();else state.Text=T("A leitura de hardware não é executada na prévia.","Hardware scan is not run in preview.");
    }
}
}
