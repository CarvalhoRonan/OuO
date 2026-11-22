using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using LibreHardwareMonitor.Hardware;

namespace AuroraSensors {
public static class Program {
    static bool stop;
    public static int Main(string[] args){
        if(args.Contains("--dependency-test")){Console.WriteLine(typeof(Computer).Assembly.FullName);return 0;}
        int parent=0;for(int i=0;i<args.Length-1;i++)if(args[i]=="--parent")Int32.TryParse(args[i+1],out parent);
        bool once=args.Contains("--once");if(!once&&parent==0)return 2;
        Computer computer=new Computer{IsCpuEnabled=true,IsGpuEnabled=true,IsMotherboardEnabled=true,IsMemoryEnabled=false,IsControllerEnabled=false,IsNetworkEnabled=false,IsStorageEnabled=true};
        Thread input=new Thread(()=>{try{while(Console.ReadLine()!=null){stop=true;break;}}catch{stop=true;}});input.IsBackground=true;if(!once)input.Start();
        try{
            computer.Open();
            do{
                if(parent!=0)try{using(Process p=Process.GetProcessById(parent)){if(p.HasExited)break;}}catch{break;}
                XElement root=new XElement("snapshot",new XAttribute("source","LibreHardwareMonitor 0.9.6"),new XAttribute("status","ready"));
                foreach(IHardware hardware in computer.Hardware)Visit(hardware,root);
                Console.WriteLine(root.ToString(SaveOptions.DisableFormatting));Console.Out.Flush();
                if(once)break;for(int i=0;i<15&&!stop;i++)Thread.Sleep(200);
            }while(!stop);return 0;
        }catch(Exception e){Console.WriteLine(new XElement("snapshot",new XAttribute("source","LibreHardwareMonitor 0.9.6"),new XAttribute("status","error: "+e.Message)).ToString(SaveOptions.DisableFormatting));return 1;}
        finally{computer.Close();}
    }
    static void Visit(IHardware hardware,XElement root){
        root.Add(new XElement("hardware",new XAttribute("type",hardware.HardwareType),new XAttribute("name",hardware.Name)));
        try{hardware.Update();foreach(ISensor sensor in hardware.Sensors){
            if(!sensor.Value.HasValue||(sensor.SensorType!=SensorType.Temperature&&sensor.SensorType!=SensorType.Load))continue;
            root.Add(new XElement("sensor",new XAttribute("hardware",hardware.HardwareType),new XAttribute("device",hardware.Name),new XAttribute("name",sensor.Name),new XAttribute("type",sensor.SensorType),new XAttribute("value",sensor.Value.Value.ToString(CultureInfo.InvariantCulture))));
        }}catch(Exception e){root.Add(new XElement("warning",e.Message));}
        foreach(IHardware child in hardware.SubHardware)Visit(child,root);
    }
}
}
