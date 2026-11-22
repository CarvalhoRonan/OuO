using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Aurora {
public partial class MainWindow {
    const string AboutPixKey="de8a6fb9-450d-4043-810f-0d2ceaf2e79c";
    const string AboutPixCode="00020126580014br.gov.bcb.pix0136de8a6fb9-450d-4043-810f-0d2ceaf2e79c5204000053039865802BR5921OVERCLOCK INFORMATICA6007AIMORES62070503***6304DF07";
    const string AboutKofi="https://ko-fi.com/overclockinformatica";
    const string AboutInstagram="https://www.instagram.com/oc.informatica/";
    const string AboutLinks="https://linkme.bio/overclockinformatica";

    void AboutCopy(string value,TextBlock feedback,string pt,string en){
        try{Clipboard.SetText(value);feedback.Text=T(pt,en);}
        catch(Exception ex){feedback.Text=T("Não foi possível copiar: ","Could not copy: ")+ex.Message;}
    }
    void AboutQr(Panel parent,string file,double size){
        string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","branding",file);
        if(!File.Exists(path))return;
        var bitmap=new BitmapImage(new Uri(path));bitmap.Freeze();
        var image=new Image{Source=bitmap,Width=size,Height=size,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,5,0,9)};System.Windows.Media.RenderOptions.SetBitmapScalingMode(image,System.Windows.Media.BitmapScalingMode.NearestNeighbor);parent.Children.Add(image);
    }
    StackPanel AboutTile(Panel parent,string title,string qr,double size){
        var tile=new StackPanel{Width=210,Margin=new Thickness(4,0,4,12),HorizontalAlignment=HorizontalAlignment.Center};
        var titleBlock=Text(title,16,Ink);titleBlock.TextAlignment=TextAlignment.Center;tile.Children.Add(titleBlock);AboutQr(tile,qr,size);parent.Children.Add(tile);return tile;
    }
    void AboutPage(){
        Heading(T("Sobre o OuO","About OuO"),T("Overclock's Utility Optimizer • desenvolvido por Overclock Informática.","Overclock's Utility Optimizer • developed by Overclock Informática."));
        var app=Card(T("Versão e finalidade","Version and purpose"),"");
        string version=System.Diagnostics.FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;
        app.Children.Add(Text("OuO "+version+" • "+T("prévia • experiência de uso e manutenção","preview • usability and maintenance"),17,Accent));
        Note(app,"Distribuição sem assinatura digital. O Controle Inteligente de Aplicativos pode impedir a execução por falta de confiança no arquivo. Consulte o relatório de validação desta compilação.","Unsigned distribution. Smart App Control may prevent execution when the file is not trusted. See the validation report for this build.");
        Note(app,"Esta versão reúne o núcleo validado e recursos em fase de testes, identificados junto aos controles. A validação foi feita em Windows 11 x64 e no Windows Sandbox; Windows 7, 8/8.1, 10, x86 e outros conjuntos de hardware/DPI ainda precisam de homologação. O nome não significa que o aplicativo ofereça controles de overclock.","This release combines the validated core with features in testing, marked beside their controls. Validation used Windows 11 x64 and Windows Sandbox; Windows 7, 8/8.1, 10, x86 and other hardware/DPI combinations still need qualification. The name does not imply overclock controls.");
        app.Children.Add(Button(T("Ver recursos em fase de testes","View features in testing"),()=>ShowNotice(String.Join("\n\n",System.Linq.Enumerable.Select(ReleaseQualification.Features,x=>x.Name+"\n"+x.Reason)),T("Validação do OuO","OuO validation"))));

        OuOUpdateCard();var ownLicense=Card(T("Licença do OuO","OuO license"),T("Uso gratuito com código visível. Reutilizar o código próprio ou distribuir versões derivadas exige autorização, respeitadas as licenças dos componentes e os direitos legais.","Free use with visible source. Reusing original code or distributing derived versions requires authorization, respecting component licenses and statutory rights."));Row(ownLicense,Button(T("Ler licença completa","Read full license"),()=>Open(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"LICENSE.md"))));BeginColumns(2);
        var support=Card(T("Apoie o desenvolvimento ♡","Support development ♡"),T("Doações são opcionais. Todas as funcionalidades permanecem gratuitas.","Donations are optional. All features remain free."));
        var donationTiles=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};support.Children.Add(donationTiles);
        var pix=AboutTile(donationTiles,T("Pix • Brasil","Pix • Brazil"),"pix.png",180);
        pix.Children.Add(Text(T("QR Pix estático • valor livre","Static Pix QR • any amount"),12,Muted));
        var pixFeedback=Text("",12,StateGreen);pix.Children.Add(pixFeedback);
        pix.Children.Add(Button(T("Copiar código Pix","Copy Pix code"),()=>AboutCopy(AboutPixCode,pixFeedback,"Código Pix copiado.","Pix code copied.")));
        pix.Children.Add(Button(T("Copiar chave Pix","Copy Pix key"),()=>AboutCopy(AboutPixKey,pixFeedback,"Chave Pix copiada.","Pix key copied.")));
        var kofi=AboutTile(donationTiles,T("Ko-fi • Internacional","Ko-fi • International"),"kofi.png",180);
        kofi.Children.Add(Text("ko-fi.com/overclockinformatica",12,Muted));
        var kofiFeedback=Text("",12,StateGreen);kofi.Children.Add(kofiFeedback);
        kofi.Children.Add(Button(T("Abrir Ko-fi ↗","Open Ko-fi ↗"),()=>Open(AboutKofi)));
        kofi.Children.Add(Button(T("Copiar link do Ko-fi","Copy Ko-fi link"),()=>AboutCopy(AboutKofi,kofiFeedback,"Link do Ko-fi copiado.","Ko-fi link copied.")));
        Note(support,"Confirme os dados do beneficiário no aplicativo do seu banco antes de doar.","Confirm the recipient details in your banking app before donating.");

        var creator=Card(T("Overclock Informática na internet","Overclock Informática online"),"");
        var onlineTiles=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center};creator.Children.Add(onlineTiles);
        var instagram=AboutTile(onlineTiles,"Instagram · @oc.informatica","instagram.png",125);
        instagram.Children.Add(Button("Instagram ↗",()=>Open(AboutInstagram)));
        var links=AboutTile(onlineTiles,T("Página de links","Links page"),"links.png",125);
        links.Children.Add(Button("Linkme.bio ↗",()=>Open(AboutLinks)));

        var components=Card(T("Componentes incluídos","Included components"),T("Licenças e avisos completos acompanham o pacote em THIRD-PARTY.md e na pasta licenses.","Full licenses and notices are bundled in THIRD-PARTY.md and the licenses folder."));
        Note(components,"Libre Hardware Monitor 0.9.6: leitura de sensores. PawnIO: acesso opcional a sensores que exigem driver. Ícones de aplicativos: marcas dos respectivos titulares. O código principal do OuO está em source/.","Libre Hardware Monitor 0.9.6: sensor readings. PawnIO: optional access to sensors requiring a driver. App icons: trademarks of their respective owners. Main OuO code is in source/.");
        Note(components,"Recuperação offline: auxiliar separado baseado no NTPWEdit 0.7/chntpw, GPL v2, com código completo em third-party-source/offline-account. O auxiliar edita cópias do SAM e não mostra hashes de senha.","Offline recovery: separate helper based on NTPWEdit 0.7/chntpw, GPL v2, with complete source in third-party-source/offline-account. The helper edits SAM copies and does not display password hashes.");
        var references=Card(T("Referências e ferramentas","References and tools"),T("Fontes oficiais usadas no desenvolvimento e na orientação do usuário.","Official sources used for development and user guidance."));
        Row(references,Button("Libre Hardware Monitor",()=>Open("https://github.com/LibreHardwareMonitor/LibreHardwareMonitor")),Button("PawnIO",()=>Open("https://pawnio.eu/")),Button("Microsoft Learn",()=>Open("https://learn.microsoft.com/windows/")));
        Row(references,Button("Catálogo Microsoft",()=>Open("https://www.catalog.update.microsoft.com/")),Button("Dell",()=>Open("https://downloads.dell.com/catalog/CatalogPC.cab")),Button("HP",()=>Open("https://ftp.hp.com/pub/softlib/software/sms_catalog/HpCatalogForSms.latest.cab")));
        Row(references,Button("WinUtil",()=>Open("https://github.com/ChrisTitusTech/winutil")),Button("Win11Debloat",()=>Open("https://github.com/Raphire/Win11Debloat")),Button("Dism++ Data",()=>Open("https://github.com/Chuyu-Team/Dism-Multi-language")));
        Row(references,Button("WinGet",()=>Open("https://learn.microsoft.com/windows/package-manager/winget/")),Button("Geek Uninstaller",()=>Open("https://geekuninstaller.com/")),Button("AdwCleaner",()=>Open("https://www.malwarebytes.com/adwcleaner")));
        Row(references,Button(T("Avisos de terceiros","Third-party notices"),()=>Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"THIRD-PARTY.md"))),Button(T("Revisão e validação da versão","Release review and validation"),()=>Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"HOMOLOGACAO-1.0.0.md"))),Button(T("Situação de todos os pedidos","Status of all requests"),()=>Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"STATUS-DO-ESCOPO.md"))));
    }
}
}
