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
