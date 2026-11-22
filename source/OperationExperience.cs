using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Aurora {
public sealed class WorkProgress {
    public string Stage; public long Done,Total; public bool Estimated,Bytes;
    public double Percent { get { return Total<=0?0:Math.Max(0,Math.Min(100,100.0*Done/Total)); } }
    public string Label { get { return Total<=0?(Stage??""):String.Format(CultureInfo.CurrentCulture,"{0} • {1}{2:0}% ({3})",Stage,Estimated?L.T("aprox. ","about "):"",Percent,Bytes?Size(Done)+" / "+Size(Total):Done+"/"+Total); } }
    static string Size(long bytes){return bytes>=1048576?(bytes/1048576.0).ToString("0.0")+" MB":bytes>=1024?(bytes/1024.0).ToString("0.0")+" KB":bytes+" B";}
}
public static class WorkReporting {
    [ThreadStatic] public static Action<WorkProgress> Sink;
    public static void Report(long done,long total,string stage,bool estimated=false) { var sink=Sink; if(sink!=null)sink(new WorkProgress{Done=Math.Max(0,done),Total=Math.Max(0,total),Stage=stage,Estimated=estimated}); }
    public static WorkProgress FromToolLine(string text,string tool) {
        if((text??"").StartsWith("OUO-PROGRESS|",StringComparison.Ordinal)){var fields=text.Split('|');long done,total;if(fields.Length>=4&&Int64.TryParse(fields[1],out done)&&Int64.TryParse(fields[2],out total)&&done>=0&&total>0&&done<=total)return new WorkProgress{Done=done,Total=total,Stage=String.Join("|",fields.Skip(3))};}
        if(!new[]{"dism.exe","winget.exe","choco.exe"}.Contains(Path.GetFileName(tool).ToLowerInvariant()))return null;
        var m=Regex.Match(text??"",@"(?:\[|\bProgress\s*:)\s*[^\r\n%]*?(\d{1,3}(?:[.,]\d+)?)\s*%",RegexOptions.IgnoreCase);
        double p;if(!m.Success||!Double.TryParse(m.Groups[1].Value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out p)||p<0||p>100)return null;
        return new WorkProgress{Done=(long)Math.Round(p*100),Total=10000,Stage=L.T("Progresso informado pela ferramenta","Progress reported by the tool")};
    }
}
public sealed class MaintenanceSample {
    public DateTime At; public ulong FreeMemory; public long FreeDisk=-1;
    public static MaintenanceSample Read(){var sample=new MaintenanceSample{At=DateTime.UtcNow};try{var m=Native.ReadMemory();if(m!=null)sample.FreeMemory=m.Free;}catch{}try{var d=new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));if(d.IsReady)sample.FreeDisk=d.AvailableFreeSpace;}catch{}return sample;}
    public static string Summary(MaintenanceSample before,MaintenanceSample after){if(before==null||after==null)return "";var b=new StringBuilder();b.AppendLine(L.T("Duração: ","Duration: ")+(after.At-before.At).TotalSeconds.ToString("0.0")+" s");if(before.FreeDisk>=0&&after.FreeDisk>=0)b.AppendLine(L.T("Espaço livre na unidade do Windows: ","Free space on the Windows drive: ")+Size(before.FreeDisk)+" → "+Size(after.FreeDisk));b.AppendLine(L.T("RAM disponível: ","Available RAM: ")+Size((long)before.FreeMemory)+" → "+Size((long)after.FreeMemory));b.AppendLine(L.T("As diferenças podem incluir atividades de outros programas; não representam um ganho permanente de desempenho.","Differences can include other applications' activity; they do not represent a permanent performance gain."));return b.ToString();}
    static string Size(long bytes){return (bytes/1073741824.0).ToString("0.00")+" GB";}
}
public sealed class OperationRecord {
    public string Id,Title,Result,SavedSummary; public DateTime Started,Finished; public MaintenanceSample Before,After;
    public bool Failed{get{return ResultTone.IsFailure(Result??"");}}
    public bool Canceled{get{return Regex.IsMatch(Result??"",@"(?i)^\s*(cancelado pelo usu[aá]rio|canceled by user)");}}
    public string Summary{get{return SavedSummary??MaintenanceSample.Summary(Before,After);}}
    public override string ToString(){return Started.ToLocalTime().ToString("dd/MM HH:mm")+" • "+Title+" • "+(Canceled?L.T("Cancelada","Canceled"):Failed?L.T("Falha","Failed"):L.T("Finalizada","Finished"));}
}
public static class DiagnosticPrivacy {
    public static string Clean(string text) {
        string value=text??"";
        value=Regex.Replace(value,@"(?i)(authorization\s*[:=]\s*(?:Bearer\s+)?|(?:access_token|refresh_token|client_secret|id_token)\s*[""']?\s*[:=]\s*[""']?)[^\s""'&,}]+","$1[removido]");
        value=Regex.Replace(value,@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}","[e-mail]");
        value=Regex.Replace(value,@"(?i)\b[A-Z]:\\[^\r\n\""<>|]*","[caminho local]");
        value=Regex.Replace(value,@"\\\\[^\r\n]+","[caminho de rede]");
        value=Regex.Replace(value,@"\bS-1-5-21-(?:\d+-){2,}\d+\b","[conta]");
        string account=Environment.UserName;if(!String.IsNullOrWhiteSpace(account)&&account.Length>2)value=Regex.Replace(value,Regex.Escape(account),"[usuário]",RegexOptions.IgnoreCase);
        return value.Length>30000?value.Substring(0,30000)+"\n[relatório resumido]":value;
    }
    public static string Build(string title,string result,string system,string version){return Clean("OuO "+version+"\nUTC: "+DateTime.UtcNow.ToString("o")+"\n"+system+"\n\n"+L.T("Operação: ","Operation: ")+title+"\n\n"+result);}
}
public sealed class ChangeGroup {
    public string Id,Title;public DateTime At;public int Count;public bool Admin;
    public override string ToString(){return At.ToLocalTime().ToString("dd/MM/yyyy HH:mm")+" • "+Title+" • "+Count+L.T(" alteração(ões)"," change(s)");}
}
public sealed partial class Journal {
    [ThreadStatic] static string operationId;
    [ThreadStatic] static string operationTitle;
    public static void SetOperation(string id,string title){operationId=id;operationTitle=title;}
    static void TagChange(XElement record){if(!String.IsNullOrEmpty(operationId)){record.SetAttributeValue("operation",operationId);record.SetAttributeValue("operationTitle",operationTitle??"");}record.SetAttributeValue("utc",DateTime.UtcNow.ToString("o"));}
    public void SaveOperation(OperationRecord record){lock(this){var x=new XElement("operation-result",new XAttribute("id",record.Id),new XAttribute("title",record.Title),new XAttribute("started",record.Started.ToString("o")),new XAttribute("finished",record.Finished.ToString("o")),new XElement("result",record.Result??""),new XElement("comparison",record.Summary));AtomicFiles.SaveXml(x,Path.Combine(Folder,"operation-"+record.Id+".xml"));}}
    public List<OperationRecord> OperationResults(){lock(this){var list=new List<OperationRecord>();foreach(string file in Directory.GetFiles(Folder,"operation-*.xml").OrderByDescending(File.GetLastWriteTimeUtc).Take(100))try{var x=XElement.Load(file);if(x.Name!="operation-result")continue;list.Add(new OperationRecord{Id=(string)x.Attribute("id"),Title=(string)x.Attribute("title"),Started=DateTime.Parse((string)x.Attribute("started"),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),Finished=DateTime.Parse((string)x.Attribute("finished"),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),Result=(string)x.Element("result"),SavedSummary=(string)x.Element("comparison")});}catch{}return list.OrderByDescending(x=>x.Started).ToList();}}
    public List<ChangeGroup> ChangeGroups(){lock(this){var all=new List<Tuple<string,XElement>>();foreach(string file in Directory.GetFiles(Folder,"*.xml")){try{var x=XElement.Load(file);if((x.Name=="change"||x.Name=="tree-change")&&(string)x.Attribute("state")=="applied")all.Add(Tuple.Create(file,x));}catch{}}return all.GroupBy(x=>(string)x.Item2.Attribute("operation")??Path.GetFileNameWithoutExtension(x.Item1)).Select(g=>new ChangeGroup{Id=g.Key,Title=(string)g.First().Item2.Attribute("operationTitle")??(string)g.First().Item2.Attribute("title")??"",At=File.GetLastWriteTimeUtc(g.First().Item1),Count=g.Count(),Admin=g.Any(x=>(string)x.Item2.Attribute("hive")!="HKCU")}).OrderByDescending(x=>x.At).ToList();}}
    public string UndoGroup(string group){lock(this){var selected=new List<Tuple<string,XElement>>();foreach(string file in Directory.GetFiles(Folder,"*.xml").OrderByDescending(x=>Path.GetFileName(x))){var x=LoadChange(file);if(x==null||(string)x.Attribute("state")!="applied")continue;if(((string)x.Attribute("operation")??Path.GetFileNameWithoutExtension(file))==group)selected.Add(Tuple.Create(file,x));}if(selected.Count==0)return L.T("Nada para restaurar nesta operação.","No changes to restore for this operation.");
        // Validate all targets before changing anything; later writes must not be overwritten.
        var newest=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var entry in selected){var x=entry.Item2;string target=(string)x.Attribute("hive")+"|"+(string)x.Attribute("path")+"|"+(string)x.Attribute("name");if(newest.Add(target))ValidateCurrent(x,entry.Item1,selected.Select(a=>a.Item1).ToArray());}
        int restored=0;foreach(var entry in selected){ValidateCurrent(entry.Item2,entry.Item1,selected.Select(x=>x.Item1).ToArray());UndoRecord(entry.Item2,entry.Item1);restored++;}return "OK • "+restored+L.T(" alteração(ões) restaurada(s)."," change(s) restored.");}}
    static XElement LoadChange(string path){try{var x=XElement.Load(path);return x.Name=="change"||x.Name=="tree-change"?x:null;}catch{return null;}}
    void ValidateCurrent(XElement x,string file,string[] ownFiles){string hive=(string)x.Attribute("hive"),path=(string)x.Attribute("path"),name=(string)x.Attribute("name");foreach(string other in Directory.GetFiles(Folder,"*.xml")){if(ownFiles.Contains(other)||String.CompareOrdinal(Path.GetFileName(other),Path.GetFileName(file))<=0)continue;var later=LoadChange(other);if(later==null||(string)later.Attribute("state")!="applied"||(string)later.Attribute("hive")!=hive)continue;string laterPath=(string)later.Attribute("path");if(String.Equals(path,laterPath,StringComparison.OrdinalIgnoreCase)&&(x.Name=="tree-change"||later.Name=="tree-change"||String.Equals(name,(string)later.Attribute("name"),StringComparison.OrdinalIgnoreCase)))throw new IOException(L.T("Há uma alteração posterior neste ajuste. Restaure a operação mais recente primeiro.","A newer operation changed this setting. Restore the newer operation first."));}
        if(x.Name=="tree-change"){var tree=store as IRegistryTreeStore;if(tree==null||tree.TreeExists(hive,path))throw new IOException(L.T("A chave foi recriada; a restauração exige revisão.","The key was recreated; restoration requires review."));return;}
        var expected=x.Element("after");if(expected==null)throw new IOException(L.T("Backup antigo sem estado final verificado. Use a restauração individual existente.","Older backup has no verified final state. Use the existing single-value restoration."));var currentValue=store.Read(hive,path,name);bool exists=(bool?)expected.Attribute("exists")==true;if(currentValue.Exists!=exists)throw new IOException(L.T("O ajuste mudou fora desta operação; restauração interrompida.","The setting changed outside this operation; restoration stopped."));if(exists){var kind=(RegistryValueKind)Enum.Parse(typeof(RegistryValueKind),(string)expected.Attribute("kind"));if(currentValue.Kind!=kind||!RegistryValueCodec.Equal(currentValue.Value,RegistryValueCodec.Decode(expected.Element("encoded"),kind)))throw new IOException(L.T("O valor atual difere do salvo; restauração interrompida.","The current value differs from the saved value; restoration stopped."));}
    }
}
}
