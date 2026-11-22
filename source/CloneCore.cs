using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
namespace Aurora {
public sealed class CloneProgress {public string Phase;public long Done,Total;public double Percent{get{return Total<=0?0:Math.Min(100,100.0*Done/Total);}}}
public sealed class CloneResult {public long Bytes;public string SourceHash;public bool Verified,GptRelocated;public TimeSpan Elapsed;}
public static class CloneCore {
    public static void ReadExact(Stream stream,byte[] buffer,int count){int read=0;while(read<count){int n=stream.Read(buffer,read,count-read);if(n==0)throw new EndOfStreamException("Source ended before the expected disk size");read+=n;}}
    public static uint Crc32(byte[] bytes,int offset,int count){uint crc=0xffffffff;for(int index=offset;index<offset+count;index++){crc^=bytes[index];for(int bit=0;bit<8;bit++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}return ~crc;}
    static void U32(byte[] data,int at,uint value){Buffer.BlockCopy(BitConverter.GetBytes(value),0,data,at,4);}
    static void U64(byte[] data,int at,ulong value){Buffer.BlockCopy(BitConverter.GetBytes(value),0,data,at,8);}
    static string Digest(SHA256 hash){return BitConverter.ToString(hash.Hash).Replace("-","");}
    public static CloneResult Copy(Stream source,Stream target,long length,long targetLength,int sector,int bufferMb,bool verify,bool relocateGpt,CancellationToken cancel,Action<CloneProgress> progress){
        if(source==null||target==null||Object.ReferenceEquals(source,target)||!source.CanRead||!target.CanWrite||!source.CanSeek||!target.CanSeek)throw new ArgumentException("Two separate seekable streams are required");
        if((sector!=512&&sector!=4096)||length<=0||length%sector!=0||targetLength<length||targetLength%sector!=0||bufferMb<1||bufferMb>64)throw new ArgumentException("Invalid disk size, sector format or buffer");
        if(verify&&!target.CanRead)throw new ArgumentException("Verification requires a readable target");
        var watch=Stopwatch.StartNew();byte[] buffer=new byte[bufferMb*1048576];long done=0;string expected;
        source.Position=0;target.Position=0;
        using(var sha=SHA256.Create()){
            while(done<length){cancel.ThrowIfCancellationRequested();int count=(int)Math.Min(buffer.Length,length-done);ReadExact(source,buffer,count);sha.TransformBlock(buffer,0,count,buffer,0);target.Write(buffer,0,count);done+=count;if(progress!=null)progress(new CloneProgress{Phase=L.T("Copiando setores","Copying sectors"),Done=done,Total=length});}
            sha.TransformFinalBlock(new byte[0],0,0);expected=Digest(sha);
        }
        target.Flush();cancel.ThrowIfCancellationRequested();
        if(verify){target.Position=0;done=0;using(var sha=SHA256.Create()){
            while(done<length){cancel.ThrowIfCancellationRequested();int count=(int)Math.Min(buffer.Length,length-done);ReadExact(target,buffer,count);sha.TransformBlock(buffer,0,count,buffer,0);done+=count;if(progress!=null)progress(new CloneProgress{Phase=L.T("Conferindo a cópia","Verifying copy"),Done=done,Total=length});}
            sha.TransformFinalBlock(new byte[0],0,0);if(Digest(sha)!=expected)throw new IOException(L.T("A cópia diverge da origem. O destino não foi aprovado.","The copy differs from its source. Target was not approved."));
        }}
        bool moved=relocateGpt&&targetLength>length&&RelocateGpt(target,length,targetLength,sector);target.Flush();
        return new CloneResult{Bytes=length,SourceHash=expected,Verified=verify,GptRelocated=moved,Elapsed=watch.Elapsed};
    }
    public static bool RelocateGpt(Stream disk,long sourceLength,long targetLength,int sector){
        if(targetLength<=sourceLength)return false;byte[] header=new byte[sector];disk.Position=sector;ReadExact(disk,header,sector);
        if(Encoding.ASCII.GetString(header,0,8)!="EFI PART")return false;
        uint headerSize=BitConverter.ToUInt32(header,12),storedCrc=BitConverter.ToUInt32(header,16);if(headerSize<92||headerSize>sector)throw new InvalidDataException("Invalid GPT header size");
        byte[] check=(byte[])header.Clone();U32(check,16,0);if(Crc32(check,0,(int)headerSize)!=storedCrc)throw new InvalidDataException("GPT header checksum is invalid");
        ulong current=BitConverter.ToUInt64(header,24),oldLast=BitConverter.ToUInt64(header,32),entriesLba=BitConverter.ToUInt64(header,72);uint count=BitConverter.ToUInt32(header,80),entrySize=BitConverter.ToUInt32(header,84);
        ulong arrayLength=(ulong)count*entrySize;if(current!=1||oldLast!=(ulong)(sourceLength/sector-1)||entriesLba<2||entrySize<128||arrayLength==0||arrayLength>16777216)throw new InvalidDataException("Unsupported GPT layout");
        int arraySectors=checked((int)((arrayLength+(ulong)sector-1)/(ulong)sector));long arrayOffset=checked((long)entriesLba*sector);if(arrayOffset<sector*2L||arrayOffset+(long)arrayLength>sourceLength)throw new InvalidDataException("GPT entry array lies outside the source");
        byte[] entries=new byte[arraySectors*sector];disk.Position=arrayOffset;ReadExact(disk,entries,entries.Length);if(Crc32(entries,0,(int)arrayLength)!=BitConverter.ToUInt32(header,88))throw new InvalidDataException("GPT partition checksum is invalid");
        ulong last=(ulong)(targetLength/sector-1),backupEntries=last-(ulong)arraySectors,newUsable=backupEntries-1;
        if(newUsable<BitConverter.ToUInt64(header,48))throw new InvalidDataException("Destination has insufficient GPT space");
        U64(header,32,last);U64(header,48,newUsable);U32(header,16,0);U32(header,16,Crc32(header,0,(int)headerSize));
        byte[] backup=(byte[])header.Clone();U64(backup,24,last);U64(backup,32,1);U64(backup,72,backupEntries);U32(backup,16,0);U32(backup,16,Crc32(backup,0,(int)headerSize));
        disk.Position=checked((long)backupEntries*sector);disk.Write(entries,0,entries.Length);disk.Position=checked((long)last*sector);disk.Write(backup,0,backup.Length);disk.Flush();
        disk.Position=sector;disk.Write(header,0,header.Length);
        byte[] mbr=new byte[sector];disk.Position=0;ReadExact(disk,mbr,sector);for(int part=0;part<4;part++){int at=446+part*16;if(mbr[at+4]==0xee){U32(mbr,at+12,(uint)Math.Min(UInt32.MaxValue,last));disk.Position=0;disk.Write(mbr,0,mbr.Length);break;}}
        disk.Flush();byte[] actual=new byte[sector];disk.Position=sector;ReadExact(disk,actual,sector);for(int i=0;i<sector;i++)if(actual[i]!=header[i])throw new IOException("Primary GPT readback mismatch");disk.Position=checked((long)last*sector);ReadExact(disk,actual,sector);for(int i=0;i<sector;i++)if(actual[i]!=backup[i])throw new IOException("Backup GPT readback mismatch");byte[] arrayCheck=new byte[entries.Length];disk.Position=checked((long)backupEntries*sector);ReadExact(disk,arrayCheck,arrayCheck.Length);for(int i=0;i<entries.Length;i++)if(arrayCheck[i]!=entries[i])throw new IOException("Backup GPT entry readback mismatch");
        return true;
    }
}
}
