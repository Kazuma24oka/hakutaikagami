using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// 全国の家の滅亡と再興（BD-46・統合案 Z4／6d、2026-11-14）。全国の開始の一覧（StrategyRestoration）を持つ世界だけ。旧保存は変えない。
// 滅亡：月の初めの決着で最後の城を失った家を、その城と落とした家とともに記録する（家の人物・所属・私財は動かさない）。
// 再興：滅んだ家の当主（家督を継いだ者を含む）が生きていて拘束されておらず、旧城を持つ家の支配が揺らいだ（1年以内に当主が代わった、3か月以内に戦に負けた）とき、
//   当主は月に30%で旧臣を集めて旧城へ兵を挙げる（兵は旧の兵の上限の3分の1）。決着は通常の出陣と同じ。勝てば旧城を取り戻し、家は城を持つ家に戻る。
// 自動では起こさない：挙兵は滅んだ家の当主の判断（くじ）で、その家に生きた当主がいなければ起きない。
public static class StrategyRestoration
{
    public static bool Enabled(ContractWorld w)=>NationalSystems.On(w,"StrategyRestoration");
    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"strategy-restoration-v1/{w.Seed}/{key}")))%1000);
    const long Day=86400;
    internal static long Strength(string house)=>Math.Max(50L,NationalStrategy.Cap(house)/3);
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}

    // 決着の後、城を持っていたのに一つも持たなくなった家を記録する。
    internal static StrategyState RecordFalls(ContractWorld w,StrategyState before,StrategyState after)
    {
        if(!Enabled(w))return after;
        var o0=NationalStrategy.Owners(w,before);var o1=NationalStrategy.Owners(w,after);
        var added=after.Outcomes.Skip(before.Outcomes.Length).Where(o=>o.Captured).Select(o=>after.Campaigns.Single(c=>c.Id==o.Campaign)).ToArray();
        var falls=(after.Falls??[]).ToList();
        foreach(var house in o0.Values.OfType<string>().Distinct(StringComparer.Ordinal).OrderBy(h=>h,StringComparer.Ordinal))
        {
            if(o1.ContainsValue(house))continue;
            var lost=added.Where(c=>c.Defender.Value==house).OrderBy(c=>c.Target.Value,StringComparer.Ordinal).LastOrDefault();
            if(lost==null)continue;
            falls.Add(new(new(house),lost.Target,lost.Attacker,w.ElapsedSeconds,w.CommitSequence));
        }
        return after with{Falls=falls.Count==0?null:falls.ToArray()};
    }
    static bool Shaken(StrategyState s,string holder,long now)=>StrategyDefections.Shaken(s,holder,now);
    internal static IEnumerable<StrategyCampaign> Uprisings(ContractWorld w,StrategyState s,Dictionary<string,string?> owners,HashSet<string> playerHouse,HashSet<string> near,List<StrategyCampaign> planned,DateTime date)
    {
        long now=w.ElapsedSeconds;
        foreach(var fall in (s.Falls??[]).GroupBy(f=>f.House.Value,StringComparer.Ordinal).Select(g=>g.Last()).OrderBy(f=>f.House.Value,StringComparer.Ordinal))
        {
            string house=fall.House.Value;
            if(playerHouse.Contains(house)||owners.ContainsValue(house)||planned.Any(c=>c.Attacker==fall.House))continue;
            var head=HouseSuccession.Head(w,fall.House);
            if(w.Characters.SingleOrDefault(c=>c.Id==head)?.Life!=LifeState.Adult||BattleCaptivity.HeldAt(w,head,now,w.CommitSequence+1))continue;
            var holder=owners.GetValueOrDefault(fall.Site.Value);
            if(holder==null||playerHouse.Contains(holder)||near.Contains(fall.Site.Value)||NationalStrategy.Resting(s,house,holder,now)||!Shaken(s,holder,now))continue;
            if(Draw(w,$"rise/{house}/{date:yyyy-MM}")>=300)continue;
            yield return new(new($"campaign:national/{date:yyyyMM}/{ContractJson.Hash(Encoding.UTF8.GetBytes(house))[..12]}/restoration"),fall.House,new(holder),fall.Site,now,w.CommitSequence,NationalStrategy.Restoration);
        }
    }
    internal static void Validate(ContractWorld w,StrategyState s,StrategyState? prior)
    {
        var falls=s.Falls??[];
        if(!Enabled(w)){Need(falls.Length==0,"この保存では家の滅亡を記録しません");return;}
        if(prior?.Falls is {} old)Need(falls.Take(old.Length).SequenceEqual(old),"過去の滅亡の記録が変更されています");
        var factions=w.NewGame!.Start.Factions.Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var f in falls)
            Need(factions.Contains(f.House.Value)&&factions.Contains(f.By.Value)&&f.House!=f.By&&f.At>=0&&f.At<=w.ElapsedSeconds&&f.Revision<=w.CommitSequence&&NationalStrategy.IsMonthStart(w,f.At)&&
                s.Campaigns.Any(c=>c.Defender==f.House&&c.Attacker==f.By&&c.Target==f.Site&&s.Outcomes.Any(o=>o.Campaign==c.Id&&o.Captured&&o.At==f.At)),"家の滅亡の記録が不正です");
        foreach(var c in s.Campaigns.Where(c=>c.Basis==NationalStrategy.Restoration))
            Need(falls.Any(f=>f.House==c.Attacker&&f.Site==c.Target&&f.At<c.At),"再興の挙兵は滅んだ家の旧城だけです");
    }
}
