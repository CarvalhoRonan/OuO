using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace Aurora {
internal static class LocalAccountCheck {
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool LogonUser(string name,string domain,string password,int type,int provider,out IntPtr token);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    static bool Accepts(string name,string password){IntPtr token;bool success=LogonUser(name,".",password,2,0,out token);if(success)CloseHandle(token);return success;}
    public static string Run(){if(!ExtendedChecks.Allowed())throw new InvalidOperationException("Disposable guest only");string name="OuOLab"+Guid.NewGuid().ToString("N").Substring(0,8),before="OuO-Bef928!",after="OuO-Aft563!";var created=Runner.Run("net.exe","user "+name+" "+before+" /add",30000);if(created.Code!=0)throw new Win32Exception(created.Output);try{if(!Accepts(name,before))throw new InvalidOperationException("Fixture's original credentials were not accepted: "+Marshal.GetLastWin32Error());string result=AccountRecovery.SetLocalPassword(name,after);if(ResultTone.IsFailure(result)||!Accepts(name,after)||Accepts(name,before))throw new InvalidOperationException("Password reset did not produce the expected logon results.");return "Production password reset on an owned guest account: new password accepted by LogonUser; old password rejected. This does not validate offline SAM recovery or Microsoft/Entra accounts.";}finally{var deleted=Runner.Run("net.exe","user "+name+" /delete",30000);if(deleted.Code!=0)throw new InvalidOperationException("Could not remove disposable account: "+deleted.Output);}}
}
}
