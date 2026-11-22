using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
namespace Aurora {
public sealed class NtfsRun {public long Cluster,Clusters;}
public sealed class NtfsRecord {
    public ulong Reference,Parent,Base;public string Name="";public bool Directory,Link,HasData,AttributeList;public long Bytes;public DateTime Modified;public List<NtfsRun> Runs;
}
// NTFS v3 parser. Every offset is bounded before use. The volume is opened READ ONLY.
public static class NtfsMetadata {
    const ulong Mask=0x0000FFFFFFFFFFFFUL;
    static void Need(byte[] data,int offset,int count,int limit){if(offset<0||count<0||offset>limit-count||limit>data.Length)throw new InvalidDataException("NTFS record bounds");}
    static ushort U16(byte[] b,int n){Need(b,n,2,b.Length);return BitConverter.ToUInt16(b,n);}
    static uint U32(byte[] b,int n){Need(b,n,4,b.Length);return BitConverter.ToUInt32(b,n);}
    static ulong U64(byte[] b,int n){Need(b,n,8,b.Length);return BitConverter.ToUInt64(b,n);}
    static long I64(byte[] b,int n){Need(b,n,8,b.Length);return BitConverter.ToInt64(b,n);}
    public static List<NtfsRun> Runs(byte[] data,int offset,int limit){
        var runs=new List<NtfsRun>();long lcn=0;bool terminated=false;
        while(offset<limit){int header=data[offset++];if(header==0){terminated=true;break;}int lengthBytes=header&15,offsetBytes=header>>4;if(lengthBytes<1||lengthBytes>8||offsetBytes<1||offsetBytes>8)throw new InvalidDataException("Sparse or invalid MFT run");Need(data,offset,lengthBytes+offsetBytes,limit);
            ulong length=0,delta=0;for(int i=0;i<lengthBytes;i++)length|=(ulong)data[offset+i]<<(8*i);offset+=lengthBytes;
            for(int i=0;i<offsetBytes;i++)delta|=(ulong)data[offset+i]<<(8*i);if(offsetBytes<8&&(data[offset+offsetBytes-1]&128)!=0)delta|=UInt64.MaxValue<<(offsetBytes*8);offset+=offsetBytes;
            if(length==0||length>Int64.MaxValue)throw new InvalidDataException("Invalid MFT extent length");lcn=checked(lcn+unchecked((long)delta));if(lcn<0)throw new InvalidDataException("Negative MFT extent");runs.Add(new NtfsRun{Cluster=lcn,Clusters=(long)length});
        }
        if(!terminated||runs.Count==0)throw new InvalidDataException("Unterminated MFT runs");return runs;
    }
    public static NtfsRecord Parse(byte[] raw,int sectorBytes,long ordinal,bool restoreSectors){
        if(raw.Length<512||raw.Length>65536||raw[0]!='F'||raw[1]!='I'||raw[2]!='L'||raw[3]!='E')return null;
        int flags=U16(raw,22);if((flags&1)==0)return null;byte[] b=raw;
        if(restoreSectors){b=(byte[])raw.Clone();int start=U16(b,4),count=U16(b,6);if(sectorBytes<512||raw.Length%sectorBytes!=0||count!=raw.Length/sectorBytes+1)throw new InvalidDataException("NTFS sector fixup count");Need(b,start,count*2,b.Length);ushort sequence=U16(b,start);
            for(int i=1;i<count;i++){int tail=i*sectorBytes-2;if(U16(b,tail)!=sequence)throw new InvalidDataException("NTFS record changed during read");b[tail]=b[start+i*2];b[tail+1]=b[start+i*2+1];}}
        int used=checked((int)U32(b,24)),position=U16(b,20);if(used<48||used>b.Length||position<42||position>=used)throw new InvalidDataException("NTFS attribute header");
        var item=new NtfsRecord{Reference=((ulong)U16(b,16)<<48)|((ulong)ordinal&Mask),Base=U64(b,32),Directory=(flags&2)!=0};int bestName=-1;bool ended=false;
        while(position<=used-4){uint type=U32(b,position);if(type==0xFFFFFFFF){ended=true;break;}Need(b,position,16,used);int length=checked((int)U32(b,position+4));if(length<24||(length&7)!=0)throw new InvalidDataException("NTFS attribute size");Need(b,position,length,used);int form=b[position+8],named=b[position+9];if(form>1)throw new InvalidDataException("NTFS attribute form");
            if(type==0x20)item.AttributeList=true;if(type==0xC0)item.Link=true;
            if(form==0){int size=checked((int)U32(b,position+16)),value=U16(b,position+20);if(value<24)throw new InvalidDataException("NTFS resident offset");Need(b,position+value,size,position+length);
                if(type==0x30){int at=position+value;Need(b,at,66,at+size);int chars=b[at+64],ns=b[at+65];Need(b,at+66,chars*2,at+size);int rank=ns==1||ns==3?3:ns==0?2:0;
                    if(rank>bestName){string name=Encoding.Unicode.GetString(b,at+66,chars*2);if(name.IndexOfAny(new[]{'\\','/','\0',':'})>=0||(name=="."&&!(ordinal==5&&item.Directory))||name=="..")throw new InvalidDataException("Invalid NTFS file name");item.Name=name;item.Parent=U64(b,at);item.Link|=(U32(b,at+56)&1024)!=0;try{item.Modified=DateTime.FromFileTimeUtc(I64(b,at+16)).ToLocalTime();}catch{item.Modified=DateTime.MinValue;}bestName=rank;}}
                if(type==0x80&&named==0){item.Bytes=size;item.HasData=true;}
            }else if(type==0x80&&named==0){Need(b,position,64,position+length);long first=I64(b,position+16);if(first==0){item.Bytes=I64(b,position+48);if(item.Bytes<0)throw new InvalidDataException("Negative NTFS file size");item.HasData=true;
                    if(ordinal==0&&item.Base==0){if((U16(b,position+12)&0xC001)!=0)throw new InvalidDataException("Unsupported MFT stream");int runOffset=U16(b,position+32);if(runOffset<64)throw new InvalidDataException("MFT mapping offset");item.Runs=Runs(b,position+runOffset,position+length);}}}
            position+=length;
        }
        if(!ended)throw new InvalidDataException("NTFS attributes missing end marker");return item;
    }
}
public static class NtfsIndex {
    sealed class Folder {public NtfsRecord Item;public string Path;public long Bytes;public int Depth;public bool Resolving;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool DeviceIoControl(SafeFileHandle h,uint code,byte[] input,int inputSize,byte[] output,int outputSize,out int returned,IntPtr overlapped);
    static void ReadExact(FileStream stream,byte[] data,int count){int done=0;while(done<count){int read=stream.Read(data,done,count-done);if(read==0)throw new EndOfStreamException("NTFS volume read");done+=read;}}
    static void Pass(FileStream volume,List<NtfsRun> runs,int clusterBytes,int recordBytes,int sectorBytes,long total,CancellationToken token,Action<long,NtfsRecord> accept){
        long ordinal=0,left=total;byte[] block=new byte[Math.Max(recordBytes,1048576/recordBytes*recordBytes)],record=new byte[recordBytes];
        foreach(var run in runs){if(left<=0)break;long length=Math.Min(left,checked(run.Clusters*clusterBytes));if(length%recordBytes!=0)throw new InvalidDataException("Unaligned MFT extent");volume.Position=checked(run.Cluster*clusterBytes);
            while(length>0){token.ThrowIfCancellationRequested();int count=(int)Math.Min(block.Length,length);ReadExact(volume,block,count);for(int at=0;at<count;at+=recordBytes){Buffer.BlockCopy(block,at,record,0,recordBytes);accept(ordinal,NtfsMetadata.Parse(record,sectorBytes,ordinal,true));ordinal++;}length-=count;left-=count;}}
        if(left!=0)throw new InvalidDataException("Incomplete MFT stream");
    }
    static string Resolve(Folder folder,Dictionary<ulong,Folder> folders,string root,int depth=0){
        if(folder.Path!=null)return folder.Path;if(depth>=512)throw new InvalidDataException("NTFS path depth");if(folder.Resolving)throw new InvalidDataException("NTFS parent cycle");folder.Resolving=true;Folder parent;
        if(!folders.TryGetValue(folder.Item.Parent,out parent))throw new InvalidDataException("NTFS parent no longer exists");string path=Resolve(parent,folders,root,depth+1);folder.Depth=parent.Depth+1;if(folder.Depth>512)throw new InvalidDataException("NTFS path depth");folder.Path=path.TrimEnd('\\')+"\\"+folder.Item.Name;folder.Resolving=false;return folder.Path;
    }
    public static bool TryScan(string root,CancellationToken token,Action<long,long> progress,Action<string,long,int> save,out List<DiskBucket> result,out string reason){
        result=null;reason="";string normalized=Path.GetFullPath(root);if(normalized.Length!=3||normalized[1]!=':'||normalized[2]!='\\'){reason="folder";return false;}
        try{if(new DriveInfo(root).DriveFormat!="NTFS"){reason="filesystem";return false;}using(var handle=CreateFile(@"\\.\"+normalized.Substring(0,2),0x80000000,7,IntPtr.Zero,3,0,IntPtr.Zero)){
            if(handle.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());byte[] info=new byte[128];int returned;
            if(!DeviceIoControl(handle,0x90064,null,0,info,info.Length,out returned,IntPtr.Zero))throw new Win32Exception(Marshal.GetLastWin32Error());
            if(returned<104||BitConverter.ToUInt16(info,100)!=3)throw new InvalidDataException("Unsupported NTFS version");
            int sector=BitConverter.ToInt32(info,40),cluster=BitConverter.ToInt32(info,44),record=BitConverter.ToInt32(info,48);long valid=BitConverter.ToInt64(info,56),start=BitConverter.ToInt64(info,64);
            if(sector<512||sector>4096||cluster<sector||cluster>2097152||record<512||record>65536||record%sector!=0||valid<record||valid%record!=0||valid/record>3000000||start<0)throw new InvalidDataException("Unsupported MFT geometry");
            using(var volume=new FileStream(handle,FileAccess.Read,1048576,false)){
                volume.Position=checked(start*cluster);byte[] first=new byte[record];ReadExact(volume,first,record);var mft=NtfsMetadata.Parse(first,sector,0,true);if(mft==null||mft.Runs==null||mft.Bytes<valid||mft.Runs.Sum(x=>checked(x.Clusters*cluster))<valid)throw new InvalidDataException("MFT extents require normal scan");
                var folders=new Dictionary<ulong,Folder>();var extensions=new Dictionary<ulong,NtfsRecord>();long count=0,bytes=0;var watch=Stopwatch.StartNew();long notification=0;
                Pass(volume,mft.Runs,cluster,record,sector,valid,token,(ordinal,item)=>{
                    if(item==null)return;if(item.Base!=0){NtfsRecord ext;if(!extensions.TryGetValue(item.Base,out ext)){extensions[item.Base]=item;return;}if(item.HasData){ext.Bytes=item.Bytes;ext.HasData=true;}if(ext.Name.Length==0){ext.Name=item.Name;ext.Parent=item.Parent;}return;}
                    if(item.Directory)folders[item.Reference]=new Folder{Item=item};if(progress!=null&&watch.ElapsedMilliseconds-notification>400){notification=watch.ElapsedMilliseconds;progress(0,0);}
                });
                foreach(var pair in folders){NtfsRecord ext;if(extensions.TryGetValue(pair.Key,out ext)&&pair.Value.Item.Name.Length==0){pair.Value.Item.Name=ext.Name;pair.Value.Item.Parent=ext.Parent;}}
                var rootFolder=folders.Values.SingleOrDefault(x=>(x.Item.Reference&0x0000FFFFFFFFFFFFUL)==5);if(rootFolder==null)throw new InvalidDataException("NTFS root missing");rootFolder.Path=normalized;
                // Reparse folders and their descendants are excluded, even though MFT enumeration can see them.
                var blocked=new HashSet<ulong>();Func<Folder,bool> accessible=null;accessible=folder=>{if(folder==rootFolder)return true;if(blocked.Contains(folder.Item.Reference))return false;if(folder.Item.Link){blocked.Add(folder.Item.Reference);return false;}try{Resolve(folder,folders,normalized);}catch{blocked.Add(folder.Item.Reference);return false;}Folder parent;if(!folders.TryGetValue(folder.Item.Parent,out parent)||!accessible(parent)){blocked.Add(folder.Item.Reference);return false;}return true;};
                foreach(var folder in folders.Values.OrderBy(x=>x.Item.Reference))accessible(folder);
                var top=new List<DiskBucket>();Pass(volume,mft.Runs,cluster,record,sector,valid,token,(ordinal,item)=>{
                    if(item==null||item.Base!=0||item.Directory||item.Link||ordinal<16)return;NtfsRecord ext;if(extensions.TryGetValue(item.Reference,out ext)){if(!item.HasData&&ext.HasData){item.Bytes=ext.Bytes;item.HasData=true;}if(item.Name.Length==0){item.Name=ext.Name;item.Parent=ext.Parent;}}
                    Folder parent;if(item.Name.Length==0||!folders.TryGetValue(item.Parent,out parent)||blocked.Contains(item.Parent))return;
                    if(!item.HasData&&item.AttributeList)throw new InvalidDataException("NTFS data attribute unresolved");parent.Bytes=checked(parent.Bytes+item.Bytes);count++;bytes=checked(bytes+item.Bytes);
                    if(parent==rootFolder)top.Add(new DiskBucket{Path=normalized+item.Name,Bytes=item.Bytes,Modified=item.Modified});
                    if(progress!=null&&watch.ElapsedMilliseconds-notification>300){notification=watch.ElapsedMilliseconds;progress(count,bytes);}
                });
                foreach(var folder in folders.Values.Where(x=>x!=rootFolder&&!blocked.Contains(x.Item.Reference)).OrderByDescending(x=>x.Depth)){
                    if(save!=null)save(folder.Path,folder.Bytes,0);Folder parent;if(folders.TryGetValue(folder.Item.Parent,out parent))parent.Bytes=checked(parent.Bytes+folder.Bytes);
                    if(folder.Item.Parent==rootFolder.Item.Reference)top.Add(new DiskBucket{Path=folder.Path,Bytes=folder.Bytes,Folder=true,Modified=folder.Item.Modified});
                }
                result=top.OrderByDescending(x=>x.Bytes).ToList();if(progress!=null)progress(count,bytes);return true;
            }
        }}catch(OperationCanceledException){throw;}catch(Exception error){reason=error.Message;return false;}
    }
}
}
