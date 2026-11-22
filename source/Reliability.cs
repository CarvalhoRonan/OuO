using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Aurora {
public static class ResultTone {
    public static bool IsFailure(string text){return Regex.IsMatch(text??"", @"(?im)(?:^|\s[•|]\s)(?:ERROR|FAIL|ERRO\b)|não foi possível|não p[oô]de (?:executar|realizar|iniciar)|could not|falhou|failed|parâmetro incorreto|acesso negado|access is denied|não confirmado|not confirmed|^\s*(?:The component store is repairable\.|O repositório de componentes (?:pode ser reparado|é reparável)\.|O armazenamento de componentes é reparável\.|Windows Resource Protection found corrupt files but was unable to fix some of them\.|Windows Resource Protection found integrity violations\.|A Proteção de Recursos do Windows encontrou violações de integridade\.|A Proteção de Recursos do Windows encontrou arquivos corrompidos, mas não (?:pôde|conseguiu) corrigir alguns deles\.)");}
    public static bool IsPendingRepair(string text){return Regex.IsMatch(text??"",@"(?im)^\s*(?:There is a system repair pending\b|(?:Há|Existe) (?:um )?reparo (?:do|de) sistema pendente\b)");}
    public static int Classify(string text){
        if(IsFailure(text)||Regex.IsMatch(text??"", @"(?i)\b(desativad[oa]s?|desabilitad[oa]s?|disabled|inativo|inactive)\b|sem resposta|no response|ameaça detectada|threat detected"))return -1;
        if(IsPendingRepair(text))return 0;
        if(Regex.IsMatch(text??"", @"(?i)não identificado|not identified|não encontrad|not found|indisponível|unavailable|nenhum.*backup|não há backup|no backup|atenção|attention|aviso|warning|cancelad|cancell?ed|não configurad|not configured"))return 0;
        if(Regex.IsMatch(text??"", @"(?i)\bOK\b|concluíd|completed|aplicad|applied|verificad|verified|confirmad|confirmed|\bativad[oa]\b|\benabled\b|restaurad|restored|encontrad|found|copiad|copied|habilitad|nenhuma atualização|no.*update"))return 1;
        return 0;
    }
    public static int Completion(string text){if(IsFailure(text))return -1;if(String.IsNullOrWhiteSpace(text)||IsPendingRepair(text)||Regex.IsMatch(text,@"(?i)cancelad|cancell?ed|aviso|warning|indisponível|unavailable|não identificado|not identified|não confirmado|not confirmed"))return 0;return 1;}
}
public sealed class PageView {public StackPanel Body;public TextBox Console;public CardColumns Columns;public double Offset;}
public partial class MainWindow {
    readonly Dictionary<string,PageView> pageCache=new Dictionary<string,PageView>();
    readonly Dictionary<string,Action> pageRefreshActions=new Dictionary<string,Action>();
    ScrollViewer pageScroll;
    Border toastHost;
    System.Windows.Threading.DispatcherTimer toastTimer;
    Brush OutcomeBrush(string text){int tone=ResultTone.Classify(text);return tone<0?Color(dark?"#FF8F91":"#B32128"):tone>0?StateGreen:Muted;}
    Brush CompletionBrush(string text){int tone=ResultTone.Completion(text);return tone<0?Color(dark?"#FF8F91":"#B32128"):tone>0?StateGreen:Muted;}
    void ShowToast(string message,bool completedAction=false){
        if(toastHost==null)return;
        var panel=new StackPanel{Margin=new Thickness(14,10,14,8)};
        // Completion tone is contextual: successfully disabling a setting is a
        // successful action, while a queried Disabled state remains red.
        var messageBlock=Text(message,13,Muted);messageBlock.Foreground=completedAction?CompletionBrush(message):OutcomeBrush(message);panel.Children.Add(messageBlock);
        toastHost.Child=panel;toastHost.Visibility=Visibility.Visible;
        if(toastTimer!=null)toastTimer.Stop();
        toastTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(8)};
        toastTimer.Tick+=(s,e)=>{toastTimer.Stop();if(toastHost!=null)toastHost.Visibility=Visibility.Collapsed;};
        toastTimer.Start();
    }
    void ShowTweakSummary(int enabled,int disabled,int unknown){
        if(toastHost==null)return;
        var panel=new StackPanel{Margin=new Thickness(14,10,14,8)};
        panel.Children.Add(Text(T("Tweaks consultados","Tweaks checked"),14,Ink));
        var line=new TextBlock{FontSize=13,TextWrapping=TextWrapping.Wrap};
        line.Inlines.Add(new System.Windows.Documents.Run(T("Ativados: ","Enabled: ")+enabled){Foreground=StateGreen});
        line.Inlines.Add(new System.Windows.Documents.Run("  •  "));
        line.Inlines.Add(new System.Windows.Documents.Run(T("Desativados: ","Disabled: ")+disabled){Foreground=Color(dark?"#FF8F91":"#B32128")});
        line.Inlines.Add(new System.Windows.Documents.Run("  •  "));
        line.Inlines.Add(new System.Windows.Documents.Run(T("Não identificados: ","Unidentified: ")+unknown){Foreground=Muted});
        panel.Children.Add(line);toastHost.Child=panel;toastHost.Visibility=Visibility.Visible;
        if(toastTimer!=null)toastTimer.Stop();
        toastTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(8)};
        toastTimer.Tick+=(s,e)=>{toastTimer.Stop();if(toastHost!=null)toastHost.Visibility=Visibility.Collapsed;};
        toastTimer.Start();
    }
    public void ShowNotice(string message,string title=null){
        lastNoticeText=message;if(preview)return;
        var dialog=new Window{Owner=this,Title=title??"OuO",Width=Math.Min(570,SystemParameters.WorkArea.Width-30),MaxHeight=Math.Max(250,SystemParameters.WorkArea.Height-60),SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,WindowStyle=WindowStyle.None,Background=Surface,Foreground=Ink,ShowInTaskbar=false};
        var content=new StackPanel{Margin=new Thickness(22)};
        content.Children.Add(Text(title??T("Informação","Information"),20,Accent));
        content.Children.Add(new ScrollViewer{MaxHeight=Math.Max(120,SystemParameters.WorkArea.Height-220),Content=Text(message,14,OutcomeBrush(message)),VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        Row(content,Button(T("Copiar detalhes","Copy details"),()=>{try{Clipboard.SetText(message);}catch{}}),Button(T("Fechar","Close"),()=>dialog.Close()));
        dialog.Content=new Border{BorderBrush=Accent,BorderThickness=new Thickness(1),Child=content};dialog.ShowDialog();
    }
    string lastNoticeText;
    void CheckBoot(TextBox target){
        if(!RequireAdmin())return;
        Work(T("Checar inicialização F8","Check F8 startup"),()=>{
            var r=Runner.Run("bcdedit.exe","/enum "+Args.Q("{current}"),30000);
            if(r.Code!=0&&Environment.UserName.Equals("WDAGUtilityAccount",StringComparison.OrdinalIgnoreCase))return T("Indisponível neste ambiente • o Windows Sandbox não expõe o menu de inicialização de uma instalação normal.","Unavailable in this environment • Windows Sandbox does not expose a normal installation's boot menu.");
            if(r.Code!=0)return Describe(r);
            Match match=Regex.Match(r.Output,@"(?im)^\s*bootmenupolicy\s+(Legacy|Standard)\b");
            return (match.Success?(match.Groups[1].Value.Equals("Legacy",StringComparison.OrdinalIgnoreCase)?T("Ativado • menu F8 tradicional.","Enabled • classic F8 menu."):T("Desativado • menu F8 tradicional. O Windows usa a inicialização padrão.","Disabled • classic F8 menu. Windows uses standard startup.")):T("Estado do F8 não identificado nesta entrada de inicialização.","F8 state not identified in this boot entry."))+"\n\n"+r.Output;
        },r=>target.Text=r);
    }
}
}
