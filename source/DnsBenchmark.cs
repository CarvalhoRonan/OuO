using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Aurora {
public static class DnsBenchmark {
    static byte[] Query(ushort id,string domain){var bytes=new List<byte>{(byte)(id>>8),(byte)id,1,0,0,1,0,0,0,0,0,0};foreach(string label in domain.Split('.')){byte[] part=Encoding.ASCII.GetBytes(label);bytes.Add((byte)part.Length);bytes.AddRange(part);}bytes.AddRange(new byte[]{0,0,1,0,1});return bytes.ToArray();}
    public static double? Probe(string local,string server,string domain){IPAddress source=IPAddress.Parse(local),target=IPAddress.Parse(server);if(source.AddressFamily!=target.AddressFamily)throw new ArgumentException("Address family mismatch");ushort id=(ushort)new Random(Guid.NewGuid().GetHashCode()).Next(1,65535);byte[] query=Query(id,domain);using(var udp=new UdpClient(source.AddressFamily)){udp.Client.ReceiveTimeout=1800;udp.Client.SendTimeout=1800;udp.Client.Bind(new IPEndPoint(source,0));udp.Connect(target,53);Stopwatch watch=Stopwatch.StartNew();udp.Send(query,query.Length);IPEndPoint from=null;byte[] response=udp.Receive(ref from);watch.Stop();if(response.Length<12||response[0]!=query[0]||response[1]!=query[1]||(response[2]&0x80)==0||(response[3]&0x0f)!=0)return null;return watch.Elapsed.TotalMilliseconds;}}
    static string TestFamily(string local,string server,string[] domains){var samples=new List<double>();int failed=0;foreach(string domain in domains)try{double? ms=Probe(local,server,domain);if(ms.HasValue)samples.Add(ms.Value);else failed++;}catch{failed++;}samples.Sort();if(samples.Count==0)return L.T("sem resposta","no response")+" • "+failed+"/"+domains.Length+L.T(" falhas"," failures");double spread=samples.Last()-samples.First();return L.T("mediana ","median ")+samples[samples.Count/2].ToString("0.0")+" ms • "+L.T("variação ","spread ")+spread.ToString("0.0")+" ms • "+failed+"/"+domains.Length+L.T(" falhas"," failures");}
    public static string Friendly(string raw){
        string best=null;double fastest=Double.MaxValue;int responding=0,bestFailures=Int32.MaxValue;
        foreach(string line in (raw??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){
            int marker=line.IndexOf(" • IPv4:",StringComparison.Ordinal);
            if(marker<1)continue;
            var match=System.Text.RegularExpressions.Regex.Match(line,@"(?:mediana|median)\s+([0-9.,]+)\s*ms");
            if(!match.Success)continue;
            double value;if(!Double.TryParse(match.Groups[1].Value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.CurrentCulture,out value))continue;
            var failureMatch=System.Text.RegularExpressions.Regex.Match(line,@"(\d+)/\d+\s+(?:falhas|failures)");int failures=failureMatch.Success?Int32.Parse(failureMatch.Groups[1].Value):0;
            responding++;if(failures<bestFailures||failures==bestFailures&&value<fastest){bestFailures=failures;fastest=value;best=line.Substring(0,marker);}
        }
        if(best==null)return L.T("Nenhum perfil respondeu nesta conexão. Verifique a internet, VPN ou firewall e tente novamente.","No profile responded on this connection. Check internet, VPN or firewall and try again.");
        return L.T("Mais rápido nesta conexão: ","Fastest on this connection: ")+best+"\n"+
            L.T("Perfis que responderam: ","Profiles responding: ")+responding+"\n"+
            L.T("Para proteção contra sites perigosos ou filtro familiar, escolha o perfil correspondente antes de aplicar. O teste não mudou suas configurações.","For protection against harmful sites or family filtering, choose the matching profile before applying. The test did not change your settings.");
    }    public static string Run(string local,IEnumerable<DnsProfile> profiles){return Run(local,null,profiles);}
    public static bool HasRespondingProfile(string raw){return System.Text.RegularExpressions.Regex.IsMatch(raw??"",@"(?:mediana|median)\s+[0-9.,]+\s*ms",System.Text.RegularExpressions.RegexOptions.IgnoreCase);}
    public static string Run(string localV4,string localV6,IEnumerable<DnsProfile> profiles){var all=profiles.ToArray();var lines=new string[all.Length];string[] domains={"example.com","www.microsoft.com","www.wikipedia.org"};Parallel.For(0,all.Length,new ParallelOptions{MaxDegreeOfParallelism=4},i=>{DnsProfile p=all[i];lines[i]=p.Name+" • IPv4: "+TestFamily(localV4,p.V4[0],domains);if(!String.IsNullOrWhiteSpace(localV6))lines[i]+="\n  IPv6: "+TestFamily(localV6,p.V6[0],domains);});return String.Join(Environment.NewLine,lines)+Environment.NewLine+Environment.NewLine+L.T("Teste direto por UDP/53. VPN, firewall e DNS seguro do navegador podem alterar a navegação real. Nenhuma configuração foi alterada.","Direct UDP/53 test. VPN, firewall and browser secure DNS may affect real browsing. No settings were changed.");}
}
}
