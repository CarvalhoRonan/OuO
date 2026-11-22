using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
namespace Aurora {
// Retain control identity: background operations may still hold references to a cached page.
public static class UiLanguage {
    static readonly ConcurrentDictionary<string,string> english=new ConcurrentDictionary<string,string>();
    static readonly ConcurrentDictionary<string,string> portuguese=new ConcurrentDictionary<string,string>();
    static readonly ConcurrentDictionary<string,string> englishCache=new ConcurrentDictionary<string,string>();
    static readonly ConcurrentDictionary<string,string> portugueseCache=new ConcurrentDictionary<string,string>();
    public static void Remember(string pt,string en){if(String.IsNullOrEmpty(pt)||String.IsNullOrEmpty(en)||pt==en)return;if(english.TryAdd(pt,en))englishCache.Clear();if(portuguese.TryAdd(en,pt))portugueseCache.Clear();}
    public static string Translate(string value,bool fromPortuguese){
        if(String.IsNullOrEmpty(value)||fromPortuguese==L.Pt)return value;
        return TranslateUsing(value,fromPortuguese?english:portuguese);
    }
    public static string English(string value){return TranslateUsing(value,english);}
    public static string Current(string value){return TranslateUsing(value,L.Pt?portuguese:english);}
    static string TranslateUsing(string value,ConcurrentDictionary<string,string> map){
        if(String.IsNullOrEmpty(value))return value;string exact;if(map.TryGetValue(value,out exact))return exact;var cache=Object.ReferenceEquals(map,english)?englishCache:portugueseCache;if(cache.TryGetValue(value,out exact))return exact;
        // Translate known UI fragments in composite labels; do not touch editable fields or reports.
        var matches=map.Where(x=>x.Key.Length>=5&&value.Contains(x.Key)).OrderByDescending(x=>x.Key.Length).ToArray();
        if(matches.Length==0){if(cache.Count<4096)cache.TryAdd(value,value);return value;}
        var output=new System.Text.StringBuilder();int offset=0;
        while(offset<value.Length){var next=matches.Select(x=>new{Pair=x,At=value.IndexOf(x.Key,offset,StringComparison.Ordinal)}).Where(x=>x.At>=0).OrderBy(x=>x.At).ThenByDescending(x=>x.Pair.Key.Length).FirstOrDefault();if(next==null){output.Append(value.Substring(offset));break;}output.Append(value.Substring(offset,next.At-offset));output.Append(next.Pair.Value);offset=next.At+next.Pair.Key.Length;}
        string translated=output.ToString();if(cache.Count<4096)cache.TryAdd(value,translated);return translated;
    }
}
sealed class ChoiceLanguage:IValueConverter {
    readonly bool sourcePt;public ChoiceLanguage(bool pt){sourcePt=pt;}
    public object Convert(object value,Type type,object parameter,CultureInfo culture){return UiLanguage.Translate(value as string,sourcePt)??value;}
    public object ConvertBack(object value,Type type,object parameter,CultureInfo culture){return Binding.DoNothing;}
}
public partial class MainWindow {
    bool rebuildingAppearance;
    static readonly DependencyProperty OriginalFontProperty=DependencyProperty.RegisterAttached("OriginalFont",typeof(double),typeof(MainWindow),new PropertyMetadata(Double.NaN));
    static readonly DependencyProperty ChoiceLanguageProperty=DependencyProperty.RegisterAttached("ChoiceLanguage",typeof(object),typeof(MainWindow),new PropertyMetadata(null));
    static IEnumerable<DependencyObject> AppearanceTree(DependencyObject root){
        var seen=new HashSet<DependencyObject>();var pending=new Stack<DependencyObject>();pending.Push(root);
        while(pending.Count>0){var item=pending.Pop();if(item==null||!seen.Add(item))continue;yield return item;
            // TextBox templates contain editable user text. Its own colors are still updated.
            if(item is TextBox||item is PasswordBox)continue;
            foreach(object child in LogicalTreeHelper.GetChildren(item)){var next=child as DependencyObject;if(next!=null)pending.Push(next);}
            var element=item as FrameworkElement;if(element!=null){if(element.ContextMenu!=null)pending.Push(element.ContextMenu);var tip=element.ToolTip as DependencyObject;if(tip!=null)pending.Push(tip);}
            if(item is Visual)for(int i=0;i<VisualTreeHelper.GetChildrenCount(item);i++)pending.Push(VisualTreeHelper.GetChild(item,i));
        }
    }
    void ChangeAppearance(Action update){
        bool previousDark=dark,previousPt=L.Pt,previousCompact=compact;var old=new[]{Bg,Surface,Ink,Muted,Accent,Color(previousDark?"#344057":"#D8E0ED"),StateGreen,Color(previousDark?"#FF8F91":"#B32128"),Color(previousDark?"#244254":"#DCECF3")};
        string previousStatus=status==null?null:status.Text;Brush previousTone=status==null?null:status.Foreground;
        UiLifetimeProbe.Stage("Update appearance");update();BuildShell(true);UiLifetimeProbe.Stage("Shell rebuilt");
        var next=new[]{Bg,Surface,Ink,Muted,Accent,Color(dark?"#344057":"#D8E0ED"),StateGreen,Color(dark?"#FF8F91":"#B32128"),Color(dark?"#244254":"#DCECF3")};
        foreach(var pair in pageCache){var view=pair.Value;UiLifetimeProbe.Stage("Page "+pair.Key);var items=AppearanceTree(view.Body).Concat(pageToolbars.ContainsKey(pair.Key)?AppearanceTree(pageToolbars[pair.Key]):new DependencyObject[0]).Distinct().ToArray();UiLifetimeProbe.Stage("Items "+items.Length);int index=0;foreach(var item in items){if(++index%200==0)UiLifetimeProbe.Stage(pair.Key+" • "+index+" • "+item.GetType().Name);RefreshAppearance(item,old,next,previousPt,previousCompact);}view.Body.Margin=compact?new Thickness(15):new Thickness(25,25,30,25);view.Body.InvalidateMeasure();}
        if(previousStatus!=null){status.Text=UiLanguage.Translate(previousStatus,previousPt);status.Foreground=RemapBrush(previousTone,old,next);}foreach(int id in activeJobs.Keys.ToArray())activeJobs[id]=Tuple.Create(UiLanguage.Translate(activeJobs[id].Item1,previousPt),activeJobs[id].Item2);UpdateWorkUi();
        UiLifetimeProbe.Stage("Update layout");UpdateLayout();UiLifetimeProbe.Stage("Appearance complete");
    }
    static Brush RemapBrush(Brush brush,Brush[] old,Brush[] next){var solid=brush as SolidColorBrush;if(solid==null)return brush;
        for(int i=0;i<5;i++)if(Object.ReferenceEquals(brush,old[i]))return next[i];
        // Semantic success wins over the mint accent when both happen to have the same color.
        for(int i=old.Length-1;i>=0;i--){var prior=old[i] as SolidColorBrush;if(prior!=null&&prior.Color==solid.Color)return next[i];}return brush;
    }
    void RefreshAppearance(DependencyObject item,Brush[] old,Brush[] next,bool previousPt,bool previousCompact){
        var control=item as Control;if(control!=null){control.Background=RemapBrush(control.Background,old,next);control.Foreground=RemapBrush(control.Foreground,old,next);control.BorderBrush=RemapBrush(control.BorderBrush,old,next);}
        var panel=item as Panel;if(panel!=null)panel.Background=RemapBrush(panel.Background,old,next);
        var border=item as Border;if(border!=null){border.Background=RemapBrush(border.Background,old,next);border.BorderBrush=RemapBrush(border.BorderBrush,old,next);if(Convert.ToString(border.Tag)=="OuO.Card"){border.Padding=new Thickness(compact?12:18);border.Margin=compact?new Thickness(0,5,0,3):new Thickness(0,9,0,6);}}
        var shape=item as Shape;if(shape!=null){shape.Fill=RemapBrush(shape.Fill,old,next);shape.Stroke=RemapBrush(shape.Stroke,old,next);}
        var text=item as TextBlock;if(text!=null){text.Foreground=RemapBrush(text.Foreground,old,next);if((text.Tag as string)!="OuO.RawText")text.Text=UiLanguage.Translate(text.Text,previousPt);double original=(double)text.GetValue(OriginalFontProperty);if(Double.IsNaN(original)){original=text.FontSize+(previousCompact&&text.FontSize>=12?1:0);text.SetValue(OriginalFontProperty,original);}text.FontSize=compact&&original>=13?original-1:original;}
        var button=item as Button;if(button!=null){button.Padding=compact?new Thickness(10,6,10,6):new Thickness(14,10,14,10);button.Margin=new Thickness(0,0,compact?5:8,compact?5:8);}
        var element=item as FrameworkElement;if(element!=null&&element.ToolTip is string)element.ToolTip=UiLanguage.Translate((string)element.ToolTip,previousPt);
        var header=item as HeaderedContentControl;if(header!=null&&header.Header is string)header.Header=UiLanguage.Translate((string)header.Header,previousPt);
        var menu=item as MenuItem;if(menu!=null&&menu.Header is string)menu.Header=UiLanguage.Translate((string)menu.Header,previousPt);
        var content=item as ContentControl;if(content!=null&&content.Content is string)content.Content=UiLanguage.Translate((string)content.Content,previousPt);
        var combo=item as ComboBox;if(combo!=null&&combo.Items.Count>0&&combo.Items.Cast<object>().All(x=>x is string)){
            // A display template changes language without replacing Items or firing SelectionChanged.
            object language=combo.GetValue(ChoiceLanguageProperty);if(language==null){language=previousPt;combo.SetValue(ChoiceLanguageProperty,language);}
            var factory=new FrameworkElementFactory(typeof(TextBlock));factory.SetBinding(TextBlock.TextProperty,new Binding{Converter=new ChoiceLanguage((bool)language)});
            combo.ItemTemplate=new DataTemplate{VisualTree=factory};
        }
    }
}
}
