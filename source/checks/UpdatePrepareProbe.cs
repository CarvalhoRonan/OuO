using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;
using Aurora;
class UpdatePrepareProbe {
    static string Digest(Stream stream){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
    static int Main(string[] args){try{string version=OuOUpdates.Current.ToString();var release=OuOUpdates.Check(true).First();int progressEvents=0;string prepared=OuOUpdates.Prepare(release,p=>progressEvents++,CancellationToken.None);string[] executables=Directory.GetFiles(prepared,"OuO.exe",SearchOption.AllDirectories);if(executables.Length!=1)throw new Exception("Prepared executable missing or duplicated");string preparedHash;using(var input=File.OpenRead(executables[0]))preparedHash=Digest(input);string backup=Directory.GetFiles(OuOUpdates.Folder,"previous-"+version+"-*.zip").OrderByDescending(File.GetLastWriteTimeUtc).First();string backupHash,currentHash;using(var input=File.OpenRead(typeof(OuOUpdates).Assembly.Location))currentHash=Digest(input);using(var archive=ZipFile.OpenRead(backup)){var exe=archive.GetEntry("OuO.exe");if(exe==null)throw new Exception("Backup executable missing");using(var input=exe.Open())backupHash=Digest(input);}if(backupHash!=currentHash)throw new Exception("Backup does not match the tested executable");Console.WriteLine(new JavaScriptSerializer().Serialize(new{Status="PASS",CurrentVersion=version,CurrentSHA256=currentHash,DownloadedTag=release.Tag,PublishedZIPHash=release.Digest,PreparedExeSHA256=preparedHash,BackupExeSHA256=backupHash,ProgressEvents=progressEvents,ActivatedUpdate=false,ChangedSettings=false}));return 0;}catch(Exception ex){Console.WriteLine(new JavaScriptSerializer().Serialize(new{Status="FAIL",Error=ex.ToString()}));return 1;}}
}
