using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
namespace Aurora {
public static class ClamAvScan {
    public static string Root{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OuO","ClamAV");}}
    public static string DefaultDatabase{get{return Path.Combine(Root,"database");}}
    public static string Engine(string file){
        string path=Args.LocalPath(file);if(!Path.GetFileName(path).Equals("clamscan.exe",StringComparison.OrdinalIgnoreCase)||!File.Exists(path))throw new FileNotFoundException(L.T("Selecione clamscan.exe da distribuição oficial do ClamAV.","Select clamscan.exe from the official ClamAV distribution."));return path;
    }
    public static string Arguments(string file,string database){
        string target=Args.LocalPath(file),db=Args.LocalPath(database);if(!File.Exists(target))throw new FileNotFoundException("Scan target unavailable",target);
        if(!Directory.Exists(db)||!Directory.EnumerateFiles(db).Any(x=>new[]{".cvd",".cld"}.Contains(Path.GetExtension(x),StringComparer.OrdinalIgnoreCase)))throw new InvalidDataException(L.T("Atualize as assinaturas antes de examinar.","Update signatures before scanning."));
        return "--stdout --infected --official-db-only=yes --follow-file-symlinks=0 --database="+Args.Q(db)+" "+Args.Q(target);
    }
    public static string Explain(CommandResult result){
        string label=result.Code==0?L.T("OK • Nenhuma detecção neste exame. Isso não garante que o arquivo seja seguro.","OK • No detection in this scan. This does not guarantee the file is safe."):
            result.Code==1?L.T("ATENÇÃO • Ameaça detectada. Nenhum arquivo foi removido pelo OuO; revise o relatório e use seu antivírus.","ATTENTION • Threat detected. OuO removed no files; review the report and use your antivirus."):
            L.T("ERROR • O exame não foi concluído.","ERROR • Scan did not complete.");
        return label+"\n"+result.Output;
    }
    public static string Scan(string engine,string target,string database){string file=Engine(engine);return Explain(Runner.Run(file,Arguments(target,database),900000,Path.GetDirectoryName(file)));}
    public static string Update(string engine){
        string file=Engine(engine),fresh=Path.Combine(Path.GetDirectoryName(file),"freshclam.exe");if(!File.Exists(fresh))throw new FileNotFoundException("freshclam.exe unavailable",fresh);
        Directory.CreateDirectory(DefaultDatabase);string config=Path.Combine(Root,"freshclam.conf");
        File.WriteAllText(config,"DatabaseDirectory \""+DefaultDatabase+"\"\nDatabaseMirror database.clamav.net\nDNSDatabaseInfo current.cvd.clamav.net\nForeground yes\nConnectTimeout 15\nReceiveTimeout 120\nScriptedUpdates yes\n",new UTF8Encoding(false));
        var result=Runner.Run(fresh,"--config-file="+Args.Q(config),900000,Path.GetDirectoryName(file));return (result.Code==0?"OK • ":"ERROR • ")+result.Output;
    }
}
public partial class MainWindow {
    void ClamAvCard(Func<string> selectedProcess){
        var card=Card(T("Segunda opinião • ClamAV","Second opinion • ClamAV"),T("Integração opcional com o motor gratuito instalado por você. Examina o arquivo escolhido sem apagar nem isolar automaticamente. As assinaturas são baixadas pelo freshclam oficial.","Optional integration with the free engine you install. Scans the selected file without automatic deletion or isolation. Signatures are downloaded by official freshclam."));
        TextBox engine=Field(card,T("clamscan.exe da instalação oficial","Official installation clamscan.exe"),"");BrowseFile(card,engine,"ClamAV scanner|clamscan.exe");
        TextBlock state=Text(T("Selecione o motor e atualize as assinaturas antes do primeiro exame.","Select the engine and update signatures before the first scan."),13,Muted);card.Children.Add(state);
        TextBox report=Input("");report.IsReadOnly=true;report.AcceptsReturn=true;report.Height=140;report.TextWrapping=System.Windows.TextWrapping.Wrap;report.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;AttachResult(card,report);
        Row(card,Button(T("Baixar ClamAV oficial","Get official ClamAV"),()=>Open("https://www.clamav.net/downloads")),Button(T("Atualizar assinaturas","Update signatures"),()=>{string path=engine.Text;if(!Confirm(T("Executar freshclam.exe ao lado do motor selecionado para baixar assinaturas oficiais? Use apenas uma distribuição obtida no site do ClamAV.","Run freshclam.exe beside the selected engine to download official signatures? Use only a distribution obtained from ClamAV's website.")))return;Work(T("Atualizar assinaturas ClamAV","Update ClamAV signatures"),()=>ClamAvScan.Update(path),r=>{report.Text=r;state.Text=ResultTone.IsFailure(r)?T("A atualização falhou; veja os detalhes.","Update failed; see details."):T("Assinaturas atualizadas.","Signatures updated.");state.Foreground=OutcomeBrush(r);});}),Button(T("Examinar arquivo do processo","Scan process file"),()=>{string path=engine.Text,target=selectedProcess();if(String.IsNullOrWhiteSpace(target)){ShowToast(T("Selecione um processo com arquivo identificado.","Select a process with an identified file."));return;}if(!Confirm(T("Executar o ClamAV selecionado para examinar este arquivo?\n","Run the selected ClamAV engine to scan this file?\n")+target))return;Work(T("Examinar com ClamAV","Scan with ClamAV"),()=>ClamAvScan.Scan(path,target,ClamAvScan.DefaultDatabase),r=>report.Text=r);}));
    }
}
}
