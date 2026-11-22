using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Aurora {
public static class OnlineReputation {
    public static string Sha256(string file){using(var hash=SHA256.Create())using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){byte[] value=hash.ComputeHash(stream);var text=new StringBuilder(64);foreach(byte b in value)text.Append(b.ToString("x2"));return text.ToString();}}
    public static string VirusTotal(string hash,string apiKey){
        if(hash==null||hash.Length!=64||!System.Text.RegularExpressions.Regex.IsMatch(hash,"^[0-9a-f]{64}$")||String.IsNullOrWhiteSpace(apiKey))throw new ArgumentException("Invalid hash or key");
        ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
        var request=(HttpWebRequest)WebRequest.Create("https://www.virustotal.com/api/v3/files/"+hash);request.Method="GET";request.Headers["x-apikey"]=apiKey;request.Timeout=20000;request.ReadWriteTimeout=20000;
        try{using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())){
            var root=new JavaScriptSerializer().DeserializeObject(reader.ReadToEnd()) as Dictionary<string,object>;
            var data=root==null?null:root["data"] as Dictionary<string,object>;
            var attrs=data==null?null:data["attributes"] as Dictionary<string,object>;
            var stats=attrs==null?null:attrs["last_analysis_stats"] as Dictionary<string,object>;
            if(stats==null)return "No analysis statistics returned for SHA-256 "+hash;
            return "SHA-256: "+hash+"\nVirusTotal: "+DateTime.Now.ToString("s")+"\nMalicious: "+Value(stats,"malicious")+" • Suspicious: "+Value(stats,"suspicious")+" • Harmless: "+Value(stats,"harmless")+" • Undetected: "+Value(stats,"undetected")+"\n"+L.T("Ausência de detecção não prova segurança. Revise assinatura, origem e comportamento.","No detection does not prove safety. Review signature, source and behavior.");
        }}catch(WebException e){var response=e.Response as HttpWebResponse;if(response!=null&&response.StatusCode==HttpStatusCode.NotFound)return "SHA-256: "+hash+"\n"+L.T("Nenhum relatório encontrado; isso não prova segurança.","No report found; this does not prove safety.");throw;}
    }
    static string Value(Dictionary<string,object> stats,string key){object value;return stats.TryGetValue(key,out value)?Convert.ToString(value):"?";}
}
}
