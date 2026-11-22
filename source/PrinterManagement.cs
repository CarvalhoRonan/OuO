using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Aurora {
public sealed class PrinterRecord {public string Name,Driver,Port;public bool Local,Shared,Default;public override string ToString(){return Name+" • "+Driver+" • "+Port+(Shared?" • shared":"");}}
public static class PrinterMaintenance {
    public static string ScriptReinstall(string name,string backup){
        string n=Runner.QuotePS(name),b=Runner.QuotePS(backup);
        return "$name="+n+"; $printer=Get-Printer -Name $name -ErrorAction Stop; if($printer.Type -ne 'Local' -or $printer.Shared){throw 'Only unshared local queues can be recreated'}; "+
            "$driver=Get-PrinterDriver -Name $printer.DriverName -ErrorAction Stop; $port=Get-PrinterPort -Name $printer.PortName -ErrorAction Stop; "+
            "$cfg=Get-PrintConfiguration -PrinterName $name -ErrorAction Stop; $wasDefault=[bool](Get-WmiObject Win32_Printer | Where-Object {$_.Name -eq $name -and $_.Default}); "+
            "[pscustomobject]@{Name=$name;Driver=$printer.DriverName;Port=$printer.PortName;Color=$cfg.Color;DuplexingMode=[string]$cfg.DuplexingMode;PaperSize=[string]$cfg.PaperSize;Collate=$cfg.Collate;Default=$wasDefault} | ConvertTo-Json | Set-Content -LiteralPath "+b+" -Encoding UTF8; "+
            "Remove-Printer -Name $name -ErrorAction Stop; try { Add-Printer -Name $name -DriverName $printer.DriverName -PortName $printer.PortName -ErrorAction Stop } catch { throw ('Queue removed; backup at '+"+b+"+'; recreation failed: '+$_.Exception.Message) }; "+
            "try {Set-PrintConfiguration -PrinterName $name -Color ([bool]$cfg.Color) -DuplexingMode $cfg.DuplexingMode -PaperSize $cfg.PaperSize -Collate ([bool]$cfg.Collate) -ErrorAction Stop} catch {Write-Output ('Preferences need manual review: '+$_.Exception.Message)}; "+
            "if($wasDefault){Get-WmiObject Win32_Printer | Where-Object {$_.Name -eq $name} | ForEach-Object {$_.SetDefaultPrinter() | Out-Null}}; Get-Printer -Name $name | Select-Object Name,DriverName,PortName | Format-List; Write-Output ('Backup: '+"+b+")";
    }
    public static string ScriptPreferences(string name,string backup,string paper,string duplex,bool color){
        if(paper!="A4"&&paper!="Letter")throw new ArgumentException("Invalid paper");if(!new[]{"OneSided","TwoSidedLongEdge","TwoSidedShortEdge"}.Contains(duplex))throw new ArgumentException("Invalid duplex");
        string n=Runner.QuotePS(name),b=Runner.QuotePS(backup);
        return "$name="+n+"; $cfg=Get-PrintConfiguration -PrinterName $name -ErrorAction Stop; [pscustomobject]@{Name=$name;Color=$cfg.Color;DuplexingMode=[string]$cfg.DuplexingMode;PaperSize=[string]$cfg.PaperSize;Collate=$cfg.Collate} | ConvertTo-Json | Set-Content -LiteralPath "+b+" -Encoding UTF8; "+
            "Set-PrintConfiguration -PrinterName $name -PaperSize "+paper+" -DuplexingMode "+duplex+" -Color $"+(color?"true":"false")+" -ErrorAction Stop; Get-PrintConfiguration -PrinterName $name | Select-Object PrinterName,PaperSize,DuplexingMode,Color,Collate | Format-List; Write-Output ('Backup: '+"+b+")";
    }
    public static string ScriptRestorePreferences(string backup){string b=Runner.QuotePS(backup);return "$cfg=Get-Content -LiteralPath "+b+" -Raw | ConvertFrom-Json; Set-PrintConfiguration -PrinterName $cfg.Name -PaperSize $cfg.PaperSize -DuplexingMode $cfg.DuplexingMode -Color ([bool]$cfg.Color) -Collate ([bool]$cfg.Collate) -ErrorAction Stop; Get-PrintConfiguration -PrinterName $cfg.Name | Format-List";}
}
public partial class MainWindow {
    void PrinterManagement(){
        var card=Card(T("Impressora selecionada","Selected printer"),T("Disponível quando o módulo PrintManagement está presente. A reinstalação preserva driver e porta; não os remove.","Available when PrintManagement is present. Reinstalling preserves driver and port; it does not remove them."));
        ListBox list=new ListBox{Height=120,Background=Surface,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};card.Children.Add(list);
        TextBox report=Input("");report.IsReadOnly=true;report.Foreground=StateGreen;report.TextChanged+=(sender,args)=>report.Foreground=OutcomeBrush(report.Text);report.AcceptsReturn=true;report.Height=120;card.Children.Add(report);
        Row(card,Button(T("Listar impressoras","List printers"),()=>{list.Items.Clear();try{foreach(var o in Machine.Query("root\\cimv2","SELECT Name,DriverName,PortName,Local,Shared,Default FROM Win32_Printer"))using(o)list.Items.Add(new PrinterRecord{Name=Machine.Value(o,"Name"),Driver=Machine.Value(o,"DriverName"),Port=Machine.Value(o,"PortName"),Local=Machine.Value(o,"Local")=="True",Shared=Machine.Value(o,"Shared")=="True",Default=Machine.Value(o,"Default")=="True"});list.Visibility=list.Items.Count>0?Visibility.Visible:Visibility.Collapsed;report.Text=list.Items.Count==0?T("Nenhuma impressora encontrada.","No printers found."):list.Items.Count+T(" impressora(s) encontrada(s)."," printer(s) found.");}catch(Exception e){report.Text="ERROR • "+e.Message;}}),Button(T("Checar preferências","Check preferences"),()=>{var p=list.SelectedItem as PrinterRecord;if(p==null)return;Work("Print configuration",()=>Describe(Runner.PS("Get-PrintConfiguration -PrinterName "+Runner.QuotePS(p.Name)+" | Select-Object PrinterName,PaperSize,DuplexingMode,Color,Collate | Format-List",30000)),r=>report.Text=r);}));
        ComboBox paper=new ComboBox{Margin=new Thickness(0,0,0,8)};paper.Items.Add("A4");paper.Items.Add("Letter");paper.SelectedIndex=0;card.Children.Add(paper);
        ComboBox duplex=new ComboBox{Margin=new Thickness(0,0,0,8)};foreach(string value in new[]{"OneSided","TwoSidedLongEdge","TwoSidedShortEdge"})duplex.Items.Add(value);duplex.SelectedIndex=0;card.Children.Add(duplex);
        CheckBox color=new CheckBox{Content=T("Impressão colorida","Color printing"),Foreground=Ink,Margin=new Thickness(0,0,0,8)};card.Children.Add(color);
        string lastBackup=null;
        Row(card,Button(T("Aplicar preferências","Apply preferences"),()=>{var p=list.SelectedItem as PrinterRecord;if(p==null||!RequireAdmin())return;string choicePaper=Convert.ToString(paper.SelectedItem),choiceDuplex=Convert.ToString(duplex.SelectedItem);if(!Confirm(T("Aplicar estas preferências à impressora ","Apply these preferences to printer ")+p.Name+"?\n"+choicePaper+" • "+choiceDuplex+" • color="+(color.IsChecked==true)))return;
            bool choiceColor=color.IsChecked==true;string backup=Path.Combine(journal.Folder,"PrinterConfig-"+DateTime.Now.ToString("yyyyMMddHHmmss")+".json");string script=PrinterMaintenance.ScriptPreferences(p.Name,backup,choicePaper,choiceDuplex,choiceColor);Work("Printer preferences",()=>Describe(Runner.PS(script,60000)),r=>{report.Text=r;if(r.StartsWith("OK"))lastBackup=backup;});
        }),Button(T("Restaurar preferências","Restore preferences"),()=>{if(lastBackup==null||!File.Exists(lastBackup)||!RequireAdmin()||!Confirm(T("Restaurar preferências salvas em ","Restore preferences saved at ")+lastBackup+"?"))return;Work("Restore printer preferences",()=>Describe(Runner.PS(PrinterMaintenance.ScriptRestorePreferences(lastBackup),60000)),r=>report.Text=r);}),Button(T("Reinstalar fila local","Reinstall local queue"),()=>{
            var p=list.SelectedItem as PrinterRecord;if(p==null||!p.Local||p.Shared||machine==null||machine.Build<9200||!RequireAdmin())return;
            if(!Confirm(T("Reinstalar apenas esta fila local? A fila atual será removida e recriada com o mesmo driver e porta. Trabalhos pendentes podem ser perdidos.\n","Reinstall only this local queue? It will be removed and recreated with the same driver and port. Pending jobs may be lost.\n")+p))return;
            string backup=Path.Combine(journal.Folder,"PrinterQueue-"+DateTime.Now.ToString("yyyyMMddHHmmss")+".json");Work("Reinstall printer queue",()=>Describe(Runner.PS(PrinterMaintenance.ScriptReinstall(p.Name,backup),120000)),r=>report.Text=r);
        }));
        AttachResult(card,report);
    }
}
}
