using System;
using System.IO;
using System.Xml.Linq;
namespace Aurora {
public static class AtomicFiles {
    public static void SaveXml(XElement value,string path){
        path=Path.GetFullPath(path);
        string temporary=Path.Combine(Path.GetDirectoryName(path),Path.GetRandomFileName());
        bool created=false;
        try{
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){created=true;value.Save(stream);}
            if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
        catch{if(created)try{if(File.Exists(temporary))File.Delete(temporary);}catch{}throw;}
    }
}
}
