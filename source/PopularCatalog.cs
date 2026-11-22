using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Aurora {
public sealed class PopularApp {
    public string Id,Choco,CategoryPt,CategoryEn,DescriptionPt,DescriptionEn;
    public string Category{get{return L.T(CategoryPt,CategoryEn);}}
    public string Description{get{return L.T(DescriptionPt,DescriptionEn);}}
}
public static class PopularCatalog {
    static readonly PopularApp[] entries={
        new PopularApp{Id="Mozilla.Firefox",Choco="firefox",CategoryPt="Navegador",CategoryEn="Browser",DescriptionPt="Navegador de internet com foco em privacidade.",DescriptionEn="Web browser focused on privacy."},
        new PopularApp{Id="Google.Chrome",Choco="googlechrome",CategoryPt="Navegador",CategoryEn="Browser",DescriptionPt="Navegador de internet do Google.",DescriptionEn="Google web browser."},
        new PopularApp{Id="Brave.Brave",Choco="brave",CategoryPt="Navegador",CategoryEn="Browser",DescriptionPt="Navegador com bloqueio integrado de rastreadores.",DescriptionEn="Browser with built-in tracker blocking."},
        new PopularApp{Id="TheDocumentFoundation.LibreOffice",Choco="libreoffice-fresh",CategoryPt="Documentos",CategoryEn="Documents",DescriptionPt="Suíte para textos, planilhas e apresentações.",DescriptionEn="Suite for documents, spreadsheets and presentations."},
        new PopularApp{Id="SumatraPDF.SumatraPDF",Choco="sumatrapdf",CategoryPt="Documentos",CategoryEn="Documents",DescriptionPt="Leitor leve de PDF e outros documentos.",DescriptionEn="Lightweight PDF and document reader."},
        new PopularApp{Id="7zip.7zip",Choco="7zip",CategoryPt="Utilitário",CategoryEn="Utility",DescriptionPt="Compacta e extrai arquivos.",DescriptionEn="Compresses and extracts files."},
        new PopularApp{Id="Microsoft.PowerToys",Choco="powertoys",CategoryPt="Utilitário",CategoryEn="Utility",DescriptionPt="Ferramentas adicionais para organizar o Windows.",DescriptionEn="Extra tools for organizing Windows."},
        new PopularApp{Id="Notepad++.Notepad++",Choco="notepadplusplus",CategoryPt="Editor de texto",CategoryEn="Text editor",DescriptionPt="Editor de texto e código com abas.",DescriptionEn="Tabbed text and code editor."},
        new PopularApp{Id="VideoLAN.VLC",Choco="vlc",CategoryPt="Multimídia",CategoryEn="Media",DescriptionPt="Reprodutor de vídeo e áudio.",DescriptionEn="Video and audio player."},
        new PopularApp{Id="Spotify.Spotify",Choco="spotify",CategoryPt="Música",CategoryEn="Music",DescriptionPt="Aplicativo de música e podcasts.",DescriptionEn="Music and podcast app."},
        new PopularApp{Id="Discord.Discord",Choco="discord",CategoryPt="Comunicação",CategoryEn="Communication",DescriptionPt="Conversas por texto, voz e vídeo.",DescriptionEn="Text, voice and video chat."},
        new PopularApp{Id="Microsoft.VisualStudioCode",Choco="vscode",CategoryPt="Editor de código",CategoryEn="Code editor",DescriptionPt="Editor de código com extensões.",DescriptionEn="Extensible code editor."},
        new PopularApp{Id="Git.Git",Choco="git",CategoryPt="Desenvolvimento",CategoryEn="Development",DescriptionPt="Controle de versões para projetos.",DescriptionEn="Version control for projects."},
        new PopularApp{Id="qBittorrent.qBittorrent",Choco="qbittorrent",CategoryPt="Transferência",CategoryEn="Downloads",DescriptionPt="Gerenciador de transferências BitTorrent.",DescriptionEn="BitTorrent transfer manager."},
        new PopularApp{Id="GIMP.GIMP",Choco="gimp",CategoryPt="Imagens",CategoryEn="Images",DescriptionPt="Editor de imagens e fotografias.",DescriptionEn="Image and photo editor."},
        new PopularApp{Id="Valve.Steam",Choco="steam",CategoryPt="Jogos",CategoryEn="Games",DescriptionPt="Loja e biblioteca de jogos para PC.",DescriptionEn="PC game store and library."},
        new PopularApp{Id="OBSProject.OBSStudio",Choco="obs-studio",CategoryPt="Streaming",CategoryEn="Streaming",DescriptionPt="Grava e transmite a tela e câmeras.",DescriptionEn="Records and streams screens and cameras."},
        new PopularApp{Id="KeePassXC.KeePassXC",Choco="keepassxc",CategoryPt="Segurança",CategoryEn="Security",DescriptionPt="Gerenciador local de senhas.",DescriptionEn="Local password manager."},
        new PopularApp{Id="Inkscape.Inkscape",Choco="inkscape",CategoryPt="Desenho",CategoryEn="Design",DescriptionPt="Cria imagens vetoriais e ilustrações.",DescriptionEn="Creates vector images and illustrations."},
        new PopularApp{Id="BlenderFoundation.Blender",Choco="blender",CategoryPt="3D",CategoryEn="3D",DescriptionPt="Modelagem e animação em três dimensões.",DescriptionEn="Three-dimensional modeling and animation."},
        new PopularApp{Id="Audacity.Audacity",Choco="audacity",CategoryPt="Áudio",CategoryEn="Audio",DescriptionPt="Grava e edita arquivos de áudio.",DescriptionEn="Records and edits audio files."},
        new PopularApp{Id="Telegram.TelegramDesktop",Choco="telegram",CategoryPt="Comunicação",CategoryEn="Communication",DescriptionPt="Mensagens e chamadas no computador.",DescriptionEn="Desktop messages and calls."},
        new PopularApp{Id="Microsoft.Teams",Choco="microsoft-teams",CategoryPt="Trabalho",CategoryEn="Work",DescriptionPt="Reuniões, mensagens e colaboração.",DescriptionEn="Meetings, messages and collaboration."},
        new PopularApp{Id="Microsoft.WindowsTerminal",Choco="microsoft-windows-terminal",CategoryPt="Desenvolvimento",CategoryEn="Development",DescriptionPt="Terminal com abas para comandos e scripts.",DescriptionEn="Tabbed terminal for commands and scripts."}
    };
    static string[] ranked;static DateTime updated;
    static string CachePath{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OuO","popular-ranking.xml");}}
    static PopularCatalog(){try{var xml=XElement.Load(CachePath);var saved=xml.Elements("item").Select(x=>(string)x.Attribute("id")).Where(x=>entries.Any(e=>e.Id==x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();if(saved.Length==entries.Length){ranked=saved;DateTime.TryParse((string)xml.Attribute("updated"),out updated);}}catch{}}
    public static PopularApp[] Current(){var order=ranked;if(order==null)return entries.ToArray();return entries.OrderBy(x=>Array.FindIndex(order,id=>String.Equals(id,x.Id,StringComparison.OrdinalIgnoreCase))).ToArray();}
    public static string Description(string id){var item=entries.FirstOrDefault(x=>String.Equals(x.Id,id,StringComparison.OrdinalIgnoreCase));return item==null?id:item.Category+" • "+item.Description;}
    public static string Status{get{return ranked==null?L.T("Sugestões iniciais; ranking não disponível.","Initial suggestions; ranking unavailable."):L.T("Popularidade no repositório Chocolatey • atualizado em ","Popularity in the Chocolatey repository • updated ")+updated.ToString("dd/MM/yyyy HH:mm");}}
    public static string Refresh(){
        CommandResult result;try{result=Runner.Run("choco.exe","search --source https://community.chocolatey.org/api/v2/ --order-by=Popularity --page=0 --page-size=300 --limit-output --approved-only",120000);}catch{return Status;}
        if(result.Code!=0)return Status;
        var positions=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);int n=0;
        foreach(string line in result.Output.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){
            string id=line.Split('|')[0].Trim();if(id.Length>0&&!positions.ContainsKey(id))positions[id]=n++;
        }
        if(entries.Count(x=>positions.ContainsKey(x.Choco))<5)return Status;
        ranked=entries.OrderBy(x=>positions.ContainsKey(x.Choco)?positions[x.Choco]:Int32.MaxValue).Select(x=>x.Id).ToArray();updated=DateTime.Now;
        try{Directory.CreateDirectory(Path.GetDirectoryName(CachePath));AtomicFiles.SaveXml(new XElement("ranking",new XAttribute("updated",updated.ToString("o")),ranked.Select(id=>new XElement("item",new XAttribute("id",id)))),CachePath);}catch{}
        return Status;
    }
}
}

