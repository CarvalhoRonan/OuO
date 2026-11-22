using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace Aurora {
public static class LegacyUpgrade {
    public static string VerifyMicrosoftSetup(string file){
        string path=Args.LocalPath(file);
        if(!File.Exists(path)||!Path.GetFileName(path).Equals("setup.exe",StringComparison.OrdinalIgnoreCase))throw new FileNotFoundException("Select Windows setup.exe",path);
        string sources=Path.Combine(Path.GetDirectoryName(path),"sources");if(!File.Exists(Path.Combine(sources,"install.wim"))&&!File.Exists(Path.Combine(sources,"install.esd")))throw new InvalidDataException(L.T("A imagem do Windows não foi encontrada ao lado do setup.exe.","Windows installation image was not found beside setup.exe."));
        string script="$s=Get-AuthenticodeSignature -LiteralPath "+Runner.QuotePS(path)+" -ErrorAction Stop; if($s.Status -ne 'Valid' -or $null -eq $s.SignerCertificate -or $s.SignerCertificate.Subject -notmatch 'Microsoft Corporation'){throw 'Setup is not validly signed by Microsoft'}; Write-Output 'Microsoft signature valid'";
        var signature=Runner.PS(script,30000);if(signature.Code!=0)throw new InvalidDataException(L.T("O instalador não passou na verificação de assinatura da Microsoft: ","Setup failed Microsoft signature validation: ")+signature.Output);
        return path;
    }
    public static void Launch(string file){string path=VerifyMicrosoftSetup(file);Process.Start(new ProcessStartInfo(path){UseShellExecute=true,Verb="runas",WorkingDirectory=Path.GetDirectoryName(path)});}
    public static string VerifyWindows11Media(string file){
        string path=VerifyMicrosoftSetup(file),source=Path.Combine(Path.GetDirectoryName(path),"sources");
        string image=File.Exists(Path.Combine(source,"install.wim"))?Path.Combine(source,"install.wim"):Path.Combine(source,"install.esd");
        var scan=Runner.Run("dism.exe","/Get-WimInfo /WimFile:"+Args.Q(image)+" /Index:1",120000);
        Match match=Regex.Match(scan.Output??"",@"\b10\.0\.(\d{5})\b");int build;
        if(scan.Code!=0||!match.Success||!Int32.TryParse(match.Groups[1].Value,out build)||build<22000)throw new InvalidDataException(L.T("Não foi possível confirmar uma imagem do Windows 11 nesta mídia. Confira a mídia oficial e sua compatibilidade.","Could not confirm a Windows 11 image in this media. Check the official media and compatibility."));
        return path;
    }
}
public partial class MainWindow {
    void LegacyUpgradeCard(){
        var card=Card(T("Windows 7 para Windows 10 por mídia","Windows 7 to Windows 10 via media"),T("No Windows 7, a atualização usa o setup.exe de uma mídia oficial compatível. A seleção de versão no Windows Update não inicia esta rota. O suporte geral do Windows 10 terminou.","On Windows 7, upgrade uses setup.exe from compatible official media. A Windows Update target does not start this route. General Windows 10 support has ended."));
        bool supported=machine!=null&&machine.Build>=7600&&machine.Build<9200;
        TextBox file=Field(card,T("Instalador da mídia do Windows 10","Windows 10 media installer"),"");BrowseFile(card,file,"Windows setup|setup.exe");TextBlock state=Text("",13,Muted);card.Children.Add(state);
        Button check=Button(T("Verificar assinatura e arquivos da mídia","Check signature and media files"),()=>{string selected=file.Text;Work(T("Verificar mídia do Windows 10","Check Windows 10 media"),()=>{string path=LegacyUpgrade.VerifyMicrosoftSetup(selected);return "OK • "+T("Instalador assinado pela Microsoft e imagem encontrados. A compatibilidade final será verificada pelo instalador.","Microsoft-signed setup and image found. Setup will determine final compatibility.")+"\n"+path;},r=>{state.Text=r;state.Foreground=OutcomeBrush(r);});});check.IsEnabled=supported;
        Button start=Button(T("Abrir instalador verificado","Open verified setup"),()=>{if(!supported)return;string selected=file.Text;if(!Confirm(T("Abrir o instalador do Windows 10 depois de verificar a assinatura? Faça backup dos arquivos. O próprio instalador decidirá o que pode ser preservado.","Open Windows 10 setup after signature verification? Back up files first. Setup decides what can be retained.")))return;Work(T("Verificar instalador","Verify setup"),()=>LegacyUpgrade.VerifyMicrosoftSetup(selected),r=>{state.Text=r;state.Foreground=OutcomeBrush(r);if(!ResultTone.IsFailure(r))try{LegacyUpgrade.Launch(selected);}catch(Exception ex){state.Text="ERROR • "+ex.Message;state.Foreground=OutcomeBrush(state.Text);}});});start.IsEnabled=supported;
        Row(card,check,start,Button(T("Obter mídia oficial","Get official media"),()=>Open("https://www.microsoft.com/software-download/windows10")));
        var next=Card(T("Etapa seguinte • Windows 10 para Windows 11","Next step • Windows 10 to Windows 11"),T("Após concluir a primeira etapa e reiniciar, abra o OuO no Windows 10 de 64 bits. Selecione a mídia oficial do Windows 11; a instalação decide quais aplicativos e arquivos podem ser preservados.","After finishing the first stage and rebooting, open OuO on 64-bit Windows 10. Select official Windows 11 media; Setup decides which apps and files can be retained."));
        bool canStart=machine!=null&&machine.Build>=19041&&machine.Build<22000&&Environment.Is64BitOperatingSystem;
        TextBox nextMedia=Field(next,T("setup.exe da mídia do Windows 11","Windows 11 media setup.exe"),"");BrowseFile(next,nextMedia,"Windows setup|setup.exe");TextBlock nextState=Text("",13,Muted);next.Children.Add(nextState);
        Button verifyNext=Button(T("Verificar mídia do Windows 11","Verify Windows 11 media"),()=>{string selected=nextMedia.Text;Work(T("Verificar mídia Windows 11","Verify Windows 11 media"),()=>"OK • "+LegacyUpgrade.VerifyWindows11Media(selected),r=>{nextState.Text=r;nextState.Foreground=OutcomeBrush(r);});});verifyNext.IsEnabled=canStart;
        Button launchNext=Button(T("Abrir atualização supervisionada","Open supervised upgrade"),()=>{if(!canStart||!Confirm(T("Abrir o instalador do Windows 11? Faça backup dos arquivos. O instalador pode impedir a preservação de aplicativos e solicitar reinicializações.","Open Windows 11 Setup? Back up your files. Setup may prevent keeping apps and require restarts.")))return;
            string selected=nextMedia.Text;Work(T("Verificar instalador Windows 11","Verify Windows 11 setup"),()=>LegacyUpgrade.VerifyWindows11Media(selected),r=>{nextState.Text=r;nextState.Foreground=OutcomeBrush(r);if(!ResultTone.IsFailure(r))try{LegacyUpgrade.Launch(selected);}catch(Exception ex){nextState.Text="ERROR • "+ex.Message;nextState.Foreground=OutcomeBrush(nextState.Text);}});});launchNext.IsEnabled=canStart;
        Row(next,verifyNext,launchNext,Button(T("Mídia oficial do Windows 11","Official Windows 11 media"),()=>Open("https://www.microsoft.com/software-download/windows11")));
    }
}
}
