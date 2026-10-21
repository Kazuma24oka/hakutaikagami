using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace HakutaiKagami.Core.Contracts;

public enum SaveStage { SnapshotCaptured, PayloadWritten, GenerationVerified, PreviousPublished, CurrentPublished }
public sealed class ContractLoadException(ResultCode code,string message,Exception? inner=null) : IOException(message,inner)
{ public ResultCode Code { get; } = code; }
public sealed record SaveWorldCamera(long X,long Y,double Span,double Yaw,bool Follow,bool Improved);
public sealed record SavePresentation(double DisplayFraction, int DisplaySpeed, [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? ScenerySkin=null,[property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] SaveWorldCamera? WorldCamera=null,[property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] bool? CouncilOpen=null);
public sealed record SaveManifest(string Format,int EnvelopeVersion,EntityId WorldId,string GenerationId,long CommitSequence,
    string TimeDomain,Dictionary<string,int> SchemaVersions,string Ruleset,string RulesHash,string DefinitionHash,
    string PayloadHash,long PayloadByteLength,string PresentationHash);
public sealed record GenerationReference(string GenerationId,string ManifestHash,EntityId WorldId,long CommitSequence);
public sealed record SaveResult(string GenerationId,long CapturedRevision,bool Published);
public sealed record RecoveryCandidate(string GenerationId,long? Seconds,bool Valid,ResultCode? Error);

public sealed class PreparedLoad
{
    internal object SyncRoot { get; }=new();
    internal ContractWorld World { get; }
    internal GenerationSaveStore Store { get; }
    internal string Slot { get; }
    internal string PayloadHash { get; }
    internal string ManifestHash { get; }
    internal string PresentationHash { get; }
    internal bool Consumed { get; set; }
    public string GenerationId { get; }
    public SavePresentation Presentation { get; }
    public long Seconds => World.ElapsedSeconds;
    internal PreparedLoad(ContractWorld world,GenerationSaveStore store,string slot,string generation,string payloadHash,
        string manifestHash,string presentationHash,SavePresentation presentation)
    {World=world;Store=store;Slot=slot;GenerationId=generation;PayloadHash=payloadHash;ManifestHash=manifestHash;PresentationHash=presentationHash;Presentation=presentation;}
}

// A bounded, explicit local store. No legacy save file or arbitrary user path is touched.
public sealed class GenerationSaveStore
{
    public const long MaximumPayloadBytes=256L*1024*1024;
    public const string Format="game-world-save";
    private readonly string root;
    private readonly string expectedDefinitionHash;
    private readonly string expectedRules;
    private static readonly ConcurrentDictionary<string,object> Gates=new(StringComparer.OrdinalIgnoreCase);
    private object Gate=>Gates.GetOrAdd(root,_=>new());
    private static readonly Regex SlotPattern=new("^[a-z][a-z0-9-]{0,63}$",RegexOptions.CultureInvariant);
    private static readonly Regex GenerationPattern=new("^g-[0-9]+-[0-9]+$",RegexOptions.CultureInvariant);
    public GenerationSaveStore(string rootPath,DefinitionPack expected)
    {
        root=Path.GetFullPath(rootPath);
        if(root.StartsWith(@"\\",StringComparison.Ordinal))throw new ArgumentException("Local filesystem required.");
        expectedDefinitionHash=ContractJson.CanonicalHash(expected);expectedRules=expected.Ruleset;
        CheckNoLinks(root);
    }
    private static string RulesHash(string rules)=>ContractJson.CanonicalHash(new[]{rules,"common-core-schema-v1","lcg64-v1"});
    private static void CheckNoLinks(string path)
    {
        for(var d=new DirectoryInfo(path);d!=null;d=d.Parent)
            if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new ContractLoadException(ResultCode.NotAvailable,"Reparse-point save path is not allowed.");
        if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new ContractLoadException(ResultCode.NotAvailable,"Reparse-point file is not allowed.");
    }
    private string SlotPath(string slot)
    {
        if(!SlotPattern.IsMatch(slot))throw new ArgumentException("Invalid slot ID.");
        var result=Path.GetFullPath(Path.Combine(root,slot));
        if(!result.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Outside save root.");
        CheckNoLinks(result);return result;
    }
    private string GenerationPath(string slot,string generation)
    {
        if(!GenerationPattern.IsMatch(generation))throw new ArgumentException("Invalid generation ID.");
        var result=Path.Combine(SlotPath(slot),generation);CheckNoLinks(result);return result;
    }
    private static byte[] ReadBounded(string path,long maximum)
    {
        CheckNoLinks(path);
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(stream.Length<1||stream.Length>maximum)throw new InvalidDataException("Save size outside limits.");
        var bytes=new byte[checked((int)stream.Length)];stream.ReadExactly(bytes);
        if(stream.ReadByte()!=-1)throw new InvalidDataException("File changed while reading.");return bytes;
    }
    private static void DurableWrite(string path,byte[] bytes)
    {
        CheckNoLinks(path);
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough);
        stream.Write(bytes);stream.Flush(flushToDisk:true);
    }
    private static void PublishReference(string path,GenerationReference reference)
    {
        CheckNoLinks(path);var tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        DurableWrite(tmp,ContractJson.Write(reference));
        // Same-volume replace; old immutable generation remains usable even if reference publication fails.
        File.Move(tmp,path,overwrite:true);
    }
    private static GenerationReference? ReadReference(string directory,string name)
    {
        var p=Path.Combine(directory,name+".ref");
        return File.Exists(p)?ContractJson.Read<GenerationReference>(ReadBounded(p,16384)):null;
    }
    public SaveResult Save(WorldSession session,string slot,EntityId expectedWorld,SavePresentation? presentation=null,
        Action<SaveStage>? fault=null)
    {
        var snapshot=session.DiagnosticSnapshot();
        if(snapshot.WorldId!=expectedWorld)throw new ContractLoadException(ResultCode.StateChanged,"World changed before capture.");
        ContractValidation.Validate(snapshot);VerifyDefinitions(snapshot.Definitions);
        var display=presentation??new SavePresentation(0,1);ValidatePresentation(display);
        fault?.Invoke(SaveStage.SnapshotCaptured);
        lock(Gate)
        {
            var dir=SlotPath(slot);Directory.CreateDirectory(dir);CheckNoLinks(dir);
            using var writerLock=new FileStream(Path.Combine(dir,"writer.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            var old=ReadReference(dir,"current");
            if(old!=null&&old.WorldId!=snapshot.WorldId)throw new ContractLoadException(ResultCode.StateChanged,"Slot belongs to a different world.");
            if(old!=null&&old.CommitSequence>snapshot.CommitSequence)return new(old.GenerationId,snapshot.CommitSequence,false);
            long saveNumber=Directory.EnumerateDirectories(dir,"g-*").Select(Path.GetFileName).Where(n=>n!=null&&GenerationPattern.IsMatch(n))
                .Select(n=>long.Parse(n!.Split('-')[2],System.Globalization.CultureInfo.InvariantCulture)).DefaultIfEmpty(0).Max();
            var generation="g-"+snapshot.CommitSequence.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+checked(saveNumber+1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var dest=GenerationPath(slot,generation);Directory.CreateDirectory(dest);CheckNoLinks(dest);
            var payload=ContractJson.Write(snapshot);var displayBytes=ContractJson.Write(display);
            if(payload.LongLength>MaximumPayloadBytes)throw new ContractLoadException(ResultCode.IoFailure,"Payload exceeds configured limit.");
            DurableWrite(Path.Combine(dest,"payload.json"),payload);DurableWrite(Path.Combine(dest,"presentation.json"),displayBytes);
            fault?.Invoke(SaveStage.PayloadWritten);
            var manifest=new SaveManifest(Format,1,snapshot.WorldId,generation,snapshot.CommitSequence,snapshot.TimeDomain,
                new(StringComparer.Ordinal){{"World",snapshot.Schema},{"Messaging",1},{"Resources",1}},expectedRules,RulesHash(expectedRules),expectedDefinitionHash,
                ContractJson.Hash(payload),payload.LongLength,ContractJson.Hash(displayBytes));
            var manifestBytes=ContractJson.Write(manifest);DurableWrite(Path.Combine(dest,"manifest.json"),manifestBytes);
            PrepareCore(slot,generation,null);fault?.Invoke(SaveStage.GenerationVerified);
            // Only promote the old pointer to backup if its manifest and full payload still validate.
            if(old!=null){PrepareCore(slot,old.GenerationId,old);PublishReference(Path.Combine(dir,"previous.ref"),old);}
            fault?.Invoke(SaveStage.PreviousPublished);
            PublishReference(Path.Combine(dir,"current.ref"),new(generation,ContractJson.Hash(manifestBytes),snapshot.WorldId,snapshot.CommitSequence));
            fault?.Invoke(SaveStage.CurrentPublished);
            return new(generation,snapshot.CommitSequence,true);
        }
    }
    public Task<SaveResult> SaveAsync(WorldSession session,string slot,EntityId expectedWorld,SavePresentation? presentation=null,
        Action<SaveStage>? fault=null,CancellationToken cancellationToken=default)
    {
        // Capture before returning the job; later gameplay cannot alter this generation's source revision.
        var captured=new WorldSession(session.DiagnosticSnapshot());
        return Task.Run(()=>Save(captured,slot,expectedWorld,presentation,fault),cancellationToken);
    }
    // Initial implementation keeps extra generations. Pruning is deliberately not enabled before crash tests.
    public PreparedLoad Prepare(string slot,string? generation=null)
    {
        lock(Gate)
        {
            try
            {
                var dir=SlotPath(slot);var reference=generation==null?ReadReference(dir,"current"):null;
                var id=generation??reference?.GenerationId??throw new InvalidDataException("No current save.");
                return PrepareCore(slot,id,reference);
            }
            catch(ContractLoadException){throw;}
            catch(Exception e) when(e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException or OverflowException)
            {throw new ContractLoadException(ResultCode.CorruptSave,"Save could not be validated; active session unchanged.",e);}
        }
    }
    private PreparedLoad PrepareCore(string slot,string generation,GenerationReference? reference)
    {
        var dir=GenerationPath(slot,generation);var manifestBytes=ReadBounded(Path.Combine(dir,"manifest.json"),1024*1024);
        var manifest=ContractJson.Read<SaveManifest>(manifestBytes);
        if(manifest.SchemaVersions==null)throw new InvalidDataException("Missing module version map.");
        if(manifest.Format!=Format||manifest.EnvelopeVersion!=1)throw new ContractLoadException(ResultCode.UnsupportedVersion,"Unsupported save envelope.");
        if(manifest.GenerationId!=generation||manifest.TimeDomain!="GameSecondsV1"||manifest.CommitSequence<0||manifest.PayloadByteLength<1||manifest.PayloadByteLength>MaximumPayloadBytes||
            !ContractValidation.IsHash(manifest.PayloadHash)||!ContractValidation.IsHash(manifest.PresentationHash))throw new InvalidDataException("Invalid manifest.");
        if(manifest.SchemaVersions.Count!=3||manifest.SchemaVersions.GetValueOrDefault("World")!=(expectedRules==BranchRoutes.Ruleset||expectedRules==MovementControl.Ruleset||expectedRules==RegionalBranch.Ruleset||expectedRules==RegionalControl.Ruleset||expectedRules==RegionalCampaign.Ruleset||expectedRules==RiverRoads.Ruleset||expectedRules==HistoricalRoads.Ruleset||expectedRules==NpcWork.Ruleset||expectedRules==RegionalWork.Ruleset||expectedRules==AssessmentNotice.Ruleset||expectedRules==MissionReview.Ruleset||expectedRules==IncomingMission.Ruleset||expectedRules==MissionEntry.Ruleset||expectedRules==OfficerLoop.Ruleset||expectedRules==ContinuousWorld.Ruleset||expectedRules==RegionalScenery.Ruleset||expectedRules==RegionalBattle.Ruleset||expectedRules==FieldWorld.Ruleset||expectedRules==TrafficWorld.Ruleset||expectedRules==TravelControl.Ruleset||expectedRules==TravelWorld.Ruleset||expectedRules==NationalRoutes.Ruleset?2:1)||!new[]{"Messaging","Resources"}.All(k=>manifest.SchemaVersions.GetValueOrDefault(k)==1))
            throw new ContractLoadException(ResultCode.UnsupportedVersion,"Unsupported module schema.");
        if(manifest.DefinitionHash!=expectedDefinitionHash||manifest.Ruleset!=expectedRules||manifest.RulesHash!=RulesHash(expectedRules))
            throw new ContractLoadException(ResultCode.DependencyMismatch,"Definition or rules dependency differs.");
        if(reference!=null&&(reference.ManifestHash!=ContractJson.Hash(manifestBytes)||reference.WorldId!=manifest.WorldId||reference.CommitSequence!=manifest.CommitSequence))
            throw new InvalidDataException("Reference/manifest mismatch.");
        var payload=ReadBounded(Path.Combine(dir,"payload.json"),MaximumPayloadBytes);
        var displayBytes=ReadBounded(Path.Combine(dir,"presentation.json"),16384);
        if(payload.LongLength!=manifest.PayloadByteLength||ContractJson.Hash(payload)!=manifest.PayloadHash||ContractJson.Hash(displayBytes)!=manifest.PresentationHash)
            throw new InvalidDataException("Payload/presentation hash or length mismatch.");
        var world=ReadPayload(payload);
        if(world.Schema!=manifest.SchemaVersions["World"])throw new InvalidDataException("Payload/manifest version mismatch");
        ContractValidation.Validate(world);VerifyDefinitions(world.Definitions);
        if(world.WorldId!=manifest.WorldId||world.CommitSequence!=manifest.CommitSequence||world.TimeDomain!=manifest.TimeDomain)throw new InvalidDataException("Mixed save generation.");
        var display=ContractJson.Read<SavePresentation>(displayBytes);ValidatePresentation(display);
        return new(world,this,slot,generation,manifest.PayloadHash,ContractJson.Hash(manifestBytes),manifest.PresentationHash,display);
    }
    public static ContractWorld ReadPayload(byte[] bytes)
    {
        // Clear migration response rather than treating a known legacy format as corrupt product data.
        try
        {
            using var doc=System.Text.Json.JsonDocument.Parse(bytes);
            if(doc.RootElement.ValueKind!=System.Text.Json.JsonValueKind.Object)throw new InvalidDataException("Save root must be an object.");
            if(doc.RootElement.TryGetProperty("Version",out var v)&&v.TryGetInt32(out int n)&&n is >=1 and <=5)
                throw new ContractLoadException(ResultCode.MigrationRequired,"Legacy tick save: use its original entry point. No migration recipe registered.");
        }
        catch(System.Text.Json.JsonException e){throw new InvalidDataException("Malformed save JSON.",e);}
        return ContractJson.Read<ContractWorld>(bytes);
    }
    internal void Revalidate(PreparedLoad candidate)
    {
        var now=Prepare(candidate.Slot,candidate.GenerationId);
        if(now.PayloadHash!=candidate.PayloadHash||now.ManifestHash!=candidate.ManifestHash||now.PresentationHash!=candidate.PresentationHash)
            throw new ContractLoadException(ResultCode.StateChanged,"Prepared save changed; prepare again.");
    }
    private void VerifyDefinitions(DefinitionPack definition)
    {
        if(definition.Ruleset!=expectedRules||ContractJson.CanonicalHash(definition)!=expectedDefinitionHash)
            throw new ContractLoadException(ResultCode.DependencyMismatch,"Definition/rules mismatch.");
    }
    private static void ValidatePresentation(SavePresentation p)
    {
        if(p.WorldCamera is {} c&&(!double.IsFinite(c.Span)||c.Span<40||c.Span>4000000||!double.IsFinite(c.Yaw)||Math.Abs(c.Yaw)>360000||Math.Abs(c.X)>4000000000L||Math.Abs(c.Y)>4000000000L))throw new InvalidDataException("Invalid world camera");
        if(p.ScenerySkin!=null&&!Regex.IsMatch(p.ScenerySkin,"^skin:[a-z0-9_-]{1,64}$"))throw new InvalidDataException("Invalid scenery skin ID");
        if(!double.IsFinite(p.DisplayFraction)||p.DisplayFraction<0||p.DisplayFraction>=1||!new[]{1,10,60,360}.Contains(p.DisplaySpeed))throw new InvalidDataException("Invalid presentation phase/speed.");
    }
    public RecoveryCandidate[] RecoveryOptions(string slot)
    {
        var dir=SlotPath(slot);if(!Directory.Exists(dir))return [];
        return Directory.EnumerateDirectories(dir,"g-*").Select(Path.GetFileName).Where(n=>n!=null&&GenerationPattern.IsMatch(n)).Select(n=>{
            try{var c=Prepare(slot,n!);return new RecoveryCandidate(n!,c.Seconds,true,null);}
            catch(ContractLoadException e){return new RecoveryCandidate(n!,null,false,e.Code);}
        }).OrderByDescending(c=>c.Seconds).ThenBy(c=>c.GenerationId,StringComparer.Ordinal).ToArray();
    }
}

public sealed class WorldSessionHost
{
    private readonly object gate=new();
    public WorldSession Current { get; private set; }
    public WorldSessionHost(WorldSession session)=>Current=session;
    public ResultCode CommitLoad(PreparedLoad candidate,WorldSession expectedSession,long expectedRevision)
    {
        lock(gate)
        lock(candidate.SyncRoot)
        {
            if(candidate.Consumed||!ReferenceEquals(Current,expectedSession)||Current.Revision!=expectedRevision)return ResultCode.StateChanged;
            candidate.Store.Revalidate(candidate);
            var state=ContractJson.Copy(candidate.World);state.Paused=true;
            var restored=new WorldSession(state);
            return Current.TryHandoff(expectedRevision,()=>{Current=restored;candidate.Consumed=true;})?ResultCode.Accepted:ResultCode.StateChanged;
        }
    }
}
