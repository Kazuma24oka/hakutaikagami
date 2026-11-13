using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// 従属の離反（BD-46、2026-11-14）。全国の開始の一覧（StrategyDefection）を持つ世界だけ。旧保存は変えない。
// 従っていた家（DBの Overlord）の支配が揺らいだ（1年以内に当主が代わった・3か月以内に戦に負けた）とき、従う家の当主は、
// 自分の家の強さが主家の半分以上なら、月に10%で主家から離れる。離れた後は、主家の境の城に数えられず、互いに攻め合える。
// 離れるのは家どうしの従属だけ。城・家臣・兵・約束は動かさない。操作する人物の家は離れず、離れられもしない。
public static class StrategyDefections
{
    public static bool Enabled(ContractWorld w)=>NationalSystems.On(w,"StrategyDefection");
    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"strategy-defection-v1/{w.Seed}/{key}")))%1000);
    const long Day=86400;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    internal static bool Left(StrategyState s,string house,string overlord)=>(s.Defections??[]).Any(d=>d.House.Value==house&&d.From.Value==overlord);
    internal static bool Shaken(StrategyState s,string house,long now)=>
        (s.Successions??[]).Any(x=>x.House.Value==house&&x.At>now-365*Day)||
        s.Outcomes.Where(o=>o.At>now-90*Day).Any(o=>s.Campaigns.Single(c=>c.Id==o.Campaign) is var c&&(c.Attacker.Value==house&&!o.Captured||c.Defender.Value==house&&o.Captured));
    internal static StrategyState Decide(ContractWorld w,StrategyState s,Dictionary<string,long> power,DateTime date)
    {
        long now=w.ElapsedSeconds;var rows=(s.Defections??[]).ToList();
        var factions=w.NewGame!.Start.Factions.Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);
        bool PlayerHouse(string f)=>HouseSuccession.Head(w,new(f))==w.PlayerControl||w.NewGame.Start.People.SingleOrDefault(p=>p.Id==w.PlayerControl.Value)?.Faction==f;
        bool Adult(string f)=>w.Characters.SingleOrDefault(c=>c.Id==HouseSuccession.Head(w,new(f)))?.Life==LifeState.Adult;
        foreach(var house in factions.OrderBy(f=>f,StringComparer.Ordinal))
        {
            if(NationalStrategy.DbOverlord(house) is not {} overlord||!factions.Contains(overlord)||Left(s,house,overlord))continue;
            if(PlayerHouse(house)||PlayerHouse(overlord)||!Adult(house)||power.GetValueOrDefault(house)==0||!Shaken(s,overlord,now))continue;
            if(power.GetValueOrDefault(house)*2<power.GetValueOrDefault(overlord,50))continue;
            if(Draw(w,$"defect/{house}/{date:yyyy-MM}")>=100)continue;
            rows.Add(new(new(house),new(overlord),now,w.CommitSequence));
        }
        return s with{Defections=rows.Count==0?null:rows.ToArray()};
    }
    internal static void Validate(ContractWorld w,StrategyState s,StrategyState? prior)
    {
        var rows=s.Defections??[];
        if(!Enabled(w)){Need(rows.Length==0,"この保存では従属の離反を記録しません");return;}
        if(prior?.Defections is {} old)Need(rows.Take(old.Length).SequenceEqual(old),"過去の離反の記録が変更されています");
        Need(rows.Select(d=>(d.House,d.From)).Distinct().Count()==rows.Length,"離反の記録が重複しています");
        foreach(var d in rows)
            Need(NationalStrategy.DbOverlord(d.House.Value)==d.From.Value&&d.At>=0&&d.At<=w.ElapsedSeconds&&d.Revision<=w.CommitSequence&&NationalStrategy.IsMonthStart(w,d.At)&&Shaken(s,d.From.Value,d.At),"離反の記録が不正です");
    }
}
