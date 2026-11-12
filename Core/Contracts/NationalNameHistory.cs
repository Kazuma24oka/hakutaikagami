using System.Text;
using System.Collections.Concurrent;

namespace HakutaiKagami.Core.Contracts;

public sealed record NationalNameChange(EntityId Id,EntityId Person,string PreviousName,string Name,int Year,string Step,string Basis,long At);
public sealed record NationalNameRecognition(EntityId Change,EntityId Observer,long LearnedAt,EntityId Site,long Revision);
public sealed record NationalNameHistoryState(int Schema,string Policy,string Edition,NationalNameChange[] Changes,NationalNameRecognition[] Recognitions);

public static class NationalNameHistory
{
    public const string Policy="national-name-history-v1";
    static readonly string BasisPrefix="hakutai-db-1560-v2:"+HakutaiDb.Source.Commit+":NameHistory";
    static readonly ConcurrentDictionary<string,Candidate[]> cache=new(StringComparer.Ordinal);
    sealed record Candidate(EntityId Id,EntityId Person,string Name,int Year,string Step,string Basis,long At);

    public static bool Enabled(ContractWorld w)=>w.NewGame?.NameHistoryPolicy==Policy;
    public static NationalNameHistoryState Initial()=>new(1,Policy,NationalStart.Edition,[],[]);

    static Candidate[] Candidates(ContractWorld w)
    {
        if(!Enabled(w)||w.NewGame is not{} game)return [];
        return cache.GetOrAdd(w.Definitions.Version,_=>BuildCandidates(w,game));
    }

    static Candidate[] BuildCandidates(ContractWorld w,NewGameState game)
    {
        var result=new List<Candidate>();
        foreach(var start in game.Start.People.OrderBy(p=>p.Id,StringComparer.Ordinal))
        {
            var db=NationalStart.DbPerson(start.Id);if(db==null)continue;
            var current=start.Name;
            var years=db.NameHistory.Where(n=>n.Step!="幼名"&&n.From is int year&&year>HakutaiDb.WorldYear&&n.Name!=start.Name&&
                (db.Death is null||year<db.Death)).GroupBy(n=>n.From!.Value).OrderBy(g=>g.Key);
            foreach(var group in years)
            {
                var names=group.Select(n=>n.Name).Distinct(StringComparer.Ordinal).ToArray();
                if(names.Length!=1)continue;
                var entry=group.OrderBy(n=>n.Step,StringComparer.Ordinal).First();
                if(entry.Name==current)continue;
                long at=SecondsAtYear(w,group.Key);
                string basis=$"{BasisPrefix}:{db.Id}";
                string key=$"{start.Id}\n{group.Key}\n{entry.Name}\n{entry.Step}";
                var id=new EntityId("national-name:"+ContractJson.Hash(Encoding.UTF8.GetBytes(key))[..24]);
                result.Add(new(id,new(start.Id),entry.Name,group.Key,entry.Step,basis,at));
                current=entry.Name;
            }
        }
        return result.OrderBy(c=>c.At).ThenBy(c=>c.Person.Value,StringComparer.Ordinal).ThenBy(c=>c.Id.Value,StringComparer.Ordinal).ToArray();
    }

