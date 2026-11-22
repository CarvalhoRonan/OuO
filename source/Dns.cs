using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Microsoft.Win32;
namespace Aurora {
public sealed class DnsProfile {
    public string Name,Description;public string[] V4,V6;public override string ToString(){return Name;}
    public static DnsProfile[] All(){return new[]{
        new DnsProfile{Name="AdGuard DNS",Description=L.T("Filtra anúncios e rastreadores.","Filters ads and trackers."),V4=new[]{"94.140.14.14","94.140.15.15"},V6=new[]{"2a10:50c0::ad1:ff","2a10:50c0::ad2:ff"}},
        new DnsProfile{Name=L.T("AdGuard DNS • Família","AdGuard DNS • Family"),Description=L.T("Filtra anúncios, rastreadores e conteúdo adulto. SafeSearch quando disponível.","Filters ads, trackers and adult content. SafeSearch where available."),V4=new[]{"94.140.14.15","94.140.15.16"},V6=new[]{"2a10:50c0::bad1:ff","2a10:50c0::bad2:ff"}},
        new DnsProfile{Name=L.T("Cloudflare • Malware","Cloudflare • Malware"),Description=L.T("Bloqueia domínios classificados como malware. Não bloqueia anúncios.","Blocks domains classified as malware. Does not block ads."),V4=new[]{"1.1.1.2","1.0.0.2"},V6=new[]{"2606:4700:4700::1112","2606:4700:4700::1002"}},
        new DnsProfile{Name=L.T("Cloudflare • Malware e adultos","Cloudflare • Malware & adult content"),Description=L.T("Filtra domínios de malware e conteúdo adulto. Não bloqueia anúncios.","Filters malware and adult-content domains. Does not block ads."),V4=new[]{"1.1.1.3","1.0.0.3"},V6=new[]{"2606:4700:4700::1113","2606:4700:4700::1003"}},
        new DnsProfile{Name="Quad9 • Secure",Description=L.T("Bloqueia domínios maliciosos; não é filtro de anúncios ou conteúdo adulto.","Blocks malicious domains; not an ad or adult-content filter."),V4=new[]{"9.9.9.9","149.112.112.112"},V6=new[]{"2620:fe::fe","2620:fe::9"}},
        new DnsProfile{Name="CleanBrowsing • Security",Description=L.T("Filtra domínios maliciosos e phishing.","Filters malicious and phishing domains."),V4=new[]{"185.228.168.9","185.228.169.9"},V6=new[]{"2a0d:2a00:1::2","2a0d:2a00:2::2"}},
        new DnsProfile{Name="CleanBrowsing • Adult",Description=L.T("Filtra conteúdo adulto e domínios maliciosos.","Filters adult content and malicious domains."),V4=new[]{"185.228.168.10","185.228.169.11"},V6=new[]{"2a0d:2a00:1::1","2a0d:2a00:2::1"}},
        new DnsProfile{Name="CleanBrowsing • Family",Description=L.T("Filtra conteúdo adulto, proxies de evasão e domínios maliciosos; ativa SafeSearch onde disponível.","Filters adult content, bypass proxies and malicious domains; enables SafeSearch where available."),V4=new[]{"185.228.168.168","185.228.169.168"},V6=new[]{"2a0d:2a00:1::","2a0d:2a00:2::"}}
    };}
}
public sealed class AdapterInfo {
    public string Id,Name,LocalV4,LocalV6;public int V4Index=-1,V6Index=-1;public string[] Current;
    public override string ToString(){return Name;}
    [DllImport("iphlpapi.dll")] static extern uint GetBestInterface(uint destination,out uint index);
    public static AdapterInfo Active(IEnumerable<AdapterInfo> adapters){
        var list=adapters.ToList();if(list.Count==0)return null;
        try{uint index;uint address=BitConverter.ToUInt32(IPAddress.Parse("1.1.1.1").GetAddressBytes(),0);if(GetBestInterface(address,out index)==0){var best=list.FirstOrDefault(x=>x.V4Index==index);if(best!=null)return best;}}catch{}
        return list.FirstOrDefault(x=>!String.IsNullOrWhiteSpace(x.LocalV4))??list[0];
    }
    public static List<AdapterInfo> List(){var result=new List<AdapterInfo>();foreach(NetworkInterface n in NetworkInterface.GetAllNetworkInterfaces()){
        if(n.NetworkInterfaceType==NetworkInterfaceType.Loopback||n.OperationalStatus!=OperationalStatus.Up)continue;try{var props=n.GetIPProperties();var a=new AdapterInfo{Id=n.Id,Name=n.Name,Current=props.DnsAddresses.Select(x=>x.ToString()).ToArray(),LocalV4=props.UnicastAddresses.Where(x=>x.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork).Select(x=>x.Address.ToString()).FirstOrDefault(),LocalV6=props.UnicastAddresses.Where(x=>x.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6&&!x.Address.IsIPv6LinkLocal).Select(x=>x.Address.ToString()).FirstOrDefault()};try{a.V4Index=props.GetIPv4Properties().Index;}catch{}try{a.V6Index=props.GetIPv6Properties().Index;}catch{}if(a.V4Index>0)result.Add(a);}catch{}
    }return result;}
}
public static class DnsManager {
    static string[] ConfiguredServers(AdapterInfo adapter){
        Guid guid;if(adapter==null||!Guid.TryParse(adapter.Id,out guid))return new string[0];
        try{using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{"+guid.ToString()+"}")){
            if(key==null)return new string[0];
            foreach(string name in new[]{"NameServer","DhcpNameServer"}){
                string raw=Convert.ToString(key.GetValue(name,""));
                var values=Regex.Split(raw.Trim(),@"[,;\s]+").Where(x=>x.Length>0).ToArray();
                if(values.Length>0&&values.All(x=>{IPAddress ip;return IPAddress.TryParse(x,out ip);}))return values;
            }
        }}catch{}return new string[0];
    }
    static string[] StaticServers(string family,string id){using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\"+(family=="ipv4"?"Tcpip":"Tcpip6")+@"\Parameters\Interfaces\"+id)){
        if(key==null)throw new IOException("Adapter configuration unavailable: "+family);object value=key.GetValue("NameServer","");string raw=value is string[]?String.Join(",",(string[])value):Convert.ToString(value);var servers=Regex.Split(raw.Trim(),@"[,;\s]+").Where(x=>x!="").ToArray();foreach(string s in servers){IPAddress ip;if(!IPAddress.TryParse(s,out ip))throw new InvalidDataException("Invalid existing DNS address");}return servers;
    }}
    public static XElement Capture(AdapterInfo a,bool ipv6){Guid guid;if(!Guid.TryParse(a.Id,out guid))throw new ArgumentException("Invalid adapter ID");var root=new XElement("dns",new XAttribute("id",a.Id),new XAttribute("name",a.Name));foreach(string family in new[]{"ipv4","ipv6"}){int index=family=="ipv4"?a.V4Index:a.V6Index;if(index<=0||family=="ipv6"&&!ipv6)continue;string[] servers=StaticServers(family,"{"+guid.ToString()+"}");root.Add(new XElement("family",new XAttribute("name",family),new XAttribute("index",index),new XAttribute("automatic",servers.Length==0),servers.Select(x=>new XElement("server",x))));}return root;}
    public static bool SameServers(IEnumerable<string> actual,IEnumerable<string> expected){
        Func<string,string> normalize=x=>IPAddress.Parse(x.Split('%')[0]).ToString();
        return new HashSet<string>(actual.Select(normalize),StringComparer.OrdinalIgnoreCase).SetEquals(expected.Select(normalize));
    }
    public static string[] Effective(AdapterInfo adapter){
        var fresh=AdapterInfo.List().FirstOrDefault(x=>String.Equals(x.Id,adapter.Id,StringComparison.OrdinalIgnoreCase));
        if(fresh==null)throw new IOException(L.T("A conexão não está mais ativa.","The connection is no longer active."));
        return fresh.Current??new string[0];
    }
    public static string Set(string family,int index,string[] servers){
        if(!new[]{"ipv4","ipv6"}.Contains(family)||index<=0)throw new ArgumentException("Invalid adapter family");
        foreach(string address in servers){IPAddress ip;if(!IPAddress.TryParse(address,out ip)||(family=="ipv4")!=(ip.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork))throw new ArgumentException("Invalid DNS address family");}
        string addresses="@("+String.Join(",",servers.Select(Runner.QuotePS).ToArray())+")";
        string script="if(!(Get-Command Get-DnsClientServerAddress -ErrorAction SilentlyContinue)){Write-Output 'OUO_LEGACY';exit 0}; $c=Get-DnsClientServerAddress -InterfaceIndex "+index+" -AddressFamily "+(family=="ipv4"?"IPv4":"IPv6")+" -ErrorAction Stop; if(!$c){throw 'DNS interface not found'}; $c | Set-DnsClientServerAddress "+(servers.Length==0?"-ResetServerAddresses":"-ServerAddresses "+addresses)+" -ErrorAction Stop; $v=Get-DnsClientServerAddress -InterfaceIndex "+index+" -AddressFamily "+(family=="ipv4"?"IPv4":"IPv6")+"; $v.ServerAddresses | ForEach-Object { 'DNS|'+$_ }";
        var modern=Runner.PS(script,45000);
        if(modern.Code!=0)throw new IOException(modern.Output);
        if(!modern.Output.Contains("OUO_LEGACY")){
            string[] actual=modern.Output.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Where(x=>x.StartsWith("DNS|",StringComparison.Ordinal)).Select(x=>x.Substring(4).Trim()).ToArray();
            if(servers.Length>0&&!SameServers(actual,servers))throw new IOException(L.T("O Windows não confirmou os servidores solicitados. A configuração anterior será restaurada.","Windows did not confirm the requested servers. Previous settings will be restored."));
            return String.Join(", ",actual);
        }
        string prefix="interface "+family+" ";
        string arguments=servers.Length==0?"set dnsservers name="+index+" source=dhcp":"set dnsservers name="+index+" source=static address="+servers[0]+" validate=no";
        var first=Runner.Run("netsh.exe",prefix+arguments,30000);if(first.Code!=0)throw new IOException(first.Output);
        for(int i=1;i<servers.Length;i++){var add=Runner.Run("netsh.exe",prefix+"add dnsservers name="+index+" address="+servers[i]+" index="+(i+1)+" validate=no",30000);if(add.Code!=0)throw new IOException(add.Output);}
        return first.Output;
    }
    static AdapterInfo throwAdapter(){throw new IOException(L.T("A conexão selecionada não está mais ativa.","The selected connection is no longer active."));}
    public static string ApplyProfile(AdapterInfo adapter,DnsProfile profile,bool both,string folder){
        adapter=AdapterInfo.List().FirstOrDefault(x=>String.Equals(x.Id,adapter.Id,StringComparison.OrdinalIgnoreCase))??throwAdapter();
        XElement before=Capture(adapter,both);string file=BackupFile(folder,adapter.Id);
        Directory.CreateDirectory(folder);
        if(!File.Exists(file))AtomicFiles.SaveXml(before,file);
        else{XElement original=XElement.Load(file);foreach(var family in before.Elements("family"))if(!original.Elements("family").Any(x=>(string)x.Attribute("name")==(string)family.Attribute("name")))original.Add(new XElement(family));AtomicFiles.SaveXml(original,file);}
        try{
            Set("ipv4",adapter.V4Index,profile.V4);
            if(both&&adapter.V6Index>0)Set("ipv6",adapter.V6Index,profile.V6);
            var flush=Runner.Run("ipconfig.exe","/flushdns",15000);
            string[] effective=Effective(adapter);
            if(!SameServers(effective.Where(x=>IPAddress.Parse(x).AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork),profile.V4))
                throw new IOException(L.T("A conexão continua usando um DNS diferente do escolhido. Verifique VPN ou políticas da rede.","The connection still uses different DNS. Check VPN or network policies."));
            if(both&&adapter.V6Index>0&&!SameServers(effective.Where(x=>IPAddress.Parse(x).AddressFamily==System.Net.Sockets.AddressFamily.InterNetworkV6),profile.V6))
                throw new IOException(L.T("O DNS IPv6 não corresponde ao perfil escolhido.","IPv6 DNS does not match the selected profile."));
            return "OK • "+L.T("DNS aplicado e confirmado: ","DNS applied and confirmed: ")+profile.Name+"\n"+adapter.Name+"\n"+String.Join(", ",effective)+(flush.Code==0?"":"\nERROR • "+flush.Output);
        }catch(Exception error){string rollback;try{Restore(before);rollback=L.T("configuração anterior restaurada.","previous settings restored.");}catch(Exception ex){rollback=L.T("falha ao restaurar: ","restore failed: ")+ex.Message;}throw new IOException(error.Message+"\n"+rollback);}
    }
    public static string CurrentLabel(AdapterInfo adapter){
        if(adapter==null)return L.T("Não identificado • nenhuma conexão ativa encontrada.","Not identified • no active connection found.");
        string[] current=Effective(adapter);bool fallback=current.Length==0;
        if(fallback)current=ConfiguredServers(adapter);
        if(current.Length==0)return L.T("Não identificado • o Windows não informou o DNS desta conexão.","Not identified • Windows did not report this connection's DNS.");
        var v4=current.Where(x=>IPAddress.Parse(x).AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork).ToArray();
        var profile=DnsProfile.All().FirstOrDefault(x=>SameServers(v4,x.V4));
        return (fallback?L.T("Configurado no adaptador • não confirmado em uso • ","Configured on adapter • not confirmed in use • "):L.T("DNS atual • ","Current DNS • "))+adapter.Name+"\n"+(profile==null?L.T("DNS da rede ou personalizado","Network or custom DNS"):profile.Name)+"\n"+String.Join(", ",current);
    }
    public static string Restore(XElement backup){var adapter=AdapterInfo.List().FirstOrDefault(x=>String.Equals(x.Id,(string)backup.Attribute("id"),StringComparison.OrdinalIgnoreCase));if(adapter==null)throw new IOException("Adapter is no longer active");string result="";foreach(var family in backup.Elements("family")){string name=(string)family.Attribute("name");int index=name=="ipv4"?adapter.V4Index:adapter.V6Index;result+=Set(name,index,(bool)family.Attribute("automatic")?new string[0]:family.Elements("server").Select(x=>x.Value).ToArray());}return result;}
    public static string BackupFile(string folder,string id){Guid guid;if(!Guid.TryParse(id,out guid))throw new ArgumentException("Invalid adapter ID");return Path.Combine(folder,"DNS-"+guid.ToString("N")+".xml");}
}
public partial class MainWindow {
    void DnsPage(){Heading(T("DNS e bloqueios","DNS & filtering"),T("Filtragem por conexão, com backup e restauração dos servidores anteriores.","Per-connection filtering, with backup and restoration of previous servers."));DnsSections();}
    Tuple<CardColumns,CardColumns> DnsSections(){BeginColumns(2);CardColumns connectionHost=cardHost;
        var p=Card(T("Conexão e perfil","Connection & profile"),T("VPN, DNS seguro do navegador, políticas corporativas e outros adaptadores podem usar uma resolução diferente.","VPNs, browser secure DNS, organization policies and other adapters may use different resolution."));ComboBox adapters=new ComboBox{Margin=new Thickness(0,0,0,12)};try{foreach(var a in AdapterInfo.List())adapters.Items.Add(a);}catch{}adapters.SelectedItem=AdapterInfo.Active(adapters.Items.OfType<AdapterInfo>());p.Children.Add(adapters);ComboBox profiles=new ComboBox{Margin=new Thickness(0,0,0,12)};foreach(var profile in DnsProfile.All())profiles.Items.Add(profile);profiles.SelectedIndex=0;p.Children.Add(profiles);TextBlock info=Text(((DnsProfile)profiles.SelectedItem).Description,13,Muted);p.Children.Add(info);profiles.SelectionChanged+=(s,e)=>info.Text=((DnsProfile)profiles.SelectedItem).Description;
        TextBlock currentDns=Text("",14,Muted);p.Children.Add(currentDns);
        Action refreshDns=()=>{var selectedAdapter=adapters.SelectedItem as AdapterInfo;string id=selectedAdapter==null?null:selectedAdapter.Id;try{var all=AdapterInfo.List();adapters.Items.Clear();foreach(var item in all)adapters.Items.Add(item);adapters.SelectedItem=all.FirstOrDefault(x=>x.Id==id)??AdapterInfo.Active(all);currentDns.Text=DnsManager.CurrentLabel(adapters.SelectedItem as AdapterInfo);currentDns.Foreground=currentDns.Text.StartsWith(T("DNS atual","Current DNS"),StringComparison.Ordinal)?StateGreen:Muted;}catch(Exception ex){currentDns.Text="ERROR • "+ex.Message;currentDns.Foreground=OutcomeBrush(currentDns.Text);}};
        bool changingAdapter=false;adapters.SelectionChanged+=(sender,args)=>{if(changingAdapter)return;try{changingAdapter=true;currentDns.Text=DnsManager.CurrentLabel(adapters.SelectedItem as AdapterInfo);currentDns.Foreground=currentDns.Text.StartsWith(T("DNS atual","Current DNS"),StringComparison.Ordinal)?StateGreen:Muted;}catch(Exception ex){currentDns.Text="ERROR • "+ex.Message;currentDns.Foreground=OutcomeBrush(currentDns.Text);}finally{changingAdapter=false;}};
        p.IsVisibleChanged+=(sender,args)=>{if(p.IsVisible)refreshDns();};
        Row(p,Button(T("Atualizar DNS atual","Refresh current DNS"),()=>refreshDns()));refreshDns();
        var v6=Choice(p,T("Configurar IPv6 também, se disponível","Also configure IPv6, when available"),true,true);TextBox output=Input("");output.IsReadOnly=true;output.Foreground=StateGreen;output.TextChanged+=(sender,args)=>output.Foreground=OutcomeBrush(output.Text);output.AcceptsReturn=true;output.Height=220;output.TextWrapping=TextWrapping.Wrap;output.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        Row(p,Button(T("Aplicar DNS","Apply DNS"),()=>{var adapter=adapters.SelectedItem as AdapterInfo;var profile=profiles.SelectedItem as DnsProfile;if(activeJobs.Values.Any(x=>x.Item1.StartsWith("DNS",StringComparison.OrdinalIgnoreCase))||adapter==null||profile==null||!RequireAdmin())return;bool both=v6.IsChecked==true;if(!Confirm(T("Alterar DNS da conexão ","Change DNS for ")+adapter.Name+"?\n"+profile.Name+"\nIPv4: "+String.Join(", ",profile.V4)+(both?"\nIPv6: "+String.Join(", ",profile.V6):"")+T("\nPode afetar serviços internos da rede. O estado anterior será salvo.","\nMay affect internal network services. Previous settings will be saved.")))return;
            Work("DNS apply",()=>DnsManager.ApplyProfile(adapter,profile,both,journal.Folder),r=>{output.Text=r;refreshDns();});
        }),Button(T("Restaurar DNS anterior","Restore previous DNS"),()=>{var adapter=adapters.SelectedItem as AdapterInfo;if(activeJobs.Values.Any(x=>x.Item1.StartsWith("DNS",StringComparison.OrdinalIgnoreCase))||adapter==null||!RequireAdmin())return;string file=DnsManager.BackupFile(journal.Folder,adapter.Id);if(!File.Exists(file)){output.Text=T("Não há backup para esta conexão.","No backup for this connection.");return;}if(!Confirm(T("Restaurar os modos automático/estático e os servidores anteriores desta conexão?","Restore this connection's previous automatic/static modes and servers?")))return;Work("DNS restore",()=>{var backup=XElement.Load(file);foreach(var family in backup.Elements("family"))family.SetAttributeValue("index",(string)family.Attribute("name")=="ipv4"?adapter.V4Index:adapter.V6Index);string result=DnsManager.Restore(backup);File.Delete(file);Runner.Run("ipconfig.exe","/flushdns",15000);return T("DNS restaurado.","DNS restored.")+"\n"+result;},r=>{output.Text=r;refreshDns();});}));AttachResult(p,output);
        Row(p,Button(T("Aplicar em todas as conexões ativas","Apply to all active connections"),()=>{
            var profile=profiles.SelectedItem as DnsProfile;var available=AdapterInfo.List().Where(x=>x.V4Index>0).ToArray();
            if(profile==null||available.Length==0||!RequireAdmin())return;
            if(!Confirm(T("Aplicar ","Apply ")+profile.Name+T(" nestas conexões?\n"," to these connections?\n")+String.Join("\n",available.Select(x=>x.Name).ToArray())+T("\nCada configuração anterior será salva.","\nEach prior configuration will be backed up.")))return;
            bool both=v6.IsChecked==true;Work(T("Aplicar DNS nas conexões","Apply DNS to connections"),()=>DnsBulk.Apply(available,profile,both,journal.Folder),r=>{output.Text=r;refreshDns();});
        }));        var benchmark=Card(T("Qual DNS combina com sua conexão?","Which DNS suits your connection?"),T("Compare velocidade, proteção e filtro para família. O teste não altera nada.","Compare speed, protection and family filtering. The test changes nothing."));TextBlock simple=Text(T("Clique em Testar minha conexão para receber uma indicação.","Click Test my connection for a recommendation."),14,StateGreen);benchmark.Children.Add(simple);TextBox results=Input("");results.Height=180;results.IsReadOnly=true;results.Foreground=StateGreen;results.TextChanged+=(sender,args)=>results.Foreground=OutcomeBrush(results.Text);results.AcceptsReturn=true;results.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;var details=new Expander{Header=T("Detalhes do teste","Test details"),Foreground=Ink,Content=results};benchmark.Children.Add(details);Row(benchmark,Button(T("Testar minha conexão","Test my connection"),()=>{var adapter=adapters.SelectedItem as AdapterInfo;if(adapter==null||String.IsNullOrEmpty(adapter.LocalV4)){simple.Text=T("Escolha uma conexão ativa acima.","Choose an active connection above.");return;}simple.Text=T("Testando opções disponíveis…","Testing available options…");Work(T("Testar DNS","Test DNS"),()=>DnsBenchmark.Run(adapter.LocalV4,adapter.LocalV6,DnsProfile.All()),r=>{results.Text=r;simple.Text=DnsBenchmark.Friendly(r);simple.Foreground=DnsBenchmark.HasRespondingProfile(r)?StateGreen:OutcomeBrush("ERROR • sem resposta");results.Foreground=simple.Foreground;});}));
        BeginColumns(2);CardColumns sitesHost=cardHost;
        var block=Card(T("Sites bloqueados por você","Sites you blocked"),T("Cole o endereço ou domínio do site. O bloqueio do Windows cobre os nomes cadastrados; a opção dos navegadores cobre também subdomínios. Reinicie os navegadores após aplicar.","Paste the address or domain. Windows blocking covers listed names; browser rules also cover subdomains. Restart browsers after applying."));
        TextBox domain=Input("");domain.ToolTip=T("Exemplo: exemplo.com","Example: example.com");block.Children.Add(domain);
        CheckBox www=Choice(block,T("Bloquear também www","Block www too"),true,true);
        CheckBox browserBlock=Choice(block,T("Aplicar regras também nos navegadores detectados","Also apply rules to detected browsers"),true,true);if(BrowserBlockPage.Active){browserBlock.IsChecked=false;browserBlock.IsEnabled=false;}
        var noSites=Text(T("Nenhum site cadastrado. Digite um endereço para adicionar.","No sites listed. Enter an address to add one."),13,Muted);block.Children.Add(noSites);
        ListBox blocked=new ListBox{Height=130,Background=Surface,Foreground=Ink,BorderBrush=Muted};block.Children.Add(blocked);
        TextBlock blockStatus=Text("",13,StateGreen);block.Children.Add(blockStatus);
        var siteActions=new StackPanel();block.Children.Add(siteActions);
        Action refreshBlocks=()=>{blocked.Items.Clear();try{foreach(string d in HostsBlock.Current())blocked.Items.Add(d);}catch(Exception ex){blockStatus.Text="ERROR • "+ex.Message;}blocked.Visibility=blocked.Items.Count>0?Visibility.Visible:Visibility.Collapsed;noSites.Visibility=blocked.Items.Count==0?Visibility.Visible:Visibility.Collapsed;};refreshBlocks();
        var styleCard=Card(T("Página exibida ao bloquear","Blocked-page appearance"),T("Personalize o título, a mensagem e a cor da página local.","Customize the local page title, message and accent color."));
        var pageStyle=BrowserBlockPage.Style();
        TextBox pageTitle=Field(styleCard,T("Título da página de bloqueio","Blocked-page title"),pageStyle.Title);
        TextBox pageMessage=Field(styleCard,T("Mensagem exibida","Displayed message"),pageStyle.Message);
        ComboBox pageColor=new ComboBox{Margin=new Thickness(0,0,0,8)};foreach(string color in new[]{"#71DCC3","#83BBFF","#C4A4FF","#F5C976"})pageColor.Items.Add(color);pageColor.SelectedItem=pageStyle.Accent; if(pageColor.SelectedItem==null)pageColor.SelectedIndex=0;styleCard.Children.Add(pageColor);
        Row(styleCard,Button(T("Salvar aparência da página","Save page appearance"),()=>{try{BrowserBlockPage.Configure(pageTitle.Text,pageMessage.Text,Convert.ToString(pageColor.SelectedItem));blockStatus.Text=BrowserBlockPage.Active?BrowserBlockPage.Generate(HostsBlock.Current()):"OK • "+T("Aparência salva.","Appearance saved.");}catch(Exception e){blockStatus.Text="ERROR • "+e.Message;}}));
        Row(siteActions,Button(T("Bloquear site","Block site"),()=>{string selected;try{selected=SiteProtection.Normalize(domain.Text);}catch(Exception ex){blockStatus.Text="ERROR • "+ex.Message;return;}if(!RequireAdmin())return;
            string[] next;try{var list=HostsBlock.Current().ToList();list.Add(selected);if(www.IsChecked==true){if(selected.StartsWith("www.",StringComparison.OrdinalIgnoreCase))list.Add(selected.Substring(4));else list.Add("www."+selected);}next=list.ToArray();}catch(Exception ex){blockStatus.Text="ERROR • "+ex.Message;return;}
            if(!Confirm(T("Bloquear ","Block ")+selected+"?\n"+(browserBlock.IsChecked==true?T("Aplicar no Windows e nas políticas dos navegadores detectados, incluindo subdomínios. Reinicie os navegadores depois.","Apply in Windows and detected browser policies, including subdomains. Restart browsers afterward."):T("Aplicar no arquivo hosts do Windows.","Apply in the Windows hosts file."))))return;bool browsers=browserBlock.IsChecked==true;Work(T("Bloquear site","Block site"),()=>HostsBlock.Apply(next,journal.Folder)+(browsers?"\n"+SiteProtection.SyncBrowsers(next,journal.Folder):BrowserBlockPage.Exists?"\n"+BrowserBlockPage.Generate(next):"")+"\n"+SiteProtection.Verify(selected),r=>{blockStatus.Text=r;refreshBlocks();});
        }),Button(T("Remover bloqueio","Remove block"),()=>{string selected=Convert.ToString(blocked.SelectedItem);if(String.IsNullOrWhiteSpace(selected)||!RequireAdmin()||!Confirm(T("Desbloquear ","Unblock ")+selected+"?"))return;
            string[] next=HostsBlock.Current().Where(x=>!String.Equals(x,selected,StringComparison.OrdinalIgnoreCase)).ToArray();Work(T("Desbloquear site","Unblock site"),()=>HostsBlock.Apply(next,journal.Folder)+"\n"+(BrowserBlockPage.Active?BrowserBlockPage.Generate(next)+"\n"+SiteProtection.SyncBrowsers(new string[0],journal.Folder):SiteProtection.SyncBrowsers(next,journal.Folder)),r=>{blockStatus.Text=r;refreshBlocks();});
        }),Button(T("Verificar bloqueio","Verify block"),()=>{string selected=Convert.ToString(blocked.SelectedItem);if(String.IsNullOrWhiteSpace(selected)){blockStatus.Text=T("Selecione um site na lista.","Select a site in the list.");return;}Work(T("Verificar bloqueio","Verify block"),()=>SiteProtection.Verify(selected),r=>blockStatus.Text=r);}));
        Row(styleCard,Button(T("Preparar página de bloqueio","Prepare blocked page"),()=>{if(!RequireAdmin())return;
            if(!Confirm(T("Preparar uma extensão local para mostrar a página de bloqueio? As regras de bloqueio do OuO nos navegadores serão substituídas pela extensão; o arquivo hosts continuará ativo. Será preciso carregar a extensão manualmente no Chrome, Edge ou Brave.","Prepare a local extension that shows the blocked page? OuO browser policy rules will be replaced by the extension; hosts blocking remains active. You must load the extension manually in Chrome, Edge or Brave.")))return;
            Work(T("Preparar página bloqueada","Prepare blocked page"),()=>BrowserBlockPage.Prepare(HostsBlock.Current().ToArray(),journal.Folder),r=>{blockStatus.Text=r;if(!ResultTone.IsFailure(r)){browserBlock.IsChecked=false;browserBlock.IsEnabled=false;}});
        }),Button(T("Abrir pasta da extensão","Open extension folder"),()=>{if(BrowserBlockPage.Exists)Open(BrowserBlockPage.Root);else ShowNotice(T("Prepare a página antes de abrir a pasta.","Prepare the page before opening its folder."));}),
            Button(T("Voltar ao bloqueio padrão","Use standard blocking"),()=>{if(!BrowserBlockPage.Active||!RequireAdmin()||!Confirm(T("Voltar às regras do navegador sem página personalizada? Recarregue ou remova a extensão nos navegadores depois.","Return to browser rules without a custom page? Reload or remove the extension in browsers afterward.")))return;
                Work(T("Restaurar bloqueio padrão","Restore standard blocking"),()=>SiteProtection.SyncBrowsers(HostsBlock.Current(),journal.Folder)+"\n"+BrowserBlockPage.Deactivate(),r=>{blockStatus.Text=r;if(!ResultTone.IsFailure(r)){browserBlock.IsChecked=true;browserBlock.IsEnabled=true;}});
            }));
        cardHost=connectionHost;
        var limits=Card(T("Limitações e outras proteções","Limitations & other protection"),T("DNS não remove todo anúncio, não é um antivírus e não garante controle parental absoluto. A filtragem depende da classificação de domínios pelo provedor; falsos positivos são possíveis.","DNS cannot remove every ad, is not antivirus and does not guarantee absolute parental control. Filtering depends on the provider's domain classification; false positives are possible."));Note(limits,"AdGuard filtra anúncios e rastreadores. O perfil Família acrescenta conteúdo adulto. Cloudflare filtra malware e, no perfil Família, conteúdo adulto; não filtra anúncios. Quad9 e CleanBrowsing oferecem políticas adicionais. Use somente um perfil por conexão, sem misturar servidores de políticas diferentes.","AdGuard filters ads and trackers. Cloudflare filters malware and adult content by profile. Quad9 and CleanBrowsing add other policies. Use one profile per connection; do not mix servers with different filtering policies.");Row(limits,Button("AdGuard DNS",()=>Open("https://adguard-dns.io/en/public-dns.html")),Button("Quad9",()=>Open("https://docs.quad9.net/services/")),Button("CleanBrowsing",()=>Open("https://cleanbrowsing.org/filters")));
        ManagedBlockDeployment(styleCard);NetworkSection();
        return Tuple.Create(connectionHost,sitesHost);
    }
}
}
