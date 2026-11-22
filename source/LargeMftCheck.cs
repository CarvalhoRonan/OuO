using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace Aurora {
internal static class LargeMftCheck {
    public static string Run(string folder){
        if(!ExtendedChecks.Allowed())throw new InvalidOperationException("Disposable guest only");if(Directory.Exists(@"T:\"))throw new PlatformNotSupportedException("Guest T: is occupied; the test will not reuse it.");string vhd=Path.Combine(folder,"many-files.vhd"),script=Path.Combine(folder,"many-files-create.txt");
        File.WriteAllText(script,"create vdisk file=\""+vhd+"\" maximum=256 type=expandable\r\nselect vdisk file=\""+vhd+"\"\r\nattach vdisk\r\ncreate partition primary\r\nformat fs=ntfs quick label=OuO-ManyFiles\r\nassign letter=T\r\nexit\r\n",Encoding.ASCII);
        try{var create=Runner.Run("diskpart.exe","/s "+Args.Q(script),60000);if(create.Code!=0||!Directory.Exists(@"T:\"))throw new IOException("Could not mount owned MFT fixture: "+create.Output);long expected=0;byte[] data=new byte[768];for(int dir=0;dir<32;dir++){string path=@"T:\Known\D"+dir;Directory.CreateDirectory(path);for(int file=0;file<500;file++){File.WriteAllBytes(Path.Combine(path,"F"+file+".bin"),data);expected+=data.Length;}}
            var watch=Stopwatch.StartNew();List<DiskBucket> found;string why;long counted=0;using(var cancel=new CancellationTokenSource(60000)){if(!NtfsIndex.TryScan(@"T:\",cancel.Token,(files,bytes)=>counted=files,null,out found,out why))throw new InvalidDataException("Large fixture requires the native MFT index: "+why);}long elapsed=watch.ElapsedMilliseconds;var known=found.SingleOrDefault(x=>x.Path.TrimEnd('\\').Equals(@"T:\Known",StringComparison.OrdinalIgnoreCase));if(known==null||known.Bytes!=expected||counted<16000)throw new InvalidDataException("MFT fixture totals differ: expected="+expected+", actual="+(known==null?-1:known.Bytes)+", files="+counted);return "Native MFT read: "+elapsed+" ms, 16,000 generated files, "+expected+" logical bytes verified. A virtual 256 MB volume does not qualify physical drives with millions of files.";
        }finally{File.WriteAllText(script,"select vdisk file=\""+vhd+"\"\r\ndetach vdisk\r\nexit\r\n",Encoding.ASCII);Runner.Run("diskpart.exe","/s "+Args.Q(script),30000);}
    }
}
}
