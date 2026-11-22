using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Reflection;
using Microsoft.Win32;

namespace Aurora {
public sealed class StartupShortcut {
    public string Name,Path,Scope;public bool Enabled,Editable;
    public ImageSource Icon{get{return AppIcons.FromFile(Path);}}
}
public static class StartupFolders {
    const string TrayName="OuO Monitor";
    const string RunPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
    public static string UserRoot{get{return Environment.GetFolderPath(Environment.SpecialFolder.Startup);}}
    public static string CommonRoot{get{return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);}}
    public static string DisabledRoot{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OuO","StartupDisabled");}}
    static bool Allowed(string file){string ext=Path.GetExtension(file);return ext.Equals(".lnk",StringComparison.OrdinalIgnoreCase)||ext.Equals(".url",StringComparison.OrdinalIgnoreCase);}
    static void Add(List<StartupShortcut> result,string root,bool enabled,bool editable,string scope){
        if(String.IsNullOrWhiteSpace(root)||!Directory.Exists(root))return;
        try{foreach(string path in Directory.GetFiles(root).Where(Allowed))result.Add(new StartupShortcut{Name=Path.GetFileNameWithoutExtension(path),Path=path,Enabled=enabled,Editable=editable,Scope=scope});}catch(UnauthorizedAccessException){}
    }
    public static List<StartupShortcut> List(){var result=new List<StartupShortcut>();Add(result,UserRoot,true,true,L.T("Seu usuário","Your account"));Add(result,DisabledRoot,false,true,L.T("Seu usuário • pausado","Your account • paused"));Add(result,CommonRoot,true,false,L.T("Todos os usuários • consulta","All users • read only"));return result.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();}
    public static void SetEnabled(StartupShortcut item,bool enabled){
        if(item==null||!item.Editable||!Allowed(item.Path))throw new InvalidOperationException(L.T("Este atalho não pode ser alterado aqui.","This shortcut cannot be changed here."));
        MoveValidated(item.Path,item.Enabled?UserRoot:DisabledRoot,enabled?UserRoot:DisabledRoot);
    }
    public static bool OuOTrayEnabled{get{try{using(var key=Registry.CurrentUser.OpenSubKey(RunPath))return String.Equals(Convert.ToString(key==null?null:key.GetValue(TrayName)),TrayCommand(),StringComparison.OrdinalIgnoreCase);}catch{return false;}}}
    static string TrayCommand(){return Args.Q(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName)+" --tray";}
    public static void SetOuOTray(bool enabled){
        using(var key=Registry.CurrentUser.CreateSubKey(RunPath)){
            string old=Convert.ToString(key.GetValue(TrayName,""));
            if(old.Length>0&&!String.Equals(old,TrayCommand(),StringComparison.OrdinalIgnoreCase))throw new IOException("An existing startup entry has this name; review it manually.");
            if(enabled)key.SetValue(TrayName,TrayCommand(),RegistryValueKind.String);else key.DeleteValue(TrayName,false);
        }
    }
    public static string AddUser(string target){
        if(String.IsNullOrWhiteSpace(target))throw new ArgumentException(L.T("Selecione um programa, atalho ou endereço da web.","Choose an app, shortcut or web address."));
        target=target.Trim();Uri address;
        bool web=Uri.TryCreate(target,UriKind.Absolute,out address)&&(address.Scheme==Uri.UriSchemeHttp||address.Scheme==Uri.UriSchemeHttps);
        if(!web){target=Args.LocalPath(target);if(!File.Exists(target)||!new[]{".exe",".lnk"}.Contains(Path.GetExtension(target),StringComparer.OrdinalIgnoreCase))throw new InvalidDataException(L.T("Escolha um arquivo .exe/.lnk existente ou uma página http(s).","Choose an existing .exe/.lnk file or an http(s) page."));}
        string name=web?address.Host:Path.GetFileNameWithoutExtension(target);
        foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');
        if(name.Length>55)name=name.Substring(0,55);
        Directory.CreateDirectory(UserRoot);
        string result=Path.Combine(UserRoot,"OuO - "+name+(web?".url":".lnk"));
        if(File.Exists(result))throw new IOException(L.T("Esse item já existe na inicialização.","This item is already in Startup."));
        if(web)File.WriteAllText(result,"[InternetShortcut]\r\nURL="+address.AbsoluteUri+"\r\n",new System.Text.UTF8Encoding(false));
        else if(Path.GetExtension(target).Equals(".lnk",StringComparison.OrdinalIgnoreCase))File.Copy(target,result,false);
        else{
            Type type=Type.GetTypeFromProgID("WScript.Shell");if(type==null)throw new NotSupportedException("Windows Script Host unavailable");
            object shell=Activator.CreateInstance(type);object link=null;
            try{link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{result});
                Type lt=link.GetType();lt.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{target});
                lt.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{Path.GetDirectoryName(target)});
                lt.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,new object[0]);}
            finally{if(link!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
        }
        return result;
    }
    public static void RemoveUser(StartupShortcut item){
        if(item==null||!item.Editable||!item.Enabled||!Path.GetFileName(item.Path).StartsWith("OuO - ",StringComparison.OrdinalIgnoreCase)||!String.Equals(Path.GetDirectoryName(Path.GetFullPath(item.Path)),Path.GetFullPath(UserRoot).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(L.T("Somente atalhos criados pelo OuO podem ser removidos aqui.","Only shortcuts created by OuO can be removed here."));
        if((File.GetAttributes(item.Path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Startup shortcut is a link");File.Delete(item.Path);
    }
    public static void MoveValidated(string path,string from,string to){
        string source=Path.GetFullPath(from).TrimEnd('\\');
        string destination=Path.GetFullPath(to).TrimEnd('\\');
        string full=Path.GetFullPath(path);
        if(!Allowed(full))throw new InvalidDataException("Only startup shortcuts may be moved");
        if(!String.Equals(Path.GetDirectoryName(full),source,StringComparison.OrdinalIgnoreCase)||!File.Exists(full)||(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Startup shortcut changed or is a link");
        if((new DirectoryInfo(source).Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Startup source is a link");
        Directory.CreateDirectory(destination);if((new DirectoryInfo(destination).Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Startup destination is a link");
        string target=Path.Combine(destination,Path.GetFileName(full));if(File.Exists(target))throw new IOException(L.T("Já existe um atalho com este nome no destino.","A shortcut with this name already exists at the destination."));
        File.Move(full,target);
    }
}
public partial class MainWindow {
    Action StartupFolderCard(){
        var card=Card(T("Atalhos da pasta Inicializar","Startup folder shortcuts"),T("Altere apenas atalhos do seu usuário. Entradas de todos os usuários são mostradas para consulta.","Change only your account's shortcuts. All-users entries are shown for reference."));
        ListBox list=new ListBox{Height=250,Background=Surface,Foreground=Ink,BorderBrush=Muted,Visibility=Visibility.Collapsed};card.Children.Add(list);
        TextBlock state=Text("",13,Muted);card.Children.Add(state);
        Action refresh=null;refresh=()=>{list.Items.Clear();try{var items=StartupFolders.List();foreach(var item in items){var choice=new CheckBox{IsChecked=item.Enabled,IsEnabled=item.Editable,Foreground=Ink,Margin=new Thickness(5,5,5,5),ToolTip=item.Path};
                choice.Tag=item;var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new Image{Source=item.Icon,Width=28,Height=28,Margin=new Thickness(0,0,10,0)});var labels=new StackPanel();labels.Children.Add(Text(item.Name,14,Ink));labels.Children.Add(Text(item.Scope,11,Muted));row.Children.Add(labels);choice.Content=row;
                Action<bool> change=desired=>{if(desired==item.Enabled)return;if(!Confirm(T("Alterar a inicialização de ","Change startup for ")+item.Name+"?\n"+item.Path)){refresh();return;}try{StartupFolders.SetEnabled(item,desired);journal.Log("Startup shortcut",item.Path+" → "+desired);state.Text="OK • "+T("Atalho atualizado.","Shortcut updated.");state.Foreground=StateGreen;}catch(Exception ex){state.Text="ERROR • "+ex.Message;state.Foreground=OutcomeBrush(state.Text);}refresh();};
                choice.Checked+=(s,e)=>change(true);choice.Unchecked+=(s,e)=>change(false);list.Items.Add(choice);
            }list.Visibility=list.Items.Count>0?Visibility.Visible:Visibility.Collapsed;if(state.Text.Length==0)state.Text=items.Count==0?T("Nenhum atalho de inicialização encontrado.","No startup shortcuts found."):items.Count+T(" atalho(s) encontrados."," shortcut(s) found.");}
            catch(Exception ex){state.Text="ERROR • "+ex.Message;state.Foreground=OutcomeBrush(state.Text);}};
        Row(card,Button(T("Atualizar atalhos","Refresh shortcuts"),()=>refresh()),Button(T("Abrir pasta Inicializar","Open Startup folder"),()=>Open(StartupFolders.UserRoot)));
        TextBox target=Field(card,T("Adicionar programa, atalho ou página web","Add app, shortcut or web page"),"");
        Row(card,Button(T("Selecionar arquivo","Select file"),()=>{string file=PickFile("Programas e atalhos|*.exe;*.lnk");if(file!=null)target.Text=file;}),Button(T("Adicionar à inicialização","Add to startup"),()=>{try{string path=StartupFolders.AddUser(target.Text);state.Text="OK • "+path;state.Foreground=StateGreen;target.Text="";refresh();}catch(Exception e){state.Text="ERROR • "+e.Message;state.Foreground=OutcomeBrush(state.Text);}}),Button(T("Remover atalho selecionado","Remove selected shortcut"),()=>{var item=(list.SelectedItem as CheckBox)==null?null:(list.SelectedItem as CheckBox).Tag as StartupShortcut;if(item==null)return;if(!Confirm(T("Remover este atalho da inicialização?\n","Remove this startup shortcut?\n")+item.Path))return;try{StartupFolders.RemoveUser(item);state.Text="OK • "+T("Atalho removido.","Shortcut removed.");refresh();}catch(Exception e){state.Text="ERROR • "+e.Message;state.Foreground=OutcomeBrush(state.Text);}}));
        CheckBox tray=Choice(card,T("Iniciar o OuO minimizado na bandeja e monitorar o hardware","Start OuO minimized in tray and monitor hardware"),true,StartupFolders.OuOTrayEnabled);
        tray.Checked+=(s,e)=>{try{StartupFolders.SetOuOTray(true);state.Text="OK • "+T("Monitor do OuO ativado na inicialização.","OuO monitor enabled at startup.");}catch(Exception ex){tray.IsChecked=false;state.Text="ERROR • "+ex.Message;}};
        tray.Unchecked+=(s,e)=>{try{StartupFolders.SetOuOTray(false);state.Text="OK • "+T("Monitor do OuO removido da inicialização.","OuO monitor removed from startup.");}catch(Exception ex){state.Text="ERROR • "+ex.Message;}};
        Note(card,"Desmarcar move o atalho para uma pasta do OuO no seu perfil; marcar devolve ao local original. O programa instalado não é removido.","Unchecking moves the shortcut into an OuO folder in your profile; checking restores it. The installed app is not removed.");refresh();return refresh;
    }
}
}
