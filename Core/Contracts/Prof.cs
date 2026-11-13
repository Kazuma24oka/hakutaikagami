namespace HakutaiKagami.Core.Contracts;
public static class Prof
{
    public static bool On=Environment.GetEnvironmentVariable("HAKUTAI_PROF")=="1";
    static readonly Dictionary<string,(long Ticks,long Count)> rows=new();
    public static void T(string name,Action a){if(!On){a();return;}long s=System.Diagnostics.Stopwatch.GetTimestamp();try{a();}finally{long e=System.Diagnostics.Stopwatch.GetTimestamp()-s;lock(rows){rows.TryGetValue(name,out var r);rows[name]=(r.Ticks+e,r.Count+1);}}}
    public static string Report(){lock(rows)return string.Join("\n",rows.OrderByDescending(r=>r.Value.Ticks).Take(60).Select(r=>$"{r.Value.Ticks*1000/System.Diagnostics.Stopwatch.Frequency,8}ms {r.Value.Count,7}  {r.Key}"));}
}
