using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Aurora {
public partial class MainWindow {
    void DashboardStorage(){
        var card=Card(T("Armazenamento","Storage"),T("Uso das unidades deste computador.","Drive usage on this computer."));
        int count=0;
        foreach(var drive in DriveInfo.GetDrives())try{if(!drive.IsReady||drive.DriveType!=DriveType.Fixed||drive.TotalSize==0)continue;
            double used=100.0*(drive.TotalSize-drive.AvailableFreeSpace)/drive.TotalSize;
            var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,10)};row.Children.Add(StorageDonut(used));
            var label=new StackPanel{Margin=new Thickness(10,15,0,0)};label.Children.Add(Text(drive.Name+" • "+used.ToString("0")+T("% usado","% used"),13,Ink));label.Children.Add(Text((drive.AvailableFreeSpace/1073741824.0).ToString("0.0")+T(" GB livres de "," GB free of ")+(drive.TotalSize/1073741824.0).ToString("0.0")+" GB",12,Muted));row.Children.Add(label);card.Children.Add(row);
            if(++count>=4)break;
        }catch{}
        if(count==0)Note(card,"Nenhuma unidade disponível para leitura.","No drive available for reading.");
    }
    void RecommendationsPage(){
        Heading(T("Recomendações para este computador","Recommendations for this computer"),T("Escolha como usa o PC; o OuO apresenta sugestões para você revisar.","Choose how you use the PC; OuO shows suggestions for your review."));
        var usage=Card(T("Como você usa o computador?","How do you use this computer?"),"");
        string[] names={T("Jogos","Games"),T("Estudo","Study"),T("Trabalho","Work"),"Streaming",T("Programação","Development"),T("Criação de conteúdo","Content creation"),T("Uso básico","Basic use")};
        var choices=new List<CheckBox>();var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=3};usage.Children.Add(grid);
        foreach(string label in names){var box=new CheckBox{Content=label,Foreground=Ink,Margin=new Thickness(0,5,8,6)};grid.Children.Add(box);choices.Add(box);}
        TextBlock advice=Text(T("Selecione os usos e clique em Analisar.","Select uses and click Analyze."),13,Muted);usage.Children.Add(advice);
        var suggested=new List<string>();
        Row(usage,Button(T("Analisar este PC","Analyze this PC"),()=>{
            var notes=new List<string>();suggested.Clear();
            if(machine!=null&&machine.Total>0&&machine.Total<=4194304)notes.Add(T("RAM de 4 GB ou menos: revise inicialização e efeitos visuais.","4 GB RAM or less: review startup and visual effects."));
            foreach(var drive in DriveInfo.GetDrives())try{if(drive.IsReady&&drive.DriveType==DriveType.Fixed&&drive.TotalSize>0&&drive.AvailableFreeSpace<drive.TotalSize/10)notes.Add(drive.Name+T(" tem menos de 10% de espaço livre; revise o mapa do disco."," has less than 10% free space; review the disk map."));}catch{}
            if(machine!=null&&!machine.Winget)notes.Add(T("WinGet não identificado: use Instalar programas para instalar ou corrigir.","WinGet not detected: use Install apps to install or repair it."));
            if(choices[0].IsChecked==true){notes.Add(T("Jogos: confira drivers, Modo de Jogo, captura em segundo plano e DirectX legado somente se exigido.","Games: review drivers, Game Mode, background capture and legacy DirectX only when required."));suggested.Add("Valve.Steam");}
            if(choices[1].IsChecked==true){suggested.Add("TheDocumentFoundation.LibreOffice");suggested.Add("SumatraPDF.SumatraPDF");}
            if(choices[2].IsChecked==true){notes.Add(T("Trabalho: confira backups e proteção do sistema.","Work: check backups and system protection."));suggested.Add("7zip.7zip");}
            if(choices[3].IsChecked==true)suggested.Add("OBSProject.OBSStudio");
            if(choices[4].IsChecked==true){suggested.Add("Git.Git");suggested.Add("Microsoft.VisualStudioCode");}
            if(choices[5].IsChecked==true)suggested.Add("GIMP.GIMP");
            if(choices[6].IsChecked==true)suggested.Add("Mozilla.Firefox");
            if(notes.Count==0)notes.Add(T("Nenhum alerta geral encontrado. Revise os módulos de acordo com sua necessidade.","No general alert found. Review modules for your needs."));
            advice.Text=String.Join("\n\n",notes.ToArray())+(suggested.Count>0?"\n\n"+T("Programas sugeridos: ","Suggested apps: ")+String.Join(", ",suggested.Distinct().ToArray()):"");advice.Foreground=StateGreen;
        }),Button(T("Adicionar programas à fila","Add apps to queue"),()=>{foreach(string id in suggested.Distinct())AddQueue(id);ShowPage("apps");}));
        RecommendationCenter();

    }
}
}
