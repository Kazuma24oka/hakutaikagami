using System.Collections.Concurrent;
using System.Diagnostics;

namespace HakutaiKagami.Core.Contracts;

// 調査用：検査の中で操作する人物（PlayerControl）を読んだ場所を集める。検査の外では何もしない。
public static class ControlProbe
{
    public static bool Enabled;
    [ThreadStatic] static int depth;
    internal static bool Active=>Enabled&&depth>0;
    static readonly ConcurrentDictionary<(IntPtr,int),int> hits=new();
    static readonly ConcurrentDictionary<(IntPtr,int),string> places=new();
    internal static void Enter(){if(Enabled)depth++;}
    internal static void Exit(){if(Enabled)depth--;}
    internal static void Hit()
    {
        var f=new StackFrame(2,false);var m=f.GetMethod();if(m==null)return;
        var key=(m.MethodHandle.Value,f.GetILOffset());
        if(!places.ContainsKey(key)){var g=new StackFrame(2,true);places.TryAdd(key,$"{System.IO.Path.GetFileName(g.GetFileName())}:{g.GetFileLineNumber()}:{g.GetFileColumnNumber()}|{m.DeclaringType?.FullName}.{m.Name}");}
        hits.AddOrUpdate(key,1,(_,v)=>v+1);
    }
    public static IEnumerable<string> Hits()=>hits.Select(x=>places[x.Key]+"|"+x.Value);
}

// 調査用：検査に通った新規ゲームの世界ごとに、別の成人へ隠居で操作を移した写しも検査し、通らなかった理由と場所を集める。
public static class HandoverProbe
{
    public static bool Enabled;
    [ThreadStatic] static bool running;
    internal static bool Running=>running;
    static readonly ConcurrentDictionary<string,int> failures=new();
    internal static void After(ContractWorld s)
    {
        if(!Enabled||running||s.NewGame==null||s.ControlHandovers!=null)return;
        var heir=s.Characters.Where(c=>c.Id!=s.PlayerControl&&c.Life==LifeState.Adult).Select(c=>c.Id).OrderBy(x=>x.Value,StringComparer.Ordinal).FirstOrDefault();
        if(heir.Value==null)return;
        running=true;
        try
        {
            var v=ContractJson.Copy(s);v.CommitSequence++;
            v.ControlHandovers=[new(v.PlayerControl,heir,v.ElapsedSeconds,v.CommitSequence,ControlSuccession.Retire)];v.PlayerControl=heir;
            ContractValidation.Validate(v);
        }
        catch(Exception e)
        {
            var inner=e;while(inner.InnerException!=null)inner=inner.InnerException;
            var frames=new StackTrace(inner,true).GetFrames().Where(f=>f.GetFileName()!=null&&f.GetMethod()?.Name is not("Need" or "Check")).Take(5)
                .Select(f=>$"{System.IO.Path.GetFileName(f.GetFileName())}:{f.GetFileLineNumber()}:{f.GetMethod()?.Name}");
            failures.AddOrUpdate($"{string.Join(" < ",frames)}|{inner.Message}",1,(_,n)=>n+1);
        }
        finally{running=false;}
    }
    public static IEnumerable<string> Failures()=>failures.Select(x=>x.Key+"|"+x.Value);
}
