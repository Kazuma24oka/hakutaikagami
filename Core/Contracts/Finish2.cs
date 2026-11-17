using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// S12（2026-11-18）：最初の完成版の残りの機能。一覧（NationalSystems）に Finish2 のある新規の全国開始だけ（Duties2 までを使う）。
// ・勲功：陣へ兵糧を届けた家の兵站の者に「兵糧」の勲功。出陣が決着した月に当主が勲功を比べて順を裁き（落城・加勢・兵糧の順）、兵糧の者が先を争えば理由つきで裁定する。
// ・通行：境に攻める城の無い家は、同盟の家の領地を通って先の城を攻めることがあり、通る家に申し入れる。許せば出陣、断られれば見送るか、無断で通って抗議を受け盟約が切れる。
// ・人質：家どうしの同盟は、結んだ次の月に弱い側の一族の一人を人質に出す。盟約が破られれば、人質を預かる家の当主が返すか留め置くかを決める（処刑はしない）。
// ・仲裁の抵触：将軍の仲裁で和睦すると、第三の家との「単独で和睦しない」の盟約や包囲網に背くなら、勝った家は理由を添えて和睦を断る。
// ・栄典：京の主の当主に御相伴衆と裏書御免、守護で御供衆の当主に屋形号と塗輿の免許、京の主の家の取次に申次衆、家老に御部屋衆。
// ・計略の発覚：流言を受けた家の目付が出所を見つければ（200＋情報×5の千分率）、抗議して記録する。
// ・寺社の職：寺社の記録から、寺は住持・社は別当を推定の印つきで置く（開始の月に一度）。
// ・子の開始時の傾向：開始の月に、未成年の子の興味を当主の得意から推定の印つきで置く。
// ・従属の不履行：証拠（記した者）・当主が知る日・責任（当主か、不在の間に代わって決めた家老か）を分けて記録する。
// ・登用の例外：天下を志す者は京の主にしか仕えない、家を滅ぼした家には仕えない、60歳以上は隠居を望んで応じない。
// ・兵糧の援け：兵糧の足りない家は同盟の家へ書状で援けを求め（遠方から初めての手配）、余りのある家は送り、敵を避ける道の日数で届く。
// ・兵糧攻めの開城：城の兵糧が足りず、水路から米が入らず（守り手の水軍が攻め手を上回らない）、攻め手が1.5倍以上なら開城する。
// ・本人の務めの返上：傷病のある本人は、依頼主へ返還・代理・延期を願い、務めは止まる（延期は回復後に改めて頼まれる約束として残す）。回復したら復帰の確かめを記録する。
public sealed record FinishEvent(string Id,string Kind,EntityId? House,EntityId? Other,EntityId? Person,long At,long Revision,long Amount,string Text)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? Arrives {get;init;}
}

