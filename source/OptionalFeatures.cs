using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Aurora {
public sealed class OptionalFeature {
    public string Name,Pt,En;public int MinBuild;
    public override string ToString(){return L.T(Pt,En);}
}
public static class OptionalFeatures {
    public static bool? EnabledState(string value){string text=(value??"").Trim();if(text=="Enabled"||text=="Ativado")return true;if(text=="Disabled"||text=="Desativado")return false;return null;}
    public static readonly OptionalFeature[] All={
        new OptionalFeature{Name="DirectPlay",Pt="DirectPlay para jogos antigos",En="DirectPlay for older games",MinBuild=9200},
        new OptionalFeature{Name="Microsoft-Windows-Subsystem-Linux",Pt="Subsistema Windows para Linux",En="Windows Subsystem for Linux",MinBuild=14393},
        new OptionalFeature{Name="VirtualMachinePlatform",Pt="Plataforma de Máquina Virtual",En="Virtual Machine Platform",MinBuild=18362},
        new OptionalFeature{Name="Containers-DisposableClientVM",Pt="Área Restrita do Windows (Sandbox)",En="Windows Sandbox",MinBuild=18362},
        new OptionalFeature{Name="ClientForNFS-Infrastructure",Pt="Cliente NFS",En="NFS Client",MinBuild=9200},
        new OptionalFeature{Name="WindowsMediaPlayer",Pt="Windows Media Player clássico",En="Classic Windows Media Player",MinBuild=7600}
    };
    public static string State(OptionalFeature feature){
        if(feature==null)throw new ArgumentNullException("feature");
        string script="$f=Get-WindowsOptionalFeature -Online -FeatureName "+Runner.QuotePS(feature.Name)+" -ErrorAction Stop; Write-Output ([string]$f.State)";
        var result=Runner.PS(script,30000);
        if(result.Code!=0)return "ERROR • "+result.Output;
        string last=result.Output.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).LastOrDefault()??"";
        return last=="Enabled"?L.T("Ativado","Enabled"):last=="Disabled"?L.T("Desativado","Disabled"):last;
    }
    public static string Set(OptionalFeature feature,bool enabled){
        if(feature==null)throw new ArgumentNullException("feature");
        var result=Runner.Run("dism.exe","/Online /"+(enabled?"Enable-Feature":"Disable-Feature")+" /FeatureName:"+feature.Name+(enabled?" /All":"")+" /NoRestart",1800000);
        return (result.Code==0||result.Code==3010?"OK":"ERROR")+" • "+feature+"\n"+result.Output;
    }
}
public partial class MainWindow {
    void OptionalFeatureCard(){
        var card=Card(T("Recursos opcionais do Windows","Optional Windows features"),T("DirectPlay, WSL, Sandbox, NFS e Media Player. A disponibilidade varia por edição e versão. Consulte antes de alterar; pode exigir reinicialização.","DirectPlay, WSL, Sandbox, NFS and Media Player. Availability varies by edition and release. Check before changing; a reboot may be required."));
        var checks=new Dictionary<OptionalFeature,CheckBox>();var edited=new HashSet<OptionalFeature>();bool updating=false;foreach(var feature in OptionalFeatures.All){var box=Choice(card,feature.ToString(),machine!=null&&machine.Build>=feature.MinBuild,false);box.ToolTip=feature.Name;checks[feature]=box;OptionalFeature item=feature;box.Checked+=(s,e)=>{if(!updating)edited.Add(item);};box.Unchecked+=(s,e)=>{if(!updating)edited.Add(item);};}
        TextBlock state=Text("",13,Muted);card.Children.Add(state);
        Row(card,Button(T("Checar recursos","Check features"),()=>{var supported=checks.Where(x=>x.Value.IsEnabled).Select(x=>x.Key).ToArray();Dictionary<OptionalFeature,string> found=null;Work(T("Checar recursos","Check features"),()=>{found=supported.ToDictionary(x=>x,x=>OptionalFeatureQuery(x));return found.Count==0?T("Nenhum recurso disponível nesta versão.","No feature available on this release."):String.Join("\n",found.Select(x=>x.Key+": "+x.Value));},r=>{state.Text=r;state.Foreground=OutcomeBrush(r);updating=true;try{if(found!=null)foreach(var pair in found)if(!edited.Contains(pair.Key)&&OptionalFeatures.EnabledState(pair.Value).HasValue)checks[pair.Key].IsChecked=OptionalFeatures.EnabledState(pair.Value).Value;}finally{updating=false;}});}),Button(T("Aplicar seleção","Apply selection"),()=>{
            var chosen=checks.Where(x=>x.Value.IsEnabled&&edited.Contains(x.Key)).Select(x=>new{Feature=x.Key,Enabled=x.Value.IsChecked==true}).ToArray();
            if(chosen.Length==0){ShowToast(T("Nenhuma alteração selecionada. Cheque o estado e altere apenas os recursos desejados.","No change selected. Check the state and change only the features you want."));return;}
            if(!RequireAdmin())return;
            string summary=String.Join("\n",chosen.Select(x=>x.Feature+" → "+(x.Enabled?T("Ativar","Enable"):T("Desativar","Disable"))));
            if(!Confirm(T("Aplicar os estados selecionados a estes recursos? Recursos usados por aplicativos podem parar de funcionar ao serem desativados.\n","Apply the selected states? Apps relying on disabled features may stop working.\n")+summary))return;
            Work(T("Recursos opcionais","Optional features"),()=>{var output=new List<string>();foreach(var feature in chosen){ReportStage(feature.Feature.ToString());string current=OptionalFeatures.State(feature.Feature);if(current.StartsWith("ERROR",StringComparison.Ordinal)){output.Add(current);continue;}bool? active=OptionalFeatures.EnabledState(current);if(!active.HasValue){output.Add("AVISO • "+feature.Feature+T(" • estado não identificado; nenhuma alteração aplicada."," • unidentified state; no change applied."));continue;}if(active.Value==feature.Enabled)continue;output.Add(OptionalFeatures.Set(feature.Feature,feature.Enabled));}return output.Count==0?"OK • "+T("Nenhuma mudança necessária.","No changes needed."):String.Join("\n",output);},r=>{state.Text=r;state.Foreground=OutcomeBrush(r);});
        }));
    }
    Func<OptionalFeature,string> OptionalFeatureQuery=OptionalFeatures.State;
}
}
