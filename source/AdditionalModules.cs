using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Aurora {
public sealed class DriverRecord {public string Name,Provider,Version,Date,DeviceId;public string[] HardwareIds=new string[0];public override string ToString(){return Name+" • "+Provider+" • "+Version;}}
public partial class MainWindow {
    string AccentHex(){
        switch(accentName){
            case "azul": return dark?"#83BBFF":"#2257A6";
            case "violeta": return dark?"#C4A4FF":"#7042B2";
            case "ambar": return dark?"#F5C976":"#895710";
            case "cinza": return dark?"#BFC9D7":"#46576D";
            default: return dark?"#71DCC3":"#087963";
        }
    }
    void SetAccent(string name){
        ChangeAppearance(()=>{accentName=name;
        if(!preview)try{using(var key=Registry.CurrentUser.CreateSubKey("Software\\OuO\\Appearance"))key.SetValue("Accent",name,RegistryValueKind.String);}catch(Exception e){ShowNotice(e.Message);}
        });
    }
    void DriversPage(){
        Heading(T("Drivers","Drivers"),T("Veja as atualizações oferecidas para este PC e escolha o que instalar.","See updates offered for this PC and choose what to install."));BeginColumns(2);cardHost.IndependentColumns=true;
        var offersCard=Card(T("Atualizações disponíveis","Available updates"),T("O Windows verifica quais pacotes se aplicam ao dispositivo. Nada é instalado durante a busca.","Windows checks which packages apply to the device. Searching installs nothing."));
        TextBlock offerSummary=Text(preview?T("Clique em Procurar atualizações.","Click Check for updates."):T("Preparando a busca de atualizações…","Preparing driver update search…"),14,Muted);offersCard.Children.Add(offerSummary);
        ListBox offers=new ListBox{Height=200,Background=Surface,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};offersCard.Children.Add(offers);
        TextBox offerResult=Input("");offerResult.IsReadOnly=true;offerResult.Foreground=StateGreen;offerResult.TextChanged+=(sender,args)=>offerResult.Foreground=OutcomeBrush(offerResult.Text);offerResult.AcceptsReturn=true;offerResult.Height=90;offerResult.Visibility=Visibility.Collapsed;offersCard.Children.Add(offerResult);
        Action<bool> searchOffers=force=>{
            List<DriverOffer> found=null;Work(T("Procurar drivers","Check driver updates"),()=>{found=DriverUpdates.Search(force);return found.Count.ToString();},r=>{
                if(found==null){offerSummary.Text=r;offerSummary.Foreground=OutcomeBrush("ERROR • "+r);return;}var remembered=new HashSet<string>(offers.Items.OfType<CheckBox>().Where(x=>x.IsChecked==true&&x.Tag is DriverOffer).Select(x=>{var entry=(DriverOffer)x.Tag;return entry.Id+"|"+entry.Revision;}),StringComparer.OrdinalIgnoreCase);offers.Items.Clear();foreach(var item in found){
                    var labels=new StackPanel();labels.Children.Add(Text(item.Model.Length>0?item.Model:item.Title,14,Ink));labels.Children.Add(Text(T("Atual: ","Current: ")+item.CurrentVersion,12,Muted));labels.Children.Add(Text(item.Title,12,Muted));
                    offers.Items.Add(new CheckBox{Content=labels,Tag=item,IsChecked=remembered.Contains(item.Id+"|"+item.Revision),Foreground=Ink,Margin=new Thickness(5),ToolTip=item.Details});
                }offers.Visibility=offers.Items.Count>0?Visibility.Visible:Visibility.Collapsed;offerSummary.Foreground=found==null?OutcomeBrush("ERROR • "+r):StateGreen;
                offerSummary.Text=found==null?r:found.Count==0?T("Nenhuma atualização de driver oferecida pelo Windows neste momento.","Windows offers no driver update at this time."):found.Count+T(" atualização(ões) disponível(is). Selecione uma para instalar."," update(s) available. Select one to install.");
            });
        };
        Button checkUpdates=Button(T("Procurar atualizações","Check for updates"),()=>searchOffers(false));Row(offersCard,checkUpdates,Button(T("Refazer busca online","Refresh online search"),()=>searchOffers(true)),Button(T("Baixar e instalar selecionados","Download and install selected"),()=>{
            var selected=offers.Items.OfType<CheckBox>().Where(x=>x.IsChecked==true).Select(x=>(DriverOffer)x.Tag).ToArray();if(selected.Length==0||!RequireAdmin())return;
            if(!Confirm(T("Instalar estes drivers oferecidos pelo Windows? Pode ser preciso reiniciar.\n","Install these drivers offered by Windows? A restart may be required.\n")+String.Join("\n",selected.Select(x=>x.Title).ToArray())))return;
            offerResult.Visibility=Visibility.Visible;offerResult.Text=T("Instalando os drivers selecionados…","Installing selected drivers…");
            Work(T("Instalar drivers","Install drivers"),()=>{int job=workerJobId;ReportStage(T("Preparando ","Preparing ")+selected.Length+T(" driver(s)"," driver(s)"));return DriverUpdates.InstallMany(selected,stage=>ReportStage(stage,job));},r=>{offerResult.Text=r;checkUpdates.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));});
        }),Button(T("Abrir Windows Update","Open Windows Update"),()=>Open("ms-settings:windowsupdate")));
        var p=Card(T("Drivers instalados","Installed drivers"),T("Fabricante, versão, data e identificador do dispositivo. Uma versão maior não significa compatibilidade melhor.","Manufacturer, version, date and device identifier. A larger version number does not guarantee a better match."));
        ListBox devices=new ListBox{Height=250,Background=Surface,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};p.Children.Add(devices);
        var output=Input("");output.AcceptsReturn=true;output.IsReadOnly=true;output.Foreground=StateGreen;output.TextChanged+=(sender,args)=>output.Foreground=OutcomeBrush(output.Text);output.Height=110;output.Visibility=Visibility.Collapsed;output.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;p.Children.Add(output);
        devices.SelectionChanged+=(s,e)=>{var item=devices.SelectedItem as DriverRecord;if(item!=null){output.Visibility=Visibility.Visible;output.Text=item.Name+"\n"+item.Provider+" • "+item.Version+" • "+item.Date+"\n"+item.DeviceId;}};
        List<DriverRecord> records=null;
        Action<bool> loadDevices=force=>Work(T("Inventariar drivers","Inventory drivers"),()=>{
            records=DriverInventory.Read(force);
            return records.Count+T(" dispositivos lidos."," devices read.");
        },r=>{if(records==null){output.Text=r;return;}var old=devices.SelectedItem as DriverRecord;string selected=old==null?null:old.DeviceId;devices.Items.Clear();foreach(var item in records){devices.Items.Add(item);if(String.Equals(item.DeviceId,selected,StringComparison.OrdinalIgnoreCase))devices.SelectedItem=item;}devices.Visibility=devices.Items.Count>0?Visibility.Visible:Visibility.Collapsed;output.Visibility=Visibility.Visible;if(devices.SelectedItem==null)output.Text=r;});
        Row(p,Button(T("Checar dispositivos","Check devices"),()=>loadDevices(true)),Button(T("Pesquisar ID no Catálogo","Search ID in Catalog"),()=>{var item=devices.SelectedItem as DriverRecord;if(item==null)return;string id=item.HardwareIds.FirstOrDefault()??item.DeviceId;if(String.IsNullOrWhiteSpace(id))return;Open("https://www.catalog.update.microsoft.com/Search.aspx?q="+Uri.EscapeDataString(id));}),Button(T("Windows Update","Windows Update"),()=>Open("ms-settings:windowsupdate")),Button(T("Catálogo Microsoft","Microsoft Catalog"),()=>Open("https://www.catalog.update.microsoft.com/")));
        Row(p,Button(T("Revisar pacote INF baixado","Review downloaded INF"),()=>{var item=devices.SelectedItem as DriverRecord;if(item==null)return;var dialog=new OpenFileDialog{Filter="Driver setup (*.inf)|*.inf",CheckFileExists=true};if(dialog.ShowDialog(this)!=true)return;
            try{string content=File.ReadAllText(dialog.FileName);string id=item.DeviceId;string token=System.Text.RegularExpressions.Regex.Match(id,@"(?:VEN_[0-9A-Fa-f]{4}&DEV_[0-9A-Fa-f]{4}|VID_[0-9A-Fa-f]{4}&PID_[0-9A-Fa-f]{4})").Value;
                bool possible=token.Length>0&&content.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0;
                output.Text=dialog.FileName+"\n"+T("Associação ao dispositivo: ","Device match: ")+(possible?T("identificador básico encontrado; confirme fabricante, versão, arquitetura e assinatura do catálogo .cat.","base ID found; verify publisher, version, architecture and .cat signature."):T("identificador básico não encontrado; não instale por este caminho.","base ID not found; do not install through this path."));
            }catch(Exception e){output.Text=e.Message;}
        }),Button(T("Abrir Gerenciador de Dispositivos","Open Device Manager"),()=>Open("devmgmt.msc")));
        Note(p,"A lista automática usa as ofertas aplicáveis do Windows Update. Compare dispositivo, versão e fabricante antes de instalar.","The automatic list uses applicable Windows Update offers. Compare device, version and publisher before installing.");
        var offline=Card(T("Pacotes de drivers baixados","Downloaded driver packs"),T("Selecione um dispositivo acima e uma pasta com arquivos INF extraídos. O OuO mostra candidatos compatíveis e verifica a assinatura do catálogo antes de instalar.","Select a device above and a folder of extracted INF files. OuO shows matching candidates and verifies the catalog signature before installation."));
        TextBox folder=Field(offline,T("Pasta de drivers extraídos","Extracted driver folder"),"");BrowseFolder(offline,folder);
        var candidates=new ListBox{Height=155,Background=Surface,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};offline.Children.Add(candidates);
        var allowWithoutPoint=Choice(offline,T("Permitir instalação se o ponto de restauração não puder ser criado","Allow installation if a restore point cannot be created"),true,false);
        TextBox offlineResult=Input("");offlineResult.IsReadOnly=true;offlineResult.AcceptsReturn=true;offlineResult.Height=120;offlineResult.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        Row(offline,Button(T("Procurar candidatos","Find candidates"),()=>{var device=devices.SelectedItem as DriverRecord;if(device==null){offlineResult.Text=T("Selecione um dispositivo em Drivers instalados.","Select a device under Installed drivers.");return;}
            string selectedFolder=folder.Text;List<OfflineDriverCandidate> found=null;Work(T("Procurar drivers locais","Scan local drivers"),()=>{found=OfflineDrivers.Scan(selectedFolder,device.HardwareIds.FirstOrDefault()??device.DeviceId);return "OK • "+found.Count+T(" candidato(s) por identificador de hardware."," candidate(s) by hardware ID.");},r=>{candidates.Items.Clear();foreach(var item in found??new List<OfflineDriverCandidate>())candidates.Items.Add(item);candidates.Visibility=candidates.Items.Count>0?Visibility.Visible:Visibility.Collapsed;offlineResult.Text=r;});
        }),Button(T("Instalar candidato selecionado","Install selected candidate"),()=>{var item=candidates.SelectedItem as OfflineDriverCandidate;if(item==null||!RequireAdmin())return;
            if(!Confirm(T("Instalar este pacote de driver? Verifique a origem e salve seu trabalho antes de continuar:\n","Install this driver package? Verify its source and save work first:\n")+item.Inf))return;
            bool permit=allowWithoutPoint.IsChecked==true;Work(T("Instalar driver local","Install local driver"),()=>{var point=Runner.PS("Checkpoint-Computer -Description 'OuO driver change' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; Write-Output 'Restore point created'",120000);if(point.Code!=0&&!permit)return "ERROR • "+T("Ponto de restauração indisponível; instalação cancelada. ","Restore point unavailable; installation canceled. ")+point.Output;
                return (point.Code==0?"OK • "+T("Ponto de restauração criado.","Restore point created."):T("Aviso • sem ponto de restauração. ","Warning • no restore point. ")+point.Output)+"\n"+OfflineDrivers.Install(item,machine==null?0:machine.Build);},r=>offlineResult.Text=r);
        }));
        Note(offline,"No Windows 7, a importação adiciona o pacote ao repositório; conclua a escolha no Gerenciador de Dispositivos. Pacotes compactados devem ser extraídos primeiro.","On Windows 7, import adds the package to the driver store; complete selection in Device Manager. Extract compressed packs first.");AttachResult(offline,offlineResult);
        DriverCatalogCard(()=>devices.SelectedItem as DriverRecord);
        pageRefreshActions["drivers"]=()=>{loadDevices(false);searchOffers(false);};if(!preview)Dispatcher.BeginInvoke(pageRefreshActions["drivers"]);
        DriverCleanupCard();
    }
    void RuntimeLibraries(){
        var card=Card(T("Bibliotecas para aplicativos e jogos","Application and game runtimes"),T("Selecione somente componentes exigidos por seus programas. Confira as versões e licenças antes de instalar.","Select only components required by your apps. Review versions and licenses before installing."));
        string[] ids={"Microsoft.DotNet.DesktopRuntime.10","Microsoft.DotNet.DesktopRuntime.8","Microsoft.VCRedist.2015+.x86","Microsoft.VCRedist.2015+.x64","Microsoft.EdgeWebView2Runtime"};
        var choices=new List<CheckBox>();
        foreach(string id in ids){var c=new CheckBox{Content=id,Tag=id,Margin=new Thickness(0,4,0,4),Foreground=Ink};card.Children.Add(c);choices.Add(c);}
        Row(card,Button(T("DirectX para jogos antigos","DirectX for older games"),()=>Open("https://www.microsoft.com/download/details.aspx?id=8109")));
        var report=Input("");report.AcceptsReturn=true;report.IsReadOnly=true;report.Foreground=StateGreen;report.TextChanged+=(sender,args)=>report.Foreground=OutcomeBrush(report.Text);report.Height=130;report.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;card.Children.Add(report);
        Row(card,Button(T("Checar componentes","Check components"),()=>Work(T("Checar bibliotecas","Check runtimes"),()=>{
            var lines=new List<string>();
            string directx=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"d3dx9_43.dll");
            lines.Add("DirectX legado: "+(File.Exists(directx)?T("Encontrado","Found"):T("Não encontrado","Not found")));
            foreach(string id in ids){
                var result=Runner.Run("winget.exe","list --id "+Args.Q(id)+" --exact --source winget --disable-interactivity --accept-source-agreements",45000);
                lines.Add(id+": "+(result.Code==0&&result.Output.IndexOf(id,StringComparison.OrdinalIgnoreCase)>=0?T("Instalado","Installed"):T("Não identificado","Not identified")));
            }
            return String.Join("\n",lines.ToArray());
        },r=>report.Text=r)),Button(T("Instalar selecionados","Install selected"),()=>{
            var selected=choices.Where(c=>c.IsChecked==true).Select(c=>Convert.ToString(c.Tag)).ToArray();
            if(selected.Length==0||machine==null||!machine.Winget)return;
            if(!Confirm(T("Instalar bibliotecas pelo WinGet? Confira os IDs e contratos:\n","Install runtimes through WinGet? Review IDs and agreements:\n")+String.Join("\n",selected)))return;
            Work(T("Instalar bibliotecas","Install runtimes"),()=>{
                var lines=new List<string>();foreach(string id in selected){ReportStage(id);lines.Add(id);lines.Add(Describe(Runner.Run("winget.exe","install --id "+id+" --exact --source winget --disable-interactivity --accept-source-agreements --accept-package-agreements",1800000)));}return String.Join("\n",lines.ToArray());
            },r=>report.Text=r);
        }),Button(T("DirectX legado oficial","Official legacy DirectX"),()=>Open("https://www.microsoft.com/en-us/download/details.aspx?id=8109")));
        Note(card,"O pacote DirectX legado é opcional para jogos antigos e não substitui o DirectX do Windows. Em Windows sem WinGet, use downloads oficiais compatíveis com a versão instalada.","Legacy DirectX is optional for older games and does not replace Windows DirectX. On Windows without WinGet, use official downloads compatible with your installed version.");
    }
    void NetworkSection(){

        var network=Card(T("Conexão de rede","Network connection"),T("Consulte adaptadores, endereços, gateway e DNS antes de renovar a rede.","Inspect adapters, addresses, gateway and DNS before renewing the network."));
        var netReport=Input("");netReport.IsReadOnly=true;netReport.Foreground=StateGreen;netReport.TextChanged+=(sender,args)=>netReport.Foreground=OutcomeBrush(netReport.Text);netReport.AcceptsReturn=true;netReport.Height=190;netReport.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;network.Children.Add(netReport);
        Row(network,Button(T("Checar configuração","Check configuration"),()=>Work("ipconfig",()=>Describe(Runner.Run("ipconfig.exe","/all",30000)),r=>netReport.Text=r)),Button(T("Limpar cache DNS","Flush DNS cache"),()=>{
            if(!Confirm(T("Limpar o cache DNS deste computador?","Flush this computer's DNS cache?")))return;
            Work("Flush DNS",()=>Describe(Runner.Run("ipconfig.exe","/flushdns",30000)),r=>netReport.Text=r);
        }),Button(T("Diagnóstico de rede","Network diagnostics"),()=>Open("ms-settings:network-status")));
        ComboBox adapters=new ComboBox{Margin=new Thickness(0,0,0,10)};network.Children.Add(adapters);
        foreach(var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.NetworkInterfaceType==NetworkInterfaceType.Ethernet||n.NetworkInterfaceType==NetworkInterfaceType.Wireless80211))adapters.Items.Add(nic);adapters.DisplayMemberPath="Name";
        if(adapters.Items.Count>0)adapters.SelectedIndex=0;
        Row(network,Button(T("Renovar DHCP do adaptador","Renew adapter DHCP"),()=>{
            var nic=adapters.SelectedItem as NetworkInterface;if(nic==null)return;
            bool dhcp=false;try{dhcp=nic.GetIPProperties().GetIPv4Properties().IsDhcpEnabled;}catch{}
            if(!dhcp){ShowNotice(T("Este adaptador não usa DHCP para IPv4.","This adapter does not use IPv4 DHCP."));return;}
            if(!Confirm(T("Renovar o endereço IPv4 de ","Renew the IPv4 address for ")+nic.Name+T("? A conexão pode cair temporariamente.","? Connectivity may briefly drop.")))return;
            Work("DHCP renew",()=>Describe(Runner.Run("ipconfig.exe","/renew "+Args.Q(nic.Name),60000)),r=>netReport.Text=r);
        }));
    }
    void PrintingPage(){
        Heading(T("Impressão","Printing"),T("Impressoras, fila e reparo do spooler.","Printers, queue and spooler repair."));BeginColumns(2);
        var print=Card(T("Fila de impressão","Print queue"),T("Reiniciar o spooler preserva a fila. Limpar a fila faz backup dos arquivos de trabalhos antes de removê-los.","Restarting the spooler preserves the queue. Clearing it backs up job files before removing them."));
        var printReport=Input("");printReport.IsReadOnly=true;printReport.Foreground=StateGreen;printReport.TextChanged+=(sender,args)=>printReport.Foreground=OutcomeBrush(printReport.Text);printReport.AcceptsReturn=true;printReport.Height=160;printReport.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;print.Children.Add(printReport);
        Row(print,Button(T("Checar spooler e impressoras","Check spooler and printers"),()=>Work("Print inventory",()=>Describe(Runner.PS("Get-Service Spooler | Format-List Name,Status,StartType; Get-Printer | Select-Object Name,DriverName,PortName,PrinterStatus | Format-Table -AutoSize",30000)),r=>printReport.Text=r)),Button(T("Reiniciar spooler","Restart spooler"),()=>{
            if(!RequireAdmin()||!Confirm(T("Reiniciar o serviço de impressão? Os trabalhos poderão ser interrompidos.","Restart the print service? Jobs may be interrupted.")))return;
            Work("Restart spooler",()=>Describe(Runner.PS("Restart-Service -Name Spooler -ErrorAction Stop; Get-Service Spooler | Select-Object Name,Status | Format-List",60000)),r=>printReport.Text=r);
        }),Button(T("Limpar fila e reiniciar","Clear queue and restart"),()=>{
            if(!RequireAdmin())return;
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","spool","PRINTERS");
            string[] jobs=Directory.Exists(path)?Directory.GetFiles(path).Where(f=>new[]{".SPL",".SHD"}.Contains(Path.GetExtension(f),StringComparer.OrdinalIgnoreCase)).ToArray():new string[0];
            if(jobs.Length==0){ShowNotice(T("Nenhum arquivo de trabalho pendente encontrado.","No pending job files found."));return;}
            if(!Confirm(T("Limpar ","Clear ")+jobs.Length+T(" arquivos da fila? Os trabalhos pendentes serão perdidos. Cópia em: "," queue files? Pending jobs will be lost. Backup at: ")+journal.Folder))return;
            Work("Clear print queue",()=>{
                string backup=Path.Combine(journal.Folder,"PrintQueue-"+DateTime.Now.ToString("yyyyMMddHHmmss"));
                var stop=Runner.PS("Stop-Service -Name Spooler -ErrorAction Stop",30000);if(stop.Code!=0)return Describe(stop);
                try{Directory.CreateDirectory(backup);int removed=0;
                    foreach(string file in jobs){string full=Path.GetFullPath(file);if(!String.Equals(Path.GetDirectoryName(full),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))continue;
                        File.Copy(full,Path.Combine(backup,Path.GetFileName(full)),false);File.Delete(full);removed++;}
                    return removed+T(" arquivos removidos. Backup: "," files removed. Backup: ")+backup;
                }finally{Runner.PS("Start-Service -Name Spooler -ErrorAction Stop",30000);}
            },r=>printReport.Text=r);
        }),Button(T("Abrir impressoras","Open printers"),()=>Open("control printers")));
        Note(print,"Antes de reinstalar uma impressora, confira driver, porta, compartilhamento e políticas do fabricante. A ação é separada e exige confirmação.","Before reinstalling a printer, check its driver, port, sharing and manufacturer policies. It is a separate action requiring confirmation.");
        AttachResult(print,printReport);
        PrinterManagement();
    }
    void ExportDiagnostic(){
        var dialog=new SaveFileDialog{Filter="Text (*.txt)|*.txt",FileName="OuO-diagnostico-"+DateTime.Now.ToString("yyyyMMdd")+".txt",AddExtension=true,DefaultExt=".txt"};
        if(dialog.ShowDialog(this)!=true)return;string destination=dialog.FileName;
        Work(T("Gerar diagnóstico","Generate diagnostics"),()=>{
            var report=new StringBuilder();report.AppendLine("OuO diagnostic • "+DateTime.Now.ToString("s"));
            if(machine!=null){report.AppendLine(machine.Caption+" • "+machine.Arch+" • build "+machine.Build);report.AppendLine("CPU: "+machine.Cpu);report.AppendLine("GPU: "+machine.Gpu);report.AppendLine("RAM: "+(machine.Total/1048576.0).ToString("0.0")+" GB");}
            if(lastSample!=null)report.AppendLine("CPU use: "+lastSample[0]+" • GPU use: "+lastSample[1]+" • CPU temp: "+lastSample[3]+" • GPU temp: "+lastSample[4]);
            foreach(var drive in DriveInfo.GetDrives())try{if(drive.IsReady&&drive.DriveType==DriveType.Fixed)report.AppendLine("Drive "+drive.Name+" • "+(drive.AvailableFreeSpace/1073741824.0).ToString("0.0")+" GB free / "+(drive.TotalSize/1073741824.0).ToString("0.0")+" GB");}catch{}
            report.AppendLine("\nEvent patterns:");report.AppendLine(EventAnalysis.Recommendations(EventAnalysis.Read(14,80)));
            File.WriteAllText(destination,report.ToString(),Encoding.UTF8);return destination;
        },r=>ShowNotice(T("Relatório salvo em: ","Report saved at: ")+r));
    }
}
}
