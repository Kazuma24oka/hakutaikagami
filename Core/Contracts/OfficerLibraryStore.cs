namespace HakutaiKagami.Core.Contracts;

// Fixed-file local library store; caller chooses its dedicated directory, never a world-save slot.
public sealed class OfficerLibraryStore
{
    readonly string directory;
    public OfficerLibraryStore(string directory) => this.directory=Path.GetFullPath(directory);
    string FilePath=>Path.Combine(directory,"officers.json");
    public OfficerLibrary Load()
    {
        using var stream=new FileStream(FilePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(stream.Length is <1 or >8388608)throw new InvalidDataException("武将ライブラリの容量が不正です");
        var bytes=new byte[(int)stream.Length];stream.ReadExactly(bytes);
        var result=ContractJson.Read<OfficerLibrary>(bytes);NewGameSetup.Validate(result);return result;
    }
    // null means create only. A stale editor must reload instead of overwriting another edit.
    public string Save(OfficerLibrary library,string? expectedHash)
    {
        NewGameSetup.Validate(library);var bytes=ContractJson.Write(library);
        if(bytes.Length>8388608)throw new InvalidDataException("武将ライブラリの容量が大きすぎます");
        Directory.CreateDirectory(directory);
        using var gate=new FileStream(Path.Combine(directory,"officers.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        bool exists=File.Exists(FilePath);
        if(exists?expectedHash==null || ContractJson.CanonicalHash(Load())!=expectedHash:expectedHash!=null)
            throw new InvalidDataException("保存前にライブラリが変更されています。読み直してください");
        string temporary=Path.Combine(directory,"officers-"+Guid.NewGuid().ToString("N")+".tmp");
        try{
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}
            File.Move(temporary,FilePath,true);
        }finally{if(File.Exists(temporary))File.Delete(temporary);}
        return ContractJson.CanonicalHash(library);
    }
    public static OfficerLibrary Import(OfficerLibrary current,OfficerLibrary incoming)
    {
        NewGameSetup.Validate(current);NewGameSetup.Validate(incoming);
        var merged=new OfficerLibrary(1,current.Ungrouped.Concat(incoming.Ungrouped).ToArray(),current.Groups.Concat(incoming.Groups).ToArray());
        NewGameSetup.Validate(merged);return ContractJson.Copy(merged);
    }
}
