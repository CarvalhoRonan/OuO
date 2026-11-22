using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Aurora {
public sealed class SensorSnapshot {
    public string Cpu="—",Gpu="—",Vrm="—",GpuLoad="",Status="",Source="",CpuName="",GpuName="",BoardName="";
    public readonly Dictionary<string,string> StorageTemperatures=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    public DateTime Updated=DateTime.UtcNow;
    public static SensorSnapshot Parse(string line){
        XElement root=XElement.Parse(line);if(root.Name!="snapshot")throw new InvalidDataException("Invalid sensor message");
        SensorSnapshot s=new SensorSnapshot{Status=(string)root.Attribute("status")??"",Source=(string)root.Attribute("source")??""};
        foreach(XElement device in root.Elements("hardware")){string type=(string)device.Attribute("type")??"",name=(string)device.Attribute("name")??"";if(type=="Cpu")s.CpuName=name;else if(type.StartsWith("Gpu"))s.GpuName=name;else if(type=="Motherboard")s.BoardName=name;}
        List<Tuple<int,double>> cpu=new List<Tuple<int,double>>(),gpu=new List<Tuple<int,double>>();List<double> vrm=new List<double>(),load=new List<double>();var storageRank=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach(XElement item in root.Elements("sensor")){
            double value;if(!Double.TryParse((string)item.Attribute("value"),NumberStyles.Float,CultureInfo.InvariantCulture,out value)||Double.IsNaN(value)||Double.IsInfinity(value))continue;
            string hardware=(string)item.Attribute("hardware")??"",type=(string)item.Attribute("type")??"",name=(string)item.Attribute("name")??"";
            if(type=="Temperature"&&value>0&&value<=160){
                if(hardware=="Cpu"){int rank=name.IndexOf("Package",StringComparison.OrdinalIgnoreCase)>=0?0:name.IndexOf("Tctl",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Tdie",StringComparison.OrdinalIgnoreCase)>=0?1:name.IndexOf("Core",StringComparison.OrdinalIgnoreCase)>=0?2:3;cpu.Add(Tuple.Create(rank,value));}else if(hardware.StartsWith("Gpu")){int rank=name.IndexOf("Core",StringComparison.OrdinalIgnoreCase)>=0?0:name.IndexOf("Package",StringComparison.OrdinalIgnoreCase)>=0?1:name.IndexOf("Hot Spot",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Hotspot",StringComparison.OrdinalIgnoreCase)>=0?4:2;gpu.Add(Tuple.Create(rank,value));}
                else if(hardware=="Storage"){
                    string device=(string)item.Attribute("device")??"";
                    if(device.Length>0&&!System.Text.RegularExpressions.Regex.IsMatch(name,"warning|critical|threshold|limit|maximum|minimum",System.Text.RegularExpressions.RegexOptions.IgnoreCase)){
                        int rank=name.IndexOf("Composite",StringComparison.OrdinalIgnoreCase)>=0?0:name.Equals("Temperature",StringComparison.OrdinalIgnoreCase)||name.Equals("Temperature #1",StringComparison.OrdinalIgnoreCase)?1:2;
                        int existing;if(!storageRank.TryGetValue(device,out existing)||rank<existing){storageRank[device]=rank;s.StorageTemperatures[device]=value.ToString("0.0",CultureInfo.CurrentCulture)+" °C";}
                    }
                }
                else if(System.Text.RegularExpressions.Regex.IsMatch(name,"(^|[^a-z])(vrm|mos|mosfet)([^a-z]|$)",System.Text.RegularExpressions.RegexOptions.IgnoreCase))vrm.Add(value);
            }
            if(type=="Load"&&hardware.StartsWith("Gpu")&&name.IndexOf("Core",StringComparison.OrdinalIgnoreCase)>=0&&value>=0&&value<=100)load.Add(value);
        }
        if(cpu.Count>0)s.Cpu=cpu.Where(x=>x.Item1==cpu.Min(y=>y.Item1)).Max(x=>x.Item2).ToString("0.0",CultureInfo.CurrentCulture)+" °C";
        if(gpu.Count>0)s.Gpu=gpu.Where(x=>x.Item1==gpu.Min(y=>y.Item1)).Max(x=>x.Item2).ToString("0.0",CultureInfo.CurrentCulture)+" °C";
        if(vrm.Count>0)s.Vrm=vrm.Max().ToString("0.0",CultureInfo.CurrentCulture)+" °C";
        if(load.Count>0)s.GpuLoad=load.Max().ToString("0")+"%";
        return s;
    }
    public static SensorSnapshot ReadWmi(){
        try{XElement root=new XElement("snapshot",new XAttribute("source","WMI LibreHardwareMonitor"),new XAttribute("status","ready"));
            foreach(var obj in Machine.Query("root\\LibreHardwareMonitor","SELECT Identifier,Name,Value,SensorType FROM Sensor"))using(obj){
                string id=Machine.Value(obj,"Identifier").ToLowerInvariant(),hardware=id.Contains("cpu")?"Cpu":id.Contains("gpu")?"Gpu":"SuperIO";
                root.Add(new XElement("sensor",new XAttribute("hardware",hardware),new XAttribute("name",Machine.Value(obj,"Name")),new XAttribute("type",Machine.Value(obj,"SensorType")),new XAttribute("value",Machine.Value(obj,"Value"))));
            }
            return root.HasElements?Parse(root.ToString()):null;
        }catch{return null;}
    }
}
public sealed class SensorClient:IDisposable {
    readonly object gate=new object();Process process;SensorSnapshot last=new SensorSnapshot{Status="starting"};bool disposed;
    public SensorSnapshot Snapshot {get{lock(gate){if((DateTime.UtcNow-last.Updated).TotalSeconds>20)return new SensorSnapshot{Status="stale"};return last;}}}
    internal int ChildId{get{lock(gate)return process==null?0:process.Id;}}
    public void Start(){lock(gate){if(disposed)return;if(process!=null&&!process.HasExited)return;
        string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Sensors","Aurora.Sensors.exe");
        if(!File.Exists(path)){last=new SensorSnapshot{Status="missing"};return;}
        bool runtime=false;try{using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))runtime=key!=null&&Convert.ToInt32(key.GetValue("Release",0))>=461808;}catch{}
        if(!runtime){try{FileVersionInfo version=FileVersionInfo.GetVersionInfo(typeof(object).Assembly.Location);runtime=version.FileMajorPart>=4&&version.FileMinorPart>=8;}catch{}}
        if(!runtime){last=new SensorSnapshot{Status="framework-required"};return;}
        try{
            ProcessStartInfo info=new ProcessStartInfo(path,"--parent "+Process.GetCurrentProcess().Id);info.WorkingDirectory=Path.GetDirectoryName(path);info.UseShellExecute=false;info.CreateNoWindow=true;info.RedirectStandardInput=true;info.RedirectStandardOutput=true;info.RedirectStandardError=true;
            process=new Process{StartInfo=info,EnableRaisingEvents=true};
            process.OutputDataReceived+=(sender,e)=>{if(e.Data==null||e.Data.Length>200000)return;try{SensorSnapshot s=SensorSnapshot.Parse(e.Data);lock(gate)last=s;}catch{}};
            process.ErrorDataReceived+=(sender,e)=>{if(!String.IsNullOrEmpty(e.Data))lock(gate)last=new SensorSnapshot{Status="error: "+e.Data};};
            process.Exited+=(sender,e)=>{lock(gate){if(!disposed)last=new SensorSnapshot{Status="stopped"};}};
            process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();
        }catch(Exception e){if(process!=null){process.Dispose();process=null;}last=new SensorSnapshot{Status="error: "+e.Message};}
    }}
    public void Restart(){Process old;lock(gate){old=process;process=null;}if(old!=null)try{if(!old.HasExited){old.StandardInput.WriteLine("stop");if(!old.WaitForExit(3500))old.Kill();}}catch{}finally{old.Dispose();}lock(gate)last=new SensorSnapshot{Status="starting"};Start();}
    public void Dispose(){Process p;lock(gate){disposed=true;p=process;}if(p==null)return;try{if(!p.HasExited){p.StandardInput.WriteLine("stop");if(!p.WaitForExit(3500))p.Kill();}}catch{}finally{p.Dispose();}}
}
public static class SensorChecks {
    public static int Run(){string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sensor-results.txt");int child=0;SensorSnapshot snapshot=null;try{
        using(SensorClient client=new SensorClient()){
            client.Start();child=client.ChildId;for(int i=0;i<40;i++){snapshot=client.Snapshot;if(snapshot.Status=="ready"||snapshot.Status.StartsWith("error:")||snapshot.Status=="framework-required")break;System.Threading.Thread.Sleep(250);}
            if(snapshot==null||snapshot.Status!="ready")throw new Exception("Sensor client: "+(snapshot==null?"null":snapshot.Status));
        }
        bool exited=false;try{using(Process p=Process.GetProcessById(child))exited=p.HasExited;}catch(ArgumentException){exited=true;}
        if(!exited)throw new Exception("Sensor child process did not exit");
        File.WriteAllText(file,"PASS: integrated sensor engine started, XML snapshot received, owned child process stopped.\nCPU: "+snapshot.Cpu+" | GPU: "+snapshot.Gpu+" | VRM: "+snapshot.Vrm+" | GPU load: "+snapshot.GpuLoad+"\n"+DateTime.Now.ToString("s"));return 0;
    }catch(Exception e){File.WriteAllText(file,"FAIL: "+e);return 1;}}
}
}
