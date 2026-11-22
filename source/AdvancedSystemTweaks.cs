using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;

namespace Aurora {
public partial class MainWindow {
    void AdvancedSystemTweaks(){
        tweakActionCategory="energy";
        var card=Card(T("Recursos avançados de energia e armazenamento","Advanced power and storage features"),T("Consulte o estado antes de alterar. Desativar hibernação também pode desativar a inicialização rápida; o armazenamento reservado ajuda o Windows Update.","Check the state before changing it. Disabling hibernation can also disable Fast Startup; reserved storage helps Windows Update."));
        TextBox report=Input("");report.IsReadOnly=true;report.AcceptsReturn=true;report.Height=125;
        Row(card,Button(T("Checar hibernação","Check hibernation"),()=>Work(T("Checar hibernação","Check hibernation"),()=>Describe(Runner.Run("powercfg.exe","/a",30000)),r=>report.Text=r)),Button(T("Ativar hibernação","Enable hibernation"),()=>{
            if(!RequireAdmin()||!Confirm(T("Ativar hibernação no Windows?","Enable Windows hibernation?")))return;Work(T("Ativar hibernação","Enable hibernation"),()=>Describe(Runner.Run("powercfg.exe","/hibernate on",30000)),r=>report.Text=r);
        }),Button(T("Desativar hibernação","Disable hibernation"),()=>{
            if(!RequireAdmin()||!Confirm(T("Desativar hibernação? Isso também pode desativar a Inicialização Rápida e remover o arquivo de hibernação.","Disable hibernation? This may also disable Fast Startup and remove the hibernation file.")))return;Work(T("Desativar hibernação","Disable hibernation"),()=>Describe(Runner.Run("powercfg.exe","/hibernate off",30000)),r=>report.Text=r);
        }));
        if(machine!=null&&machine.Build>=19041)Row(card,Button(T("Checar armazenamento reservado","Check reserved storage"),()=>Work(T("Checar armazenamento reservado","Check reserved storage"),()=>Describe(Runner.Run("dism.exe","/Online /Get-ReservedStorageState",120000)),r=>report.Text=r)),Button(T("Ativar armazenamento reservado","Enable reserved storage"),()=>{
            if(!RequireAdmin()||!Confirm(T("Ativar o armazenamento reservado para manutenção do Windows?","Enable reserved storage for Windows maintenance?")))return;Work(T("Ativar armazenamento reservado","Enable reserved storage"),()=>Describe(Runner.Run("dism.exe","/Online /Set-ReservedStorageState /State:Enabled",180000)),r=>report.Text=r);
        }),Button(T("Desativar armazenamento reservado","Disable reserved storage"),()=>{
            if(!RequireAdmin()||!Confirm(T("Desativar armazenamento reservado? Atualizações futuras podem precisar de mais espaço livre.","Disable reserved storage? Future updates may need more free disk space.")))return;Work(T("Desativar armazenamento reservado","Disable reserved storage"),()=>Describe(Runner.Run("dism.exe","/Online /Set-ReservedStorageState /State:Disabled",180000)),r=>report.Text=r);
        }));
        AttachResult(card,report);
        tweakActionCategory="security";var encryption=Card(T("BitLocker","BitLocker"),T("A desativação inicia a descriptografia da unidade do Windows e pode demorar. Salve sua chave de recuperação e mantenha o computador ligado.","Disabling starts decrypting the Windows drive and may take time. Save your recovery key and keep the computer powered."));
        string systemDrive=Path.GetPathRoot(Environment.SystemDirectory).Substring(0,2);TextBox state=Input("");state.IsReadOnly=true;state.AcceptsReturn=true;state.Height=100;
        Row(encryption,Button(T("Checar BitLocker","Check BitLocker"),()=>Work(T("Checar BitLocker","Check BitLocker"),()=>Describe(Runner.Run("manage-bde.exe","-status "+systemDrive,30000)),r=>state.Text=r)),Button(T("Iniciar descriptografia","Start decryption"),()=>{
            if(!RequireAdmin()||!Confirm(T("Iniciar a descriptografia BitLocker de ","Start BitLocker decryption of ")+systemDrive+T("? Guarde a chave de recuperação antes de continuar. A ação não é desfeita pelo histórico do OuO.","? Save the recovery key first. OuO history cannot undo this operation.")))return;
            Work(T("Desativar BitLocker","Disable BitLocker"),()=>Describe(Runner.Run("manage-bde.exe","-off "+systemDrive,30000)),r=>state.Text=r);
        }),Button(T("Abrir gerenciamento nativo","Open native management"),()=>{try{Process.Start(new ProcessStartInfo("control.exe","/name Microsoft.BitLockerDriveEncryption"){UseShellExecute=true});}catch(Exception ex){ShowNotice(ex.Message);}}));
        AttachResult(encryption,state);
    }
}
}
