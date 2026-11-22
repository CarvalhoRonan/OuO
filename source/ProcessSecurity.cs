using System;
using System.IO;

namespace Aurora {
public static class ProcessSecurity {
    public static string Defender(string path){
        if(String.IsNullOrWhiteSpace(path)||!File.Exists(path))throw new FileNotFoundException("Executable unavailable",path);
        string script="if(-not (Get-Command Start-MpScan -ErrorAction SilentlyContinue)){throw 'Microsoft Defender scanner unavailable'}; Start-MpScan -ScanType CustomScan -ScanPath "+Runner.QuotePS(path)+" -ErrorAction Stop; Write-Output 'Scan completed. Review Windows Security for any detection or quarantine.'";
        var result=Runner.PS(script,900000);return (result.Code==0?"OK":"ERROR")+" • "+result.Output;
    }
    public static string AdwCleaner(string path){
        if(String.IsNullOrWhiteSpace(path)||!File.Exists(path)||!Path.GetFileName(path).StartsWith("adwcleaner",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Choose the official AdwCleaner executable");
        string verify="$s=Get-AuthenticodeSignature -LiteralPath "+Runner.QuotePS(path)+"; if($s.Status -ne 'Valid' -or $s.SignerCertificate.Subject -notmatch 'Malwarebytes'){throw 'Signature is not valid Malwarebytes'}; Write-Output 'VALID'";
        var signature=Runner.PS(verify,30000);if(signature.Code!=0||!signature.Output.Contains("VALID"))throw new InvalidDataException(signature.Output);
        var result=Runner.Run(path,"/scan",900000);return (result.Code==0?"OK":"ERROR")+" • "+result.Output+"\nLogs: C:\\AdwCleaner\\Logs";
    }
}
}
