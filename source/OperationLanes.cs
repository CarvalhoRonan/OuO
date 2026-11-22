using System;
using System.Collections.Generic;
using System.Threading;
namespace Aurora {
public static class OperationLanes {
    static readonly object installers=new object(),network=new object(),servicing=installers,storage=new object(),printing=new object();
    public static string Lane(string title){
        string t=(title??"").ToLowerInvariant();
        if(t.Contains("clonar disco")||t.Contains("clone disk")||t.Contains("criar imagem do sistema")||t.Contains("create system image")||t.Contains("preparar disco de clonagem")||t.Contains("prepare cloning disk"))return "storage";
        if(t.Contains("scan")||t.Contains("search")||t.Contains("check")||t.Contains("inspect")||t.Contains("query")||t.Contains("inventory")||t.Contains("test")||t.Contains("checar")||t.Contains("consultar")||t.Contains("testar")||t.Contains("mapear")||t.Contains("mapear")||t.Contains("procurar")||t.Contains("inventariar"))return "";
        if(t.Contains("printer preferences")||t.Contains("printer queue")||t.Contains("print queue")||t.Contains("spooler"))return "printing";
        if(t.Contains("install")||t.Contains("upgrade")&&t.Contains("selected")||t.Contains("desinstalar")||t.Contains("instalar")||t.Contains("desinstalação forçada")||t.Contains("repair winget")||t.Contains("corrigir winget")||t=="appx removal")return "installers";
        if(t.Contains("dns")||t.Contains("dhcp renew")||t.Contains("bloquear site")||t.Contains("unblock")||t.Contains("block site")||t.Contains("blocked page")||t.Contains("página bloqueada")||t.Contains("restore standard blocking")||t.Contains("restaurar bloqueio padrão"))return "network";
        if(t.Contains("hyper-v")||t.Contains("dism")||t.Contains("sfc")||t.Contains("boot policy")||t.Contains("system repair")||t.Contains("reparo do sistema")||t.Contains("optional features")||t.Contains("recursos opcionais")||t.StartsWith("enable .net")||t.StartsWith("habilitar .net")||(t.Contains("windows update")&&(t.Contains("reset")||t.Contains("redefinir"))))return "servicing";
        return "";
    }
    public static string Execute(string title,Func<string> action,Action waiting){
        string lane=Lane(title);object sync=lane=="installers"?installers:lane=="network"?network:lane=="servicing"?servicing:lane=="storage"?storage:lane=="printing"?printing:null;
        if(sync==null)return action();
        bool entered=Monitor.TryEnter(sync);
        if(!entered){if(waiting!=null)waiting();Monitor.Enter(sync);entered=true;}
        try{return action();}finally{if(entered)Monitor.Exit(sync);}
    }
}
}
