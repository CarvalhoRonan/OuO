using System;
using Microsoft.Win32;
namespace Aurora {
public partial class MainWindow {
    void SetAppTheme(bool value){ChangeAppearance(()=>{dark=value;SavePreference("Theme",value?"dark":"light");});}
    void SetLanguage(bool portuguese){ChangeAppearance(()=>{L.Pt=portuguese;SavePreference("Language",portuguese?"pt":"en");});}
    void SetCompact(bool value){ChangeAppearance(()=>{compact=value;SavePreference("Compact",value?"1":"0");});}
    void SavePreference(string name,string value){if(preview)return;try{using(var key=Registry.CurrentUser.CreateSubKey(@"Software\OuO\Appearance"))key.SetValue(name,value,RegistryValueKind.String);}catch(Exception ex){ShowNotice(ex.Message);}}
    void LoadPreferences(){try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\OuO\Appearance"))if(key!=null){
        string theme=Convert.ToString(key.GetValue("Theme","system")),language=Convert.ToString(key.GetValue("Language","system"));compact=Convert.ToString(key.GetValue("Compact","0"))=="1";
        if(theme=="dark"||theme=="light")dark=theme=="dark";if(language=="pt"||language=="en")L.Pt=language=="pt";
    }}catch{}}
}
}
