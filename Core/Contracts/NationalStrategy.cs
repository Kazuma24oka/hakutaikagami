using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

// 全国の家の戦略 V1（BD-01・BD-03、2026-11-12）。版（NewGameState.StrategyPolicy）のある全国開始の世界だけで動く。
// 月の初めに、操作する人物の家でない各家の当主が、城の移動網で境を接する家との強さ（兵の上限×今の城の数の割合）を比べて出陣を決める。
// 史実の事件は、前提（当事者の家と当主・境の城）がそろったときに当主がその判断を選びやすくなる重みで、結果は強制しない。
// 遠方の出陣は翌月の初めに簡単な計算で決着し、勝てばその城の支配が移る。結果は境の城から当事者の当主へ書状で届く。
// 操作する人物の家への出陣と、操作する人物から30km以内の城への出陣は、実際の軍勢の工程まで行わない。
public sealed record StrategyCampaign(EntityId Id,EntityId Attacker,EntityId Defender,EntityId Target,long At,long Revision,string Basis);
public sealed record StrategyOutcome(EntityId Campaign,long At,long Revision,bool Captured);
// 史実の出陣のその月の結果：fired（出陣した）・premise（前提が崩れて不発）・declined（前提はそろったが当主が選ばなかった）。
public sealed record StrategyHistoryRecord(string Event,long At,long Revision,string Result);
public sealed record StrategySuccession(EntityId House,EntityId Previous,EntityId Heir,long At,long Revision);
public sealed record StrategyState(int Schema,string Policy,StrategyCampaign[] Campaigns,StrategyOutcome[] Outcomes)
{
    // 当主が亡くなった家の家督（全国の家の戦略の版だけ）。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public StrategySuccession[]? Successions {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public StrategyHistoryRecord[]? History {get;init;}
}
public sealed record HistoricalCampaign(string Id,string Name,string Attacker,string Defender,string? AttackerHead,int Year,int Month,string Source);

public static class NationalStrategy
{
    public const string Policy="national-strategy-v1";
    public const string LetterKind="strategy-outcome";
    public const string Border="border";
    public static bool Enabled(ContractWorld w)=>w.NewGame?.StrategyPolicy==Policy;
    public static void ValidatePolicy(NewGameState data){if(data.StrategyPolicy is not(null or Policy))throw new InvalidDataException("全国の戦略の版が不正です");if(data.StrategyPolicy!=null&&!WorldNetwork.Castle(data))throw new InvalidDataException("全国の戦略は城の移動網の世界だけで使えます");}

    // 史実の出陣（公知の通説。年と月は事件の起きた頃）。DBの家と、そのとき出陣を決めた当主。
    public static readonly HistoricalCampaign[] History=[
        new("okehazama-1560","尾張への出陣（桶狭間の戦い）","G-000123","G-000131","P-005312",1560,5,"通説：永禄3年5月、今川義元が尾張へ出陣"),
        new("odawara-1561","小田原城攻め","G-000134","G-000104","P-005249",1561,3,"通説：永禄4年3月、長尾景虎が小田原城を囲む"),
        new("kawanakajima-1561","第四次川中島の戦い","G-000134","G-000106","P-005249",1561,9,"通説：永禄4年9月"),
        new("mino-1567","稲葉山城の攻略","G-000131","G-000117","P-005235",1567,8,"通説：永禄10年8月、織田信長が稲葉山城を落とす"),
        new("kannonji-1568","上洛と観音寺城の攻略","G-000131","G-000150","P-005235",1568,9,"通説：永禄11年9月"),
        new("suruga-1568","駿河侵攻","G-000106","G-000123","P-005247",1568,12,"通説：永禄11年12月、武田信玄が駿河へ攻め込む"),
        new("gassantoda-1565","月山富田城攻め","G-000188","G-000196","P-005270",1565,4,"通説：永禄8年4月から包囲、9年11月に開城"),
        new("anegawa-1570","浅井攻め（姉川の戦い）","G-000131","G-000152","P-005235",1570,6,"通説：元亀元年6月"),
        new("ichijodani-1573","朝倉攻め","G-000131","G-000147","P-005235",1573,8,"通説：天正元年8月、一乗谷が落ちる"),
        new("odani-1573","小谷城攻め","G-000131","G-000152","P-005235",1573,8,"通説：天正元年8月から9月"),
        new("koshu-1582","甲州征伐","G-000131","G-000106","P-005235",1582,2,"通説：天正10年2月"),
        new("mimikawa-1578","日向への出陣（耳川の戦い）","G-000210","G-000236","P-005322",1578,10,"通説：天正6年10月から11月"),
        new("okitanawate-1584","島原への出陣（沖田畷の戦い）","G-000236","G-000223",null,1584,3,"通説：天正12年3月"),
        new("takaya-1560","河内への出陣（高屋城）","G-000199","G-000160","P-004257",1560,10,"通説：永禄3年10月、三好長慶が河内を平定"),
    ];

