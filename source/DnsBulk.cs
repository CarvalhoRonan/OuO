using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Aurora {
public static class DnsBulk {
    public static string Apply(IEnumerable<AdapterInfo> adapters,DnsProfile profile,bool ipv6,string folder){
        var report=new StringBuilder();foreach(AdapterInfo adapter in adapters){
            try{
                report.AppendLine(DnsManager.ApplyProfile(adapter,profile,ipv6,folder));
            }catch(Exception ex){report.AppendLine("ERROR • "+adapter.Name+" • "+ex.Message);}
        }
        Runner.Run("ipconfig.exe","/flushdns",15000);return report.ToString();
    }
}
}
