using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Aurora {
public static class PackageProfiles {
    public static void Save(string file,IEnumerable<string> ids){
        string[] values=(ids??new string[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(values.Length==0||values.Length>200||values.Any(x=>!Runner.ValidId(x)))throw new ArgumentException("Invalid package list");
        new XElement("ouo-packages",new XAttribute("version","1"),values.Select(x=>new XElement("package",new XAttribute("id",x)))).Save(file);
    }
    public static string[] Load(string file){
        XElement root=XElement.Load(file);if(root.Name!="ouo-packages"||(string)root.Attribute("version")!="1")throw new InvalidDataException("Not an OuO package list");
        string[] ids=root.Elements("package").Select(x=>(string)x.Attribute("id")).ToArray();
        if(ids.Length==0||ids.Length>200||ids.Any(x=>!Runner.ValidId(x)))throw new InvalidDataException("Invalid package list");
        return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
}
