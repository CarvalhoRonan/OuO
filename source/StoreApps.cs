using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
namespace Aurora {
public static class StoreApps {
    [DllImport("shlwapi.dll",CharSet=CharSet.Unicode)]static extern int SHLoadIndirectString(string source,StringBuilder result,uint count,IntPtr reserved);
    static string Friendly(string value,string package,string identity,string fallback){
        if(String.IsNullOrWhiteSpace(value))return fallback;
        if(!value.StartsWith("ms-resource:",StringComparison.OrdinalIgnoreCase))return value;
        string tail=value.Substring(12).TrimStart('/');
        foreach(string locator in new[]{value,"ms-resource://"+identity+"/resources/"+tail,"ms-resource://"+identity+"/"+tail}){
            var buffer=new StringBuilder(2048);try{if(SHLoadIndirectString("@{"+package+"?"+locator+"}",buffer,2048,IntPtr.Zero)==0&&buffer.Length>0)return buffer.ToString();}catch{}
        }
        return fallback;
    }
    public static string LogoPath(string directory,string relative){
        if(String.IsNullOrWhiteSpace(directory)||String.IsNullOrWhiteSpace(relative)||relative.StartsWith("ms-resource:",StringComparison.OrdinalIgnoreCase))return null;
        try{
            string root=Path.GetFullPath(directory).TrimEnd('\\')+"\\",full=Path.GetFullPath(Path.Combine(root,relative));
            if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase))return null;
            if(File.Exists(full))return full;
            string parent=Path.GetDirectoryName(full),stem=Path.GetFileNameWithoutExtension(full),extension=Path.GetExtension(full);
            if(!Directory.Exists(parent))return null;
            return Directory.GetFiles(parent,stem+".*"+extension).OrderBy(x=>x.IndexOf("targetsize-48",StringComparison.OrdinalIgnoreCase)>=0?0:x.IndexOf("targetsize-32",StringComparison.OrdinalIgnoreCase)>=0?1:x.IndexOf("scale-200",StringComparison.OrdinalIgnoreCase)>=0?2:3).FirstOrDefault();
        }catch{return null;}
    }
    static string FallbackLogoPath(string directory){
        try{
            string assets=Path.Combine(Path.GetFullPath(directory),"Assets");if(!Directory.Exists(assets)||(new DirectoryInfo(assets).Attributes&FileAttributes.ReparsePoint)!=0)return null;
            var files=new List<string>();var folders=new Queue<Tuple<string,int>>();folders.Enqueue(Tuple.Create(assets,0));
            while(folders.Count>0&&files.Count<300){var folder=folders.Dequeue();try{files.AddRange(Directory.GetFiles(folder.Item1).Take(300-files.Count));if(folder.Item2<2)foreach(string sub in Directory.GetDirectories(folder.Item1))if(((new DirectoryInfo(sub).Attributes&FileAttributes.ReparsePoint)==0))folders.Enqueue(Tuple.Create(sub,folder.Item2+1));}catch(UnauthorizedAccessException){}catch(IOException){}}
            return files.Where(x=>new[]{".png",".ico",".jpg",".jpeg"}.Contains(Path.GetExtension(x).ToLowerInvariant())&&Path.GetFileName(x).IndexOf("logo",StringComparison.OrdinalIgnoreCase)>=0)
                .OrderBy(x=>Path.GetFileName(x).IndexOf("44",StringComparison.OrdinalIgnoreCase)>=0?0:Path.GetFileName(x).IndexOf("scale-200",StringComparison.OrdinalIgnoreCase)>=0?1:2).FirstOrDefault();
        }catch{return null;}
    }
    static ImageSource LoadLogo(string file){
        if(file==null)return null;
        try{using(var stream=File.OpenRead(file)){var bitmap=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];bitmap.Freeze();return bitmap;}}catch{return null;}
    }
    static string Value(Dictionary<string,object> row,string name){object value;return row.TryGetValue(name,out value)?Convert.ToString(value):"";}
    public static List<InstalledProgram> Inventory(){
        // Query the package registry once. Opening the shell AppsFolder and every
        // AppX manifest from PowerShell made this screen take minutes on some PCs.
        const string script="$rows=@(Get-AppxPackage -ErrorAction Stop | Where-Object {-not $_.IsFramework -and -not $_.IsResourcePackage -and -not $_.NonRemovable} | ForEach-Object {[pscustomobject]@{Name=[string]$_.Name;Identity=[string]$_.Name;Package=[string]$_.PackageFullName;Location=[string]$_.InstallLocation;Version=[string]$_.Version;Publisher=[string]$_.Publisher}});ConvertTo-Json -InputObject $rows -Depth 3 -Compress";
        var result=Runner.PS(script,120000);if(result.Code!=0)throw new IOException(result.Output);
        var serializer=new JavaScriptSerializer{MaxJsonLength=16*1024*1024};
        object[] records=serializer.DeserializeObject(result.Output.Trim()) as object[];var list=new List<InstalledProgram>();if(records==null)return list;
        foreach(var row in records.OfType<Dictionary<string,object>>()){
            string package=Value(row,"Package"),identity=Value(row,"Identity"),directory=Value(row,"Location"),title=Value(row,"Name");
            var program=new InstalledProgram{Store=true,Scope="Microsoft Store",Name=title,Package=package,Location=directory,Version=Value(row,"Version"),Publisher=Value(row,"Publisher")};
            try{
                var manifest=XDocument.Load(Path.Combine(directory,"AppxManifest.xml"));
                var properties=manifest.Descendants().FirstOrDefault(x=>x.Name.LocalName=="Properties");
                var visual=manifest.Descendants().FirstOrDefault(x=>x.Name.LocalName=="VisualElements");
                Func<string,string> property=name=>properties==null?"":Convert.ToString(properties.Elements().FirstOrDefault(x=>x.Name.LocalName==name));
                string publisher=properties==null?"":properties.Elements().Where(x=>x.Name.LocalName=="PublisherDisplayName").Select(x=>x.Value).FirstOrDefault();
                program.Publisher=Friendly(publisher,package,identity,program.Publisher);
                if(title==identity&&visual!=null)program.Name=Friendly((string)visual.Attribute("DisplayName"),package,identity,title);
                string relative=visual==null?null:(string)visual.Attribute("Square44x44Logo")??(string)visual.Attribute("SmallLogo")??(string)visual.Attribute("Square150x150Logo");
                if(relative==null&&properties!=null)relative=properties.Elements().Where(x=>x.Name.LocalName=="Logo").Select(x=>x.Value).FirstOrDefault();
                program.DisplayIcon=LogoPath(directory,relative)??FallbackLogoPath(directory);program.StoreIcon=LoadLogo(program.DisplayIcon);
            }catch{}
            if(String.IsNullOrWhiteSpace(program.Name))program.Name=identity;
            list.Add(program);
        }
        return list;
    }
}
}
