namespace HakutaiKagami.Core.Contracts;

public static partial class NewGameWorld
{
    // Only immutable start inputs are cached. Every call serializes and compares all
    // input bytes, including nested arrays. Runtime relations, grants and location
    // are still checked against the projection on every commit and every load.
    // Private entries never expose arrays to callers. Four entries and 64 MiB of
    // start bytes bound retention; eviction affects speed, never world semantics.
    // 2026-11-12：全国の開始（約4,200人）でも記憶が効くよう 4 MiB から広げた。
    sealed record StartProjection(byte[] Input,string Version,string CharactersHash,string OrganizationsHash,
        (TimedRelation[] Members,TimedRelation[] Bonds,TimedRelation[] Offices,TimedRelation[] Lands) Relations,
        string OfficesHash,string LandsHash,string GrantsHash,
        Dictionary<string,(EntityId Appearance,LifeState Life)> People);
    static readonly object startProjectionGate=new();
    static readonly LinkedList<StartProjection> startProjections=new();
    const int ProjectionEntries=4,ProjectionBytes=64*1024*1024;
    // 開始の配置を検査し直した回数（この糸の中だけ。記憶が効いているかの検査用）。
    [ThreadStatic]internal static int StartVerifications;
    static StartProjection VerifiedStart(NewGameState data)
    {
        var bytes=ContractJson.Write(data);
        lock(startProjectionGate){var node=startProjections.First;while(node!=null){var next=node.Next;if(node.Value.Input.AsSpan().SequenceEqual(bytes)){startProjections.Remove(node);startProjections.AddFirst(node);return node.Value;}node=next;}}
        // Validate the exact private copy used to compile expectations. Failed
        // validation is never cached. Public data can be edited after this call.
        StartVerifications++;
        var stable=ContractJson.Read<NewGameState>(bytes);ValidateStart(stable);
        var rel=InitialRelations(stable);
        var value=new StartProjection(bytes,Version(stable),
            ContractJson.CanonicalHash(stable.Start.People.Select(p=>new DefinitionEntry(new(p.Id),p.Name)).ToArray()),
            ContractJson.CanonicalHash(stable.Start.Factions.Select(f=>new DefinitionEntry(new(f.Id),f.Name)).ToArray()),
            rel,ContractJson.CanonicalHash(rel.Offices),ContractJson.CanonicalHash(rel.Lands),ContractJson.CanonicalHash(Grants(stable)),
            stable.Start.People.ToDictionary(p=>p.Id,p=>(new EntityId("appearance:"+ContractJson.Hash(ContractJson.Write(Look(stable,p.Id)))),CanPlay(stable.Start,p,stable.AdultAge)?LifeState.Adult:LifeState.Minor)));
        if(bytes.Length<=ProjectionBytes)lock(startProjectionGate){startProjections.AddFirst(value);while(startProjections.Count>ProjectionEntries||startProjections.Sum(p=>p.Input.Length)>ProjectionBytes)startProjections.RemoveLast();}
        return value;
    }
}
