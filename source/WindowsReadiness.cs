using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Aurora {
public static class WindowsReadiness {
    public static string Explain(Machine machine,string target){
        if(machine==null)return L.T("Sistema ainda não detectado.","System not detected yet.");
        target=target??"Windows 11 26H2";
        var text=new StringBuilder();text.AppendLine(L.T("Instalado: ","Installed: ")+machine.Caption+" • "+machine.Arch+" • build "+machine.Build);
        text.AppendLine(L.T("Versão escolhida: ","Selected release: ")+target);
        double ram=machine.Total/1048576.0;string tpm="";try{foreach(var device in Machine.Query("root\\CIMV2\\Security\\MicrosoftTpm","SELECT SpecVersion FROM Win32_Tpm"))using(device){tpm=Machine.Value(device,"SpecVersion");break;}}catch{}
        string secure="?";if(machine.Build>=9200){var boot=Runner.PS("try { Write-Output (Confirm-SecureBootUEFI -ErrorAction Stop) } catch { Write-Output '?' }",15000);if(boot.Code==0){string value=boot.Output.Trim();if(value=="True"||value=="False")secure=value;}}
        var problems=new List<string>();
        if(!Environment.Is64BitOperatingSystem)problems.Add(L.T("Windows de 32 bits","32-bit Windows"));
        if(ram>0&&ram<4)problems.Add(L.T("menos de 4 GB de RAM","less than 4 GB RAM"));
        if(tpm.Length>0&&!tpm.StartsWith("2.0",StringComparison.Ordinal))problems.Add("TPM "+tpm);
        if(secure=="False")problems.Add(L.T("Secure Boot desligado","Secure Boot disabled"));
        if(target.StartsWith("Windows 11",StringComparison.Ordinal)){
            text.AppendLine(L.T("Caminho oficial: ","Official route: ")+(problems.Count>0?L.T("requisitos básicos não atendidos: ","basic requirements missing: ")+String.Join(", ",problems):L.T("pré-checagem sem bloqueio básico confirmado; CPU, drivers e bloqueios ainda dependem do instalador.","precheck found no confirmed basic block; CPU, drivers and holds still require Setup.")));
            text.AppendLine(L.T("Com ajustes: o contorno de TPM/CPU permite apenas uma tentativa; mídia, instruções da CPU e suporte continuam sujeitos a verificação.","With adjustments: the TPM/CPU bypass allows only an attempt; media, CPU instructions and support still require checking."));
            if(target.EndsWith("26H1",StringComparison.Ordinal))text.AppendLine(L.T("26H1 foi criada para aparelhos novos específicos; não a trate como atualização comum.","26H1 targets specific new devices; do not treat it as a normal upgrade."));
        }else text.AppendLine(L.T("Caminho oficial: confira a rota com a mídia da versão escolhida. O suporte geral do Windows 10 terminou; Windows Update não faz downgrade do Windows 11.","Official route: check the path with matching media. General Windows 10 support ended; Windows Update cannot downgrade Windows 11."));
        text.AppendLine("RAM: "+(ram>0?ram.ToString("0.0")+" GB":"?")+" • TPM: "+(tpm.Length>0?tpm:"?")+" • Secure Boot: "+secure);
        text.AppendLine("CPU: "+machine.Cpu+L.T(" • validar na mídia oficial"," • validate with official media"));
        try{var drive=new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));text.AppendLine(L.T("Espaço livre: ","Free space: ")+(drive.AvailableFreeSpace/1073741824.0).ToString("0.0")+" GB");}catch{}
        return text.ToString();
    }
    public static string ScanMedia(string setup,string logFolder,int currentBuild){
        if(currentBuild<10240)throw new InvalidOperationException(L.T("A verificação automatizada desta mídia exige Windows 10 ou posterior. Consulte a rota de migração para Windows 7/8.","Automated media scan requires Windows 10 or later. Check the migration path for Windows 7/8."));
        string file=Args.LocalPath(setup);if(!File.Exists(file)||!Path.GetFileName(file).Equals("setup.exe",StringComparison.OrdinalIgnoreCase))throw new FileNotFoundException("Select the setup.exe from official Windows installation media",file);
        string folder=Path.GetDirectoryName(file);if(!File.Exists(Path.Combine(folder,"sources","install.wim"))&&!File.Exists(Path.Combine(folder,"sources","install.esd")))throw new InvalidDataException("Windows installation image was not found beside setup.exe");
        var signature=Runner.PS("$s=Get-AuthenticodeSignature -LiteralPath "+Runner.QuotePS(file)+"; if($s.Status -ne 'Valid' -or $s.SignerCertificate.Subject -notmatch 'Microsoft'){throw 'Signed Microsoft setup.exe required'}; Write-Output 'Microsoft signature verified'",30000);
        if(signature.Code!=0)throw new InvalidDataException(signature.Output);
        Directory.CreateDirectory(logFolder);string log=Path.Combine(logFolder,"Windows-compat-"+DateTime.Now.ToString("yyyyMMddHHmmss")+".zip");
        var result=Runner.Run(file,"/auto upgrade /quiet /compat scanonly /copylogs "+Args.Q(log),1800000,folder);
        uint code=unchecked((uint)result.Code);string status=code==0xC1900210?L.T("OK • O instalador não encontrou bloqueio nesta verificação.","OK • Setup found no block in this scan."):
            code==0xC1900200?L.T("ERROR • Requisitos do sistema bloqueiam a atualização.","ERROR • System requirements block the upgrade."):
            code==0xC1900208?L.T("ERROR • Um aplicativo ou driver exige ação antes de atualizar.","ERROR • An app or driver requires action before upgrading."):
            code==0xC190020E?L.T("ERROR • Espaço insuficiente para atualizar.","ERROR • Insufficient disk space to upgrade."):
            code==0xC1900204?L.T("ERROR • Esta rota não preserva os itens escolhidos.","ERROR • This route cannot preserve the selected items."):
            L.T("Verificação inconclusiva. Código: ","Scan inconclusive. Code: ")+"0x"+code.ToString("X8");
        return status+"\n"+L.T("A análise refere-se à mídia escolhida; confira se ela contém a versão desejada.","The scan refers to the chosen media; check that it contains the desired release.")+"\n"+L.T("Mídia: ","Media: ")+folder+"\n"+L.T("Registro: ","Log: ")+log+"\n"+result.Output;
    }
}
}
