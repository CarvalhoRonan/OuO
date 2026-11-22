using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Aurora {
public partial class MainWindow {
    sealed class NavigationSection {public string Id;public Button Header;public StackPanel Items;}
    readonly Dictionary<string,Button> navigationButtons=new Dictionary<string,Button>();
    readonly Dictionary<string,NavigationSection> navigationSections=new Dictionary<string,NavigationSection>();
    readonly HashSet<string> openNavigationSections=new HashSet<string>();
    string navigationQuery="";TextBox navigationSearch;
    string NavigationSectionId(string id){
        if(id=="dashboard"||id=="recommendations")return "overview";
        if(new[]{"processes","stability","storage","cloud-storage"}.Contains(id))return "diagnosis";
        if(new[]{"performance","tweaks","repair","repair-advanced","protection","cleanup","upgrade"}.Contains(id))return "maintenance";
        if(id=="browser"||id=="network"||id=="dns")return "connection";
        if(new[]{"startup","apps","apps-updates","office","bloat","recovery","drivers"}.Contains(id))return "applications";
        return "preferences";
    }
    string NavigationSectionName(string id){switch(id){case "overview":return T("Visão do computador","Computer overview");case "diagnosis":return T("Diagnóstico","Diagnostics");case "maintenance":return T("Manutenção do Windows","Windows maintenance");case "connection":return T("Internet e impressão","Internet and printing");case "applications":return T("Aplicativos e drivers","Apps and drivers");default:return T("Configurações e histórico","Settings and history");}}
    void InitializeNavigation(){
        navigationButtons.Clear();navigationSections.Clear();
        var searchLabel=Text(T("Buscar função","Find a tool"),11,Muted);searchLabel.Margin=new Thickness(8,0,8,3);nav.Children.Add(searchLabel);
        navigationSearch=Input(navigationQuery);navigationSearch.Padding=new Thickness(8);navigationSearch.Margin=new Thickness(0,0,0,12);navigationSearch.ToolTip=T("Digite parte do nome de uma tela.","Type part of a page name.");nav.Children.Add(navigationSearch);
        navigationSearch.TextChanged+=(s,e)=>{navigationQuery=navigationSearch.Text;FilterNavigation();};
    }
    void BuildNavigationEntry(string id,string label){
        string category=NavigationSectionId(id);NavigationSection section;
        if(!navigationSections.TryGetValue(category,out section)){
            section=new NavigationSection{Id=category,Items=new StackPanel()};section.Header=Button("",()=>{if(openNavigationSections.Contains(category))openNavigationSections.Remove(category);else openNavigationSections.Add(category);FilterNavigation();});
            section.Header.HorizontalContentAlignment=HorizontalAlignment.Left;section.Header.HorizontalAlignment=HorizontalAlignment.Stretch;section.Header.Background=Brushes.Transparent;section.Header.BorderThickness=new Thickness(0);section.Header.Padding=new Thickness(5,8,5,8);section.Header.Margin=new Thickness(0,3,0,0);section.Header.FontSize=11;section.Header.FontWeight=FontWeights.SemiBold;
            nav.Children.Add(section.Header);nav.Children.Add(section.Items);navigationSections[category]=section;if(NavigationSectionId(current)==category)openNavigationSections.Add(category);
        }
        var button=Button(label,()=>ShowPage(id));button.Tag=id;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.FontSize=12;button.Padding=new Thickness(9,8,6,8);button.Margin=new Thickness(0,1,0,2);button.BorderThickness=new Thickness(0);
        var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(27)});header.ColumnDefinitions.Add(new ColumnDefinition());var icon=Symbol(id,18);icon.VerticalAlignment=VerticalAlignment.Center;header.Children.Add(icon);var text=new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(text,1);header.Children.Add(text);button.Content=header;
        System.Windows.Automation.AutomationProperties.SetName(button,label);section.Items.Children.Add(button);navigationButtons[id]=button;
    }
    void FilterNavigation(){
        string query=(navigationQuery??"").Trim();foreach(var section in navigationSections.Values){
            bool any=false;foreach(var button in section.Items.Children.OfType<Button>()){string name=System.Windows.Automation.AutomationProperties.GetName(button);bool match=query.Length==0||name.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0;button.Visibility=match?Visibility.Visible:Visibility.Collapsed;any|=match;}
            bool opened=query.Length>0||openNavigationSections.Contains(section.Id);section.Items.Visibility=any&&opened?Visibility.Visible:Visibility.Collapsed;section.Header.Visibility=any?Visibility.Visible:Visibility.Collapsed;section.Header.Content=(opened?"▾  ":"▸  ")+NavigationSectionName(section.Id);
        }
    }
    void SelectNavigation(string id){
        string selected=id=="repair-advanced"?"repair":id=="apps-updates"?"apps":id=="dns"?"browser":id;if(!rebuildingAppearance)openNavigationSections.Clear();openNavigationSections.Add(NavigationSectionId(selected));
        foreach(var pair in navigationButtons){bool active=pair.Key==selected;pair.Value.BorderThickness=new Thickness(active?3:0,0,0,0);pair.Value.BorderBrush=Accent;pair.Value.Background=active?Color(dark?"#244254":"#DCECF3"):Brushes.Transparent;}
        FilterNavigation();
    }
}
}
