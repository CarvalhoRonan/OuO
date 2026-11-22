using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace Aurora {
public sealed class TweakSection {
    public string Id,Pt,En;public string Title{get{return L.T(Pt,En);}}
    public TweakSection(string id,string pt,string en){Id=id;Pt=pt;En=en;}
}
public static class TweakOrganization {
    public static readonly TweakSection[] Sections={
        new TweakSection("gaming","Jogos e periféricos","Gaming and input"),
        new TweakSection("privacy","Privacidade e telemetria","Privacy and telemetry"),
        new TweakSection("taskbar","Iniciar e barra de tarefas","Start and taskbar"),
        new TweakSection("explorer","Explorador e área de trabalho","Explorer and desktop"),
        new TweakSection("visual","Aparência e janelas","Appearance and windows"),
        new TweakSection("energy","Energia e inicialização","Power and startup"),
        new TweakSection("updates","Windows Update","Windows Update"),
        new TweakSection("network","Conectividade","Connectivity"),
        new TweakSection("apps","Aplicativos e navegadores","Apps and browsers"),
        new TweakSection("security","Segurança e avisos","Security and prompts"),
        new TweakSection("performance","Serviços e desempenho","Services and performance"),
        new TweakSection("system","Sistema e entrada de texto","System and text input")};
    public static string Category(TweakPreference value){
        string id=value.Id;
        if(id.StartsWith("dism-10-"))return "apps";
        if(id.StartsWith("dism-17-")||id.StartsWith("dism-14-")||id=="new-outlook"||id=="edge-background"||id=="edge-promotions"||id=="brave-extras"||id.Contains("EdgeAI")||id.Contains("ChromeAI")||id.Contains("PaintAI")||id.Contains("NotepadAI"))return "apps";
        if(id=="network-protocol"||id=="service-SharedAccess")return "network";
        if(id=="delivery-peers"||id.Contains("Update"))return "updates";
        if(id.Contains("Standby")||id.Contains("S3Sleep")||id.Contains("FastStartup"))return "energy";
        if(id.Contains("Bitlocker")||id=="wpbt"||id=="rdp-unsigned-warning"||id=="dism-2-4-0"||id=="dism-0-2-0"||id=="dism-20-7-0")return "security";
        if(id=="visual-best"||id=="window-snapping"||id.Contains("SnapAssist")||id.Contains("SnapLayouts")||id=="dism-1-0-0"||id=="dism-1-3-0"||id=="dism-3-4-0")return "visual";
        if(value.Category=="interface"||id.StartsWith("dism-0-")||id=="dism-3-6-0"||id=="dism-4-21-0")return "taskbar";
        if(id=="service-DiagTrack")return "privacy";
        if(id=="device-companion"||id.Contains("RazerBlock"))return "apps";
        return value.Category;
    }
    public static string Help(TweakPreference value){
        string explanation=L.T(value.DescriptionPt,value.DescriptionEn);
        if(value.Multiple){
            var known=new Dictionary<string,string[]>{
                {"search-style",new[]{"Define como a pesquisa aparece na barra: oculta, ícone, rótulo ou caixa. Não desativa o serviço de pesquisa.","Choose a hidden search button, icon, label or search box. The search service remains available."}},
                {"task-alignment",new[]{"Posiciona os ícones da barra de tarefas no centro ou à esquerda. Requer Windows 11.","Place taskbar icons in the center or on the left. Requires Windows 11."}},
                {"window-transparency",new[]{"Liga ou desliga transparências do Windows. Desativar pode reduzir o trabalho gráfico em máquinas simples.","Turn Windows transparency on or off. Disabling can reduce graphics work on modest PCs."}},
                {"window-snapping",new[]{"Ativa ou desativa o encaixe automático das janelas ao arrastá-las para as bordas.","Enable or disable automatic window snapping when dragging to screen edges."}},
                {"desktop-spotlight",new[]{"Escolhe se o Windows troca o papel de parede e se mostra o atalho de informações da imagem.","Choose whether Windows changes the wallpaper and shows its image information shortcut."}},
                {"all-apps-layout",new[]{"Escolhe a apresentação da lista de aplicativos no menu Iniciar. Os modos disponíveis dependem da versão do Windows.","Choose the Start app-list layout. Available modes depend on the Windows release."}},
                {"alt-tab-tabs",new[]{"Define quantas abas de aplicativos compatíveis aparecem junto às janelas ao usar Alt+Tab.","Choose how many tabs from supported apps appear beside windows in Alt+Tab."}},
                {"taskbar-combine",new[]{"Define quando janelas do mesmo programa são agrupadas na barra da tela principal.","Choose when windows of the same app are grouped on the main taskbar."}},
                {"secondary-combine",new[]{"Define o agrupamento das janelas nas barras dos monitores secundários.","Choose window grouping on taskbars of secondary monitors."}},
                {"taskbar-monitors",new[]{"Escolhe em quais monitores os botões dos aplicativos aparecem. Só afeta configurações com várias telas.","Choose which monitors show app taskbar buttons. Only affects multiple-display setups."}},
                {"explorer-start",new[]{"Define a pasta inicial ao abrir uma nova janela do Explorador. Não move nem exclui arquivos.","Set the starting location for new File Explorer windows. Files are not moved or deleted."}},
                {"drive-letters",new[]{"Escolhe a posição das letras C:, D: etc. nos nomes das unidades do Explorador.","Choose where C:, D: and other drive letters appear in Explorer drive names."}},
                {"wallpaper-quality",new[]{"Escolhe a compressão JPEG usada pelo Windows no papel de parede. 100% pode aumentar o uso de memória da imagem.","Choose Windows wallpaper JPEG compression. 100% may increase image memory use."}},
                {"ime-mode",new[]{"Escolhe o modo inicial do editor de entrada chinês IME. Não altera o idioma de todo o sistema.","Choose the initial Chinese IME input mode. This does not change the whole system language."}},
                {"network-protocol",new[]{"A preferência IPv4 mantém IPv6 disponível. Desativar IPv6 ou Teredo pode afetar rede corporativa, VPN e jogos Xbox. Exige reinicialização.","Prefer IPv4 while keeping IPv6 available. Disabling IPv6 or Teredo may affect corporate networks, VPNs and Xbox games. Requires a restart."}},
                {"hidden-items",new[]{"Controla a exibição de arquivos ocultos e protegidos no Explorador. Mostrar arquivos protegidos aumenta o risco de apagá-los por engano.","Control hidden and protected files in Explorer. Showing protected files makes accidental deletion easier."}},
                {"settings-home",new[]{"Mostra ou oculta a página inicial do aplicativo Configurações, preservando as restrições de outras páginas.","Show or hide the Settings home page while preserving other page restrictions."}}
            };
            string[] found;if(known.TryGetValue(value.Id,out found))explanation=L.T(found[0],found[1]);
            else if(value.Id.StartsWith("desktop-icon-"))explanation=L.T("Mostra, oculta ou devolve ao padrão este ícone da área de trabalho. Os arquivos e programas continuam no computador.","Show, hide or restore the default for this desktop icon. Files and apps stay on the computer.");
            else explanation=String.Join("\n",value.Variants.Select(x=>x.Label+": "+L.T(x.Definition.DescriptionPt,x.Definition.DescriptionEn)).Distinct());
        }
        return value.Title+"\n\n"+explanation+L.T("\n\nA seleção só será aplicada após sua confirmação. O estado anterior é salvo para restauração.","\n\nChanges run only after your confirmation. Previous state is saved for restoration.");
    }
}
public partial class MainWindow {
    readonly Dictionary<FrameworkElement,string> tweakExtraCards=new Dictionary<FrameworkElement,string>();string tweakActionCategory="";
    void RegisterTweakExtra(FrameworkElement card){if(current=="tweaks"&&tweakActionCategory.Length>0)tweakExtraCards[card]=tweakActionCategory;}
    void FilterTweakExtras(string category,string term){foreach(var entry in tweakExtraCards){string text=String.Join(" ",AppearanceTree(entry.Key).OfType<TextBlock>().Select(x=>x.Text));entry.Key.Visibility=(category.Length==0||entry.Value==category)&&(term.Length==0||text.IndexOf(term,StringComparison.CurrentCultureIgnoreCase)>=0)?Visibility.Visible:Visibility.Collapsed;}}
    ComboBox tweakCategoryFilter;TextBox tweakSearch;
    ToolTip TweakTooltip(string text){var tip=new ToolTip{Content=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,MaxWidth=420},MaxWidth=460,Padding=new Thickness(14)};return tip;}
}
}
