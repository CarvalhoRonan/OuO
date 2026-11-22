using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace Aurora {
public static class WindowsMaintenance {
    public static string ResetUpdateCache(){
        string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string software=Path.Combine(windows,"SoftwareDistribution"),catalog=Path.Combine(windows,"System32","catroot2");
        foreach(string path in new[]{software,catalog})if(Directory.Exists(path)&&(new DirectoryInfo(path).Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Update folder is a reparse point: "+path);
        string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string script="$ErrorActionPreference='Stop'; $services=@('bits','wuauserv','cryptsvc'); "+
            "try { foreach($n in $services){Stop-Service -Name $n -Force -ErrorAction SilentlyContinue}; "+
            "foreach($p in @("+Runner.QuotePS(software)+","+Runner.QuotePS(catalog)+")){ if(Test-Path -LiteralPath $p){$name=[IO.Path]::GetFileName($p)+'.OuO-backup-"+stamp+"'; Rename-Item -LiteralPath $p -NewName $name -ErrorAction Stop; Write-Output ('Backup: '+(Join-Path ([IO.Path]::GetDirectoryName($p)) $name))} } } "+
            "finally { foreach($n in $services){Start-Service -Name $n -ErrorAction SilentlyContinue} }; "+
            "Get-Service bits,wuauserv,cryptsvc | ForEach-Object {Write-Output ($_.Name+' • '+$_.Status)}";
        var result=Runner.PS(script,180000);return (result.Code==0?"OK • ":"ERROR • ")+result.Output;
    }
    public static string ResetLocalRegistryPolicies(string backupRoot){
        foreach(ManagementObject pc in Machine.Query("root\\cimv2","SELECT PartOfDomain FROM Win32_ComputerSystem"))using(pc)if(String.Equals(Machine.Value(pc,"PartOfDomain"),"True",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(L.T("Este PC participa de um domínio. Revise as políticas com o administrador.","This PC is domain joined. Review policies with the administrator."));
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","GroupPolicy");
        string[] files={Path.Combine(root,"Machine","Registry.pol"),Path.Combine(root,"User","Registry.pol")};
        string folder=Path.Combine(backupRoot,"LocalPolicies-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));var changed=new List<string>();
        foreach(string file in files){if(!File.Exists(file))continue;if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Policy file is a reparse point");Directory.CreateDirectory(folder);string copy=Path.Combine(folder,Path.GetFileName(Path.GetDirectoryName(file))+"-Registry.pol");File.Copy(file,copy,false);changed.Add(file);}
        foreach(string file in changed)File.Delete(file);
        var update=Runner.Run("gpupdate.exe","/force",120000);
        return (update.Code==0?"OK • ":"ERROR • ")+changed.Count+L.T(" arquivo(s) de políticas locais salvo(s) em "," local policy file(s) backed up to ")+folder+"\n"+update.Output;
    }
    public static string RestoreLocalRegistryPolicies(string backupRoot){
        string backup=Directory.Exists(backupRoot)?Directory.GetDirectories(backupRoot,"LocalPolicies-*").OrderByDescending(x=>x).FirstOrDefault():null;
        if(backup==null)throw new FileNotFoundException(L.T("Nenhuma cópia de políticas do OuO encontrada.","No OuO policy backup found."));
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","GroupPolicy");int count=0;
        var copies=new List<Tuple<string,string>>();foreach(string part in new[]{"Machine","User"}){string source=Path.Combine(backup,part+"-Registry.pol");if(!File.Exists(source))continue;string target=Path.Combine(root,part,"Registry.pol");if(File.Exists(target))throw new IOException(L.T("Políticas novas já existem; restauração automática interrompida: ","New policies already exist; automatic restore stopped: ")+target);copies.Add(Tuple.Create(source,target));}
        foreach(var copy in copies){Directory.CreateDirectory(Path.GetDirectoryName(copy.Item2));File.Copy(copy.Item1,copy.Item2,false);count++;}
        var update=Runner.Run("gpupdate.exe","/force",120000);return (update.Code==0?"OK":"ERROR")+" • "+count+L.T(" arquivo(s) restaurado(s) da cópia "," file(s) restored from backup ")+backup+"\n"+update.Output;
    }
}
public static class AccountRecovery {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct USER_INFO_1003 { [MarshalAs(UnmanagedType.LPWStr)]public string Password; }
    [DllImport("Netapi32.dll",CharSet=CharSet.Unicode)]static extern int NetUserSetInfo(string server,string user,int level,ref USER_INFO_1003 info,out uint errorParameter);
    public static string[] LocalUsers(){var names=new List<string>();foreach(ManagementObject user in Machine.Query("root\\cimv2","SELECT Name,LocalAccount,Disabled FROM Win32_UserAccount WHERE LocalAccount=True"))using(user){string name=Machine.Value(user,"Name");if(name.Length>0&&!names.Contains(name))names.Add(name);}return names.OrderBy(x=>x,StringComparer.CurrentCultureIgnoreCase).ToArray();}
    public static string SetLocalPassword(string user,string password){
        if(String.IsNullOrWhiteSpace(user)||!LocalUsers().Contains(user,StringComparer.OrdinalIgnoreCase))throw new ArgumentException(L.T("Selecione uma conta local válida.","Select a valid local account."));
        if(String.IsNullOrEmpty(password))throw new ArgumentException(L.T("Informe uma nova senha.","Enter a new password."));
        uint parameter;var info=new USER_INFO_1003{Password=password};int result=NetUserSetInfo(null,user,1003,ref info,out parameter);
        if(result!=0)throw new System.ComponentModel.Win32Exception(result);
        return "OK • "+L.T("Senha redefinida para a conta local ","Password reset for local account ")+user;
    }
}
public partial class MainWindow {
    void MaintenanceRepairCards(){
        var update=Card(T("Redefinir Windows Update","Reset Windows Update"),T("Interrompe serviços, guarda as pastas de cache com outro nome e inicia os serviços novamente. Atualizações em andamento serão interrompidas.","Stops services, renames cache folders as backups and restarts services. Updates in progress will be interrupted."));
        TextBlock updateState=Text("",13,Muted);update.Children.Add(updateState);
        Row(update,Button(T("Redefinir componentes de atualização","Reset update components"),()=>{if(!RequireAdmin()||!Confirm(T("Redefinir o cache do Windows Update? Salve seu trabalho; o OuO não apagará os backups gerados.","Reset Windows Update cache? Save your work; OuO will keep the generated backups.")))return;Work("Reset Windows Update",()=>WindowsMaintenance.ResetUpdateCache(),r=>{updateState.Text=r;updateState.Foreground=OutcomeBrush(r);});}));
        var policies=Card(T("Políticas locais","Local policies"),T("Restaura somente os arquivos de políticas locais de Registro. Políticas de domínio ou MDM não são alteradas.","Restores local Registry policy files only. Domain or MDM policies are not changed."));
        TextBlock policyState=Text("",13,Muted);policies.Children.Add(policyState);
        Row(policies,Button(T("Restaurar políticas ao padrão","Reset local policies"),()=>{if(!RequireAdmin()||!Confirm(T("Guardar e remover as políticas locais de Registro deste PC? Configurações feitas por administradores poderão voltar ao padrão.","Back up and remove this PC's local Registry policies? Administrator settings may return to defaults.")))return;Work("Reset local policies",()=>WindowsMaintenance.ResetLocalRegistryPolicies(journal.Folder),r=>{policyState.Text=r;policyState.Foreground=OutcomeBrush(r);});}),Button(T("Recuperar última cópia","Restore latest backup"),()=>{if(!RequireAdmin()||!Confirm(T("Recuperar a última cópia de políticas locais criada pelo OuO?","Restore the latest local policy backup created by OuO?")))return;Work("Restore local policies",()=>WindowsMaintenance.RestoreLocalRegistryPolicies(journal.Folder),r=>{policyState.Text=r;policyState.Foreground=OutcomeBrush(r);});}));
        var account=Card(T("Recuperação de conta local","Local account recovery"),T("Um administrador pode definir uma nova senha para uma conta local. Isto não revela a senha antiga nem recupera contas Microsoft ou de domínio.","An administrator can set a new password for a local account. This does not reveal the old password or recover Microsoft/domain accounts."));
        ComboBox users=new ComboBox{Margin=new Thickness(0,0,0,8)};account.Children.Add(users);PasswordBox password=new PasswordBox{Height=38,Padding=new Thickness(8),Background=Bg,Foreground=Ink,BorderBrush=Muted,Margin=new Thickness(0,0,0,8)};account.Children.Add(password);
        TextBlock accountState=Text("",13,Muted);account.Children.Add(accountState);
        Action refresh=()=>{string[] found=null;Work(T("Listar contas locais","List local accounts"),()=>{found=AccountRecovery.LocalUsers();return "OK • "+found.Length+T(" conta(s) local(is)."," local account(s). ");},r=>{users.Items.Clear();foreach(string name in found??new string[0])users.Items.Add(name);if(users.Items.Count>0)users.SelectedIndex=0;accountState.Text=r;accountState.Foreground=OutcomeBrush(r);});};
        Row(account,Button(T("Listar contas locais","List local accounts"),()=>refresh()),Button(T("Definir nova senha","Set new password"),()=>{if(!RequireAdmin()||users.SelectedItem==null)return;string name=Convert.ToString(users.SelectedItem),secret=password.Password;password.Clear();if(!Confirm(T("Definir uma nova senha para a conta local ","Set a new password for local account ")+name+"?"))return;Work("Local account password reset",()=>AccountRecovery.SetLocalPassword(name,secret),r=>{accountState.Text=r;accountState.Foreground=OutcomeBrush(r);});}));
        if(!preview)refresh();
        OfflineAccountCard();
    }
}
}
