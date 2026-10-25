namespace HakutaiKagami.Core.Contracts;

// A clock-control request captures a private immutable generation, not a public
// diagnostic copy. Preparation performs exactly the existing SetPaused operation.
public sealed class PauseCapture
{
    internal readonly WorldSession Source;
    internal readonly ContractWorld World;
    public bool DesiredPause {get;}
    public long SourceRevision {get;}
    public long SourceSeconds {get;}
    internal PauseCapture(WorldSession source,ContractWorld world,bool desired)
    {Source=source;World=world;DesiredPause=desired;SourceRevision=world.CommitSequence;SourceSeconds=world.ElapsedSeconds;}
    public bool Matches(WorldSession session)=>ReferenceEquals(Source,session)&&session.MatchesPause(this);
    public PreparedPause Prepare(CancellationToken cancellation=default,Action? beforePreparation=null)
    {
        cancellation.ThrowIfCancellationRequested();beforePreparation?.Invoke();cancellation.ThrowIfCancellationRequested();
        var future=new WorldSession(World);future.SetPaused(DesiredPause);
        var display=future.DiagnosticSnapshot();cancellation.ThrowIfCancellationRequested();
        return new(this,future,display,cancellation);
    }
    public Task<PreparedPause> PrepareAsync(CancellationToken cancellation=default)=>Task.Run(()=>Prepare(cancellation),cancellation);
}
public sealed class PreparedPause
{
    internal readonly object SyncRoot=new();
    internal readonly PauseCapture Capture;
    internal readonly WorldSession Future;
    internal readonly ContractWorld Display;
    internal readonly CancellationToken Cancellation;
    internal bool Consumed;
    internal PreparedPause(PauseCapture capture,WorldSession future,ContractWorld display,CancellationToken cancellation)
    {Capture=capture;Future=future;Display=display;Cancellation=cancellation;}
}
public sealed partial class WorldSession
{
    public PauseCapture CapturePause(bool paused,long expectedRevision)
    {lock(gate){if(expectedRevision!=state.CommitSequence)throw new InvalidOperationException("Clock source changed");return new(this,state,paused);}}
    internal bool MatchesPause(PauseCapture capture)
    {lock(gate)return ReferenceEquals(capture.Source,this)&&ReferenceEquals(capture.World,state)&&capture.SourceRevision==state.CommitSequence&&capture.SourceSeconds==state.ElapsedSeconds;}
    internal bool TryPublishPause(PauseCapture capture,Action swap)
    {lock(gate){if(!ReferenceEquals(capture.Source,this)||!ReferenceEquals(capture.World,state)||capture.SourceRevision!=state.CommitSequence||capture.SourceSeconds!=state.ElapsedSeconds)return false;swap();return true;}}
}
public sealed partial class WorldSessionHost
{
    public ResultCode CommitPause(PreparedPause candidate,out ContractWorld? display)
    {
        display=null;
        lock(gate)lock(candidate.SyncRoot){
            if(candidate.Consumed||!ReferenceEquals(Current,candidate.Capture.Source))return ResultCode.StateChanged;
            if(candidate.Cancellation.IsCancellationRequested)return ResultCode.NotAvailable;
            if(!Current.TryPublishPause(candidate.Capture,()=>{Current=candidate.Future;candidate.Consumed=true;}))return ResultCode.StateChanged;
            display=candidate.Display;return ResultCode.Accepted;
        }
    }
}
