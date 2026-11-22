using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
namespace Aurora {
public sealed class OfflineAccount {
    public int Rid,Locked;public string Name;
    public override string ToString(){return Name+" • RID "+Rid+(Locked==1?L.T(" • bloqueada"," • locked"):"");}
}
public sealed class OfflineAccountSession {
    public string WindowsPath,Folder,SamPath;public List<OfflineAccount> Accounts;internal string OriginalHash,CommittedHash;
}
public static class OfflineAccountRecovery {
    static void Administrator(){if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException(L.T("Esta operação exige um administrador.","This operation requires an administrator."));}
    internal static string Hash(string path){using(var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    static void NoLinks(string path){for(string at=Path.GetFullPath(path);!String.IsNullOrEmpty(at);at=Path.GetDirectoryName(at)){if((Directory.Exists(at)||File.Exists(at))&&(File.GetAttributes(at)&FileAttributes.ReparsePoint)!=0)throw new IOException(L.T("A recuperação não pode seguir links ou junções.","Recovery cannot follow links or junctions."));}}
    public static string ValidateWindowsPath(string path){
        string full=Args.LocalPath(path).TrimEnd('\\'),active=Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
        if(String.Equals(full,active,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(L.T("A instalação em uso não pode ser recuperada offline.","The running Windows installation cannot be recovered offline."));
        NoLinks(full);foreach(string name in new[]{"SAM","SYSTEM"})if(!File.Exists(Path.Combine(full,"System32","config",name)))throw new DirectoryNotFoundException(L.T("Selecione a pasta Windows de uma instalação offline desbloqueada.","Select the Windows folder of an unlocked offline installation."));
        string hibernation=Path.Combine(Path.GetPathRoot(full),"hiberfil.sys");if(File.Exists(hibernation))using(var file=File.Open(hibernation,FileMode.Open,FileAccess.Read,FileShare.Read)){byte[] header=new byte[4];if(file.Read(header,0,4)!=4)throw new IOException("Hibernation state unavailable");string magic=Encoding.ASCII.GetString(header).ToLowerInvariant();if(magic=="hibr"||magic=="wake")throw new InvalidOperationException(L.T("O Windows de destino está hibernado. Faça um desligamento completo antes da recuperação.","Target Windows is hibernated. Fully shut it down before recovery."));}
        return full;
    }
    static void PrivateDirectory(string path){Directory.CreateDirectory(path);var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);acl.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));foreach(var id in new[]{WellKnownSidType.BuiltinAdministratorsSid,WellKnownSidType.LocalSystemSid})acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(id,null),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));Directory.SetAccessControl(path,acl);}
    static void PrivateFile(string path){var acl=new FileSecurity();acl.SetAccessRuleProtection(true,false);acl.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));foreach(var id in new[]{WellKnownSidType.BuiltinAdministratorsSid,WellKnownSidType.LocalSystemSid})acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(id,null),FileSystemRights.FullControl,AccessControlType.Allow));File.SetAccessControl(path,acl);}
    internal static void ValidateHive(string path){var info=new FileInfo(path);if(info.Length<8192||info.Length>67108864)throw new InvalidDataException("Unsupported SAM hive size");RegistryHiveValidation.Validate(File.ReadAllBytes(path));}
    public static int ValidateHiveInChild(string path){try{ValidateHive(Args.LocalPath(path));Console.WriteLine("VALID");return 0;}catch(Exception ex){Console.WriteLine(ex.Message);return 1;}}
    static CommandResult Engine(string folder,string mode,int rid){
        string arch=Environment.Is64BitOperatingSystem?"x64":"x86",exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Tools","OfflineAccount",arch,"OuO.OfflineAccount.exe");
        string expected=arch=="x64"?"37AB61020C94E954FED50FED6972F3C18D506F163C13294315CBB6C7320DF565":"11506AD2A41A2D3E97179BE13BCD491FA1225EB8B1BB6D8A14C072235552EB77";
        if(!File.Exists(exe)||Hash(exe)!=expected)throw new InvalidDataException(L.T("O motor de recuperação está ausente ou foi alterado. Reextraia o pacote original.","Recovery engine is missing or was changed. Extract the original package again."));
        return Runner.Run(exe,mode+(rid==0?"":" "+rid),30000,folder);
    }
    public static List<OfflineAccount> Parse(string output){var list=new List<OfflineAccount>();foreach(string line in output.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){string[] fields=line.Split('|');int rid,locked;if(fields.Length!=4||fields[0]!="USER"||!Int32.TryParse(fields[1],out rid)||rid<500||!Int32.TryParse(fields[3],out locked))continue;string name=Encoding.Unicode.GetString(Convert.FromBase64String(fields[2]));if(String.IsNullOrWhiteSpace(name)||name.Any(Char.IsControl))throw new InvalidDataException("Invalid local account name");list.Add(new OfflineAccount{Rid=rid,Name=name,Locked=locked});}return list;}
    public static OfflineAccountSession Open(string windows){
        Administrator();windows=ValidateWindowsPath(windows);string source=Path.Combine(windows,"System32","config","SAM");NoLinks(source);string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"OuO","OfflineRecovery");NoLinks(root);PrivateDirectory(root);string folder=Path.Combine(root,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));PrivateDirectory(folder);
        // Only copies are handed to the native parser. Listing never edits the target hive.
        using(var original=File.Open(source,FileMode.Open,FileAccess.Read,FileShare.None))using(var copy=File.Create(Path.Combine(folder,"SAM")))original.CopyTo(copy);
        ValidateHive(Path.Combine(folder,"SAM"));File.WriteAllText(Path.Combine(folder,".ouo-offline-work"),"OuO",Encoding.ASCII);var run=Engine(folder,"list",0);if(run.Code!=0)throw new IOException(run.Output);
        var accounts=Parse(run.Output);if(accounts.Count==0)throw new InvalidDataException(L.T("Nenhuma conta local válida encontrada.","No valid local accounts found."));
        return new OfflineAccountSession{WindowsPath=windows,Folder=folder,SamPath=source,Accounts=accounts,OriginalHash=Hash(Path.Combine(folder,"SAM"))};
    }
    public static string Apply(OfflineAccountSession session,int rid,bool clearPassword){
        Administrator();lock(session){ValidateWindowsPath(session.WindowsPath);if(!session.Accounts.Any(x=>x.Rid==rid))throw new InvalidDataException("Account is not part of this session");NoLinks(session.SamPath);string baseline=session.CommittedHash??session.OriginalHash;if(Hash(session.SamPath)!=baseline)throw new IOException(L.T("O Registro de destino mudou. Reabra a instalação antes de continuar.","Target registry changed. Reopen the installation before continuing."));
            string config=Path.GetDirectoryName(session.SamPath),stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N");string backup=Path.Combine(session.Folder,"Backup-"+stamp);PrivateDirectory(backup);
            foreach(string name in new[]{"SAM","SYSTEM","SECURITY"}){string original=Path.Combine(config,name);if(File.Exists(original)){NoLinks(original);File.Copy(original,Path.Combine(backup,name),false);}}
            File.Copy(session.SamPath,Path.Combine(session.Folder,"SAM"),true);var result=Engine(session.Folder,clearPassword?"clear":"unlock",rid);if(result.Code!=0)throw new IOException(result.Output);ValidateHive(Path.Combine(session.Folder,"SAM"));
            string stage=Path.Combine(config,"OuO-"+Guid.NewGuid().ToString("N")+".tmp"),localBackup=Path.Combine(config,"SAM.OuO-"+stamp+".bak");
            try{File.Copy(Path.Combine(session.Folder,"SAM"),stage,false);PrivateFile(stage);if(Hash(session.SamPath)!=baseline)throw new IOException("Target SAM changed before commit");File.Replace(stage,session.SamPath,localBackup);PrivateFile(localBackup);session.CommittedHash=Hash(session.SamPath);}
            finally{if(File.Exists(stage))File.Delete(stage);}
            return "OK • "+L.T(clearPassword?"Senha local removida. Defina uma nova senha no Windows após entrar.":"Conta local desbloqueada.",clearPassword?"Local password cleared. Set a new Windows password after sign-in.":"Local account unlocked.")+"\n"+L.T("Cópia de segurança: ","Backup: ")+backup+"\n"+localBackup;
        }
    }
}
public partial class MainWindow {
    void OfflineAccountCard(){
        var card=Card(T("Recuperar conta em outro Windows","Recover an account in another Windows"),T("Para uma instalação offline em um disco conectado ou ambiente de recuperação com .NET. O Windows em uso é bloqueado. Não recupera contas Microsoft, domínio, PIN ou arquivos EFS.","For offline Windows on an attached disk or a recovery environment with .NET. Running Windows is blocked. Does not recover Microsoft/domain accounts, PINs or EFS files."));
        var path=Field(card,T("Pasta Windows da instalação offline","Offline installation's Windows folder"),"");BrowseFolder(card,path);var users=new ComboBox{Margin=new Thickness(0,0,0,8)};card.Children.Add(users);var state=Text("",13,Muted);card.Children.Add(state);OfflineAccountSession session=null;
        var consent=Choice(card,T("Entendo que remover a senha pode impedir o acesso a dados EFS, senhas salvas e outros dados protegidos da conta.","I understand clearing the password may prevent access to EFS data, saved passwords and other account-protected data."),true,false);
        Action<bool> change=clear=>{if(!RequireAdmin()||session==null||users.SelectedItem==null)return;if(consent.IsChecked!=true){ShowNotice(T("Leia e marque o aviso antes de continuar.","Read and check the notice before continuing."));return;}var selected=(OfflineAccount)users.SelectedItem;var target=session;if(!Confirm(T(clear?"Remover a senha local offline de ":"Desbloquear a conta local offline ",clear?"Clear the offline local password of ":"Unlock the offline local account ")+selected.Name+"\n"+target.WindowsPath+T("\n\nUma cópia dos Registros será salva antes da alteração. Não use em Windows hibernado.","\n\nRegistry hives will be backed up before changing them. Do not use on hibernated Windows.")))return;Work(T("Recuperar conta offline","Recover offline account"),()=>OfflineAccountRecovery.Apply(target,selected.Rid,clear),r=>{state.Text=r;state.Foreground=OutcomeBrush(r);});};
        Row(card,Button(T("Ler contas offline","Read offline accounts"),()=>{if(!RequireAdmin())return;string selected=path.Text;OfflineAccountSession found=null;Work(T("Ler contas offline","Read offline accounts"),()=>{found=OfflineAccountRecovery.Open(selected);return "OK • "+found.Accounts.Count+T(" conta(s) local(is). Nenhuma alteração na instalação de destino."," local account(s). Target installation was not modified.");},r=>{session=found;users.Items.Clear();foreach(var user in found==null?new List<OfflineAccount>():found.Accounts)users.Items.Add(user);if(users.Items.Count>0)users.SelectedIndex=0;state.Text=r;state.Foreground=OutcomeBrush(r);});}),Button(T("Desbloquear conta","Unlock account"),()=>change(false)),Button(T("Remover senha local","Clear local password"),()=>change(true)));
    }
}
}
