using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace Aurora {
public sealed class DiskNavigationHistory {
    readonly Stack<string> previous=new Stack<string>(),next=new Stack<string>();
    public string Current{get;private set;}public bool CanBack{get{return previous.Count>0;}}public bool CanForward{get{return next.Count>0;}}
    public static string Normalize(string path){string full=Path.GetFullPath(path);return full.Length>Path.GetPathRoot(full).Length?full.TrimEnd('\\'):full;}
    public static string Parent(string path){if(String.IsNullOrWhiteSpace(path))return null;string full=Normalize(path);return full.Equals(Path.GetPathRoot(full),StringComparison.OrdinalIgnoreCase)?null:Path.GetDirectoryName(full);}
    public static bool Within(string path,string root){string full=Normalize(path),basePath=Normalize(root);return full.Equals(basePath,StringComparison.OrdinalIgnoreCase)||full.StartsWith(basePath.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);}
    public void Visit(string path,bool record){path=Normalize(path);if(record&&Current!=null&&!String.Equals(Current,path,StringComparison.OrdinalIgnoreCase)){previous.Push(Current);next.Clear();}Current=path;}
    public string Back(){if(!CanBack)return null;next.Push(Current);Current=previous.Pop();return Current;}
    public string Forward(){if(!CanForward)return null;previous.Push(Current);Current=next.Pop();return Current;}
}
public sealed class DiskTreeNavigator {
    readonly TreeView tree;readonly Func<string,IEnumerable<string>> children;readonly Action<string> navigate;
    readonly HashSet<string> loaded=new HashSet<string>(StringComparer.OrdinalIgnoreCase);public bool Synchronizing{get;private set;}public string Root{get;private set;}public TreeViewItem CurrentNode{get;private set;}
    public DiskTreeNavigator(TreeView control,Func<string,IEnumerable<string>> directories,Action<string> onNavigate){tree=control;children=directories;navigate=onNavigate;tree.AddHandler(TreeViewItem.ExpandedEvent,new RoutedEventHandler((s,e)=>Fill(e.OriginalSource as TreeViewItem)));tree.SelectedItemChanged+=(s,e)=>{var node=tree.SelectedItem as TreeViewItem;if(!Synchronizing&&node!=null&&navigate!=null)navigate(Convert.ToString(node.Tag));};}
    TreeViewItem Node(string path,bool root){var node=new TreeViewItem{Header=(root?"💾 ":"📁 ")+(root&&path.Equals(Path.GetPathRoot(path),StringComparison.OrdinalIgnoreCase)?path:Path.GetFileName(path.TrimEnd('\\'))),Tag=path,Foreground=tree.Foreground};node.Items.Add("…");return node;}
    public void Reset(string root){tree.Items.Clear();loaded.Clear();CurrentNode=null;Root=String.IsNullOrWhiteSpace(root)?null:DiskNavigationHistory.Normalize(root);if(Root!=null)tree.Items.Add(Node(Root,true));}
    void Fill(TreeViewItem node){if(node==null)return;string path=Convert.ToString(node.Tag);if(loaded.Contains(path))return;node.Items.Clear();try{foreach(string child in children(path).OrderBy(x=>x,StringComparer.CurrentCultureIgnoreCase).Take(1000))node.Items.Add(Node(DiskNavigationHistory.Normalize(child),false));loaded.Add(path);}catch{node.Items.Add(new TreeViewItem{Header=L.T("Leitura indisponível","Cannot read folder"),IsEnabled=false});}}
    public TreeViewItem Synchronize(string path,string preferredRoot){path=DiskNavigationHistory.Normalize(path);string root=Root;if(root==null||!DiskNavigationHistory.Within(path,root))root=!String.IsNullOrEmpty(preferredRoot)&&DiskNavigationHistory.Within(path,preferredRoot)?preferredRoot:Path.GetPathRoot(path);Synchronizing=true;try{if(Root==null||!Root.Equals(DiskNavigationHistory.Normalize(root),StringComparison.OrdinalIgnoreCase))Reset(root);var node=(TreeViewItem)tree.Items[0];string cursor=Root;node.IsExpanded=true;Fill(node);foreach(string segment in path.Substring(Root.Length).TrimStart('\\').Split(new[]{'\\'},StringSplitOptions.RemoveEmptyEntries)){cursor=Path.Combine(cursor,segment);Fill(node);var child=node.Items.OfType<TreeViewItem>().FirstOrDefault(x=>String.Equals(Convert.ToString(x.Tag),cursor,StringComparison.OrdinalIgnoreCase));if(child==null){child=Node(cursor,false);node.Items.Add(child);}node.IsExpanded=true;node=child;}CurrentNode=node;node.IsSelected=true;node.IsExpanded=true;node.BringIntoView();return node;}finally{Synchronizing=false;}}
}
}
