using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;

namespace Aurora {
public partial class MainWindow {
    void RenderMetrics(){if(lastSample==null||machine==null)return;string[] v=lastSample;
        if(hardwareIdentity!=null)hardwareIdentity.Text=machine.Cpu+"\n"+machine.Gpu+"\n"+machine.Board;
        if(metricCpu!=null)metricCpu.Text=v[0];if(metricGpu!=null)metricGpu.Text=v[1];ulong free;UInt64.TryParse(v[2],out free);
        if(metricRam!=null)metricRam.Text=machine.Total==0?"—":((machine.Total-Math.Min(machine.Total,free))/1048576.0).ToString("0.0")+" / "+(machine.Total/1048576.0).ToString("0.0")+" GB";
        if(tempCpu!=null)tempCpu.Text=v[3];if(tempGpu!=null)tempGpu.Text=v[4];if(tempVrm!=null)tempVrm.Text=v[5];if(metricTemp!=null)metricTemp.Text=UiLanguage.Translate(v[6],!L.Pt);
    }
    void AppearanceOverview(){
        var p=Card(T("Aparência","Appearance"),"");
        ComboBox choices=new ComboBox{Margin=new Thickness(0,0,0,8)};
        foreach(string label in new[]{T("Escolher aparência…","Choose appearance…"),T("☀ OuO claro","☀ Light OuO"),T("☾ OuO escuro","☾ Dark OuO"),T("☀ Windows claro","☀ Light Windows"),T("☾ Windows escuro","☾ Dark Windows"),T("◫ Tema compacto","◫ Compact theme"),T("▣ Tema normal","▣ Standard theme")})choices.Items.Add(label);
        choices.SelectedIndex=0;p.Children.Add(choices);
        choices.SelectionChanged+=(s,e)=>{int option=choices.SelectedIndex;if(option==1)SetAppTheme(false);else if(option==2)SetAppTheme(true);else if(option==3)SystemTheme(true);else if(option==4)SystemTheme(false);else if(option==5)SetCompact(true);else if(option==6)SetCompact(false);};
    }
    void SystemTheme(bool light){if(machine==null||machine.Build<14393)return;if(!Confirm(T("Aplicar tema ","Apply ")+(light?T("claro","light"):T("escuro","dark"))+T(" ao Windows? Os valores anteriores serão salvos no histórico."," theme to Windows? Previous values will be saved in history.")))return;
        Work("Windows theme",()=>{string path="Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";journal.RegistryChange("Windows app theme","HKCU",path,"AppsUseLightTheme",light?1:0,Microsoft.Win32.RegistryValueKind.DWord);if(machine.Build>=18362)journal.RegistryChange("Windows system theme","HKCU",path,"SystemUsesLightTheme",light?1:0,Microsoft.Win32.RegistryValueKind.DWord);return T("Tema aplicado. Reabra os aplicativos para refletir a alteração.","Theme applied. Reopen apps to reflect the change.");},r=>ShowNotice(r));
    }
    void InstallSensorDriver(){
        if(preview)return;
        if(!Confirm(T("Preparar o acesso aos sensores de CPU e placa-mãe?\n\nSerá aberto o instalador assinado do PawnIO incluído no pacote oficial do Libre Hardware Monitor. O Windows poderá solicitar administrador. Revise e confirme a instalação; depois reinicie os sensores. Alguns equipamentos não expõem temperaturas de VRM.","Set up CPU and motherboard sensor access?\n\nThe signed PawnIO installer included in the official Libre Hardware Monitor package will open. Windows may request administrator. Review and confirm installation, then restart sensors. Some devices do not expose VRM temperatures.")))return;
        string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Sensors","PawnIO_setup.exe");
        try{
            string digest;using(SHA256 sha=SHA256.Create())using(var stream=File.OpenRead(file))digest=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
            if(digest!="A3A46226C5E2824F4CDD42BE0EECBABFC672C86F7889710F5AB1E6AD385B47A0")throw new InvalidDataException(T("O instalador não corresponde ao pacote verificado.","Installer does not match the verified package."));
            Process.Start(new ProcessStartInfo(file){UseShellExecute=true,Verb="runas",WorkingDirectory=Path.GetDirectoryName(file)});journal.Log("PawnIO installer","Opened signed installer; completion not monitored.");
        }catch(Exception e){ShowNotice(e.Message);}
    }
    const string MemoryQuery="if (!(Get-Command Get-MMAgent -ErrorAction SilentlyContinue)) { throw 'MMAgent unavailable' }; $m = Get-MMAgent; if (!$m.PSObject.Properties['MemoryCompression']) { throw 'MemoryCompression unavailable' }; Write-Output ('AURORA-MEMORY:' + [string]$m.MemoryCompression)";
    string MemoryFile{get{return Path.Combine(journal.Folder,"memory-compression.previous");}}
    internal static bool ParseMemory(string text){Match match=Regex.Match(text??"","AURORA-MEMORY:(True|False)",RegexOptions.IgnoreCase);if(!match.Success)throw new InvalidDataException(L.T("Não foi possível consultar a compactação de memória.","Could not query memory compression."));return Boolean.Parse(match.Groups[1].Value);}
    internal static string MemoryScript(bool desired){return "if (!(Get-Command "+(desired?"Enable-MMAgent":"Disable-MMAgent")+" -ErrorAction SilentlyContinue).Parameters.ContainsKey('MemoryCompression')) { throw 'Unsupported MemoryCompression parameter' }; "+(desired?"Enable-MMAgent":"Disable-MMAgent")+" -MemoryCompression; "+MemoryQuery;}
    void MemoryCompression(){
        var p=Card(T("Compactação de memória","Memory compression"),T("Usa o mecanismo nativo do Windows para manter mais dados na RAM. Pode reduzir paginação, mas exige processamento. Não substitui RAM física e não esvazia a memória à força.","Uses Windows' native mechanism to keep more data in RAM. May reduce paging, at a CPU cost. Does not replace physical RAM or forcibly empty memory."));
        bool supported=machine!=null&&machine.Build>=10240;
        TextBlock state=Text(supported?T("Estado ainda não consultado.","State not queried yet."):T("Requer Windows 10/11 e o recurso MemoryCompression do MMAgent. Windows 7/8/8.1 não oferecem este controle.","Requires Windows 10/11 and MMAgent MemoryCompression support. Windows 7/8/8.1 do not provide this control."),13,StateGreen);p.Children.Add(state);
        Button query=Button(T("Consultar estado","Query state"),()=>{if(!RequireAdmin())return;Work("Memory compression status",()=>{var result=Runner.PS(MemoryQuery,30000);if(result.Code!=0)return Describe(result);return ParseMemory(result.Output)?T("Compactação de memória: habilitada.","Memory compression: enabled."):T("Compactação de memória: desabilitada.","Memory compression: disabled.");},r=>state.Text=r);});
        Button enable=Button(T("Ativar compactação","Enable compression"),()=>SetMemory(true,false,state));Button disable=Button(T("Desativar compactação","Disable compression"),()=>SetMemory(false,false,state));Button restore=Button(T("Restaurar estado anterior","Restore previous state"),()=>SetMemory(false,true,state));query.IsEnabled=enable.IsEnabled=disable.IsEnabled=restore.IsEnabled=supported;Row(p,query,enable,disable,restore);
        Note(p,"O estado anterior é salvo antes da primeira alteração. Pode ser necessário reiniciar o Windows. Nenhuma alteração é feita ao abrir esta tela.","The original state is saved before the first change. A Windows restart may be required. Opening this page changes nothing.");
    }
    void SetMemory(bool enabled,bool restore,TextBlock state){
        if(machine==null||machine.Build<10240||!RequireAdmin())return;
        if(restore&&!File.Exists(MemoryFile)){state.Text=T("Nenhum estado anterior salvo.","No previous state saved.");return;}
        if(restore){bool parsed;if(!Boolean.TryParse(File.ReadAllText(MemoryFile).Trim(),out parsed)){state.Text=T("Backup inválido.","Invalid backup.");return;}enabled=parsed;}
        if(!Confirm((restore?T("Restaurar","Restore"):enabled?T("Ativar","Enable"):T("Desativar","Disable"))+T(" compactação de memória?\nO Windows poderá exigir reinicialização."," memory compression?\nWindows may require a restart.")))return;
        bool desired=enabled;Work("Memory compression",()=>{
            var query=Runner.PS(MemoryQuery,30000);if(query.Code!=0)return Describe(query);bool previous=ParseMemory(query.Output);
            if(!restore&&!File.Exists(MemoryFile))File.WriteAllText(MemoryFile,previous.ToString());
            var result=Runner.PS(MemoryScript(desired),60000);
            if(result.Code!=0)return Describe(result);bool applied=ParseMemory(result.Output);
            if(applied!=desired)throw new InvalidOperationException(T("O estado consultado não corresponde ao solicitado. Reinicie e consulte novamente.","Queried state does not match the request. Restart and query again."));
            if(restore)File.Delete(MemoryFile);return desired?T("Compactação habilitada. Reinicie o Windows se necessário.","Compression enabled. Restart Windows if needed."):T("Compactação desabilitada. Reinicie o Windows se necessário.","Compression disabled. Restart Windows if needed.");
        },r=>state.Text=r);
    }
}
}
