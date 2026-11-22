using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
namespace Aurora {
public sealed class FastDiskEntry {public string Path;public long Bytes;public bool Directory,Link;public DateTime Modified;}
public static class FastDisk {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct FindData {public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Creation,Access,Write;public uint High,Low,Reserved0,Reserved1;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=14)]public string Alt;}
    sealed class FindHandle:SafeHandleZeroOrMinusOneIsInvalid {private FindHandle():base(true){}protected override bool ReleaseHandle(){return FindClose(handle);}}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern FindHandle FindFirstFileEx(string file,int info,out FindData data,int search,IntPtr filter,int flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool FindNextFile(FindHandle h,out FindData data);
    [DllImport("kernel32.dll")]static extern bool FindClose(IntPtr h);
    public static IEnumerable<FastDiskEntry> Entries(string path){
        string full=Path.GetFullPath(path).TrimEnd('\\'),pattern=(full.StartsWith(@"\\")?@"\\?\UNC\"+full.Substring(2):@"\\?\"+full)+@"\*";
        FindData data;FindHandle handle=FindFirstFileEx(pattern,1,out data,0,IntPtr.Zero,2);
        if(handle.IsInvalid&&Marshal.GetLastWin32Error()==87){handle.Dispose();handle=FindFirstFileEx(pattern,0,out data,0,IntPtr.Zero,0);}
        if(handle.IsInvalid){int error=Marshal.GetLastWin32Error();handle.Dispose();if(error==2)return Enumerable.Empty<FastDiskEntry>();throw new Win32Exception(error);}
        return Read(handle,data,full);
    }
    static IEnumerable<FastDiskEntry> Read(FindHandle handle,FindData first,string parent){
        using(handle){FindData data=first;while(true){
            if(data.Name!="."&&data.Name!="..")yield return new FastDiskEntry{Path=parent+"\\"+data.Name,Directory=(data.Attributes&16)!=0,Link=(data.Attributes&1024)!=0,Bytes=((long)data.High<<32)|data.Low,Modified=LastWrite(data)};
            if(!FindNextFile(handle,out data)){int error=Marshal.GetLastWin32Error();if(error!=18)throw new Win32Exception(error);break;}
        }}
    }
    static DateTime LastWrite(FindData data){try{return DateTime.FromFileTimeUtc(((long)data.Write.dwHighDateTime<<32)|(uint)data.Write.dwLowDateTime).ToLocalTime();}catch{return DateTime.MinValue;}}
}
public static class DiskIndex {
    sealed class Cached {public long Bytes;public int Errors;public DateTime Time;}
    sealed class Record {public string Path,Parent;public int Depth,Errors;public long Bytes;}
    static readonly Dictionary<string,Cached> cache=new Dictionary<string,Cached>(StringComparer.OrdinalIgnoreCase);
    static readonly object gate=new object();
    static int VolatileRead(ref int value){return Interlocked.CompareExchange(ref value,0,0);}
    public static void Invalidate(){lock(gate)cache.Clear();}
    static bool ReadCache(string path,out Cached value){lock(gate){if(cache.TryGetValue(path.TrimEnd('\\'),out value)&&DateTime.UtcNow-value.Time<TimeSpan.FromMinutes(10))return true;value=null;return false;}}
    static void Save(string path,long size,int errors){lock(gate){if(cache.Count<150000)cache[path.TrimEnd('\\')]=new Cached{Bytes=size,Errors=errors,Time=DateTime.UtcNow};}}
    public static List<DiskBucket> Scan(string path,CancellationToken cancel,Action<long,long> progress,bool refresh){
        cancel.ThrowIfCancellationRequested();string full=Path.GetFullPath(path);if(!Directory.Exists(full))throw new DirectoryNotFoundException(full);
        if(refresh)Invalidate();
        List<DiskBucket> indexed;string reason;if(NtfsIndex.TryScan(full,cancel,progress,Save,out indexed,out reason))return indexed;
        var top=FastDisk.Entries(full).ToList();var result=new List<DiskBucket>();long files=0,bytes=0;object notify=new object();var watch=Stopwatch.StartNew();long lastNotify=0;
        Action<long> counted=size=>{long count=Interlocked.Increment(ref files);long total=Interlocked.Add(ref bytes,size);lock(notify){if(progress!=null&&watch.ElapsedMilliseconds-lastNotify>300){lastNotify=watch.ElapsedMilliseconds;progress(count,total);}}};
        foreach(var entry in top.Where(x=>!x.Directory&&!x.Link)){result.Add(new DiskBucket{Path=entry.Path,Bytes=entry.Bytes,Modified=entry.Modified});counted(entry.Bytes);}
        var folders=top.Where(x=>x.Directory&&!x.Link).ToArray();
        var records=new ConcurrentDictionary<string,Record>(StringComparer.OrdinalIgnoreCase);
        var work=new ConcurrentQueue<Record>();int outstanding=0,recordCount=0;
        foreach(var folder in folders){var record=new Record{Path=folder.Path,Depth=0};records[record.Path]=record;work.Enqueue(record);outstanding++;recordCount++;}
        Action consume=()=>{
            while(VolatileRead(ref outstanding)>0){
                cancel.ThrowIfCancellationRequested();Record record;
                if(!work.TryDequeue(out record)){Thread.Sleep(1);continue;}
                try{
                    Cached saved;
                    if(ReadCache(record.Path,out saved)){record.Bytes=saved.Bytes;record.Errors=saved.Errors;continue;}
                    foreach(var entry in FastDisk.Entries(record.Path)){
                        cancel.ThrowIfCancellationRequested();if(entry.Link)continue;
                        if(!entry.Directory){record.Bytes+=entry.Bytes;counted(entry.Bytes);continue;}
                        if(record.Depth>=512||Interlocked.Increment(ref recordCount)>300000){record.Errors++;continue;}
                        var child=new Record{Path=entry.Path,Parent=record.Path,Depth=record.Depth+1};records[child.Path]=child;
                        Interlocked.Increment(ref outstanding);work.Enqueue(child);
                    }
                }catch(OperationCanceledException){throw;}catch{record.Errors++;}finally{Interlocked.Decrement(ref outstanding);}
            }
        };
        int threads=Math.Min(4,Math.Max(2,Environment.ProcessorCount));
        var workers=Enumerable.Range(0,threads).Select(i=>Task.Factory.StartNew(consume,CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default)).ToArray();
        try{Task.WaitAll(workers);}catch(AggregateException ex){if(cancel.IsCancellationRequested)throw new OperationCanceledException(cancel);throw ex.Flatten();}
        cancel.ThrowIfCancellationRequested();
        foreach(var record in records.Values.OrderByDescending(x=>x.Depth)){
            Save(record.Path,record.Bytes,record.Errors);Record parent;
            if(record.Parent!=null&&records.TryGetValue(record.Parent,out parent)){parent.Bytes+=record.Bytes;parent.Errors+=record.Errors;}
        }
        foreach(var folder in folders){Record record;if(records.TryGetValue(folder.Path,out record))result.Add(new DiskBucket{Path=folder.Path,Bytes=record.Bytes,Errors=record.Errors,Folder=true,Modified=folder.Modified});}
        if(progress!=null)progress(files,bytes);
        return result.OrderByDescending(x=>x.Bytes).ToList();
    }
}
}
