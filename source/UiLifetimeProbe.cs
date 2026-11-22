using System;
using System.Collections.Generic;
using System.Linq;
namespace Aurora {
internal static class UiLifetimeProbe {
    static readonly List<Tuple<string,WeakReference>> discarded=new List<Tuple<string,WeakReference>>();
    public static bool Enabled{get{return Environment.GetCommandLineArgs().Contains("--soak-ui")||Environment.GetCommandLineArgs().Contains("--audit-ui");}}
    public static void Record(object value,string label){if(Enabled&&value!=null)discarded.Add(Tuple.Create(label,new WeakReference(value)));}
    public static void Stage(string text){if(Enabled)System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"appearance-stage.txt"),DateTime.Now.ToString("o")+" • "+text+"\n");}
    public static string Report(){return String.Join("\n",discarded.GroupBy(x=>x.Item1).Select(g=>g.Key+": "+g.Count(x=>x.Item2.IsAlive)+" alive / "+g.Count()+" discarded"));}
}
}
