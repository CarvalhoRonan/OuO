using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Aurora {
public sealed class PackageItem:System.ComponentModel.INotifyPropertyChanged {
    public string Id {get;set;}
    public string Name {get;set;}
    string summary;
    public string Summary{get{return summary;}set{summary=value;if(PropertyChanged!=null)PropertyChanged(this,new System.ComponentModel.PropertyChangedEventArgs("Description"));}}
    public string Description{get{return Name+"\n"+(String.IsNullOrWhiteSpace(summary)?PopularCatalog.Description(Id):summary)+"\n"+Id;}}
    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public ImageSource Icon {get;set;}
    public override string ToString(){return Id;}
}
public static class AppIcons {
    static readonly Dictionary<string,ImageSource> cache=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string,string> names=new Dictionary<string,string>{
        {"Mozilla.Firefox","Firefox"},{"Google.Chrome","Google Chrome"},{"Brave.Brave","Brave"},
        {"TheDocumentFoundation.LibreOffice","LibreOffice"},{"SumatraPDF.SumatraPDF","SumatraPDF"},
        {"7zip.7zip","7-Zip"},{"Microsoft.PowerToys","PowerToys"},{"Notepad++.Notepad++","Notepad++"},
        {"VideoLAN.VLC","VLC"},{"Spotify.Spotify","Spotify"},{"Discord.Discord","Discord"},
        {"Microsoft.VisualStudioCode","Visual Studio Code"},{"Git.Git","Git"}
    };
    public static PackageItem Package(string id,string title=null){string name;return new PackageItem{Id=id,Name=title??(names.TryGetValue(id,out name)?name:id),Icon=ForPackage(id)};}
    public static ImageSource ForPackage(string id){
        ImageSource icon;if(cache.TryGetValue("package:"+id,out icon))return icon;
        if(Regex.IsMatch(id??"","^[A-Za-z0-9._+-]+$"))foreach(string ext in new[]{".png",".ico"}){
            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","icons",id+ext);
            if(File.Exists(path))try{using(var f=File.OpenRead(path)){var decoder=BitmapDecoder.Create(f,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);icon=decoder.Frames.OrderByDescending(x=>x.PixelWidth).First();icon.Freeze();cache["package:"+id]=icon;return icon;}}catch{}
        }
        icon=Generic();cache["package:"+id]=icon;return icon;
    }
    static ImageSource generic;
    public static ImageSource Generic(){if(generic!=null)return generic;DrawingGroup group=new DrawingGroup();using(DrawingContext c=group.Open()){
        c.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(58,111,155)),null,new Rect(0,0,40,40),9,9);
        Brush white=Brushes.White;foreach(int x in new[]{10,23})foreach(int y in new[]{10,23})c.DrawRoundedRectangle(white,null,new Rect(x,y,8,8),2,2);
    }generic=new DrawingImage(group);generic.Freeze();return generic;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct ShellInfo {public IntPtr Icon;public int Index;public uint Attributes;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string DisplayName;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string TypeName;}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path,uint attributes,out ShellInfo info,uint size,uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    public static string ExecutablePath(string command){
        if(String.IsNullOrWhiteSpace(command))return "";string text=Environment.ExpandEnvironmentVariables(command.Trim());string path="";
        if(text.StartsWith("\"")){int end=text.IndexOf('"',1);if(end>1)path=text.Substring(1,end-1);}else{Match m=Regex.Match(text,@"^(.+?\.(?:exe|com|dll))(?=\s|,|$)",RegexOptions.IgnoreCase);if(m.Success)path=m.Groups[1].Value;}
        if(path==""||path.StartsWith(@"\\")||path.StartsWith("http",StringComparison.OrdinalIgnoreCase))return "";
        try{if(!Path.IsPathRooted(path)){string local=Path.Combine(Environment.SystemDirectory,path);if(!File.Exists(local))local=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),path);path=local;}return File.Exists(path)?Path.GetFullPath(path):"";}catch{return "";}
    }
    public static ImageSource FromCommand(string command){
        string path=ExecutablePath(command);if(path=="")return Generic();ImageSource cached;if(cache.TryGetValue(path,out cached))return cached;
        ShellInfo info;try{if(SHGetFileInfo(path,0,out info,(uint)Marshal.SizeOf(typeof(ShellInfo)),0x100|0x1)!=IntPtr.Zero&&info.Icon!=IntPtr.Zero){try{cached=Imaging.CreateBitmapSourceFromHIcon(info.Icon,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(32,32));cached.Freeze();cache[path]=cached;return cached;}finally{DestroyIcon(info.Icon);}}}catch{}
        return Generic();
    }
    public static ImageSource FromFile(string file){
        try{string path=Path.GetFullPath(file??"");if(!File.Exists(path))return Generic();
            ImageSource cached;if(cache.TryGetValue("file:"+path,out cached))return cached;
            ShellInfo info;if(SHGetFileInfo(path,0,out info,(uint)Marshal.SizeOf(typeof(ShellInfo)),0x100|0x1)!=IntPtr.Zero&&info.Icon!=IntPtr.Zero){try{cached=Imaging.CreateBitmapSourceFromHIcon(info.Icon,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(32,32));cached.Freeze();cache["file:"+path]=cached;return cached;}finally{DestroyIcon(info.Icon);}}
        }catch{}return Generic();
    }
    public static ImageSource FromInstallLocation(string directory,string name){
        try{
            string full=Path.GetFullPath(directory??"");if(!Path.IsPathRooted(full)||!Directory.Exists(full)||(new DirectoryInfo(full).Attributes&FileAttributes.ReparsePoint)!=0)return Generic();
            string[] roots={Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)};
            if(!roots.Where(x=>!String.IsNullOrWhiteSpace(x)).Any(x=>full.StartsWith(Path.GetFullPath(x).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)))return Generic();
            string key="location:"+full;ImageSource cached;if(cache.TryGetValue(key,out cached))return cached;
            string shortName=Regex.Replace(name??"","[^A-Za-z0-9]","").ToLowerInvariant();
            foreach(string file in Directory.GetFiles(full,"*.exe").Take(40).OrderBy(x=>{string stem=Regex.Replace(Path.GetFileNameWithoutExtension(x),"[^A-Za-z0-9]","").ToLowerInvariant();return shortName.Contains(stem)&&stem.Length>=4?0:Regex.IsMatch(stem,"^(unins|setup|update|remove)")?2:1;})){
                ImageSource icon=FromCommand(file);if(!Object.ReferenceEquals(icon,Generic())){cache[key]=icon;return icon;}
            }
        }catch{}
        return Generic();
    }
    public static DataTemplate Template(bool startup){
        DataTemplate template=new DataTemplate();FrameworkElementFactory row=new FrameworkElementFactory(typeof(StackPanel));row.SetBinding(FrameworkElement.ToolTipProperty,new Binding("Description"));row.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);row.SetValue(FrameworkElement.MarginProperty,new Thickness(4,5,4,5));
        FrameworkElementFactory image=new FrameworkElementFactory(typeof(Image));image.SetValue(FrameworkElement.WidthProperty,28.0);image.SetValue(FrameworkElement.HeightProperty,28.0);image.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,12,0));image.SetBinding(Image.SourceProperty,new Binding("Icon"));row.AppendChild(image);
        FrameworkElementFactory labels=new FrameworkElementFactory(typeof(StackPanel));FrameworkElementFactory title=new FrameworkElementFactory(typeof(TextBlock));title.SetValue(TextBlock.FontSizeProperty,14.0);title.SetBinding(TextBlock.TextProperty,new Binding("Name"));labels.AppendChild(title);
        FrameworkElementFactory detail=new FrameworkElementFactory(typeof(TextBlock));detail.SetValue(TextBlock.FontSizeProperty,11.0);detail.SetValue(UIElement.OpacityProperty,0.75);detail.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);detail.SetValue(FrameworkElement.MaxWidthProperty,650.0);detail.SetBinding(TextBlock.TextProperty,new Binding(startup?"Detail":"Id"));labels.AppendChild(detail);row.AppendChild(labels);template.VisualTree=row;return template;
    }
    public static string[] Selected(ListBox list){return list.SelectedItems.OfType<PackageItem>().Select(x=>x.Id).ToArray();}
}
public partial class MainWindow {
    FrameworkElement Symbol(string type,double size){return new Image{Source=new DrawingImage(NavigationIcons.Draw(type,Accent)),Width=size,Height=size,Margin=new Thickness(0,0,10,0)};}
    Button ThemeButton(bool isDark,Action clicked){Button b=Button("",clicked);StackPanel label=new StackPanel{Orientation=Orientation.Horizontal};label.Children.Add(Symbol(isDark?"moon":"sun",21));label.Children.Add(new TextBlock{Text=isDark?T("Escuro","Dark"):T("Claro","Light"),VerticalAlignment=VerticalAlignment.Center});b.Content=label;b.BorderBrush=dark==isDark?Accent:Muted;return b;}
    ListBox IconList(double height,bool multi,bool startup){return new ListBox{Height=height,SelectionMode=multi?SelectionMode.Multiple:SelectionMode.Single,Margin=new Thickness(0,0,0,10),Background=Surface,Foreground=Ink,BorderBrush=Muted,ItemTemplate=AppIcons.Template(startup)};}
    readonly Dictionary<string,CheckBox> packageChoices=new Dictionary<string,CheckBox>();bool syncingPackages;
    FrameworkElement PackageButton(string id){var item=AppIcons.Package(id);CheckBox choice=new CheckBox{Foreground=Ink,IsChecked=queue.Contains(id),VerticalContentAlignment=VerticalAlignment.Center};StackPanel row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new Image{Source=item.Icon,Width=30,Height=30,Margin=new Thickness(8,0,12,0)});StackPanel labels=new StackPanel();labels.Children.Add(Text(item.Name,13,Ink));labels.Children.Add(Text(PopularCatalog.Description(id).Split('•')[0].Trim(),10,Muted));row.Children.Add(labels);choice.Content=row;choice.ToolTip=PopularCatalog.Description(id);packageChoices[id]=choice;choice.Checked+=(s,e)=>{if(!syncingPackages)AddQueue(id);};choice.Unchecked+=(s,e)=>{if(!syncingPackages){queue.Remove(id);RefreshQueue();}};return new Border{Child=choice,Background=Surface,BorderBrush=Muted,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Padding=new Thickness(8),MinHeight=65,Margin=new Thickness(0,0,8,8)};}
}
}
