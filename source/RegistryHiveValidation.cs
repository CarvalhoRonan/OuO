using System;
using System.IO;
using System.Text;
namespace Aurora {
public static class RegistryHiveValidation {
    // Structural checks run before the separate native SAM editor. No hive is
    // mounted in the live Registry; RegLoadAppKey cannot validate mixed-ACL SAMs.
    public static void Validate(byte[] data){
        if(data==null||data.Length<8192||data.Length>67108864||Encoding.ASCII.GetString(data,0,4)!="regf")throw new InvalidDataException("Invalid registry hive header");
        if(BitConverter.ToUInt32(data,4)!=BitConverter.ToUInt32(data,8))throw new InvalidDataException(L.T("O Registro tem transações pendentes. Inicie e desligue completamente o Windows de destino antes de continuar.","Registry transactions are pending. Boot and fully shut down target Windows before continuing."));
        if(BitConverter.ToUInt32(data,20)!=1||BitConverter.ToUInt32(data,24)<3||BitConverter.ToUInt32(data,24)>6||BitConverter.ToUInt32(data,28)!=0||BitConverter.ToUInt32(data,32)!=1)throw new InvalidDataException("Unsupported registry hive format");
        uint checksum=0;for(int i=0;i<508;i+=4)checksum^=BitConverter.ToUInt32(data,i);uint stored=BitConverter.ToUInt32(data,508),normalized=checksum==0?1:checksum==UInt32.MaxValue?UInt32.MaxValue-1:checksum;if(stored!=checksum&&stored!=normalized)throw new InvalidDataException("Registry hive checksum mismatch");
        long length=BitConverter.ToUInt32(data,40);if(length<4096||length%4096!=0||length+4096!=data.Length)throw new InvalidDataException("Registry hive bin length mismatch");
        long root=4096L+BitConverter.ToUInt32(data,36);bool rootFound=false;int bins=4096,cells=0;
        while(bins<data.Length){if(data.Length-bins<32||Encoding.ASCII.GetString(data,bins,4)!="hbin"||BitConverter.ToUInt32(data,bins+4)!=(uint)(bins-4096))throw new InvalidDataException("Registry hive bin header mismatch");uint binLength=BitConverter.ToUInt32(data,bins+8);if(binLength<4096||binLength%4096!=0||binLength>data.Length-bins)throw new InvalidDataException("Registry hive bin outside file");int end=bins+(int)binLength,at=bins+32;
            while(at<end){if(end-at<4||++cells>1000000)throw new InvalidDataException("Registry hive cell limit");int signed=BitConverter.ToInt32(data,at);if(signed==Int32.MinValue)throw new InvalidDataException("Registry hive cell size");int size=Math.Abs(signed);if(size<8||(size&7)!=0||size>end-at)throw new InvalidDataException("Registry hive cell outside bin");if(at==root){if(signed>=0||size<80||data[at+4]!='n'||data[at+5]!='k')throw new InvalidDataException("Registry hive root is not an allocated key");rootFound=true;}at+=size;}
            bins=end;
        }
        if(!rootFound)throw new InvalidDataException("Registry hive root was not found");
    }
}
}
