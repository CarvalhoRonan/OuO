using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Aurora {
public sealed class OfficeAutoJob {
    public string Product,Architecture,Language,Version;
    public string[] Apps;
    public bool AcceptTerms,RemoveExisting;
    public OfficeEdition Edition {get{return OfficeEdition.All().Single(x=>x.Product==Product);}}
    public void Validate(){
        if(!AcceptTerms)throw new ArgumentException(L.T("Confirme os termos de licença antes de instalar.","Accept the license terms before installing."));
        if(String.IsNullOrEmpty(Product)||!OfficeEdition.All().Any(x=>x.Product==Product))throw new ArgumentException("Invalid Office edition");
        OfficeConfiguration.Build(Edition,Architecture,Language,Version,Apps??new string[0],null,true);
    }
    public string Encode(){Validate();string joined=String.Join(",",Apps);string payload=String.Join("|",new[]{"2",Product,Architecture,Language,Version??"",joined,"yes",RemoveExisting?"remove":"keep"});return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));}
    public static OfficeAutoJob Decode(string encoded){
        if(encoded==null||encoded.Length>8192)throw new ArgumentException("Invalid Office request");
        string[] p=Encoding.UTF8.GetString(Convert.FromBase64String(encoded)).Split('|');
        if(!((p.Length==7&&p[0]=="1")||(p.Length==8&&p[0]=="2"&&(p[7]=="remove"||p[7]=="keep")))||p[6]!="yes")throw new ArgumentException("Invalid Office request");
        var job=new OfficeAutoJob{Product=p[1],Architecture=p[2],Language=p[3],Version=p[4],Apps=p[5].Split(','),AcceptTerms=true,RemoveExisting=p.Length==8&&p[7]=="remove"};job.Validate();return job;
    }
}
public static class OfficeAutomatic {
    public static void ConfigureUnattended(XDocument configuration){configuration.Root.Element("Display").SetAttributeValue("Level","None");}
    const string Page="https://www.microsoft.com/en-us/download/details.aspx?id=49117";
    const string KnownDownload="https://download.microsoft.com/download/6c1eeb25-cf8b-41d9-8d0d-cc1dbc032140/officedeploymenttool_20430-20146.exe";
    static readonly string[] AppProcesses={"WINWORD","EXCEL","POWERPNT","OUTLOOK","ONENOTE","MSACCESS","MSPUB","VISIO","WINPROJ"};
    public static bool IsOfficialDownload(string value){Uri u;return Uri.TryCreate(value,UriKind.Absolute,out u)&&u.Scheme=="https"&&u.Host.Equals("download.microsoft.com",StringComparison.OrdinalIgnoreCase)&&Regex.IsMatch(u.AbsolutePath,@"^/download/[A-Za-z0-9/._-]+/officedeploymenttool_[0-9-]+\.exe$",RegexOptions.IgnoreCase)&&String.IsNullOrEmpty(u.Query)&&String.IsNullOrEmpty(u.UserInfo);}
    public static string Resolve(string html){
        string decoded=WebUtility.HtmlDecode((html??"").Replace("\\/","/"));
        foreach(Match m in Regex.Matches(decoded,@"https://download\.microsoft\.com/download/[A-Za-z0-9/._-]+/officedeploymenttool_[0-9-]+\.exe",RegexOptions.IgnoreCase))if(IsOfficialDownload(m.Value))return m.Value;
        throw new InvalidDataException(L.T("A página oficial não forneceu um link do ODT reconhecido.","The official page did not provide a recognized ODT link."));
    }
    static HttpWebResponse Request(Uri uri){
        ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;
        if(uri.Scheme!="https"||uri.UserInfo!=""||!(uri.Host.Equals("www.microsoft.com",StringComparison.OrdinalIgnoreCase)||uri.Host.Equals("download.microsoft.com",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Untrusted download host");
        var req=(HttpWebRequest)WebRequest.Create(uri);req.AllowAutoRedirect=false;req.Timeout=30000;req.ReadWriteTimeout=60000;req.UserAgent="OuO/0.5.2 (Office Deployment Tool)";
        for(int i=0;i<5;i++){
            HttpWebResponse response=(HttpWebResponse)req.GetResponse();
            if((int)response.StatusCode<300)return response;
            string location=response.Headers["Location"];response.Close();
            if(String.IsNullOrEmpty(location))throw new IOException("Redirect without Location");
            uri=new Uri(uri,location);
            if(uri.Scheme!="https"||uri.UserInfo!=""||!(uri.Host.Equals("www.microsoft.com",StringComparison.OrdinalIgnoreCase)||IsOfficialDownload(uri.AbsoluteUri)))throw new InvalidDataException("Untrusted redirect");
            req=(HttpWebRequest)WebRequest.Create(uri);req.AllowAutoRedirect=false;req.Timeout=30000;req.ReadWriteTimeout=60000;req.UserAgent="OuO/0.5.2 (Office Deployment Tool)";
        }
        throw new IOException("Too many redirects");
    }
    static string ReadPage(){using(var response=Request(new Uri(Page)))using(var stream=response.GetResponseStream())using(var reader=new StreamReader(stream)){char[] data=new char[8*1024*1024];int length=reader.ReadBlock(data,0,data.Length);if(length==data.Length)throw new InvalidDataException("Download page too large");return new string(data,0,length);}}
    static void Download(string url,string destination,Action<string> progress){
        if(!IsOfficialDownload(url))throw new InvalidDataException("Untrusted ODT URL");
        using(var response=Request(new Uri(url)))using(var input=response.GetResponseStream())using(var output=new FileStream(destination+".download",FileMode.CreateNew,FileAccess.Write,FileShare.None)){
            byte[] buffer=new byte[65536];long total=0;int read;while((read=input.Read(buffer,0,buffer.Length))>0){total+=read;if(total>64*1024*1024)throw new InvalidDataException("ODT package too large");output.Write(buffer,0,read);progress(L.T("Baixando ferramenta oficial: ","Downloading official tool: ")+(total/1024)+" KiB");}
            if(total<100000||response.ContentLength>=0&&total!=response.ContentLength)throw new InvalidDataException("Incomplete ODT download");
        }
        File.Move(destination+".download",destination);
    }
    static void VerifySigned(string path,bool setup){
        if(!File.Exists(path))throw new FileNotFoundException("ODT executable missing",path);
        string details=(FileVersionInfo.GetVersionInfo(path).FileDescription??"")+" "+(FileVersionInfo.GetVersionInfo(path).ProductName??"");
        if(setup&&details.IndexOf("Office",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidDataException("Extracted tool does not identify as Office Deployment Tool");
        if(setup){OfficeConfiguration.VerifyTool(path);return;}
        CommandResult r=Runner.PS("$s=Get-AuthenticodeSignature -LiteralPath "+Runner.QuotePS(path)+"; if ($s.Status -ne 'Valid' -or $s.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') { throw 'Microsoft signature required' }; 'Verified'",30000);
        if(r.Code!=0)throw new InvalidDataException(r.Output);
    }
    static string Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    static void Preflight(OfficeAutoJob job){
        if(Environment.OSVersion.Version.Build<10240)throw new InvalidOperationException(L.T("Esta edição do Office requer Windows 10 ou 11.","This Office edition requires Windows 10 or 11."));
        if(job.Architecture=="64"&&!Environment.Is64BitOperatingSystem)throw new InvalidOperationException("64-bit Office requires 64-bit Windows");
        foreach(string name in AppProcesses)if(Process.GetProcessesByName(name).Length>0)throw new InvalidOperationException(L.T("Feche os aplicativos do Office e salve seus documentos: ","Close Office apps and save documents: ")+name);
        foreach(RegistryView view in new[]{RegistryView.Registry64,RegistryView.Registry32}){
            using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view))using(var key=root.OpenSubKey(@"SOFTWARE\Microsoft\Office\ClickToRun\Configuration"))if(key!=null){string platform=Convert.ToString(key.GetValue("Platform",""));if(!job.RemoveExisting&&(platform.Equals("x86",StringComparison.OrdinalIgnoreCase)&&job.Architecture=="64"||platform.Equals("x64",StringComparison.OrdinalIgnoreCase)&&job.Architecture=="32"))throw new InvalidOperationException(L.T("O Office instalado usa outra arquitetura. Marque “Remover Office anterior” na instalação automática ou escolha a arquitetura já instalada.","Installed Office uses a different architecture. Select “Remove previous Office” for automatic installation or choose its existing architecture."));}
            using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view))using(var uninstall=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))if(uninstall!=null)foreach(string id in uninstall.GetSubKeyNames())using(var item=uninstall.OpenSubKey(id)){if(item==null)continue;string name=Convert.ToString(item.GetValue("DisplayName",""));if(!job.RemoveExisting&&Convert.ToInt32(item.GetValue("WindowsInstaller",0))==1&&Regex.IsMatch(name,@"^(Microsoft )?(Office|Visio|Project)\b",RegexOptions.IgnoreCase))throw new InvalidOperationException(L.T("Foi encontrado um Office/Visio/Project por MSI. Marque “Remover Office anterior” na instalação automática para substituí-lo: ","An MSI Office/Visio/Project installation was found. Select “Remove previous Office” for automatic installation to replace it: ")+name);}
        }
    }
    public static string Install(OfficeAutoJob job,Action<string> progress){
        job.Validate();Preflight(job);DailyMaintenance.ProtectRoot();
        string folder=Path.Combine(DailyMaintenance.Root,"Office-Job-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));string tool=Path.Combine(folder,"Tool"),media=Path.Combine(folder,"Media");Directory.CreateDirectory(tool);Directory.CreateDirectory(media);
        string report=Path.Combine(folder,"report.txt");Action<string> stage=s=>{progress(s);File.AppendAllText(report,DateTime.UtcNow.ToString("o")+" "+s+Environment.NewLine);};
        stage(L.T("Localizando ODT na Microsoft…","Finding ODT at Microsoft…"));string url;try{url=Resolve(ReadPage());}catch(Exception error){if(!(error is WebException||error is InvalidDataException))throw;url=KnownDownload;stage(L.T("Usando versão oficial conhecida do ODT.","Using a known official ODT version."));}
        string wrapper=Path.Combine(folder,"officedeploymenttool.exe");stage(L.T("Baixando ODT…","Downloading ODT…"));Download(url,wrapper,progress);VerifySigned(wrapper,false);stage("ODT URL: "+url+" SHA-256: "+Hash(wrapper));
        stage(L.T("Extraindo a ferramenta…","Extracting deployment tool…"));var extracted=Runner.Run(wrapper,"/quiet /extract:"+Args.Q(tool),120000,folder);if(extracted.Code!=0)throw new IOException("ODT extraction: "+extracted.Code+" "+extracted.Output);
        string setup=Path.Combine(tool,"setup.exe");VerifySigned(setup,true);stage("setup.exe SHA-256: "+Hash(setup));
        string xml=Path.Combine(folder,"configuration.xml");var config=OfficeConfiguration.Build(job.Edition,job.Architecture,job.Language,job.Version,job.Apps,media,true);ConfigureUnattended(config);if(job.RemoveExisting)config.Root.Add(new XElement("RemoveMSI"));config.Save(xml);
        stage(L.T("Baixando os arquivos do Office…","Downloading Office files…"));var downloaded=Runner.Run(setup,"/download "+Args.Q(xml),Int32.MaxValue,tool,allowBackground:true);if(downloaded.Code!=0)throw new IOException("Office download: "+downloaded.Code+" "+downloaded.Output);
        Preflight(job);if(job.RemoveExisting)OfficeReplacement.RemoveClickToRun(setup,folder,stage);stage(L.T("Instalando os aplicativos selecionados…","Installing selected apps…"));var installed=Runner.Run(setup,"/configure "+Args.Q(xml),Int32.MaxValue,tool,allowBackground:true);if(installed.Code!=0&&installed.Code!=3010&&installed.Code!=1641)throw new IOException("Office installation: "+installed.Code+" "+installed.Output);
        stage(L.T("Instalação concluída pelo ODT. Entre com sua conta ou licença para ativar. Reinicialização: ","ODT installation completed. Sign in or enter your license to activate. Restart: ")+(installed.Code==3010||installed.Code==1641?L.T("necessária","required"):L.T("não solicitada","not requested")));
        return L.T("Instalação concluída. Relatório: ","Installation completed. Report: ")+report;
    }
}
public partial class MainWindow {
    void StartOfficeAuto(OfficeAutoJob job,bool resumed){
        if(repairRunning)return;
        try{job.Validate();if(machine==null||machine.Build<10240)throw new InvalidOperationException(T("Office atual requer Windows 10 ou 11.","Current Office requires Windows 10 or 11."));}catch(Exception e){ShowNotice(e.Message);return;}
        if(!resumed&&!Confirm(T("Baixar e instalar Office com os aplicativos escolhidos? Salve seus documentos antes de continuar. Edição: ","Download and install Office with selected apps? Save documents first. Edition: ")+job.Edition.Name+"\n"+String.Join(", ",job.Apps)+"\n"+job.Architecture+" bits • "+job.Language+(job.RemoveExisting?T("\n\nRemover as instalações anteriores, incluindo Visio e Project. Os aplicativos precisarão ser reinstalados e ativados conforme sua licença.\n","\n\nRemove previous installations, including Visio and Project. Apps must be reinstalled and activated with your license.\n")+String.Join("\n",OfficeReplacement.Inventory().ToArray()):"")))return;
        if(!machine.Admin){try{var info=new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--office-auto "+Args.Q(job.Encode())){UseShellExecute=true,Verb="runas"};Process.Start(info);exitFinalized=true;Close();}catch(Exception e){ShowNotice(e.Message);}return;}
        ShowPage("office");TextBox progressBox=officeAutoOutput;if(progressBox==null)return;progressBox.Text="";repairRunning=true;
        Work("Office automatic installation",()=>OfficeAutomatic.Install(job,s=>{ReportStage(s);Dispatcher.BeginInvoke(new Action(()=>{progressBox.AppendText(s+Environment.NewLine);progressBox.ScrollToEnd();status.Text=s;}));}),r=>{repairRunning=false;progressBox.AppendText(r+Environment.NewLine);progressBox.ScrollToEnd();});
    }
}
}
