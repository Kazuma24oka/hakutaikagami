namespace HakutaiKagami.Core.Contracts;

// An opaque immutable capture of an actual session generation. No future world,
// result or display snapshot is public before a successful host publication.
public sealed class AdvanceCapture
{
    internal readonly WorldSession Source;
    internal readonly ContractWorld World;
    internal readonly int Budget;
    public long SourceRevision { get; }
    public long SourceSeconds { get; }
    public long Target { get; }
    internal AdvanceCapture(WorldSession source,ContractWorld world,long target,int budget)
    {Source=source;World=world;SourceRevision=world.CommitSequence;SourceSeconds=world.ElapsedSeconds;Target=target;Budget=budget;}
    public bool Matches(WorldSession session)=>ReferenceEquals(Source,session)&&session.MatchesAdvance(this);
    public PreparedAdvance Prepare(CancellationToken cancellation=default,Action? beforeFutureCommit=null)
    {
        cancellation.ThrowIfCancellationRequested();
        // WorldSession owns private copy-on-write generations: constructor takes a
        // deep copy; Change changes another deep copy and only then replaces state.
        // The captured generation stays private and is never edited by a worker.
        var future=new WorldSession(World);
        var result=future.AdvanceTo(Target,Budget,()=>{cancellation.ThrowIfCancellationRequested();beforeFutureCommit?.Invoke();cancellation.ThrowIfCancellationRequested();});
        var display=future.DiagnosticSnapshot();cancellation.ThrowIfCancellationRequested();
        return new(this,future,display,result,cancellation);
    }
    public Task<PreparedAdvance> PrepareAsync(CancellationToken cancellation=default)=>Task.Run(()=>Prepare(cancellation),cancellation);
}
public sealed class PreparedAdvance
{
    internal readonly object SyncRoot=new();
    internal readonly AdvanceCapture Capture;
    internal readonly WorldSession Future;
    internal readonly ContractWorld Display;
    internal readonly AdvanceResult Result;
    internal readonly CancellationToken Cancellation;
    internal bool Consumed;
    internal PreparedAdvance(AdvanceCapture capture,WorldSession future,ContractWorld display,AdvanceResult result,CancellationToken cancellation)
    {Capture=capture;Future=future;Display=display;Result=result;Cancellation=cancellation;}
}
public sealed record AdvancePublication(AdvanceResult Result,ContractWorld Display);
public sealed partial class WorldSession
{
    public AdvanceCapture CaptureAdvance(long target,long expectedRevision,int eventBudget=10000)
    {
        lock(gate){if(expectedRevision!=state.CommitSequence)throw new InvalidOperationException("Advance source changed");if(target<state.ElapsedSeconds||eventBudget<1)throw new ArgumentOutOfRangeException(nameof(target));return new(this,state,target,eventBudget);}
    }
    internal bool MatchesAdvance(AdvanceCapture capture)
    {lock(gate)return ReferenceEquals(capture.Source,this)&&ReferenceEquals(capture.World,state)&&capture.SourceRevision==state.CommitSequence&&capture.SourceSeconds==state.ElapsedSeconds;}
    internal bool TryPublishAdvance(AdvanceCapture capture,Action swap)
    {lock(gate){if(!ReferenceEquals(capture.Source,this)||!ReferenceEquals(capture.World,state)||capture.SourceRevision!=state.CommitSequence||capture.SourceSeconds!=state.ElapsedSeconds)return false;swap();return true;}}
}
public sealed partial class WorldSessionHost
{
    public ResultCode CommitAdvance(PreparedAdvance candidate,out AdvancePublication? publication)
    {
        publication=null;
        lock(gate)lock(candidate.SyncRoot)
        {
            if(candidate.Consumed||!ReferenceEquals(Current,candidate.Capture.Source))return ResultCode.StateChanged;
            if(candidate.Cancellation.IsCancellationRequested)return ResultCode.NotAvailable;
            if(!Current.TryPublishAdvance(candidate.Capture,()=>{Current=candidate.Future;candidate.Consumed=true;}))return ResultCode.StateChanged;
            // The independent display copy was built on the worker. Mutating it
            // cannot mutate the committed session or another generation.
            publication=new(candidate.Result,candidate.Display);return ResultCode.Accepted;
        }
    }
}
