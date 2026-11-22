using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using Microsoft.Win32;
namespace Aurora {
public static class DriverInventory {
    static readonly object gate=new object();static List<DriverRecord> cached;static DateTime at;
    public static void Invalidate(){lock(gate){cached=null;}}
    public static List<DriverRecord> Read(bool force=false){lock(gate){
        if(!force&&cached!=null&&(DateTime.UtcNow-at).TotalMinutes<10)return new List<DriverRecord>(cached);
        var ids=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);var names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        try{foreach(ManagementObject device in Machine.Query("root\\cimv2","SELECT DeviceID,Name,HardwareID,CompatibleID FROM Win32_PnPEntity"))using(device){
            string key=Machine.Value(device,"DeviceID");var values=new List<string>();
            foreach(string property in new[]{"HardwareID","CompatibleID"})try{var array=device[property] as string[];if(array!=null)values.AddRange(array);}catch{}
            if(key.Length>0){ids[key]=values.Where(x=>!String.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();names[key]=Machine.Value(device,"Name");}
        }}catch{}
        var found=new List<DriverRecord>();
        foreach(ManagementObject item in Machine.Query("root\\cimv2","SELECT DeviceName,DriverProviderName,DriverVersion,DriverDate,DeviceID FROM Win32_PnPSignedDriver"))using(item){
            string name=Machine.Value(item,"DeviceName"),id=Machine.Value(item,"DeviceID");if(String.IsNullOrWhiteSpace(name))continue;
            string[] hardware;ids.TryGetValue(id,out hardware);string date=Machine.Value(item,"DriverDate");
            try{if(date.Length>0)date=ManagementDateTimeConverter.ToDateTime(date).ToString("dd/MM/yyyy");}catch{}
            found.Add(new DriverRecord{Name=name,Provider=Machine.Value(item,"DriverProviderName"),Version=Machine.Value(item,"DriverVersion"),Date=date,DeviceId=id,HardwareIds=hardware??new string[0]});
        }
        // Win32_PnPSignedDriver may lag behind an installation or omit a root
        // device. Read installed class metadata only for present PnP devices.
        var seen=new HashSet<string>(found.Select(x=>x.DeviceId),StringComparer.OrdinalIgnoreCase);
        foreach(var entry in ids.Where(x=>!seen.Contains(x.Key)))try{
            string name;names.TryGetValue(entry.Key,out name);if(String.IsNullOrWhiteSpace(name))continue;
            using(var device=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\"+entry.Key)){
                string driver=device==null?"":Convert.ToString(device.GetValue("Driver"));if(!System.Text.RegularExpressions.Regex.IsMatch(driver,@"^\{[0-9a-f-]{36}\}\\[0-9]{4,}$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))continue;
                using(var installed=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\"+driver))if(installed!=null){string version=Convert.ToString(installed.GetValue("DriverVersion"));if(String.IsNullOrWhiteSpace(version))continue;found.Add(new DriverRecord{Name=name,DeviceId=entry.Key,HardwareIds=entry.Value,Version=version,Provider=Convert.ToString(installed.GetValue("ProviderName")),Date=Convert.ToString(installed.GetValue("DriverDate"))});}
            }
        }catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}catch(System.IO.IOException){}
        cached=found.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();at=DateTime.UtcNow;return new List<DriverRecord>(cached);
    }}
    public static DriverRecord[] Match(IEnumerable<DriverRecord> records,string hardwareId){
        if(String.IsNullOrWhiteSpace(hardwareId))return new DriverRecord[0];
        return records.Where(x=>x.HardwareIds.Any(id=>id.Equals(hardwareId,StringComparison.OrdinalIgnoreCase))||String.Equals(x.DeviceId,hardwareId,StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
}
