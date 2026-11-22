using System;

namespace Aurora {
public static class WingetSetup {
    public static string Repair(){
        const string script="[Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor 3072; "+
            "Install-PackageProvider -Name NuGet -Force -Scope CurrentUser -ErrorAction Stop | Out-Null; "+
            "Install-Module -Name Microsoft.WinGet.Client -Force -Scope CurrentUser -Repository PSGallery -ErrorAction Stop | Out-Null; "+
            "Import-Module Microsoft.WinGet.Client -ErrorAction Stop; Repair-WinGetPackageManager -Force -Latest -ErrorAction Stop; "+
            "Write-Output 'WinGet repair command completed.'";
        var result=Runner.PS(script,600000);
        return (result.Code==0?"OK • ":"ERROR • ")+result.Output;
    }
}
}
