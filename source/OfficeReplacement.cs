using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;
namespace Aurora {
public static class OfficeReplacement {
    public static string Summarize(IEnumerable<string> entries){
        string[] all=(entries??Enumerable.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(all.Length==0)return L.T("Nenhuma instalação reconhecida foi encontrada.","No recognized installation was found.");
        string[] components={"Proofing","Shared"," MUI"," OSM","32-bit Components","64-bit Components","Language Pack"};
        string[] suites=all.Where(x=>!components.Any(c=>x.IndexOf(c,StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
        var lines=new List<string>();
        foreach(string suite in suites){
            lines.Add(suite);
            if(suite.StartsWith("Office MSI",StringComparison.OrdinalIgnoreCase)){
                string bits=all.Any(x=>x.IndexOf("32-bit Components",StringComparison.OrdinalIgnoreCase)>=0)?"32 bits":all.Any(x=>x.IndexOf("64-bit Components",StringComparison.OrdinalIgnoreCase)>=0)?"64 bits":L.T("arquitetura não confirmada","architecture unconfirmed");
                lines.Add(L.T("Arquitetura: ","Architecture: ")+bits);
            }
        }
        if(suites.Length==0)lines.Add(L.T("Componentes Office encontrados; edição principal não identificada.","Office components found; main edition unidentified."));
        int extras=all.Length-suites.Length;
        if(extras>0)lines.Add(extras+L.T(" componente(s) auxiliar(es): idiomas, revisão e integração."," supporting components: languages, proofing and integration."));
        return String.Join("\n",lines.ToArray());
    }
    public static List<string> Inventory(){
        var list=new List<string>();
        foreach(RegistryView view in new[]{RegistryView.Registry64,RegistryView.Registry32})try{
            using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view)){
                using(var config=root.OpenSubKey(@"SOFTWARE\Microsoft\Office\ClickToRun\Configuration"))if(config!=null){
                    string ids=Convert.ToString(config.GetValue("ProductReleaseIds",""));
                    if(!String.IsNullOrWhiteSpace(ids))list.Add("Office Click-to-Run • "+ids+" • "+Convert.ToString(config.GetValue("Platform",""))+" • "+Convert.ToString(config.GetValue("VersionToReport","")));
                }
                using(var uninstall=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))if(uninstall!=null)foreach(string id in uninstall.GetSubKeyNames())using(var item=uninstall.OpenSubKey(id)){
                    if(item==null)continue;string name=Convert.ToString(item.GetValue("DisplayName",""));
                    if(Convert.ToString(item.GetValue("WindowsInstaller",0))=="1"&&Regex.IsMatch(name,@"^(Microsoft )?(Office|Visio|Project)\b",RegexOptions.IgnoreCase))list.Add("Office MSI • "+name);
                }
            }
        }catch{}
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
    public static XDocument RemovalConfiguration(){
        return new XDocument(new XElement("Configuration",new XElement("Remove",new XAttribute("All","TRUE")),new XElement("Display",new XAttribute("Level","Full"),new XAttribute("AcceptEULA","TRUE")),new XElement("Property",new XAttribute("Name","FORCEAPPSHUTDOWN"),new XAttribute("Value","FALSE"))));
    }
    public static void RemoveClickToRun(string setup,string folder,Action<string> stage){
        if(!Inventory().Any(x=>x.StartsWith("Office Click-to-Run",StringComparison.Ordinal)))return;
        string file=Path.Combine(folder,"remove-existing-office.xml");var removal=RemovalConfiguration();OfficeAutomatic.ConfigureUnattended(removal);removal.Save(file);
        stage(L.T("Removendo o Office Click-to-Run anterior com a ferramenta Microsoft…","Removing previous Click-to-Run Office with Microsoft's tool…"));
        var result=Runner.Run(setup,"/configure "+Args.Q(file),Int32.MaxValue,Path.GetDirectoryName(setup),allowBackground:true);
        if(result.Code==3010||result.Code==1641)throw new IOException(L.T("O Office anterior foi removido, mas o Windows solicitou reinicialização. Reinicie e execute a instalação novamente.","Previous Office was removed, but Windows requested a restart. Restart and run the installation again."));
        if(result.Code!=0)throw new IOException(L.T("Falha ao remover o Office anterior: ","Could not remove previous Office: ")+result.Code+"\n"+result.Output);
        if(Inventory().Any(x=>x.StartsWith("Office Click-to-Run",StringComparison.Ordinal)))throw new IOException(L.T("O Office anterior ainda foi detectado. A instalação nova foi interrompida para revisar o conflito.","Previous Office is still detected. New installation stopped to review the conflict."));
        stage(L.T("OK • Office Click-to-Run anterior removido.","OK • Previous Click-to-Run Office removed."));
    }
}
}