public static class Finish2
{
    public const string Name=NationalSystems.Finish2;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name)&&Duties2.On(w);
    const long Day=86400,Year=365*Day;
    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"finish-v1/{w.Seed}/{key}")))%1000);
    static string Hash(string s)=>ContractJson.Hash(Encoding.UTF8.GetBytes(s))[..12];
    static string HouseName(ContractWorld w,EntityId h)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==h)?.Name??h.Value;
    static string PersonName(ContractWorld w,EntityId p)=>w.Definitions.Characters.SingleOrDefault(c=>c.Id==p)?.Name??FamilyBirth.People(w).SingleOrDefault(x=>x.Id==p.Value)?.Name??p.Value;
    static bool Player(ContractWorld w,string house)=>HouseSuccession.Head(w,new(house))==w.PlayerControl||FamilyBirth.People(w).SingleOrDefault(p=>p.Id==w.PlayerControl.Value)?.Faction==house;
    static string HomeOf(ContractWorld w,string house)=>w.NewGame!.Start.Factions.FirstOrDefault(f=>f.Id==house)?.Home??"";
    public static FinishEvent[] Events(ContractWorld w)=>w.Strategy?.Finish??[];
    static bool Bound(IEnumerable<StrategyPact> pacts,string a,string b,long now)=>pacts.Any(p=>p.Broken==null&&p.From<=now&&now<p.Until&&(p.A.Value==a&&p.B.Value==b||p.A.Value==b&&p.B.Value==a));
    static EntityId[] Adults(ContractWorld w,string house,HashSet<EntityId> alive)=>FamilyBirth.People(w).Where(p=>p.Faction==house&&alive.Contains(new EntityId(p.Id))).Select(p=>new EntityId(p.Id)).OrderBy(p=>p.Value,StringComparer.Ordinal).ToArray();

    internal static StrategyState Month(ContractWorld w,StrategyState s,Dictionary<string,string?> owners)
    {
        if(!On(w))return s;
        long now=w.ElapsedSeconds,rev=w.CommitSequence;var date=WorldCalendar.At(w.EpochDate,now);
        var events=(s.Finish??[]).ToList();
        void Add(string kind,EntityId? house,EntityId? other,EntityId? person,long amount,string text,long? arrives=null)=>
            events.Add(new($"finish:{kind}/{date:yyyyMM}/{Hash(kind+"/"+house?.Value+"/"+other?.Value+"/"+person?.Value+"/"+events.Count)}",kind,house,other,person,now,rev,amount,text){Arrives=arrives});
        if(date.Month==1)events=events.Where(e=>now-e.At<2*Year||e.Kind is "hostage" or "temple-office" or "honor"||(e.Arrives??0)>now).ToList();
        var alive=w.Characters.Where(c=>c.Life==LifeState.Adult).Select(c=>c.Id).ToHashSet();
        var pacts=(s.Pacts??[]).ToList();var houses=(s.Houses??[]).ToDictionary(h=>h.House.Value,StringComparer.Ordinal);
        var power=NationalStrategy.Powers(w,owners);var first=!events.Any(e=>e.Kind=="started");
        if(first)Add("started",null,null,null,0,"記録の始まり");
        // 本人が癒えたら、返上した務めの復帰の確かめ（任務・許しは消えていない）を一度だけ記録する。
        if(PersonalHealth.Active(w,w.PlayerControl,now).Length==0)
            foreach(var e in events.Where(e=>e.Kind=="relief"&&e.Person==w.PlayerControl&&!events.Any(x=>x.Kind=="recovered"&&x.Text.EndsWith(e.Id))).ToArray())
                Add("recovered",null,e.Other,w.PlayerControl,0,$"癒えて務めに戻れることを{PersonName(w,e.Other!.Value)}に知らせた　"+e.Id);

        // ---- 開始の月：寺社の職と子の傾向（推定） ----
        if(first)
        {
            foreach(var t in (s.Temples??[]).OrderBy(t=>t.Temple,StringComparer.Ordinal).Take(400))
                Add("temple-office",t.Lord,null,null,0,$"{t.Temple}の{(t.Temple.EndsWith("社")||t.Temple.EndsWith("宮")?"別当":"住持")}（推定。史料に名の残らない職）");
            int year=date.Year;
            foreach(var p in FamilyBirth.People(w).Where(p=>p.Faction!=null&&p.BirthYear is int b&&year-b<FamilyKin.ComingOfAge&&year-b>=3).OrderBy(p=>p.Id,StringComparer.Ordinal).Take(300))
            {
                var head=HouseSuccession.Head(w,new(p.Faction!));
                var best=CharacterDevelopment.Skills(w).OrderByDescending(k=>CharacterDevelopment.LevelAt(w,head,k,now)).ThenBy(k=>k.ToString(),StringComparer.Ordinal).First();
                Add("child-start",new(p.Faction!),null,new(p.Id),0,$"{p.Name}は{CharacterDevelopment.Name(best)}に興味を示している（推定。家の当主の得意から）");
            }
        }

        // ---- 勲功：兵糧の勲功と、決着した月の裁定 ----
        foreach(var o in s.Outcomes.Where(o=>o.At==now))
        {
            var c=s.Campaigns.Single(c=>c.Id==o.Campaign);var house=c.Attacker.Value;if(Player(w,house))continue;
            bool fed=(s.Logistics??[]).Any(e=>e.Kind=="supply"&&e.House==c.Attacker&&e.Site==c.Target.Value&&e.At>=c.At&&e.Amount>0);
            var rows=(s.Merits??[]).Where(m=>m.Campaign==c.Id).Select(m=>(m.Person,m.Kind,Rank:m.Kind=="落城"?3:2)).ToList();
            if(fed&&Adults(w,house,alive).Where(p=>p!=HouseSuccession.Head(w,c.Attacker)).OrderByDescending(p=>CharacterDevelopment.LevelAt(w,p,DevelopmentSkill.Logistics,now)).ThenBy(p=>p.Value,StringComparer.Ordinal).FirstOrDefault() is {Value:not null} carrier)
            {
                Add("merit-supply",c.Attacker,c.Defender,carrier,0,$"{PersonName(w,carrier)}が{NewGameWorld.SiteName(c.Target.Value)}の陣へ兵糧を届けた手柄");
                rows.Add((carrier,"兵糧",1));
            }
            if(rows.Count<2)continue;
            var order=rows.OrderByDescending(r=>r.Rank).ThenBy(r=>r.Person.Value,StringComparer.Ordinal).ToArray();
            Add("adjudicate",c.Attacker,c.Defender,order[0].Person,order.Length,$"{HouseName(w,c.Attacker)}の当主が手柄を比べ、{string.Join("・",order.Select(r=>PersonName(w,r.Person)+"（"+r.Kind+"）"))}の順に恩賞を定めた");
            var supply=order.FirstOrDefault(r=>r.Kind=="兵糧");
            if(supply.Person.Value!=null&&order[0].Kind!="兵糧"&&CharacterDevelopment.LevelAt(w,supply.Person,DevelopmentSkill.Logistics,now)>=30)
                Add("dispute",c.Attacker,null,supply.Person,0,$"{PersonName(w,supply.Person)}は兵糧なくして城は落ちぬと先を争ったが、当主は城を落とした手柄を先とする理由を示して裁いた");
        }

        // ---- 通行：同盟の家の領地を通って攻める ----
        var campaigns=s.Campaigns.ToList();
        foreach(var f in owners.Values.Where(v=>v!=null).Select(v=>v!).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal))
        {
            if(Player(w,f)||campaigns.Any(c=>c.Attacker.Value==f&&c.At==now)||Draw(w,$"passage/{f}/{date:yyyy-MM}")>=40)continue;
            var head=HouseSuccession.Head(w,new(f));if(!alive.Contains(head))continue;
            var mine=owners.Where(x=>x.Value==f).Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
            var near=mine.SelectMany(NationalStrategy.Neighbors).ToHashSet(StringComparer.Ordinal);
            var route=pacts.Where(p=>p.Kind=="alliance"&&p.Broken==null&&p.From<=now&&now<p.Until&&(p.A.Value==f||p.B.Value==f)).Select(p=>p.A.Value==f?p.B.Value:p.A.Value)
                .SelectMany(ally=>owners.Where(x=>x.Value==ally&&near.Contains(x.Key)).SelectMany(x=>NationalStrategy.Neighbors(x.Key).Select(t=>(Ally:ally,Through:x.Key,Target:t))))
                .Where(x=>owners.GetValueOrDefault(x.Target) is {} o&&o!=f&&o!=x.Ally&&!near.Contains(x.Target)&&!Bound(pacts,f,o,now)&&!Player(w,o)&&power.GetValueOrDefault(f,50)*2>=power.GetValueOrDefault(o,50)*3)
                .OrderBy(x=>x.Target,StringComparer.Ordinal).FirstOrDefault();
            if(route.Target==null)continue;
            var target=owners[route.Target]!;bool joint=pacts.Any(p=>p.Broken==null&&(p.A.Value==f&&p.B.Value==route.Ally||p.B.Value==f&&p.A.Value==route.Ally)&&Politics2.Terms(w,p.Id).Any(t=>t.Clause=="共同の出陣"));
            bool grant=joint||power.GetValueOrDefault(route.Ally,50)<=power.GetValueOrDefault(f,50);
            var id=new EntityId($"campaign:national/{date:yyyyMM}/{Hash(f+"/passage")}");
            if(grant)
            {
                Add("passage",new(f),new(route.Ally),null,0,$"{HouseName(w,new(route.Ally))}は{HouseName(w,new(f))}の軍が領地を通って{NewGameWorld.SiteName(route.Target)}を攻めることを許した");
                campaigns.Add(new(id,new(f),new(target),new(route.Target),now,rev,NationalStrategy.Border));
            }
            else if(Draw(w,$"trespass/{f}/{date:yyyy-MM}")<300)
            {
                Add("protest",new(route.Ally),new(f),null,0,$"{HouseName(w,new(f))}の軍が断りなく{HouseName(w,new(route.Ally))}の領地を通り、{HouseName(w,new(route.Ally))}は抗議して盟約を切った");
                campaigns.Add(new(id,new(f),new(target),new(route.Target),now,rev,NationalStrategy.Border));
                for(int n=0;n<pacts.Count;n++)if(pacts[n].Broken==null&&(pacts[n].A.Value==f&&pacts[n].B.Value==route.Ally||pacts[n].B.Value==f&&pacts[n].A.Value==route.Ally))pacts[n]=pacts[n] with{Broken=now};
            }
            else Add("passage-refused",new(f),new(route.Ally),null,0,$"{HouseName(w,new(route.Ally))}は{HouseName(w,new(f))}の軍の通行を断り、{HouseName(w,new(f))}は出陣を見送った");
        }

        // ---- 人質：同盟を結んだ次の月に弱い側から、盟約が終わった月に預かる家の当主が決める ----
        foreach(var p in pacts.Where(p=>p.Kind=="alliance"&&p.From<now&&now-p.From<=32*Day&&p.Broken==null))
        {
            var (weak,strong)=power.GetValueOrDefault(p.A.Value,50)<=power.GetValueOrDefault(p.B.Value,50)?(p.A,p.B):(p.B,p.A);
            if(Player(w,weak.Value)||Player(w,strong.Value))continue;
            var kin=Adults(w,weak.Value,alive).Where(x=>x!=HouseSuccession.Head(w,weak)&&!events.Any(e=>e.Kind=="hostage"&&e.Person==x&&!events.Any(r=>r.Kind is "hostage-returned" or "hostage-kept"&&r.Person==x))).FirstOrDefault();
            if(kin.Value==null)continue;
            Add("hostage",weak,strong,kin,0,$"{HouseName(w,weak)}は{PersonName(w,kin)}を人質として{HouseName(w,strong)}へ送った（盟約の証）");
        }
        foreach(var h in events.Where(e=>e.Kind=="hostage"&&!events.Any(r=>r.Kind is "hostage-returned" or "hostage-kept"&&r.Person==e.Person)).ToArray())
        {
            var pact=pacts.FirstOrDefault(p=>p.Kind=="alliance"&&(p.A==h.House&&p.B==h.Other||p.B==h.House&&p.A==h.Other));
            bool over=pact==null||pact.Broken!=null||now>=pact.Until;if(!over)continue;
            // 破ったのが人質を出した家（弱い側）の出陣なら留め置く。そうでなければ返す。
            bool betrayed=pact?.Broken is long b&&campaigns.Any(c=>c.At==b&&c.Attacker==h.House&&c.Defender==h.Other);
            Add(betrayed?"hostage-kept":"hostage-returned",h.Other,h.House,h.Person,0,betrayed?$"{HouseName(w,h.Other!.Value)}の当主は、盟約を破った{HouseName(w,h.House!.Value)}の人質{PersonName(w,h.Person!.Value)}を留め置くと決めた":$"盟約が終わり、{HouseName(w,h.Other!.Value)}は人質{PersonName(w,h.Person!.Value)}を{HouseName(w,h.House!.Value)}へ返した");
        }

        // ---- 栄典 ----
        bool Has(string honor,EntityId p)=>events.Any(e=>e.Kind=="honor"&&e.Person==p&&e.Text.Contains(honor));
        void Honor(EntityId house,EntityId person,string honor){if(!alive.Contains(person)||Has(honor,person))return;Add("honor",house,null,person,0,$"将軍が{PersonName(w,person)}に{honor}を許した");}
        if(NationalStrategy.Capital(w) is {} capital&&!Player(w,capital.Value))
        {
            var head=HouseSuccession.Head(w,capital);Honor(capital,head,"御相伴衆");Honor(capital,head,"裏書御免");
            if(houses.GetValueOrDefault(capital.Value) is {} ch)
            {
                if(ch.Posts.FirstOrDefault(p=>p.Post==Domestic2.Envoy) is {} envoy)Honor(capital,envoy.Person,"申次衆");
                if(ch.Posts.FirstOrDefault(p=>p.Post==Domestic2.Elder) is {} elder)Honor(capital,elder.Person,"御部屋衆");
            }
        }
        foreach(var sh in (s.Shugo??[]).Where(x=>x.At<=now).GroupBy(x=>x.House).Select(g=>g.Last()))
        {
            if(Player(w,sh.House.Value))continue;var head=HouseSuccession.Head(w,sh.House);
            if((s.Honors??[]).Any(x=>x.House==sh.House&&x.Kind=="御供衆")){Honor(sh.House,head,"屋形号");Honor(sh.House,head,"塗輿の免許");}
        }

        // ---- 計略の発覚 ----
        foreach(var r in (s.Politics??[]).Where(e=>e.Kind=="rumor"&&e.At==now&&e.House!=null&&e.Other!=null))
        {
            if(Player(w,r.Other!.Value.Value))continue;var insp=houses.GetValueOrDefault(r.Other!.Value.Value)?.Posts.FirstOrDefault(p=>p.Post==Domestic2.Inspector);if(insp==null)continue;
            if(Draw(w,"rumor-caught/"+r.Id)<200+CharacterDevelopment.LevelAt(w,insp.Person,DevelopmentSkill.IntelligenceGathering,now)*5)
                Add("scheme-caught",r.Other,r.House,insp.Person,0,$"{HouseName(w,r.Other!.Value)}の目付{PersonName(w,insp.Person)}が流言の出所を{HouseName(w,r.House!.Value)}と突き止め、抗議した");
        }

        // ---- 従属の不履行：証拠・当主の認識・責任と処遇を分ける ----
        foreach(var v in (s.Politics??[]).Where(e=>e.Kind=="violation"&&e.At==now&&e.House!=null&&e.Other!=null))
        {
            var lord=v.House!.Value;var vassal=v.Other!.Value;if(Player(w,lord.Value)||Player(w,vassal.Value))continue;
            var witness=houses.GetValueOrDefault(lord.Value)?.Posts.FirstOrDefault(p=>p.Post==Domestic2.Inspector||p.Post==Domestic2.Envoy)?.Person??HouseSuccession.Head(w,lord);
            long days=Duties2.Route(w,s,vassal.Value,HomeOf(w,vassal.Value),HomeOf(w,lord.Value),0,now).Days;
            var opener=Duties2.Opener(w,vassal.Value,now);var head=HouseSuccession.Head(w,vassal);bool proxy=opener!=head;
            Add("violation-evidence",lord,vassal,witness,v.Amount,$"{PersonName(w,witness)}が{HouseName(w,vassal)}の不履行を帳簿に記した（証拠）");
            Add("violation-known",lord,vassal,HouseSuccession.Head(w,lord),days,$"{HouseName(w,lord)}の当主が不履行を知るのは{days}日後",now+days*Day);
            Add("violation-responsibility",vassal,lord,proxy?opener:head,proxy?1:0,proxy?$"不履行の責任は当主の不在の間に代わって決めた{PersonName(w,opener)}にあり、当主は家老を罰して処遇の軽減を願う":$"不履行の責任は当主{PersonName(w,head)}にある");
        }

        // ---- 兵糧の援け（遠方から書状で初めて手配する） ----
        foreach(var a in events.Where(e=>e.Kind=="aid-sent"&&e.Arrives is long t&&t<=now&&!events.Any(x=>x.Kind=="aid-arrived"&&x.Text.EndsWith(e.Id))).ToArray())
            if(houses.TryGetValue(a.Other!.Value.Value,out var to)){houses[to.House.Value]=to with{Rice=to.Rice+a.Amount};Add("aid-arrived",a.Other,a.House,null,a.Amount,$"{HouseName(w,a.House!.Value)}からの兵糧{a.Amount}石が{HouseName(w,a.Other!.Value)}に届いた　"+a.Id);}
        foreach(var h in houses.Values.Where(h=>h.Rice<0||h.Deficit>0).OrderBy(h=>h.House.Value,StringComparer.Ordinal).ToArray())
        {
            if(Player(w,h.House.Value)||events.Any(e=>e.Kind is "aid-request"&&e.House==h.House&&now-e.At<180*Day))continue;
            var giver=pacts.Where(p=>p.Kind=="alliance"&&p.Broken==null&&p.From<=now&&now<p.Until&&(p.A==h.House||p.B==h.House)).Select(p=>p.A==h.House?p.B:p.A)
                .Where(x=>!Player(w,x.Value)&&houses.TryGetValue(x.Value,out var g)&&g.Rice>=600).OrderByDescending(x=>houses[x.Value].Rice).ThenBy(x=>x.Value,StringComparer.Ordinal).FirstOrDefault();
            if(giver.Value==null)continue;
            Add("aid-request",h.House,giver,null,300,$"{HouseName(w,h.House)}は書状で{HouseName(w,giver)}に兵糧の援けを求めた");
            var g=houses[giver.Value];houses[giver.Value]=g with{Rice=g.Rice-300};
            long days=Duties2.Route(w,s,giver.Value,HomeOf(w,giver.Value),HomeOf(w,h.House.Value),0,now).Days;
            Add("aid-sent",giver,h.House,null,300,$"{HouseName(w,giver)}は兵糧300石を{HouseName(w,h.House)}へ送った（{days}日）",now+days*Day);
        }
        return s with{Finish=events.ToArray(),Pacts=pacts.Count==0?null:pacts.ToArray(),Campaigns=campaigns.ToArray(),Houses=s.Houses==null?null:houses.Values.OrderBy(h=>Array.FindIndex(s.Houses,x=>x.House==h.House)).ToArray()};
    }

    // ---- 仲裁の抵触 ----
    internal static string? MediationConflict(ContractWorld w,StrategyState s,EntityId winner,EntityId loser,long now)
    {
        if(!On(w))return null;
        foreach(var p in (s.Pacts??[]).Where(p=>p.Kind=="alliance"&&p.Broken==null&&p.From<=now&&now<p.Until&&(p.A==winner||p.B==winner)))
        {
            var third=p.A==winner?p.B:p.A;if(third==loser)continue;
            bool fighting=s.Campaigns.Any(c=>now-c.At<180*Day&&(c.Attacker==third&&c.Defender==loser||c.Attacker==loser&&c.Defender==third));
            if(fighting&&(s.Terms??[]).Where(t=>t.Pact==p.Id).Any(t=>t.Clause=="単独で和睦しない"&&t.Until>now))return $"{HouseName(w,third)}との「単独で和睦しない」の盟約";
        }
        if((s.Coalitions??[]).Any(c=>c.Target==loser&&c.Members.Contains(winner)&&now-c.At<2*Year))return $"{HouseName(w,loser)}への包囲網";
        return null;
    }
    // ---- 登用の例外 ----
    internal static string? RecruitRefusal(ContractWorld w,StrategyState s,string person,EntityId house,long now)
    {
        if(!On(w))return null;var p=FamilyBirth.People(w).Single(x=>x.Id==person);
        if(p.BirthYear is int b&&WorldCalendar.At(w.EpochDate,now).Year-b>=60)return "隠居を望む";
        if(Persons2.On(w)&&Persons2.Aspiration(w,new(person))?.Value=="天下"&&NationalStrategy.Capital(w)!=house)return "天下を志す";
        if((s.Falls??[]).Any(f=>f.By==house&&w.NewGame!.Start.People.Any(x=>x.Id==person&&x.Faction==f.House.Value)))return "家を滅ぼした仇";
        return null;
    }
    // ---- 兵糧攻めの開城 ----
    internal static bool Starved(ContractWorld w,StrategyState s,StrategyCampaign c,long a,long d,long attackerPower,long defenderPower)
    {
        if(!On(w)||!Logistics2.On(w)||HouseSuccession.Head(w,c.Defender)==w.PlayerControl)return false;
        var line=Logistics2.Supply(w,s,c,attackerPower,defenderPower);
        // 守り手の水軍が攻め手を上回れば、水路から米が入って持ちこたえる。
        long mine=Logistics2.Store(w,c.Defender)?.Fleet??0,theirs=Logistics2.Store(w,c.Attacker)?.Fleet??0;bool water=mine>0&&mine>theirs;
        return !water&&line.DefenderNeed>0&&line.DefenderStock<line.DefenderNeed&&a*2>=d*3;
    }
    // ---- 代理の開封：家老がいなければ後見、それもいなければ正室 ----
    internal static EntityId? Proxy(ContractWorld w,string house)
    {
        if(!On(w))return null;var head=HouseSuccession.Head(w,new(house));
        if(Duties2.Events(w).LastOrDefault(e=>e.Kind=="guardian"&&e.House?.Value==house&&!Duties2.Events(w).Any(x=>x.Kind=="guardian-end"&&x.Detail==e.Id))?.Person is {} g)return g;
        return (w.Strategy?.Marriages??[]).Where(m=>m.Man==head&&w.Characters.Any(c=>c.Id==m.Woman&&c.Life==LifeState.Adult)).Select(m=>(EntityId?)m.Woman).LastOrDefault();
    }

    // ---- 本人の務めの返上 ----
    public static readonly string[] ReliefKinds=["返還","代理","延期"];
    internal static void Relief(ContractWorld w,EntityId task,string kind)
    {
        if(!On(w))throw new InvalidDataException("務めの返上はこの世界では使えません");
        if(!ReliefKinds.Contains(kind))throw new InvalidDataException("返還・代理・延期から選んでください");
        var me=w.PlayerControl;long now=w.ElapsedSeconds;var t=w.Tasks.SingleOrDefault(t=>t.Id==task);
        if(t==null||t.Executor!=me||t.Issuer==me||t.Execution is ExecutionPhase.Completed or ExecutionPhase.Stopped or ExecutionPhase.Failed)throw new InvalidDataException("返上できる務めではありません");
        if(PersonalHealth.Active(w,me,now).Length==0)throw new InvalidDataException("傷病のないときは務めを返上できません");
        var s=w.Strategy??throw new InvalidDataException("全国の戦略がありません");
        string text=kind switch{"代理"=>$"傷病のため、{PersonName(w,t.Issuer)}の務めを代わりの者に預けたいと願い出た","延期"=>$"傷病のため、{PersonName(w,t.Issuer)}の務めを癒えるまで待ってほしいと願い出た（癒えたら改めて頼まれる）",_=>$"傷病のため、{PersonName(w,t.Issuer)}の務めを返したいと願い出た"};
        WorldSession.Stop(w,t,"relief:"+kind);
        w.Strategy=s with{Finish=[..s.Finish??[],new($"finish:relief/{Hash(task.Value+"/"+now)}","relief",null,t.Issuer,me,now,w.CommitSequence,0,text){Arrives=null}]};
    }
    internal static void Validate(ContractWorld w,StrategyState s,StrategyState? prior)
    {
        if(s.Finish==null)return;
        if(!On(w))throw new InvalidDataException("最初の完成版の残りの記録は一覧に Finish2 のある世界だけです");
        if(s.Finish.Select(e=>e.Id).Distinct(StringComparer.Ordinal).Count()!=s.Finish.Length)throw new InvalidDataException("残りの記録が重複しています");
        foreach(var e in s.Finish)if(e.At>w.ElapsedSeconds||e.Revision>w.CommitSequence)throw new InvalidDataException("残りの記録が不正です");
        if(prior?.Finish is {} old){var map=s.Finish.ToDictionary(e=>e.Id,StringComparer.Ordinal);foreach(var o in old)if(map.TryGetValue(o.Id,out var n)&&n!=o)throw new InvalidDataException("過去の残りの記録が変更されています");}
    }
}

public sealed partial class WorldSession
{
    public FinishEvent[] FinishEvents(){lock(gate)return Finish2.Events(state);}
    public CommandResult RequestRelief(EntityId task,string kind,long revision)
    {using var _ccRev=ControlContext.At(revision);
        lock(gate)
        {
            var id=new EntityId("request:relief/"+revision);
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
            var trial=ContractJson.Read<ContractWorld>(ContractJson.Write(state));
            try{Finish2.Relief(trial,task,kind);}catch(InvalidDataException e){return new(ResultCode.NotAvailable,id,state.CommitSequence,e.Message);}
            Change(w=>Finish2.Relief(w,task,kind));
            return new(ResultCode.Accepted,id,state.CommitSequence,"務めの"+kind+"を願い出た");
        }
    }
}