    static readonly Lazy<Dictionary<string,string[]>> adjacency=new(BuildAdjacency);
    static Dictionary<string,string[]> BuildAdjacency()
    {
        var castles=CastleRoutes.Catalog.Nodes.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal);
        var edges=CastleRoutes.Edges.SelectMany(e=>new[]{(e.From,e.To),(e.To,e.From)}).GroupBy(x=>x.Item1,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Item2).Distinct(StringComparer.Ordinal).ToArray(),StringComparer.Ordinal);
        var result=new Dictionary<string,string[]>(StringComparer.Ordinal);
        foreach(var c in castles)
        {
            var near=new HashSet<string>(StringComparer.Ordinal);
            foreach(var n in edges.GetValueOrDefault(c)??[])
            {
                if(castles.Contains(n)){near.Add(n);continue;}
                foreach(var m in edges.GetValueOrDefault(n)??[])if(m!=c&&castles.Contains(m))near.Add(m);
            }
            result["site:"+c]=near.Select(x=>"site:"+x).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        return result;
    }
    public static string[] Neighbors(string site)=>adjacency.Value.GetValueOrDefault(site)??[];

    // その時点の城の支配（開始時の持ち主に、出陣の結果を順に当てる）。
    public static EntityId? Owner(ContractWorld w,EntityId site,long at,long rev)
    {
        var start=w.NewGame!.Start.Castles.SingleOrDefault(c=>c.Id==site.Value)?.Faction;
        EntityId? owner=start==null?null:new(start);
        if(w.Strategy is not {} s)return owner;
        foreach(var o in s.Outcomes)
        {
            if(!o.Captured||o.At>at||o.At==at&&o.Revision>rev)continue;
            var c=s.Campaigns.Single(c=>c.Id==o.Campaign);if(c.Target==site)owner=c.Attacker;
        }
        return owner;
    }
    static Dictionary<string,string?> Owners(ContractWorld w,StrategyState? s)
    {
        var map=w.NewGame!.Start.Castles.ToDictionary(c=>c.Id,c=>c.Faction,StringComparer.Ordinal);
        if(s!=null)foreach(var o in s.Outcomes.Where(o=>o.Captured)){var c=s.Campaigns.Single(c=>c.Id==o.Campaign);map[c.Target.Value]=c.Attacker.Value;}
        return map;
    }

    static long MonthStart(ContractWorld w,int year,int month)
    {
        var epoch=DateOnly.ParseExact(w.EpochDate,"yyyy-MM-dd",CultureInfo.InvariantCulture);
        return ((long)new DateOnly(year,month,1).DayNumber-epoch.DayNumber)*86400;
    }
    // at より後の最初の月の初め。
    static long NextMonthStart(ContractWorld w,long at)
    {
        var d=WorldCalendar.At(w.EpochDate,at);var next=MonthStart(w,d.Year,d.Month);
        if(next<=at){var m=d.AddMonths(1);next=MonthStart(w,m.Year,m.Month);}
        return next;
    }
    static bool IsMonthStart(ContractWorld w,long at){var d=WorldCalendar.At(w.EpochDate,at);return MonthStart(w,d.Year,d.Month)==at;}
    internal static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;long next=NextMonthStart(w,w.ElapsedSeconds);
        return next<=target?next:null;
    }

    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"national-strategy-v1/{w.Seed}/{key}")))%1000);
    static long Strength(ContractWorld w,string faction,Dictionary<string,string?> owners,Dictionary<string,int> initial)
    {
        var db=NationalStart.DbHouse(faction);long cap=db==null?0:HakutaiDbStart.Roster(db).SoldierCap;
        int now=owners.Values.Count(v=>v==faction),start=initial.GetValueOrDefault(faction);
        return Math.Max(50,start==0?0:cap*now/start);
    }
    static bool Allied(string a,string b)
    {
        var x=NationalStart.DbHouse(a) is {} da?HakutaiDb.House(da):null;var y=NationalStart.DbHouse(b) is {} db?HakutaiDb.House(db):null;
        return x?.Overlord!=null&&x.Overlord==y?.Id||y?.Overlord!=null&&y.Overlord==x?.Id||x?.Overlord!=null&&x.Overlord==y?.Overlord;
    }
    // 決着した出陣の後、同じ二つの家は三か月は互いに出陣しない（戦いの疲れ。和睦の交渉は後の工程）。
    static bool Resting(StrategyState s,string a,string b,long now)=>s.Outcomes.Any(o=>o.At>now-90*86400&&s.Campaigns.Single(c=>c.Id==o.Campaign) is var c&&(c.Attacker.Value==a&&c.Defender.Value==b||c.Attacker.Value==b&&c.Defender.Value==a));
    static readonly Lazy<Dictionary<string,HashSet<string>>> vassalsOf=new(()=>HakutaiDb.Houses.Where(h=>h.Overlord!=null).GroupBy(h=>NationalStart.FactionId(h.Overlord!),StringComparer.Ordinal)
        .ToDictionary(g=>g.Key,g=>g.Select(h=>NationalStart.FactionId(h.Id)).ToHashSet(StringComparer.Ordinal),StringComparer.Ordinal));
    static HashSet<string> Vassals(string faction)=>vassalsOf.Value.GetValueOrDefault(faction)??[];
    static HistoryMode Mode(ContractWorld w)=>w.NewGame!.Judgment?.History??HistoryMode.SemiHistorical;
    static bool NearPlayer(ContractWorld w,string site)
    {
        var p=RegionalCampaign.ActorPointAt(w,w.PlayerControl,w.ElapsedSeconds);
        var node=BranchRoutes.GraphNodes(w).SingleOrDefault(n=>"site:"+n.Id==site);
        if(node==null)return false;
        long de=node.Position.EastMm-p.EastMm,dn=node.Position.NorthMm-p.NorthMm;
        return (double)de*de+(double)dn*dn<=(double)NpcDormancy.RadiusMm*NpcDormancy.RadiusMm;
    }

    internal static void Apply(ContractWorld w)
    {
        if(!Enabled(w)||!IsMonthStart(w,w.ElapsedSeconds))return;
        var s=w.Strategy??new StrategyState(1,Policy,[],[]);
        var date=WorldCalendar.At(w.EpochDate,w.ElapsedSeconds);
        s=s with{Successions=Succeed(w,s)};w.Strategy=s;
        var outcomes=s.Outcomes.ToList();
        // 前の月の出陣を決着させる。
        foreach(var c in s.Campaigns.Where(c=>!s.Outcomes.Any(o=>o.Campaign==c.Id)&&c.At<w.ElapsedSeconds))
        {
            var owners0=Owners(w,s with{Outcomes=outcomes.ToArray()});var initial0=InitialCounts(w);
            long a=Strength(w,c.Attacker.Value,owners0,initial0),d=Strength(w,c.Defender.Value,owners0,initial0);
            bool held=owners0.GetValueOrDefault(c.Target.Value)==c.Defender.Value;
            bool captured=held&&Draw(w,"outcome/"+c.Id.Value)<1000*a/(a+d);
            outcomes.Add(new(c.Id,w.ElapsedSeconds,w.CommitSequence,captured));
            Letters(w,c,captured);
        }
        s=s with{Outcomes=outcomes.ToArray()};
        var owners=Owners(w,s);var initial=InitialCounts(w);var campaigns=s.Campaigns.ToList();
        var player=w.PlayerControl;var playerHouse=w.NewGame!.Start.Factions.Where(f=>HouseSuccession.Head(w,new(f.Id))==player).Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);
        var playerMember=w.NewGame.Start.People.SingleOrDefault(p=>p.Id==player.Value)?.Faction;if(playerMember!=null)playerHouse.Add(playerMember);
        var mode=Mode(w);var history=(s.History??[]).ToList();
        // その月の史実の出陣で、攻める家が存在しないか当主が操作する人物の家などで判断に入らないものは、前提が崩れた不発として残す。
        foreach(var e in History.Where(e=>e.Year==date.Year&&e.Month==date.Month))
        {
            var f=NationalStart.FactionId(e.Attacker);var head=w.NewGame.Start.Factions.Any(x=>x.Id==f)?HouseSuccession.Head(w,new(f)):default;
            if(mode==HistoryMode.Fictional||head.Value==null||playerHouse.Contains(f)||w.Characters.SingleOrDefault(c=>c.Id==head)?.Life!=LifeState.Adult||!owners.ContainsValue(f))
                history.Add(new(e.Id,w.ElapsedSeconds,w.CommitSequence,mode==HistoryMode.Fictional?"declined":"premise"));
        }
        foreach(var f in w.NewGame.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            if(playerHouse.Contains(f.Id))continue;
            var head=HouseSuccession.Head(w,new(f.Id));if(w.Characters.SingleOrDefault(c=>c.Id==head)?.Life!=LifeState.Adult)continue;
            var own=owners.Where(x=>x.Value==f.Id).Select(x=>x.Key).ToArray();if(own.Length==0)continue;
            // 境の城は、自分の城と、自分に従う家（DBの従属先が自分の家）の城の隣。
            var vassals=Vassals(f.Id);var front=owners.Where(x=>x.Value==f.Id||x.Value!=null&&vassals.Contains(x.Value)).Select(x=>x.Key).ToArray();
            long mine=Strength(w,f.Id,owners,initial);
            var targets=front.SelectMany(Neighbors).Distinct(StringComparer.Ordinal)
                .Select(t=>(Site:t,Owner:owners.GetValueOrDefault(t))).Where(t=>t.Owner!=null&&t.Owner!=f.Id&&!vassals.Contains(t.Owner!)&&!playerHouse.Contains(t.Owner!)&&!Allied(f.Id,t.Owner!)&&!Resting(s,f.Id,t.Owner!,w.ElapsedSeconds)&&!NearPlayer(w,t.Site)).ToArray();
            if(targets.Length==0)continue;
            string? basis=null;(string Site,string? Owner) pick=default;
            if(mode!=HistoryMode.Fictional)
                foreach(var e in History.Where(e=>e.Year==date.Year&&e.Month==date.Month&&NationalStart.FactionId(e.Attacker)==f.Id))
                {
                    var defender=NationalStart.FactionId(e.Defender);var known=targets.Where(t=>t.Owner==defender).OrderBy(t=>t.Site,StringComparer.Ordinal).ToArray();
                    if(known.Length==0||e.AttackerHead!=null&&NationalStart.DbPerson(head.Value)?.Id!=e.AttackerHead){history.Add(new(e.Id,w.ElapsedSeconds,w.CommitSequence,"premise"));continue;} // 前提が崩れていれば不発
                    if(basis==null&&Draw(w,"history/"+e.Id+"/"+f.Id)<(mode==HistoryMode.Historical?900:500)){basis="history:"+e.Id;pick=known[0];history.Add(new(e.Id,w.ElapsedSeconds,w.CommitSequence,"fired"));}
                    else history.Add(new(e.Id,w.ElapsedSeconds,w.CommitSequence,"declined"));
                }
            if(basis==null)
            {
                var best=targets.Select(t=>(t,Ratio:mine*100/Strength(w,t.Owner!,owners,initial))).OrderByDescending(x=>x.Ratio).ThenBy(x=>x.t.Site,StringComparer.Ordinal).First();
                if(best.Ratio<150||Draw(w,$"border/{f.Id}/{date:yyyy-MM}")>=60)continue;
                basis=Border;pick=best.t;
            }
            var id=new EntityId($"campaign:national/{date:yyyyMM}/{ContractJson.Hash(Encoding.UTF8.GetBytes(f.Id))[..12]}");
            campaigns.Add(new(id,new(f.Id),new(pick.Owner!),new(pick.Site!),w.ElapsedSeconds,w.CommitSequence,basis));
        }
        w.Strategy=s with{Campaigns=campaigns.ToArray(),History=history.Count==0?null:history.ToArray()};
    }
    // 当主が亡くなった家（操作する人物の家を除く）の家督：成人した実子の年長者、いなければ家中の筆頭格の成人。
    static StrategySuccession[]? Succeed(ContractWorld w,StrategyState s)
    {
        var rows=(s.Successions??[]).ToList();var people=FamilyBirth.People(w).ToArray();var life=w.Characters.ToDictionary(c=>c.Id.Value,c=>c.Life,StringComparer.Ordinal);
        foreach(var f in w.NewGame!.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            var head=HouseSuccession.Head(w,new(f.Id));if(life.GetValueOrDefault(head.Value)!=LifeState.Dead||head==w.PlayerControl)continue;
            var heir=people.Where(p=>(p.BirthParents??[]).Contains(head.Value)&&life.GetValueOrDefault(p.Id)==LifeState.Adult).OrderBy(p=>p.BirthYear??NationalStart.Life(p.Id)?.Born??int.MaxValue).ThenBy(p=>p.Id,StringComparer.Ordinal).FirstOrDefault()
                ??people.Where(p=>p.Faction==f.Id&&life.GetValueOrDefault(p.Id)==LifeState.Adult).OrderBy(p=>p.Lord==null?0:1).ThenBy(p=>p.Id,StringComparer.Ordinal).FirstOrDefault();
            if(heir==null)continue;
            rows.Add(new(new(f.Id),head,new(heir.Id),w.ElapsedSeconds,w.CommitSequence));
        }
        return rows.Count==0?null:rows.ToArray();
    }
    static Dictionary<string,int> InitialCounts(ContractWorld w)=>w.NewGame!.Start.Castles.Where(c=>c.Faction!=null).GroupBy(c=>c.Faction!,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.Count(),StringComparer.Ordinal);

    // 結果の書状：境の城にいる人物から、双方の当主へ。城に誰もいなければ書状はない（後で噂で知る）。
    static void Letters(ContractWorld w,StrategyCampaign c,bool captured)
    {
        var sender=w.Locations.Where(l=>l.Kind==LocationKind.AtSite&&l.Site==c.Target&&w.Characters.Single(x=>x.Id==l.Actor).Life==LifeState.Adult).Select(l=>l.Actor).OrderBy(x=>x.Value,StringComparer.Ordinal).FirstOrDefault();
        if(sender.Value==null)return;
        string castle=w.Definitions.Sites.SingleOrDefault(s=>s.Id==c.Target)?.Name??c.Target.Value;
        string attacker=w.Definitions.Organizations.Single(o=>o.Id==c.Attacker).Name,defender=w.Definitions.Organizations.Single(o=>o.Id==c.Defender).Name;
        string body=captured?$"{castle}は{attacker}の手に落ちました。":$"{attacker}の軍勢は{castle}を落とせずに退きました。";
        // 史実の出陣は、年代記に残るときの名で知らせる。
        if(History.SingleOrDefault(e=>"history:"+e.Id==c.Basis) is {} named)body=named.Name+"　"+body;
        foreach(var house in new[]{c.Attacker,c.Defender})
        {
            var head=HouseSuccession.Head(w,house);if(head==sender||w.Characters.Single(x=>x.Id==head).Life!=LifeState.Adult)continue;
            var site=FamilyBirth.People(w).Single(p=>p.Id==head.Value).Site;
            string id=$"message:strategy/{ContractJson.Hash(Encoding.UTF8.GetBytes(c.Id.Value+"/"+house.Value))[..16]}";
            MessageFlow.Send(w.Messaging,new MessageEnvelope(id,c.Id.Value,LetterKind,sender.Value,head.Value,body,site,w.ElapsedSeconds),w.ElapsedSeconds,1);
            w.SealedMessages=[..w.SealedMessages,new(id)];
        }
        RegionalCampaign.Bind(w);
    }

    internal static void Validate(ContractWorld w,ContractWorld? prior)
    {
        if(w.Strategy is not {} s){if(Enabled(w)&&prior?.Strategy!=null)throw new InvalidDataException("全国の戦略の記録が消えています");return;}
        if(!Enabled(w)||s.Schema!=1||s.Policy!=Policy)throw new InvalidDataException("全国の戦略の版が不正です");
        if(prior?.Strategy is {} old&&(!s.Campaigns.Take(old.Campaigns.Length).SequenceEqual(old.Campaigns)||!s.Outcomes.Take(old.Outcomes.Length).SequenceEqual(old.Outcomes)))throw new InvalidDataException("過去の出陣の記録が変更されています");
        var factions=w.NewGame!.Start.Factions.Select(f=>f.Id).ToHashSet(StringComparer.Ordinal);
        if(s.Campaigns.Select(c=>c.Id).Distinct().Count()!=s.Campaigns.Length||s.Outcomes.Select(o=>o.Campaign).Distinct().Count()!=s.Outcomes.Length)throw new InvalidDataException("出陣の記録が重複しています");
        foreach(var c in s.Campaigns)
            if(c.Attacker==c.Defender||!factions.Contains(c.Attacker.Value)||!factions.Contains(c.Defender.Value)||c.At<0||c.At>w.ElapsedSeconds||c.Revision<0||c.Revision>w.CommitSequence||!IsMonthStart(w,c.At)||
                !(c.Basis==Border||c.Basis.StartsWith("history:",StringComparison.Ordinal)&&History.Any(e=>"history:"+e.Id==c.Basis))||Owner(w,c.Target,c.At,c.Revision)!=c.Defender)
                throw new InvalidDataException("出陣の記録が不正です");
        foreach(var x in s.Successions??[])if(!factions.Contains(x.House.Value)||x.At>w.ElapsedSeconds||x.Revision>w.CommitSequence||!IsMonthStart(w,x.At)||w.Characters.SingleOrDefault(c=>c.Id==x.Previous)?.Life!=LifeState.Dead||!w.Characters.Any(c=>c.Id==x.Heir))throw new InvalidDataException("家督の記録が不正です");
        if(prior?.Strategy?.Successions is {} oldSuccessions&&!(s.Successions??[]).Take(oldSuccessions.Length).SequenceEqual(oldSuccessions))throw new InvalidDataException("過去の家督の記録が変更されています");
        foreach(var o in s.Outcomes){var c=s.Campaigns.SingleOrDefault(c=>c.Id==o.Campaign);if(c==null||o.At<=c.At||o.At>w.ElapsedSeconds||o.Revision>w.CommitSequence||!IsMonthStart(w,o.At))throw new InvalidDataException("出陣の結果が不正です");}
    }
}
