namespace HakutaiKagami.Core.Contracts;

// Captures one real immutable session generation and an immutable value-only Intent.
// No private future result or world is public before host acceptance.
public sealed class SubmitCapture
{
    internal readonly WorldSession Source;
    internal readonly ContractWorld World;
    internal readonly Intent? Input;
    internal readonly ArmyRoadInput? ArmyInput;
    internal readonly bool UnpauseAfterAcceptance;
    public long SourceRevision {get;}
    public long SourceSeconds {get;}
    internal SubmitCapture(WorldSession source,ContractWorld world,Intent input,bool unpause)
    {Source=source;World=world;Input=input;UnpauseAfterAcceptance=unpause;SourceRevision=world.CommitSequence;SourceSeconds=world.ElapsedSeconds;}
    internal SubmitCapture(WorldSession source,ContractWorld world,ArmyRoadInput input,bool unpause)
    {Source=source;World=world;ArmyInput=input;UnpauseAfterAcceptance=unpause;SourceRevision=world.CommitSequence;SourceSeconds=world.ElapsedSeconds;}
    public bool Matches(WorldSession session)=>ReferenceEquals(Source,session)&&session.MatchesSubmit(this);
    public PreparedSubmit Prepare(CancellationToken cancellation=default,Action? beforePublication=null)
    {
        cancellation.ThrowIfCancellationRequested();
        var future=new WorldSession(World);
        var result=ArmyInput is {} army?future.ManageArmyRoad(army,future.PlayerControl,SourceRevision):future.Submit(Input!,SourceRevision);
        if(UnpauseAfterAcceptance&&(result.Code is ResultCode.Accepted or ResultCode.AlreadyAccepted))future.SetPaused(false);
        cancellation.ThrowIfCancellationRequested();beforePublication?.Invoke();cancellation.ThrowIfCancellationRequested();
        var display=future.DiagnosticSnapshot();cancellation.ThrowIfCancellationRequested();
        return new(this,future,display,result,cancellation);
    }
    public Task<PreparedSubmit> PrepareAsync(CancellationToken cancellation=default)=>Task.Run(()=>Prepare(cancellation),cancellation);
}
public sealed class PreparedSubmit
{
    internal readonly object SyncRoot=new();
    internal readonly SubmitCapture Capture;
    internal readonly WorldSession Future;
    internal readonly ContractWorld Display;
    internal readonly CommandResult Result;
    internal readonly CancellationToken Cancellation;
    internal bool Consumed;
    internal PreparedSubmit(SubmitCapture capture,WorldSession future,ContractWorld display,CommandResult result,CancellationToken cancellation)
    {Capture=capture;Future=future;Display=display;Result=result;Cancellation=cancellation;}
}
public sealed record SubmitPublication(CommandResult Result,ContractWorld Display);
public sealed partial class WorldSession
{
    public SubmitCapture CaptureSubmit(Intent input,long expectedRevision,bool unpauseAfterAcceptance=false)
    {
        ArgumentNullException.ThrowIfNull(input);
        lock(gate){if(expectedRevision!=state.CommitSequence)throw new InvalidOperationException("Submit source changed");return new(this,state,input,unpauseAfterAcceptance);}
    }
    public SubmitCapture CaptureArmyRoad(ArmyRoadInput input,long expectedRevision,bool unpauseAfterAcceptance=false)
    {ArgumentNullException.ThrowIfNull(input);lock(gate){if(expectedRevision!=state.CommitSequence)throw new InvalidOperationException("Army march source changed");return new(this,state,input,unpauseAfterAcceptance);}}
    internal bool MatchesSubmit(SubmitCapture capture)
    {lock(gate)return ReferenceEquals(capture.Source,this)&&ReferenceEquals(capture.World,state)&&capture.SourceRevision==state.CommitSequence&&capture.SourceSeconds==state.ElapsedSeconds;}
    internal bool TryPublishSubmit(SubmitCapture capture,Action swap)
    {lock(gate){if(!ReferenceEquals(capture.Source,this)||!ReferenceEquals(capture.World,state)||capture.SourceRevision!=state.CommitSequence||capture.SourceSeconds!=state.ElapsedSeconds)return false;swap();return true;}}
}
public sealed partial class WorldSessionHost
{
    public ResultCode CommitSubmit(PreparedSubmit candidate,out SubmitPublication? publication)
    {
        publication=null;
        lock(gate)lock(candidate.SyncRoot)
        {
            if(candidate.Consumed||!ReferenceEquals(Current,candidate.Capture.Source))return ResultCode.StateChanged;
            if(candidate.Cancellation.IsCancellationRequested)return ResultCode.NotAvailable;
            if(!Current.TryPublishSubmit(candidate.Capture,()=>{Current=candidate.Future;candidate.Consumed=true;}))return ResultCode.StateChanged;
            publication=new(candidate.Result,candidate.Display);return ResultCode.Accepted;
        }
    }
}
