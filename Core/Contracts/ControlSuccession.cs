using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

// 操作の引き継ぎ（BD-07、2026-11-12）。操作する人物が死ぬと世界を止め、本人が知っていた成人の実子・養子から一人を選んで続ける。存命のうちの隠居でも同じ候補へ移せる。
// 候補がなければ、その人生（年代記）を終える。操作の引き継ぎと家督は別で、家督・所領・兵・私財・記憶は移さない。
// 版（NewGameState.ControlPolicy）のある新規開始の世界だけに使う。旧保存は読み替えない。
public sealed record ControlHandover(EntityId From,EntityId To,long At,long Revision,string Reason)
{
    // 後見（Reason=guardian）：未成年の子（Ward）の代わりに操作する成人。子が元服したら、その子へ譲れる。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public EntityId? Ward {get;init;}
}

public static class ControlSuccession
{
    public const string Policy="control-handover-v1";
    public const string Death="death";
    public const string Retire="retire";
    public const string Guardian="guardian";
    public static bool Enabled(ContractWorld w)=>w.NewGame?.ControlPolicy==Policy;
    public static void ValidatePolicy(string? policy){if(policy is not(null or Policy))throw new InvalidDataException("操作の引き継ぎの版が不正です");}

    // その時点（版）で操作していた人物。引き継ぎの記録がなければ開始の人物。
    public static EntityId PlayerAtRevision(ContractWorld w,long revision)
    {
        var rows=w.ControlHandovers;if(rows==null||rows.Length==0)return w.PlayerControl;
        var player=rows[0].From;foreach(var h in rows)if(h.Revision<=revision)player=h.To;return player;
    }
    public static EntityId PlayerAtRevision(ContractWorld w,long? revision)=>PlayerAtRevision(w,revision??long.MaxValue);
    public static EntityId PlayerAtTime(ContractWorld w,long at)
    {
        var rows=w.ControlHandovers;if(rows==null||rows.Length==0)return w.PlayerControl;
        var player=rows[0].From;foreach(var h in rows)if(h.At<at)player=h.To;return player;
    }
    // これまでに操作した人物（開始の人物から順に）。
    public static EntityId[] Players(ContractWorld w){var rows=w.ControlHandovers;return rows==null||rows.Length==0?[w.CurrentPlayer()]:[rows[0].From,..rows.Select(h=>h.To)];}
    // 一度でも操作した人物（過去の記録の本人の判定に使う）。
    public static bool EverPlayed(ContractWorld w,EntityId person)=>person==w.PlayerControl||w.NewGame?.Selected==person||(w.ControlHandovers??[]).Any(h=>h.From==person||h.To==person);

    static bool Dead(ContractWorld w,EntityId person)=>w.Characters.SingleOrDefault(c=>c.Id==person)?.Life==LifeState.Dead;
    // 操作する人物が死んで、引き継ぎを待っている。
    public static bool Awaiting(ContractWorld w)=>Enabled(w)&&Dead(w,w.CurrentPlayer());

    // 亡くなった（隠居するなら本人の）本人が知っていた、生きている成人の実子・養子。後継に指名した人を先に、生年の順。
    // 成人の候補がなく、知っていた未成年の子がいれば、その子の後見になれる成人（子のもう一方の実の親、亡くなった本人の兄弟姉妹）で続けられる。
    public static EntityId[] Candidates(ContractWorld w)=>Awaiting(w)?(Known(w) is {Length:>0} adults?adults:Guardians(w).Select(g=>g.Proxy).Distinct().ToArray()):[];
    public static (EntityId Proxy,EntityId Ward)[] Guardians(ContractWorld w)
    {
        if(!Awaiting(w)||Known(w).Length>0)return [];
        var head=w.PlayerControl;var people=FamilyBirth.People(w);var life=w.Characters.ToDictionary(c=>c.Id.Value,c=>c.Life,StringComparer.Ordinal);
        var parents=people.SingleOrDefault(p=>p.Id==head.Value)?.BirthParents??[];
        var siblings=parents.Length==0?[]:people.Where(p=>p.Id!=head.Value&&(p.BirthParents??[]).Intersect(parents).Any()).Select(p=>p.Id).ToArray();
        var rows=new List<(EntityId,EntityId)>();
        foreach(var ward in Known(w,LifeState.Minor))
        {
            var other=(people.Single(p=>p.Id==ward.Value).BirthParents??[]).Where(x=>x!=head.Value);
            foreach(var proxy in other.Concat(siblings).Distinct().Where(x=>life.GetValueOrDefault(x)==LifeState.Adult).OrderBy(x=>x,StringComparer.Ordinal))rows.Add((new(proxy),ward));
        }
        return rows.ToArray();
    }
    // 存命のうちに隠居して、子へ操作を移す候補（行動中でないとき）。
    public static EntityId[] RetireCandidates(ContractWorld w)=>Enabled(w)&&!Awaiting(w)&&!w.Tasks.Any(t=>t.Executor==w.PlayerControl&&t.Execution is not(ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed))?[..Ward(w),..Known(w)]:[];
    // 後見している子が元服して生きていれば、その子（後見を終えて譲る）。
    static EntityId[] Ward(ContractWorld w)=>w.ControlHandovers?.LastOrDefault() is {Reason:Guardian,Ward:{} ward} last&&last.To==w.PlayerControl&&w.Characters.SingleOrDefault(c=>c.Id==ward)?.Life==LifeState.Adult?[ward]:[];
    static EntityId[] Known(ContractWorld w,LifeState state=LifeState.Adult)
    {
        var head=w.PlayerControl;var result=new List<(EntityId Person,bool Named,int Born)>();
        var named=(w.HeirNominations??[]).Where(n=>n.Head==head).OrderBy(n=>n.Revision).LastOrDefault()?.Heir;
        foreach(var child in FamilyBirth.People(w))
        {
            var id=new EntityId(child.Id);
            if(!(child.BirthParents??[]).Contains(head.Value))continue;
            var birth=w.Births?.SingleOrDefault(b=>b.Child==id);
            if(birth!=null&&!FamilyNotice.WitnessedBirth(w,birth,head)&&!w.Messaging.Messages.Any(m=>m.Kind==FamilyNotice.BirthKind&&m.CorrelationId==child.Id&&MessageFlow.Knows(w.Messaging,m.Id,head.Value)))continue;
            result.Add((id,id==named,child.BirthYear??int.MaxValue));
        }
        var entries=w.Adoption?.Entries??[];
        foreach(var entry in entries.Where(e=>e.Input.Action==AdoptionAction.Implement))
        {
            var terms=entries.Single(e=>e.Input.Id==entry.Input.Original).Terms!;
            if(terms.Parent!=head)continue;
            var person=FamilyBirth.People(w).Single(p=>p.Id==terms.Child.Value);
            result.Add((terms.Child,terms.Child==named,person.BirthYear??int.MaxValue));
        }
        return result.Where(c=>w.Characters.SingleOrDefault(x=>x.Id==c.Person)?.Life==state)
            .DistinctBy(c=>c.Person).OrderByDescending(c=>c.Named).ThenBy(c=>c.Born).ThenBy(c=>c.Person.Value,StringComparer.Ordinal).Select(c=>c.Person).ToArray();
    }
    // 引き継ぐ人がいないので、この人生は終わった。
    public static bool Ended(ContractWorld w)=>Awaiting(w)&&Candidates(w).Length==0;