    static long SecondsAtYear(ContractWorld w,int year)
    {
        var start=DateOnly.ParseExact(w.EpochDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
        var target=new DateOnly(year,1,1);
        return (target.ToDateTime(TimeOnly.MinValue)-start.ToDateTime(TimeOnly.MinValue)).Ticks/TimeSpan.TicksPerSecond;
    }

    internal static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w)||w.NationalNames is not{Schema:1,Policy:Policy} state)return null;
        var applied=state.Changes.Select(c=>c.Id).ToHashSet();
        return Candidates(w).Where(c=>!applied.Contains(c.Id)&&c.At>w.ElapsedSeconds&&c.At<=target)
            .Select(c=>(long?)c.At).FirstOrDefault();
    }

    internal static int BoundaryCost(ContractWorld w,long at)
    {
        if(!Enabled(w)||w.NationalNames is not{Schema:1,Policy:Policy} state)return 0;
        var applied=state.Changes.Select(c=>c.Id).ToHashSet();
        return Candidates(w).Count(c=>c.At==at&&!applied.Contains(c.Id));
    }

    internal static void ApplyDue(ContractWorld w)
    {
        if(!Enabled(w)||w.NationalNames is not{Schema:1,Policy:Policy} state)return;
        var applied=state.Changes.Select(c=>c.Id).ToHashSet();
        var names=state.Changes.GroupBy(c=>c.Person).ToDictionary(g=>g.Key,g=>g.OrderBy(c=>c.At).Last().Name);
        var due=Candidates(w).Where(c=>c.At<=w.ElapsedSeconds&&!applied.Contains(c.Id)).ToArray();
        if(due.Length==0)return;
        var added=new List<NationalNameChange>();
        foreach(var c in due)
        {
            string previous=names.GetValueOrDefault(c.Person,w.Definitions.Characters.Single(x=>x.Id==c.Person).Name);
            if(previous==c.Name)continue;
            added.Add(new(c.Id,c.Person,previous,c.Name,c.Year,c.Step,c.Basis,c.At));
            names[c.Person]=c.Name;
        }
        if(added.Count>0)w.NationalNames=state with{Changes=[..state.Changes,..added]};
    }

    // A changed name becomes known through the person's own knowledge or a direct local encounter.
    // This is called inside each committed world change, after current locations have been updated.
    internal static void ObserveLocal(ContractWorld w)
    {
        if(!Enabled(w)||w.NationalNames is not{Schema:1,Policy:Policy} state)return;
        var ownSite=SocialWorld.Site(w,w.PlayerControl);if(ownSite==null)return;
        var recognized=state.Recognitions.Select(r=>(r.Change,r.Observer)).ToHashSet();
        var rows=state.Recognitions.ToList();
        foreach(var change in state.Changes.Where(c=>c.At<=w.ElapsedSeconds).OrderBy(c=>c.At).ThenBy(c=>c.Id.Value,StringComparer.Ordinal))
        {
            if(change.Person==w.PlayerControl||recognized.Contains((change.Id,w.PlayerControl))||SocialWorld.Site(w,change.Person)!=ownSite)continue;
            rows.Add(new(change.Id,w.PlayerControl,w.ElapsedSeconds,ownSite.Value,w.CommitSequence));
            recognized.Add((change.Id,w.PlayerControl));
        }
        if(rows.Count!=state.Recognitions.Length)w.NationalNames=state with{Recognitions=rows.ToArray()};
    }

    public static string DisplayName(ContractWorld w,EntityId observer,EntityId subject)
    {
        var originalEntry=w.Definitions.Characters.SingleOrDefault(c=>c.Id==subject);
        if(originalEntry==null)return "";
        var original=originalEntry.Name;
        if(!Enabled(w)||w.NationalNames is not{Schema:1,Policy:Policy} state||
            !w.Characters.Any(c=>c.Id==observer)||!w.Characters.Any(c=>c.Id==subject)||
            !w.NewGame!.Start.People.Any(p=>p.Id==subject.Value))return original;
        var known=state.Recognitions.Where(r=>r.Observer==observer&&r.LearnedAt<=w.ElapsedSeconds).Select(r=>r.Change).ToHashSet();
        return state.Changes.Where(c=>c.Person==subject&&c.At<=w.ElapsedSeconds&&(observer==subject||known.Contains(c.Id))).OrderBy(c=>c.At).LastOrDefault()?.Name??original;
    }

    public static void Validate(ContractWorld w,bool intermediate=false)
    {
        void Need(bool ok,string reason){if(!ok)throw new InvalidDataException("全国の名乗り時系列："+reason);}
        if(!Enabled(w)){Need(w.NationalNames==null,"対象外または旧版の世界に名乗り時系列を混在できません");return;}
        Need(w.NewGame!.HistoricalCatalog==NationalStart.Edition&&w.NewGame.DistrictControlPolicy==NationalDistrictControl.Policy&&w.NewGame.Start.EpochDate=="1560-01-01","新規の1560年全国開始だけで使用できます");
        Need(w.NationalNames is{Schema:1,Policy:Policy,Edition:NationalStart.Edition,Changes:not null,Recognitions:not null},"台帳の版が違います");
        var state=w.NationalNames!;var changes=state.Changes!;var recognitions=state.Recognitions!;var candidates=Candidates(w).ToDictionary(c=>c.Id);
        Need(changes.Select(c=>c.Id).Distinct().Count()==changes.Length,"改名が重複しています");
        Need(changes.Zip(changes.Skip(1)).All(p=>p.First.At<=p.Second.At),"改名の時刻が逆転しています");
        var names=w.Definitions.Characters.ToDictionary(c=>c.Id,c=>c.Name);
        foreach(var change in changes)
        {
            Need(candidates.TryGetValue(change.Id,out var c)&&c.Person==change.Person&&c.Name==change.Name&&c.Year==change.Year&&c.Step==change.Step&&c.Basis==change.Basis&&c.At==change.At,"DBにない改名記録です");
            Need(change.At<=w.ElapsedSeconds&&change.At==SecondsAtYear(w,change.Year),"未来または年境界外の改名です");
            Need(names.TryGetValue(change.Person,out var previous)&&previous==change.PreviousName,"改名前後の人物名が一致しません");
            names[change.Person]=change.Name;
        }
        var applied=changes.Select(c=>c.Id).ToHashSet();
        Need(Candidates(w).Where(c=>c.At<=w.ElapsedSeconds).All(c=>applied.Contains(c.Id)),"到来済みの改名が台帳に反映されていません");
        Need(recognitions.Select(r=>(r.Change,r.Observer)).Distinct().Count()==recognitions.Length,"名乗りの認識記録が重複しています");
        Need(recognitions.All(r=>applied.Contains(r.Change)&&w.Characters.Any(c=>c.Id==r.Observer)&&r.LearnedAt>=changes.Single(c=>c.Id==r.Change).At&&r.LearnedAt<=w.ElapsedSeconds&&r.Revision>0&&r.Revision<=w.CommitSequence&&w.Definitions.Sites.Any(s=>s.Id==r.Site)),"本人が現地で得ていない名乗り情報です");
    }
}
