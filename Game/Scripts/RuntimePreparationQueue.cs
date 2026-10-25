using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// Pure managed scheduler: never reads a Godot node or a live session.
internal sealed class RuntimePreparationQueue
{
    readonly SemaphoreSlim lease=new(1,1);
    readonly object sync=new();
    readonly List<Task> jobs=[];
    int active,maximum,waiting,started,closing,disposed;
    internal int Active=>Volatile.Read(ref active);
    internal int Maximum=>Volatile.Read(ref maximum);
    internal int Waiting=>Volatile.Read(ref waiting);
    internal int Started=>Volatile.Read(ref started);
    internal bool IsDisposed=>Volatile.Read(ref disposed)!=0;
    internal Task<T> Run<T>(Func<T> operation,CancellationToken token)
    {
        lock(sync){
            if(closing!=0)throw new ObjectDisposedException(nameof(RuntimePreparationQueue));
            jobs.RemoveAll(t=>{if(!t.IsCompleted)return false;_=t.Exception;return true;});
            var task=Task.Run(async()=>{
                Interlocked.Increment(ref waiting);
                try{await lease.WaitAsync(token).ConfigureAwait(false);}finally{Interlocked.Decrement(ref waiting);}
                int concurrent=Interlocked.Increment(ref active);Interlocked.Increment(ref started);
                int observed;while((observed=Volatile.Read(ref maximum))<concurrent&&Interlocked.CompareExchange(ref maximum,concurrent,observed)!=observed){}
                try{token.ThrowIfCancellationRequested();return operation();}
                finally{Interlocked.Decrement(ref active);lease.Release();}
            });
            jobs.Add(task);return task;
        }
    }
    internal void CloseWhenIdle()
    {
        lock(sync){
            if(closing!=0)return;closing=1;var all=Task.WhenAll(jobs.ToArray());jobs.Clear();
            _=all.ContinueWith(t=>{_=t.Exception;lease.Dispose();Volatile.Write(ref disposed,1);},CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
        }
    }
}
public partial class NewGameRuntime
{
    readonly RuntimePreparationQueue preparations=new();
    internal int PreparationActive=>preparations.Active;
    internal int PreparationMaximum=>preparations.Maximum;
    internal int PreparationWaiting=>preparations.Waiting;
    internal int PreparationStarted=>preparations.Started;
    internal bool PreparationDisposed=>preparations.IsDisposed;
}
