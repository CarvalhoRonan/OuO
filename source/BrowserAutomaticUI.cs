using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Aurora {
public partial class MainWindow {
    void BrowserAutomatic(){
        var card=Card(T("Instalar extensões automaticamente","Install extensions automatically"),T("O navegador baixa as extensões de sua loja oficial após receber a política. A escolha se aplica aos perfis deste usuário nos navegadores detectados.","The browser downloads extensions from its official store after receiving the policy. The choice applies to this user's profiles in detected browsers."));
        BrowserInfo[] found=BrowserExtensions.Detect();var choices=new Dictionary<string,CheckBox>();
        foreach(var browser in found)choices[browser.Id]=Choice(card,browser.Name+(browser.Installed?"":T(" • não encontrado"," • not found")),browser.Installed,browser.Installed);
        card.Children.Add(Text(T("Extensões","Extensions"),14,Accent));
        CheckBox block=Choice(card,T("uBlock Origin no Firefox; uBlock Origin Lite nos Chromium","uBlock Origin on Firefox; uBlock Origin Lite on Chromium"),true,true);
        CheckBox wot=Choice(card,T("WOT Web of Trust (opcional)","WOT Web of Trust (optional)"),true,false);
        CheckBox protect=Choice(card,T("Solicitar proteção contra remoção no Chrome, Edge e Brave","Request removal protection in Chrome, Edge and Brave"),true,false);
        Note(card,"A proteção usa a política de instalação obrigatória para extensões das lojas oficiais. A aceitação depende do navegador e do gerenciamento do computador. A extensão local da página de bloqueio continua removível.","Protection uses mandatory-install policy for official store extensions. Acceptance depends on the browser and computer management. The local blocked-page extension remains removable.");
        Note(card,"WOT solicita acesso amplo à navegação. Leia sua política de privacidade antes de marcar. Políticas do navegador podem indicar que ele é gerenciado. Reinicie e confira as extensões; uma política corporativa pode prevalecer. Opera e outros navegadores continuam com instalação manual.","WOT requests broad browsing access. Read its privacy policy before selecting it. Browser policies may show a managed-browser indicator. Restart and check the extensions; organization policy may take precedence. Opera and other browsers still require manual installation.");
        TextBox report=Input("");report.IsReadOnly=true;report.Foreground=StateGreen;report.TextChanged+=(sender,args)=>report.Foreground=OutcomeBrush(report.Text);report.AcceptsReturn=true;report.Height=190;report.TextWrapping=TextWrapping.Wrap;report.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        Row(card,Button(T("Preparar instalação automática","Set up automatic installation"),()=>{
            if(activeJobs.Values.Any(x=>x.Item1=="Browser extensions"))return;bool u=block.IsChecked==true,w=wot.IsChecked==true,locked=protect.IsChecked==true;
            BrowserInfo[] selected=found.Where(x=>choices[x.Id].IsChecked==true).ToArray();
            if(!u&&!w||selected.Length==0){ShowNotice(T("Selecione uma extensão e um navegador encontrado.","Select an extension and a detected browser."));return;}
            if(!Confirm(T("Preparar instalação automática em: ","Set up automatic installation in: ")+String.Join(", ",selected.Select(x=>x.Name))+"\n"+(u?"uBlock Origin / Lite\n":"")+(w?"WOT Web of Trust\n":"")+(locked?T("Proteção contra remoção solicitada nos Chromium compatíveis. Para desfazer, use o Histórico do OuO.\n","Removal protection requested on compatible Chromium browsers. Undo through OuO History.\n"):"")+T("O navegador buscará as extensões na loja oficial quando atualizar suas políticas. WOT pode acessar páginas visitadas.","The browser will fetch extensions from its official store when policies refresh. WOT may access visited pages.")))return;
            Work("Browser extensions",()=>BrowserExtensions.Apply(selected,u,w,new WindowsRegistryStore(),journal,locked),r=>report.Text=r);
        }),Button(T("Privacidade do WOT","WOT privacy"),()=>Open("https://www.mywot.com/en/privacy/privacy_policy")));
        AttachResult(card,report);
    }
}
}