    internal static void Apply(ContractWorld w,EntityId heir)
    {
        bool death=Awaiting(w);
        if(!(death?Candidates(w):RetireCandidates(w)).Contains(heir))throw new InvalidDataException("その人物には引き継げません");
        var ward=death&&Known(w).Length==0?Guardians(w).First(g=>g.Proxy==heir).Ward:(EntityId?)null;
        w.ControlHandovers=[..w.ControlHandovers??[],new(w.PlayerControl,heir,w.ElapsedSeconds,w.CommitSequence,ward!=null?Guardian:death?Death:Retire){Ward=ward}];
        w.PlayerControl=heir;
    }

    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        var rows=w.ControlHandovers;
        if(rows==null)return;
        if(!Enabled(w)&&!HandoverProbe.Running)throw new InvalidDataException("操作の引き継ぎは版のある世界だけで使えます");
        if(prior?.ControlHandovers is {} old&&!rows.Take(old.Length).SequenceEqual(old))throw new InvalidDataException("過去の操作の引き継ぎが変更されています");
        var player=w.NewGame!.Selected;long lastRevision=0,lastAt=0;
        foreach(var h in rows)
        {
            if(h.From!=player||h.To==h.From||h.Reason is not(Death or Retire or Guardian)||(h.Reason==Guardian)!=(h.Ward!=null)||h.Revision<=lastRevision||h.Revision>w.CommitSequence||h.At<lastAt||h.At>w.ElapsedSeconds||!w.Characters.Any(c=>c.Id==h.To)||h.Reason is Death or Guardian&&!Dead(w,h.From))
                throw new InvalidDataException("操作の引き継ぎの記録が不正です");
            player=h.To;lastRevision=h.Revision;lastAt=h.At;
        }
        if(w.CurrentPlayer()!=player)throw new InvalidDataException("操作する人物が引き継ぎの記録と一致しません");
    }
}

// 過去の記録を検査する間だけ、その記録の版を覚えておく。この間に読む操作する人物（PlayerControl）は、その版で操作していた人物になる。
// 引き継ぎの記録がない世界では何も変わらない。
public static class ControlContext
{
    [ThreadStatic] static long? revision;
    [ThreadStatic] static long? time;
    public static long? Revision=>revision;
    public static long? Time=>time;
    public static Scope At(long rev){var old=(revision,time);revision=rev;time=null;return new(old.revision,old.time);}
    // 版を持たない記録は、その時刻で操作していた人物にする。
    public static Scope AtTime(long at){var old=(revision,time);revision=null;time=at;return new(old.revision,old.time);}
    // 式の中だけで版を置く（ラムダの中の検査用）。
    public static T With<T>(long rev,Func<T> f){using var _=At(rev);return f();}
    // 何も変えない（入れ子の外側の版をそのまま残す）。
    public static Scope Keep()=>new(revision,time);
    public readonly struct Scope(long? oldRevision,long? oldTime):IDisposable{public void Dispose(){revision=oldRevision;time=oldTime;}}
}
