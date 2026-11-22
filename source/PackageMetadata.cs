using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace Aurora {
public static class PackageMetadata {
    static readonly object gate=new object();static readonly Dictionary<string,string> cached=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    public static string Parse(string output){
        string[] lines=(output??"").Split('\n');for(int index=0;index<lines.Length;index++){
            Match match=Regex.Match(lines[index],@"^\s*(?:Description|Descrição)\s*:\s*(.*)$",RegexOptions.IgnoreCase);if(!match.Success)continue;
            var text=new List<string>();if(match.Groups[1].Value.Trim().Length>0)text.Add(match.Groups[1].Value.Trim());
            for(int next=index+1;next<lines.Length&&text.Count<6;next++){string line=lines[next];if(!Regex.IsMatch(line,@"^\s{2,}\S"))break;text.Add(line.Trim());}
            string value=String.Join(" ",text);return value.Length>360?value.Substring(0,357)+"…":value;
        }return "";
    }
    public static string Get(string id){
        if(!Runner.ValidId(id))throw new ArgumentException("Invalid package ID");string value;lock(gate)if(cached.TryGetValue(id,out value))return value;
        var result=Runner.Run("winget.exe","show --id "+Args.Q(id)+" --exact --source winget --disable-interactivity --accept-source-agreements",45000);value=result.Code==0?Parse(result.Output):"";
        if(value.Length==0)value=L.T("Descrição indisponível no catálogo. Use Detalhes e licença para conferir o pacote.","Catalog description unavailable. Use Details & license to review this package.");
        lock(gate)cached[id]=value;return value;
    }
}
public partial class MainWindow {
    void AttachPackageTip(System.Windows.FrameworkElement target,string id,string versions){
        string description=PopularCatalog.Description(id);bool loaded=description!=id;
        target.ToolTip=(loaded?description+"\n\n":"")+versions;
        target.ToolTipOpening+=(s,e)=>{if(loaded||preview)return;loaded=true;Work(L.T("Descrição de ","Description of ")+id,()=>PackageMetadata.Get(id),r=>target.ToolTip=r+"\n\n"+versions);};
    }
}
}
