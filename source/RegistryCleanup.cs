using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;
namespace Aurora {
public sealed class RegistryFinding {
    public string Category,Hive,Key,Name,Target,Reason;public RegistryView View;public RegistrySnapshot Observed;
    public override string ToString(){return Category+" • "+(View==RegistryView.Registry64?"64-bit":"32-bit")+" • "+(String.IsNullOrEmpty(Name)?Path.GetFileName(Key):Name)+"\n"+Target;}
}
public sealed class RegistryViewStore:IRegistryStore {
    readonly RegistryView view;public RegistryViewStore(RegistryView value){view=value;}
    RegistryKey Root(string hive){if(hive!="HKCU"&&hive!="HKLM")throw new ArgumentException("Unsupported registry hive");return RegistryKey.OpenBaseKey(hive=="HKCU"?RegistryHive.CurrentUser:RegistryHive.LocalMachine,view);}
    public RegistrySnapshot Read(string hive,string path,string name){using(var root=Root(hive))using(var key=root.OpenSubKey(path)){if(key==null||!key.GetValueNames().Contains(name))return new RegistrySnapshot();return new RegistrySnapshot{Exists=true,Value=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames),Kind=key.GetValueKind(name)};}}
    public void Set(string hive,string path,string name,object value,RegistryValueKind kind){using(var root=Root(hive))using(var key=root.CreateSubKey(path))key.SetValue(name,value,kind);}
    public void Delete(string hive,string path,string name){using(var root=Root(hive))using(var key=root.OpenSubKey(path,true))if(key!=null)key.DeleteValue(name,false);}
}
public static class RegistryCleanup {
    const string Current=@"Software\Microsoft\Windows\CurrentVersion\";
    const string Classes=@"Software\Classes\";
    public static string TargetPath(string value){
        if(String.IsNullOrWhiteSpace(value))return null;string text=Environment.ExpandEnvironmentVariables(value.Trim()),path;
        if(text.StartsWith("\"")){int end=text.IndexOf('"',1);if(end<2)return null;path=text.Substring(1,end-1);}else{Match match=Regex.Match(text,@"^(.+?\.(?:exe|dll|ocx|tlb))(?=\s|,|$)",RegexOptions.IgnoreCase);if(!match.Success)return null;path=match.Groups[1].Value;}
        if(!Path.IsPathRooted(path)||path.StartsWith(@"\\")||path.Contains("%")||path.IndexOfAny(new[]{'\r','\n','\0','"'})>=0)return null;
        try{return Path.GetFullPath(path);}catch{return null;}
    }
    public static bool ExternalPath(string path){
        if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path)||path.StartsWith(@"\\"))return false;
        string full;try{full=Path.GetFullPath(path);}catch{return false;}
        foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.Windows),Environment.GetFolderPath(Environment.SpecialFolder.System),Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86)})if(!String.IsNullOrWhiteSpace(root)&&(full.Equals(root,StringComparison.OrdinalIgnoreCase)||full.StartsWith(root.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)))return false;
        return Regex.IsMatch(full,@"\.(exe|dll|ocx|tlb)$",RegexOptions.IgnoreCase)&&full.Length>8;
    }
    public static bool AllowedKey(string key){
        if(String.IsNullOrWhiteSpace(key)||key.Length>2048||key.IndexOfAny(new[]{'\r','\n','\0'})>=0)return false;
        if(key.Equals(Current+"Run",StringComparison.OrdinalIgnoreCase)||key.Equals(Current+"RunOnce",StringComparison.OrdinalIgnoreCase)||key.Equals(Current+"SharedDLLs",StringComparison.OrdinalIgnoreCase)||key.Equals(Classes+@"Local Settings\Software\Microsoft\Windows\Shell\MuiCache",StringComparison.OrdinalIgnoreCase))return true;
        if(key.StartsWith(Current+"App Paths\\",StringComparison.OrdinalIgnoreCase)&&key.Substring((Current+"App Paths\\").Length).IndexOf('\\')<0)return true;
        return Regex.IsMatch(key,@"^Software\\Classes\\(?:[^\\]+\\shell\\(?:open|edit)\\command|CLSID\\\{[0-9a-f-]{36}\}\\(?:InprocServer32|LocalServer32)|TypeLib\\\{[0-9a-f-]{36}\}\\[^\\]+\\[^\\]+\\win(?:32|64))$",RegexOptions.IgnoreCase);
    }
    public static bool Eligible(RegistryFinding item,Func<string,bool> fileExists=null,Func<string,bool> directoryExists=null){
        if(item==null||(item.Hive!="HKCU"&&item.Hive!="HKLM")||!AllowedKey(item.Key)||item.Observed==null||!item.Observed.Exists||!ExternalPath(item.Target))return false;
        string expected=item.Category=="MuiCache"?TargetPath(Regex.Replace(item.Name??"",@"\.(FriendlyAppName|ApplicationCompany)$","",RegexOptions.IgnoreCase)):item.Category=="SharedDLLs"?TargetPath(item.Name):TargetPath(Convert.ToString(item.Observed.Value));
        if(!String.Equals(expected,item.Target,StringComparison.OrdinalIgnoreCase))return false;
        fileExists=fileExists??File.Exists;directoryExists=directoryExists??Directory.Exists;
        if(fileExists(item.Target))return false;
        if((item.Category=="COM"||item.Category=="TypeLib"||item.Category=="SharedDLLs")&&directoryExists(Path.GetDirectoryName(item.Target)))return false;
        return true;
    }
    static void Add(List<RegistryFinding> result,string category,string hive,RegistryView view,string key,string name,RegistryKey source,string target){
        try{if(source==null||!source.GetValueNames().Contains(name)||result.Count>=5000)return;var item=new RegistryFinding{Category=category,Hive=hive,View=view,Key=key,Name=name,Target=target,Observed=new RegistrySnapshot{Exists=true,Value=source.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames),Kind=source.GetValueKind(name)},Reason=L.T("Referência para arquivo ausente fora das pastas protegidas do Windows.","Reference to a missing file outside protected Windows folders.")};if(Eligible(item))result.Add(item);}catch{}
    }
    static void Command(List<RegistryFinding> result,RegistryKey root,string category,string hive,RegistryView view,string path){using(var key=root.OpenSubKey(path)){if(key==null||key.GetValue("DelegateExecute")!=null||key.GetValue("DDEApplication")!=null)return;Add(result,category,hive,view,path,"",key,TargetPath(Convert.ToString(key.GetValue("",null,RegistryValueOptions.DoNotExpandEnvironmentNames))));}}
    public static List<RegistryFinding> Scan(CancellationToken cancel,Action<int> progress){
        var found=new List<RegistryFinding>();int examined=0;
        foreach(string hive in new[]{"HKCU","HKLM"})foreach(RegistryView view in Environment.Is64BitOperatingSystem?new[]{RegistryView.Registry64,RegistryView.Registry32}:new[]{RegistryView.Registry32})try{using(var root=RegistryKey.OpenBaseKey(hive=="HKCU"?RegistryHive.CurrentUser:RegistryHive.LocalMachine,view)){
            foreach(string path in new[]{Current+"Run",Current+"RunOnce"})using(var key=root.OpenSubKey(path))if(key!=null)foreach(string name in key.GetValueNames()){cancel.ThrowIfCancellationRequested();Add(found,L.T("Inicialização","Startup"),hive,view,path,name,key,TargetPath(Convert.ToString(key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames))));}
            using(var apps=root.OpenSubKey(Current+"App Paths"))if(apps!=null)foreach(string name in apps.GetSubKeyNames()){cancel.ThrowIfCancellationRequested();try{Command(found,root,"App Paths",hive,view,Current+"App Paths\\"+name);}catch{}}
            foreach(string path in new[]{Current+"SharedDLLs",Classes+@"Local Settings\Software\Microsoft\Windows\Shell\MuiCache"})using(var key=root.OpenSubKey(path))if(key!=null)foreach(string name in key.GetValueNames()){cancel.ThrowIfCancellationRequested();string target=path.EndsWith("MuiCache",StringComparison.OrdinalIgnoreCase)?Regex.Replace(name,@"\.(FriendlyAppName|ApplicationCompany)$","",RegexOptions.IgnoreCase):name;Add(found,path.EndsWith("MuiCache",StringComparison.OrdinalIgnoreCase)?"MuiCache":"SharedDLLs",hive,view,path,name,key,TargetPath(target));}
            using(var classes=root.OpenSubKey(Classes.TrimEnd('\\')))if(classes!=null)foreach(string name in classes.GetSubKeyNames().Take(40000)){
                cancel.ThrowIfCancellationRequested();if(++examined%200==0&&progress!=null)progress(examined);
                if(name.StartsWith(".")||new[]{"CLSID","TypeLib","Interface","AppID","Local Settings"}.Contains(name,StringComparer.OrdinalIgnoreCase))continue;
                try{foreach(string verb in new[]{"open","edit"})Command(found,root,L.T("Associação de aplicativo","App association"),hive,view,Classes+name+"\\shell\\"+verb+"\\command");}catch{}
            }
            using(var com=root.OpenSubKey(Classes+"CLSID"))if(com!=null)foreach(string id in com.GetSubKeyNames().Take(40000)){cancel.ThrowIfCancellationRequested();if(++examined%200==0&&progress!=null)progress(examined);try{foreach(string server in new[]{"InprocServer32","LocalServer32"})Command(found,root,"COM",hive,view,Classes+"CLSID\\"+id+"\\"+server);}catch{}}
            using(var types=root.OpenSubKey(Classes+"TypeLib"))if(types!=null)foreach(string id in types.GetSubKeyNames().Take(10000)){cancel.ThrowIfCancellationRequested();try{using(var type=types.OpenSubKey(id))foreach(string version in type.GetSubKeyNames().Take(12))using(var ver=type.OpenSubKey(version))foreach(string locale in ver.GetSubKeyNames().Take(12))foreach(string platform in new[]{"win32","win64"})Command(found,root,"TypeLib",hive,view,Classes+"TypeLib\\"+id+"\\"+version+"\\"+locale+"\\"+platform);}catch{}}
        }}catch(OperationCanceledException){throw;}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}catch(IOException){}
        return found.GroupBy(x=>x.Hive+"|"+x.View+"|"+x.Key+"|"+x.Name,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).OrderBy(x=>x.Category).ThenBy(x=>x.Key).ToList();
    }
    static bool Same(RegistrySnapshot left,RegistrySnapshot right){return left.Exists&&right.Exists&&left.Kind==right.Kind&&(left.Value is byte[]&&right.Value is byte[]?((byte[])left.Value).SequenceEqual((byte[])right.Value):left.Value is string[]&&right.Value is string[]?((string[])left.Value).SequenceEqual((string[])right.Value):Object.Equals(left.Value,right.Value));}
    static XElement SaveValue(RegistryFinding item){var json=new JavaScriptSerializer();return new XElement("registry-value-backup",new XAttribute("version","1"),new XAttribute("hive",item.Hive),new XAttribute("view",item.View),new XAttribute("key",item.Key),new XAttribute("name",item.Name),new XAttribute("kind",item.Observed.Kind),new XElement("data",Convert.ToBase64String(Encoding.UTF8.GetBytes(json.Serialize(item.Observed.Value)))));}
    public static string Delete(RegistryFinding item,string backupRoot,IRegistryStore testStore=null,Func<string,bool> fileExists=null,Func<string,bool> directoryExists=null){
        if(!Eligible(item,fileExists,directoryExists))throw new InvalidOperationException(L.T("A referência não atende mais aos critérios de remoção.","The reference no longer meets removal criteria."));
        var store=testStore??new RegistryViewStore(item.View);if(!Same(item.Observed,store.Read(item.Hive,item.Key,item.Name)))throw new InvalidOperationException(L.T("O valor mudou desde a análise; faça uma nova checagem.","The value changed since analysis; scan again."));
        Directory.CreateDirectory(backupRoot);string prefix=Path.Combine(backupRoot,"RegistryValue-"+Guid.NewGuid().ToString("N"));
        if(testStore==null){var saved=Runner.Run("reg.exe","export "+Args.Q(item.Hive+"\\"+item.Key)+" "+Args.Q(prefix+".reg")+" /y "+(item.View==RegistryView.Registry32?"/reg:32":"/reg:64"),30000);if(saved.Code!=0||!File.Exists(prefix+".reg"))throw new IOException(saved.Output);}
        AtomicFiles.SaveXml(SaveValue(item),prefix+".xml");if(!Eligible(item,fileExists,directoryExists)||!Same(item.Observed,store.Read(item.Hive,item.Key,item.Name)))throw new InvalidOperationException(L.T("A referência mudou durante o backup; ela foi preservada. Analise novamente.","Reference changed during backup; it was preserved. Scan again."));store.Delete(item.Hive,item.Key,item.Name);if(store.Read(item.Hive,item.Key,item.Name).Exists)throw new IOException("Registry value deletion was not confirmed");return prefix+".xml";
    }
    public static string Restore(string file,string backupRoot,IRegistryStore testStore=null){
        string path=Args.LocalPath(file),root=Path.GetFullPath(backupRoot).TrimEnd('\\')+"\\";if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(path).StartsWith("RegistryValue-",StringComparison.Ordinal)||!File.Exists(path))throw new ArgumentException("Select an OuO Registry value backup");
        XElement xml;using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))xml=XElement.Load(reader);
        string hive=(string)xml.Attribute("hive"),key=(string)xml.Attribute("key"),name=(string)xml.Attribute("name");RegistryView view;RegistryValueKind kind;
        if(xml.Name!="registry-value-backup"||(string)xml.Attribute("version")!="1"||(hive!="HKCU"&&hive!="HKLM")||!AllowedKey(key)||name==null||!Enum.TryParse((string)xml.Attribute("view"),out view)||!Enum.TryParse((string)xml.Attribute("kind"),out kind))throw new InvalidDataException("Invalid registry backup");
        var json=new JavaScriptSerializer();string data=Encoding.UTF8.GetString(Convert.FromBase64String((string)xml.Element("data")));object value;
        switch(kind){case RegistryValueKind.DWord:value=json.Deserialize<int>(data);break;case RegistryValueKind.QWord:value=json.Deserialize<long>(data);break;case RegistryValueKind.Binary:value=json.Deserialize<byte[]>(data);break;case RegistryValueKind.MultiString:value=json.Deserialize<string[]>(data);break;case RegistryValueKind.String:case RegistryValueKind.ExpandString:value=json.Deserialize<string>(data);break;default:throw new InvalidDataException("Unsupported registry value kind");}
        var store=testStore??new RegistryViewStore(view);if(store.Read(hive,key,name).Exists)throw new IOException(L.T("Já existe um valor nesse local. A restauração não o substituirá.","A value already exists here. Restore will not overwrite it."));store.Set(hive,key,name,value,kind);return "OK • "+hive+"\\"+key+" • "+name;
    }
}
public partial class MainWindow {
    Action ExpandedRegistryCard(){
        var card=Card(T("Análise ampliada do Registro","Extended Registry analysis"),T("Procura referências a arquivos ausentes em inicialização, App Paths, associações, COM, bibliotecas de tipos, SharedDLLs e cache de nomes. Nenhum item é marcado automaticamente; cada valor é conferido novamente e recebe backup antes de excluir.","Finds missing-file references in startup, App Paths, associations, COM, type libraries, SharedDLLs and name cache. Nothing is preselected; each value is rechecked and backed up before deletion."));
        var filter=new ComboBox{Margin=new Thickness(0,0,0,8)};filter.Items.Add(T("Todas as categorias","All categories"));filter.SelectedIndex=0;card.Children.Add(filter);
        var list=new ListBox{Height=300,Background=Surface,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);list.HorizontalContentAlignment=HorizontalAlignment.Stretch;card.Children.Add(list);
        var details=Text("",12,Muted);card.Children.Add(details);var state=Text(T("Clique em Analisar Registro para iniciar.","Select Analyze Registry to begin."),13,Muted);card.Children.Add(state);
        var checks=new Dictionary<RegistryFinding,CheckBox>();List<RegistryFinding> findings=new List<RegistryFinding>();CancellationTokenSource cancel=null;
        Action show=()=>{list.Items.Clear();checks.Clear();string category=filter.SelectedIndex>0?Convert.ToString(filter.SelectedItem):null;foreach(var item in findings.Where(x=>category==null||x.Category==category)){var box=new CheckBox{Content=new TextBlock{Text=item.ToString(),TextWrapping=TextWrapping.Wrap},Tag=item,Foreground=Ink,Margin=new Thickness(4),ToolTip=item.Hive+"\\"+item.Key+"\n"+item.Name+"\n"+item.Reason};list.Items.Add(box);checks[item]=box;}list.Visibility=list.Items.Count>0?Visibility.Visible:Visibility.Collapsed;};filter.SelectionChanged+=(s,e)=>show();list.SelectionChanged+=(s,e)=>{var box=list.SelectedItem as CheckBox;var item=box==null?null:box.Tag as RegistryFinding;details.Text=item==null?"":item.Hive+"\\"+item.Key+"\n"+item.Target+"\n"+item.Reason;};
        Action scan=()=>{if(cancel!=null)return;cancel=new CancellationTokenSource();var token=cancel.Token;List<RegistryFinding> found=null;Work(T("Analisar referências do Registro","Analyze Registry references"),()=>{int job=workerJobId;found=RegistryCleanup.Scan(token,n=>ReportStage(T("Analisando Registro • ","Analyzing Registry • ")+n+T(" referências"," references"),job));return "OK • "+found.Count+T(" referência(s) para revisar."," reference(s) to review.");},r=>{cancel.Dispose();cancel=null;if(found!=null){findings=found;filter.Items.Clear();filter.Items.Add(T("Todas as categorias","All categories"));foreach(string category in found.Select(x=>x.Category).Distinct())filter.Items.Add(category);filter.SelectedIndex=0;show();}state.Text=r;state.Foreground=OutcomeBrush(r);});};
        Row(card,Button(T("Analisar Registro","Analyze Registry"),()=>scan()),Button(T("Cancelar análise","Cancel analysis"),()=>{if(cancel!=null)cancel.Cancel();}),Button(T("Marcar itens exibidos","Select displayed items"),()=>{foreach(var box in checks.Values)box.IsChecked=true;}),Button(T("Excluir referências selecionadas","Delete selected references"),()=>{var chosen=checks.Where(x=>x.Value.IsChecked==true).Select(x=>x.Key).ToArray();if(chosen.Length==0||cancel!=null||chosen.Any(x=>x.Hive=="HKLM")&&!RequireAdmin())return;if(!Confirm(T("Exportar e excluir ","Back up and delete ")+chosen.Length+T(" valor(es) do Registro? Revise os detalhes. Isso pode remover configurações de aplicativos já ausentes."," Registry value(s)? Review details. This may remove settings for apps that are no longer present.")))return;
            Work(T("Excluir referências do Registro","Delete Registry references"),()=>{var result=new List<string>();foreach(var item in chosen)try{result.Add("OK • "+RegistryCleanup.Delete(item,journal.Folder));}catch(Exception ex){result.Add("ERROR • "+item.Key+" • "+ex.Message);}return String.Join("\n",result);},r=>{ShowNotice(r);scan();});}),Button(T("Restaurar backup de referência","Restore reference backup"),()=>{string file=PickFile("OuO Registry value backup|RegistryValue-*.xml");if(file==null||!RequireAdmin()||!Confirm(T("Restaurar o valor salvo neste backup do OuO?","Restore the value saved in this OuO backup?")))return;Work(T("Restaurar referência do Registro","Restore Registry reference"),()=>RegistryCleanup.Restore(file,journal.Folder),r=>ShowNotice(r));}),Button(T("Abrir backups","Open backups"),()=>Open(journal.Folder)));
        return scan;
    }
}
}
