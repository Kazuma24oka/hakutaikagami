using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

// S09C（2026-11-17）：外交・家中・国人の交渉。一覧（NationalSystems）に Politics2 のある新規の全国開始だけ（Domestic2・Persons2・Armies2 を使う）。
// ・約束の条項：同盟には「共同の出陣」「不可侵」「単独で和睦しない」の条項と期限を持たせ、力が3倍以上に開けば強い側が従属への変更を求め、弱い側が受けるか断るかを決める。
// ・NPCからの申し入れ：共通の強い隣を持つ家は、本人の家にも同盟を書状で申し入れる。本人は受けるか断るかを決める（WorldSession.AnswerProposal）。
// ・多者の和睦：同じ地方で三家以上が互いに戦っていれば、将軍が和睦を呼びかけ、書状が届いた家ごとに受けるかを決め、受けた家どうしが和睦する。
// ・官位の願い：取次と千貫の銭のある家の当主は、朝廷へ官位を願い、京からの道のりの後に従五位下を許される。
// ・制裁の現地履行：裏切られた家の同盟の家は、裏切った家との隊商を通さず、裏切った家の銭の入りは2年のあいだ毎月5%減る。
// ・従属の条項：従う家は毎年1月に主へ銭を納め（自治の強い家は5%、ほかは10%）、納められなければ違反。主の加勢の命を断った（Armies2 の競合で約束を守った）家も違反。
//   主は違反を知ると、自分が3倍以上強ければ従属を解いて攻めの的にし、ほかは抗議する。
// ・婚姻：同盟を結んだ家に成人の未婚の男女がいなければ、未成年どうしを婚約させ、15歳になれば輿入れし、道のりの日数の後に夫婦になる。
// ・養子：成人の後継の無い家は、強い同盟の家の当主の子（跡継ぎでない男子）を養子に迎え、当主が亡くなれば家督を継ぐ。
// ・独立の伝達：独立の宣言は元の家の軍団長に道のりの日数で届き、軍団長は自分と当主の関係（一族か）で付く家を決める。付いた兵は独立した家の力になる。
// ・派閥：家中の一門・譜代と外様の支持（%）を持ち、勝てば譜代、給付を削れば外様に動く。外様が6割を超え当主が若いか病なら、従う家が離反しやすい。
// ・辞去と転籍：給付を削られた家・裏切りの悪名の主の家臣は、書状で辞去し、主のない人になる。主のない人は、評判の悪い家と給付を削った家の誘いには応じない。
// ・捕虜の交換：互いの家の者を捕らえている二つの家は交換を取り決め、境で受け渡し、それぞれ本拠へ戻る。
// ・偵察と流言：境の家の力は、偵察（目付の技能）で見立て、誤差がある（出陣の判断はこの見立てを使う）。軍備の家は敵の家中に流言を流し、外様の支持を動かす。
public sealed record PactTerm(EntityId Pact,string Clause,long Until);
public sealed record HouseFactions(EntityId House,int Kin,int Outsiders,long At);
public sealed record PoliticsEvent(string Id,string Kind,string? Site,EntityId? House,EntityId? Other,EntityId? Person,long At,long Revision,long Amount,string Text)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? Arrives {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public EntityId? Person2 {get;init;}
}

public static class Politics2
{
    public const string Name=NationalSystems.Politics2;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name)&&Armies2.On(w);
    const long Day=86400,Year=365*Day;
    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"politics-v1/{w.Seed}/{key}")))%1000);
    static string Hash(string s)=>ContractJson.Hash(Encoding.UTF8.GetBytes(s))[..12];
    static string HouseName(ContractWorld w,EntityId h)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==h)?.Name??h.Value;
    static string PersonName(ContractWorld w,EntityId p)=>w.Definitions.Characters.SingleOrDefault(c=>c.Id==p)?.Name??p.Value;
    static string HomeOf(ContractWorld w,string house)=>w.NewGame!.Start.Factions.FirstOrDefault(f=>f.Id==house)?.Home??"";
    static bool Player(ContractWorld w,string house)=>HouseSuccession.Head(w,new(house))==w.PlayerControl;
    public static readonly string[] Clauses=["共同の出陣","不可侵","単独で和睦しない"];

    public static PoliticsEvent[] Events(ContractWorld w)=>w.Strategy?.Politics??[];
    public static PactTerm[] Terms(ContractWorld w,EntityId pact)=>(w.Strategy?.Terms??[]).Where(t=>t.Pact==pact).ToArray();
    public static HouseFactions? Factions(ContractWorld w,EntityId house)=>w.Strategy?.Factions?.LastOrDefault(f=>f.House==house);

    // ---- 見立て（出陣の判断に使う） ----
    // 相手の力の見立て：年ごとに決まる誤差（±20%）を、自分の家の目付の技能で小さくする。
    public static long Seen(ContractWorld w,string viewer,string other,long truth)
    {
        if(!On(w))return truth;int year=WorldCalendar.At(w.EpochDate,w.ElapsedSeconds).Year;
        int err=Draw(w,$"seen/{viewer}/{other}/{year}")%41-20;
        int eye=eyes.GetValue((object?)w.Strategy??w.NewGame!,_=>new()).GetOrAdd(viewer,v=>Domestic2.House(w,new(v))?.Posts.FirstOrDefault(p=>p.Post==Domestic2.Inspector) is {} insp?Math.Min(80,Persons2.Skill(w,insp.Person,DevelopmentSkill.IntelligenceGathering,w.ElapsedSeconds)):0);
        err=err*(100-eye)/100;
        return Math.Max(1,truth*(100+err)/100);
    }
    // 家ごとの目付の技能（全国の戦略の状態ごとに一度だけ求める）。
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object,System.Collections.Concurrent.ConcurrentDictionary<string,int>> eyes=new();
    // 制裁：裏切った家に対して、裏切られた家の同盟の家が、2年のあいだ隊商を通さない。
    public static bool Sanctioning(ContractWorld w,string a,string b,long at)=>On(w)&&Events(w).Any(e=>e.Kind=="sanction"&&at-e.At<2*Year&&(e.House?.Value==a&&e.Other?.Value==b||e.House?.Value==b&&e.Other?.Value==a));
    // 養子の後継。
    public static EntityId? AdoptedHeir(ContractWorld w,EntityId house)=>!On(w)?null:Events(w).LastOrDefault(e=>e.Kind=="adopt"&&e.House==house)?.Person;
    // 交換で放たれた人。
    public static bool Released(ContractWorld w,EntityId person,long capturedRevision,long at,long rev)=>On(w)&&Events(w).Any(e=>e.Kind=="exchange"&&(e.Person==person||e.Person2==person)&&e.Revision>=capturedRevision&&(e.At<at||e.At==at&&e.Revision<rev));
    // 外様の派閥が強く当主が若いか病の主からは、従う家が離反しやすい（離反の確率 10%→15%）。
    public static int DefectPerMille(ContractWorld w,string overlord)
    {
        if(!On(w)||Factions(w,new(overlord)) is not {} f||f.Outsiders<=60)return 100;
        var head=HouseSuccession.Head(w,new(overlord));int year=WorldCalendar.At(w.EpochDate,w.ElapsedSeconds).Year;
        bool weak=FamilyBirth.People(w).SingleOrDefault(p=>p.Id==head.Value)?.BirthYear is int b&&year-b<20||MortalSuccession.On(w)&&MortalSuccession.Incapacitated(w,head,w.ElapsedSeconds);
        return weak?150:100;
    }
    // 独立した家に付いた兵。
    internal static long Joined(StrategyState s,EntityId house,long at)=>(s.Politics??[]).Where(e=>e.Kind=="allegiance"&&e.House==house&&e.At<=at&&at-e.At<Year).Sum(e=>e.Amount);

    // ---- 月の初め（NationalStrategy.Apply から） ----
    internal static StrategyState Month(ContractWorld w,StrategyState s,Dictionary<string,string?> owners)
    {
        if(!On(w))return s;
        long now=w.ElapsedSeconds,rev=w.CommitSequence;var date=WorldCalendar.At(w.EpochDate,now);int year=date.Year,month=date.Month;
        var events=(s.Politics??[]).ToList();var terms=(s.Terms??[]).ToList();var factions=(s.Factions??[]).ToList();var pacts=(s.Pacts??[]).ToList();
        var houses=(s.Houses??[]).ToDictionary(h=>h.House.Value,h=>h,StringComparer.Ordinal);var defections=(s.Defections??[]).ToList();
        var persons=(s.Persons??[]).ToList();var marriages=(s.Marriages??[]).ToList();var honors=(s.Honors??[]).ToList();
        if(month==1)events=events.Where(e=>now-e.At<Year||e.Kind is "adopt" or "exchange" or "sanction" or "betroth" or "proposal"||e.Arrives is long a&&a>now).ToList();
        void Add(string kind,string? site,EntityId? house,EntityId? other,EntityId? person,long amount,string text,long? arrives=null,EntityId? person2=null)=>
            events.Add(new($"politics:{kind}/{date:yyyyMM}/{Hash(kind+"/"+site+"/"+house?.Value+"/"+other?.Value+"/"+person?.Value+"/"+events.Count)}",kind,site,house,other,person,now,rev,amount,text){Arrives=arrives,Person2=person2});
        var pm=date.AddMonths(-1);long prevStart=NationalStrategy.MonthStart(w,pm.Year,pm.Month);
        var power=NationalStrategy.Powers(w,owners);var alive=w.Characters.Where(c=>c.Life==LifeState.Adult).Select(c=>c.Id).ToHashSet();
        var people=FamilyBirth.People(w).GroupBy(p=>p.Id,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.Ordinal);
        int Age(string person)=>people.GetValueOrDefault(person)?.BirthYear is int b?year-b:30;
        string? Sex(string person)=>NationalStart.DbPerson(person)?.Sex;
        bool Live(string house)=>owners.ContainsValue(house);
        bool Active(StrategyPact p)=>p.Broken==null&&p.From<=now&&now<p.Until;

        // 約束の条項：新しい同盟に三つの条項（共同の出陣は1年、ほかは約束の期限まで）。
        foreach(var p in pacts.Where(p=>p.Kind=="alliance"&&p.From>prevStart&&p.From<=now&&!terms.Any(t=>t.Pact==p.Id)))
            foreach(var c in Clauses)terms.Add(new(p.Id,c,c=="共同の出陣"?Math.Min(p.Until,p.From+Year):p.Until));
        // 条件の変更：力が3倍以上に開いた同盟は、強い側が従属への変更を求める。弱い側は、強い側が隣にいれば受け（同盟を解いて従属）、ほかは断る。
        for(int i=0;i<pacts.Count;i++)
        {
            var p=pacts[i];if(p.Kind!="alliance"||!Active(p)||Player(w,p.A.Value)||Player(w,p.B.Value))continue;
            long pa=power.GetValueOrDefault(p.A.Value),pb=power.GetValueOrDefault(p.B.Value);
            if(pa<pb*3&&pb<pa*3)continue;var strong=pa>pb?p.A:p.B;var weak=pa>pb?p.B:p.A;
            if(events.Any(e=>e.Kind=="revise"&&e.House==strong&&e.Other==weak&&now-e.At<Year))continue;
            bool border=owners.Where(x=>x.Value==weak.Value).Any(x=>NationalStrategy.Neighbors(x.Key).Any(n=>owners.GetValueOrDefault(n)==strong.Value));
            Add("revise",null,strong,weak,null,border?1:0,$"{HouseName(w,strong)}が{HouseName(w,weak)}に従属への変更を求め、{HouseName(w,weak)}は{(border?"受けた":"断った")}");
            if(border){pacts[i]=p with{Broken=now};Add("vassal",null,weak,strong,null,0,$"{HouseName(w,weak)}が{HouseName(w,strong)}に従った");}
        }
        // 本人の家への同盟の申し入れ：本人の家と境を接し、共通の強い隣を持つ家は、年に一度申し入れる。
        var mineHouse=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==w.PlayerControl.Value)?.Faction;
        if(mineHouse!=null&&Player(w,mineHouse)&&Live(mineHouse))
            foreach(var f in w.NewGame.Start.Factions.Where(f=>f.Id!=mineHouse&&Live(f.Id)).OrderBy(f=>f.Id,StringComparer.Ordinal))
            {
                bool touches=owners.Where(x=>x.Value==f.Id).Any(x=>NationalStrategy.Neighbors(x.Key).Any(n=>owners.GetValueOrDefault(n)==mineHouse));
                if(!touches||events.Any(e=>e.Kind=="proposal"&&e.House?.Value==f.Id&&now-e.At<Year)||pacts.Any(p=>Active(p)&&(p.A.Value==f.Id&&p.B.Value==mineHouse||p.B.Value==f.Id&&p.A.Value==mineHouse)))continue;
                var threat=power.Where(x=>x.Key!=f.Id&&x.Key!=mineHouse&&x.Value>=2*power.GetValueOrDefault(f.Id)&&x.Value>=2*power.GetValueOrDefault(mineHouse)).Select(x=>x.Key).FirstOrDefault(t=>owners.Any(o=>o.Value==t&&NationalStrategy.Neighbors(o.Key).Any(n=>owners.GetValueOrDefault(n)==f.Id)));
                if(threat==null||Draw(w,$"propose/{f.Id}/{year}")>=500)continue;
                long arrive=now+Domestic2.Days(f.Home,HomeOf(w,mineHouse))*Day;
                Add("proposal",f.Home,new(f.Id),new(mineHouse),null,0,$"{HouseName(w,new(f.Id))}から同盟の申し入れ（{HouseName(w,new(threat))}に備えて）",arrive);
                Letter(w,f.Id,mineHouse,$"{HouseName(w,new(threat))}の勢いに備え、両家で盟を結びたし。");
            }
        // 多者の和睦：同じ地方（国の島）で三家以上が、この3か月に互いに攻め合っていれば、将軍が和睦を呼びかける。受けるのは、城を失ったか力が相手より弱い家。
        if(SupremeOffice.ShogunAt(w,now,rev) is {} shogun&&shogun!=w.PlayerControl)
        {
            var wars=s.Campaigns.Where(c=>now-c.At<90*Day&&c.At<now).Select(c=>(A:c.Attacker.Value,B:c.Defender.Value)).Where(x=>!Player(w,x.A)&&!Player(w,x.B)).Distinct().ToArray();
            var members=wars.SelectMany(x=>new[]{x.A,x.B}).GroupBy(x=>x).Where(g=>g.Count()>=2).Select(g=>g.Key).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            if(members.Length>=3&&!events.Any(e=>e.Kind=="peace-call"&&now-e.At<Year))
            {
                Add("peace-call",null,null,null,shogun,members.Length,$"将軍が{string.Join("・",members.Select(m=>HouseName(w,new(m))))}に和睦を呼びかけた");
                var kyoto=CentralCourtCatalog.KyotoIn(w).Value;
                var accept=members.Where(m=>s.Outcomes.Any(o=>o.Captured&&now-o.At<90*Day&&s.Campaigns.Single(c=>c.Id==o.Campaign).Defender.Value==m)||wars.Where(x=>x.A==m||x.B==m).All(x=>power.GetValueOrDefault(m)<power.GetValueOrDefault(x.A==m?x.B:x.A))).ToHashSet(StringComparer.Ordinal);
                foreach(var m in members)Add("peace-answer",HomeOf(w,m),new(m),null,shogun,accept.Contains(m)?1:0,$"{HouseName(w,new(m))}は将軍の和睦の呼びかけを{(accept.Contains(m)?"受けた":"断った")}",now+Domestic2.Days(kyoto,HomeOf(w,m))*Day);
                foreach(var (a,b) in wars.Where(x=>accept.Contains(x.A)&&accept.Contains(x.B)))
                    if(!pacts.Any(p=>Active(p)&&(p.A.Value==a&&p.B.Value==b||p.A.Value==b&&p.B.Value==a)))pacts.Add(new(new($"pact:national/{date:yyyyMM}/{Hash(a+"/"+b+"/multi-peace")}"),new(a),new(b),"truce",now,now+Year,rev,"multi-peace:shogun"));
            }
        }
        // 官位の願い：取次がいて銭が千貫以上の、官位の無い当主は、朝廷へ願い（銭300貫）、道のりの後に従五位下を許される。
        foreach(var (key,h) in houses.OrderBy(x=>x.Key,StringComparer.Ordinal).ToArray())
        {
            var head=HouseSuccession.Head(w,h.House);if(Player(w,key)||!alive.Contains(head)||h.Coin<1000||!h.Posts.Any(p=>p.Post==Domestic2.Envoy))continue;
            if(CentralTitles.Held1560(w,head).Any()||honors.Any(x=>x.Person==head&&x.Kind=="従五位下")||events.Any(e=>e.Kind=="rank-ask"&&e.Person==head))continue;
            // 京官の名乗りの当主（一覧の Cases2）は願いが通りやすい。
            if(Draw(w,$"rank/{head.Value}/{year}")>=(Cases2.CourtOffice(w,head)?100:50))continue;
            var kyoto=CentralCourtCatalog.KyotoIn(w).Value;long arrive=now+Domestic2.Days(HomeOf(w,key),kyoto)*Day;
            houses[key]=h with{Coin=h.Coin-300};Add("rank-ask",HomeOf(w,key),h.House,null,head,300,$"{PersonName(w,head)}が取次を通して朝廷に官位を願った",arrive);
        }
        foreach(var e in events.Where(e=>e.Kind=="rank-ask"&&e.Arrives is long a&&a>prevStart&&a<=now).ToArray())
            if(e.Person is {} p&&alive.Contains(p)&&!honors.Any(x=>x.Person==p&&x.Kind=="従五位下"))honors.Add(new(e.House!.Value,p,"従五位下",p,null,now,rev));
        // 制裁の現地履行：この月に裏切りで破られた約束があれば、裏切られた家の同盟の家が裏切った家を制裁する。制裁された家は銭の入りが減る。
        foreach(var c in s.Campaigns.Where(c=>c.Basis=="betrayal"&&c.At>prevStart&&c.At<=now))
            foreach(var friend in pacts.Where(p=>p.Kind=="alliance"&&p.From<=now&&now<p.Until&&(p.A==c.Defender||p.B==c.Defender)).Select(p=>p.A==c.Defender?p.B:p.A).Where(x=>x!=c.Attacker).Distinct())
                if(!events.Any(e=>e.Kind=="sanction"&&e.House==friend&&e.Other==c.Attacker&&now-e.At<Year))Add("sanction",null,friend,c.Attacker,null,0,$"{HouseName(w,friend)}が裏切った{HouseName(w,c.Attacker)}との往来を止めた");
        foreach(var target in events.Where(e=>e.Kind=="sanction"&&now-e.At<2*Year).Select(e=>e.Other!.Value.Value).Distinct().ToArray())
            if(houses.TryGetValue(target,out var th))houses[target]=th with{Coin=th.Coin*95/100};
        // 従属の条項：毎年1月、従う家は主へ銭を納める。納められない、または主の加勢を断った家は違反。主は3倍以上強ければ従属を解き、ほかは抗議する。
        if(month==1)
            foreach(var f in w.NewGame.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
            {
                if(NationalStrategy.DbOverlord(w,f.Id) is not {} lord||!Live(f.Id)||!Live(lord)||defections.Any(d=>d.House.Value==f.Id&&d.From.Value==lord)||Player(w,f.Id)||Player(w,lord))continue;
                bool autonomous=power.GetValueOrDefault(f.Id)*2>=power.GetValueOrDefault(lord);int rate=autonomous?5:10;
                houses.TryGetValue(f.Id,out var vh);houses.TryGetValue(lord,out var lh);
                long due=(vh?.Coin??0)*rate/100;bool paid=vh!=null&&due>0;
                if(paid&&lh!=null){houses[f.Id]=vh! with{Coin=vh.Coin-due};houses[lord]=lh with{Coin=lh.Coin+due};Add("tribute",null,new(f.Id),new(lord),null,due,$"{HouseName(w,new(f.Id))}が{HouseName(w,new(lord))}に{due}貫を納めた（{(autonomous?"自治の強い従属":"従属")}）");}
                bool refused=Armies2.Events(w).Any(e=>e.Kind=="conflict"&&e.House?.Value==f.Id&&e.Amount==1&&now-e.At<Year);
                if(paid&&!refused)continue;
                string why=!paid?"貢ぎを納めなかった":"加勢の命を断った";
                bool expel=power.GetValueOrDefault(lord)>=3*power.GetValueOrDefault(f.Id);
                Add("violation",null,new(lord),new(f.Id),null,expel?1:0,$"{HouseName(w,new(f.Id))}が{why}。{HouseName(w,new(lord))}は{(expel?"従属を解いて攻めの的とした":"抗議した")}");
                if(expel)defections.Add(new(new(f.Id),new(lord),now,rev));
            }
        // 婚姻：今月の同盟で成人の夫婦ができなかった家は、未成年の男女を婚約させる。15歳になれば輿入れし、道のりの後に夫婦となる。
        foreach(var p in pacts.Where(p=>p.Kind=="alliance"&&p.From>prevStart&&p.From<=now&&!marriages.Any(m=>m.Pact==p.Id)&&!Player(w,p.A.Value)&&!Player(w,p.B.Value)))
        {
            string[] Kids(string house)=>people.Values.Where(x=>x.Faction==house&&alive.Contains(new EntityId(x.Id))==false&&w.Characters.Any(c=>c.Id.Value==x.Id&&c.Life==LifeState.Minor)).Select(x=>x.Id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            var a=Kids(p.A.Value);var b=Kids(p.B.Value);
            var man=a.FirstOrDefault(x=>Sex(x)=="男")??b.FirstOrDefault(x=>Sex(x)=="男");var woman=man==null?null:(a.Contains(man)?b:a).FirstOrDefault(x=>Sex(x)=="女");
            if(man!=null&&woman!=null)Add("betroth",null,p.A,p.B,new(man),0,$"{PersonName(w,new(man))}と{PersonName(w,new(woman))}が婚約した（両家の同盟）",null,new(woman));
        }
        foreach(var e in events.Where(e=>e.Kind=="betroth").ToArray())
        {
            if(events.Any(x=>x.Kind=="wed"&&x.Person==e.Person))continue;
            string man=e.Person!.Value.Value,woman=e.Person2!.Value.Value;if(!alive.Contains(new(man))||!alive.Contains(new(woman)))continue;
            var pact=pacts.FirstOrDefault(p=>(p.A==e.House&&p.B==e.Other||p.A==e.Other&&p.B==e.House)&&p.Kind=="alliance");if(pact==null||pact.Broken!=null)continue;
            long days=Domestic2.Days(HomeOf(w,e.House!.Value.Value),HomeOf(w,e.Other!.Value.Value));
            Add("wed",null,e.House,e.Other,e.Person,days,$"{PersonName(w,new(woman))}が{PersonName(w,new(man))}のもとへ輿入れした",now+days*Day,e.Person2);
        }
        foreach(var e in events.Where(e=>e.Kind=="wed"&&e.Arrives is long a&&a>prevStart&&a<=now).ToArray())
        {
            var pact=pacts.FirstOrDefault(p=>(p.A==e.House&&p.B==e.Other||p.A==e.Other&&p.B==e.House)&&p.Kind=="alliance");
            if(pact!=null&&!marriages.Any(m=>m.Man==e.Person&&m.Woman==e.Person2))marriages.Add(new(pact.Id,e.Person!.Value,e.Person2!.Value,now,rev));
        }
        // 養子：成人の後継の無い家は、強い同盟の家の当主の子（跡継ぎでない男子）を養子に迎える。
        foreach(var f in w.NewGame.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            if(!Live(f.Id)||Player(w,f.Id)||events.Any(e=>e.Kind=="adopt"&&e.House?.Value==f.Id))continue;
            var head=HouseSuccession.Head(w,new(f.Id));if(!alive.Contains(head)||Age(head.Value)<50)continue;
            bool heir=people.Values.Any(x=>x.Faction==f.Id&&x.Id!=head.Value&&x.BirthParents?.Contains(head.Value)==true&&alive.Contains(new EntityId(x.Id)));if(heir)continue;
            var ally=pacts.Where(p=>p.Kind=="alliance"&&Active(p)&&(p.A.Value==f.Id||p.B.Value==f.Id)).Select(p=>p.A.Value==f.Id?p.B.Value:p.A.Value).Where(x=>power.GetValueOrDefault(x)>power.GetValueOrDefault(f.Id)&&!Player(w,x)).OrderByDescending(x=>power.GetValueOrDefault(x)).FirstOrDefault();
            if(ally==null)continue;var allyHead=HouseSuccession.Head(w,new(ally));
            var sons=people.Values.Where(x=>x.BirthParents?.Contains(allyHead.Value)==true&&alive.Contains(new EntityId(x.Id))&&Sex(x.Id)!="女").OrderBy(x=>x.BirthYear??0).ThenBy(x=>x.Id,StringComparer.Ordinal).ToArray();
            if(sons.Length<2)continue;var son=sons[1];
            Add("adopt",null,new(f.Id),new(ally),new(son.Id),0,$"{HouseName(w,new(f.Id))}が{HouseName(w,new(ally))}から{son.Name}を養子に迎えた");
        }
        // 独立の伝達：この月の独立の宣言は、元の家の軍団長へ道のりの日数で届き、一族でない軍団長は独立した家に付く。
        foreach(var d in (w.Declarations??[]).Where(d=>d.At>prevStart&&d.At<=now))
            foreach(var c in Armies2.Corps(w,d.FormerHouse).GroupBy(c=>c.Province).Select(g=>g.Last()).Where(c=>alive.Contains(c.Commander)))
            {
                long days=Domestic2.Days(HomeOf(w,d.FormerHouse.Value),owners.FirstOrDefault(x=>x.Value==d.FormerHouse.Value&&CentralStanding.ProvinceOf(new(x.Key))==c.Province).Key??HomeOf(w,d.FormerHouse.Value));
                bool join=!c.Kin&&Draw(w,$"allegiance/{d.House.Value}/{c.Commander.Value}")<500;
                Add("allegiance",c.Province,join?d.House:d.FormerHouse,join?d.FormerHouse:d.House,c.Commander,join?power.GetValueOrDefault(d.FormerHouse.Value)/10:0,$"{PersonName(w,c.Commander)}（{c.Province}の軍団長）は{(join?HouseName(w,d.House)+"に付いた":HouseName(w,d.FormerHouse)+"に留まった")}",now+days*Day);
            }
        // 派閥：一門・譜代と外様の支持。勝てば譜代、負ければ・給付を削れば外様へ動く。軍備の家の流言は、相手の外様の支持を5動かす。
        foreach(var (key,h) in houses.OrderBy(x=>x.Key,StringComparer.Ordinal))
        {
            var cur=factions.LastOrDefault(f=>f.House.Value==key)??new(h.House,60,40,now);int kin=cur.Kin;
            int won=s.Outcomes.Count(o=>o.At>prevStart&&o.At<=now&&o.Captured&&s.Campaigns.Single(c=>c.Id==o.Campaign).Attacker.Value==key);
            int lost=s.Outcomes.Count(o=>o.At>prevStart&&o.At<=now&&o.Captured&&s.Campaigns.Single(c=>c.Id==o.Campaign).Defender.Value==key);
            kin=Math.Clamp(kin+won*3-lost*5-(h.StipendCut>0?2:0),10,90);
            if(kin!=cur.Kin||!factions.Any(f=>f.House.Value==key))factions.Add(new(h.House,kin,100-kin,now));
        }
        foreach(var (key,h) in houses.Where(x=>x.Value.Policy==Domestic2.Arms&&!Player(w,x.Key)).OrderBy(x=>x.Key,StringComparer.Ordinal))
        {
            var enemy=s.Campaigns.Where(c=>now-c.At<180*Day&&(c.Attacker.Value==key||c.Defender.Value==key)).Select(c=>c.Attacker.Value==key?c.Defender.Value:c.Attacker.Value).Where(x=>houses.ContainsKey(x)).OrderBy(x=>x,StringComparer.Ordinal).FirstOrDefault();
            if(enemy==null||Draw(w,$"rumor/{key}/{date:yyyyMM}")>=200)continue;
            var ef=factions.LastOrDefault(f=>f.House.Value==enemy)??new(new(enemy),60,40,now);
            factions.Add(ef with{Kin=Math.Max(10,ef.Kin-5),Outsiders=Math.Min(90,ef.Outsiders+5),At=now});
            Add("rumor",null,h.House,new(enemy),null,5,$"{HouseName(w,new(enemy))}の家中に「当主は外様を疑っている」という流言が広まった");
        }
        // 偵察：境の相手（この半年に戦った家）を偵察し、見立てを記録する（家中だけが知る）。
        foreach(var (key,h) in houses.Where(x=>x.Value.Posts.Any(p=>p.Post==Domestic2.Inspector)&&!Player(w,x.Key)).OrderBy(x=>x.Key,StringComparer.Ordinal))
        {
            var enemy=s.Campaigns.Where(c=>now-c.At<180*Day&&(c.Attacker.Value==key||c.Defender.Value==key)).Select(c=>c.Attacker.Value==key?c.Defender.Value:c.Attacker.Value).OrderBy(x=>x,StringComparer.Ordinal).FirstOrDefault();
            if(enemy==null||month%3!=0)continue;
            Add("scout",null,h.House,new(enemy),null,Seen(w,key,enemy,power.GetValueOrDefault(enemy)),$"{HouseName(w,h.House)}の目付が{HouseName(w,new(enemy))}の兵を約{Seen(w,key,enemy,power.GetValueOrDefault(enemy))}と見立てた");
        }
        // 辞去と転籍：給付を削った家・裏切りの主の家臣（一族でなく役の無い人）は、月に2%で辞去し、主のない人になる。
        var recruits=Persons2.Recruits(w);
        foreach(var (key,h) in houses.OrderBy(x=>x.Key,StringComparer.Ordinal))
        {
            var head=HouseSuccession.Head(w,h.House);if(Player(w,key))continue;
            bool bad=h.StipendCut>0||s.Campaigns.Any(c=>c.Basis=="betrayal"&&c.Attacker==h.House&&now-c.At<2*Year);if(!bad)continue;
            var posted=h.Posts.Select(p=>p.Person).ToHashSet();
            var who=people.Values.Where(x=>(recruits.TryGetValue(x.Id,out var r)?r:x.Faction)==key&&x.Id!=head.Value&&alive.Contains(new EntityId(x.Id))&&!posted.Contains(new(x.Id))&&x.BirthParents?.Contains(head.Value)!=true&&x.Id!=w.PlayerControl.Value)
                .OrderBy(x=>Draw(w,$"resign/{x.Id}/{date:yyyyMM}")).FirstOrDefault();
            if(who==null||Draw(w,$"resign/{key}/{date:yyyyMM}")>=20)continue;
            long arrive=now+Domestic2.Days(who.Site,HomeOf(w,key))*Day;
            persons.Add(new($"person:resign/{date:yyyyMM}/{Hash(who.Id)}","resign",new(who.Id),null,now,rev,$"{who.Name}が{HouseName(w,h.House)}に書状で暇を請うた"));
            Add("resign",who.Site,h.House,null,new(who.Id),0,$"{who.Name}が{HouseName(w,h.House)}を辞去した（{(h.StipendCut>0?"給付を削られた":"主の裏切り")}）",arrive);
        }
        // 捕虜の交換：互いの家の者を捕らえている二つの家（操作する人物の家でない）は、一人ずつ交換し、境で受け渡して本拠へ戻す。
        var held=BattleCaptivity.AllCaptures(w).Select(c=>c.Person).Distinct().Select(p=>BattleCaptivity.CaptivityAt(w,p,now,rev+1)).Where(c=>c!=null&&!Released(w,c.Person,0,now,rev+1)).Select(c=>c!).ToArray();
        foreach(var x in held.OrderBy(c=>c.Person.Value,StringComparer.Ordinal))
        {
            if(Player(w,x.CaptorOwner.Value)||Player(w,x.FromOwner.Value)||events.Any(e=>e.Kind=="exchange"&&(e.Person==x.Person||e.Person2==x.Person)))continue;
            var y=held.FirstOrDefault(c=>c.CaptorOwner==x.FromOwner&&c.FromOwner==x.CaptorOwner&&!events.Any(e=>e.Kind=="exchange"&&(e.Person==c.Person||e.Person2==c.Person)));if(y==null)continue;
            long days=Domestic2.Days(x.Site.Value,y.Site.Value)/2+1;
            Add("exchange",x.Site.Value,x.FromOwner,x.CaptorOwner,x.Person,days,$"{HouseName(w,x.FromOwner)}と{HouseName(w,x.CaptorOwner)}が捕虜を交換し、{PersonName(w,x.Person)}と{PersonName(w,y.Person)}を境で受け渡した",now+days*Day,y.Person);
        }
        return s with{Politics=events.Count==0?null:events.ToArray(),Terms=terms.Count==0?null:terms.ToArray(),Factions=factions.Count==0?null:factions.ToArray(),Pacts=pacts.Count==0?null:pacts.ToArray(),
            Houses=houses.Count==0?s.Houses:houses.Values.OrderBy(h=>h.House.Value,StringComparer.Ordinal).ToArray(),Defections=defections.Count==0?null:defections.ToArray(),
            Persons=persons.Count==0?null:persons.ToArray(),Marriages=marriages.Count==0?null:marriages.ToArray(),Honors=honors.Count==0?null:honors.ToArray()};
    }
    static void Letter(ContractWorld w,string from,string to,string body)
    {
        var sender=HouseSuccession.Head(w,new(from));var head=HouseSuccession.Head(w,new(to));
        if(w.Characters.SingleOrDefault(x=>x.Id==sender)?.Life!=LifeState.Adult||w.Characters.SingleOrDefault(x=>x.Id==head)?.Life!=LifeState.Adult)return;
        string id=$"message:politics-proposal/{Hash(from+"/"+to+"/"+w.ElapsedSeconds)}";
        MessageFlow.Send(w.Messaging,new MessageEnvelope(id,"proposal:"+from,NationalStrategy.LetterKind,sender.Value,head.Value,body,FamilyBirth.People(w).Single(x=>x.Id==head.Value).Site,w.ElapsedSeconds),w.ElapsedSeconds,1);
        w.SealedMessages=[..w.SealedMessages,new(id)];RegionalCampaign.Bind(w);
    }
    // 本人（当主）が、届いた同盟の申し入れに答える。受ければ二年の同盟。
    public static PoliticsEvent[] Proposals(ContractWorld w,EntityId viewer)=>!On(w)?[]:Events(w).Where(e=>e.Kind=="proposal"&&HouseSuccession.Head(w,e.Other!.Value)==viewer&&e.Arrives<=w.ElapsedSeconds&&!Events(w).Any(x=>x.Kind=="answer"&&x.Site==e.Id)).ToArray();
    public static void Answer(ContractWorld w,EntityId actor,string proposal,bool accept)
    {
        var p=Proposals(w,actor).SingleOrDefault(x=>x.Id==proposal)??throw new InvalidDataException("答えられる申し入れはありません");
        long now=w.ElapsedSeconds,rev=w.CommitSequence;var s=w.Strategy!;var events=(s.Politics??[]).ToList();
        events.Add(new($"politics:answer/{Hash(proposal+"/"+now)}","answer",proposal,p.Other,p.House,actor,now,rev,accept?1:0,$"{HouseName(w,p.House!.Value)}の同盟の申し入れを{(accept?"受けた":"断った")}"));
        var pacts=(s.Pacts??[]).ToList();
        // 約束は今の月の初めの日付で記録する（全国の約束の決まり：月の初め）。
        var d=WorldCalendar.At(w.EpochDate,now);long from=NationalStrategy.MonthStart(w,d.Year,d.Month);
        if(accept)pacts.Add(new(new($"pact:national/{d:yyyyMM}/{Hash(p.House.Value.Value+"/"+p.Other!.Value.Value+"/proposal")}"),p.House.Value,p.Other!.Value,"alliance",from,from+2*Year,rev,"proposal:"+proposal));
        w.Strategy=s with{Politics=events.ToArray(),Pacts=pacts.Count==0?null:pacts.ToArray()};
    }

    public static DomesticRumor[] Heard(ContractWorld w,EntityId viewer,long at)
    {
        if(!On(w))return [];var rows=new List<DomesticRumor>();var mine=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==viewer.Value)?.Faction;
        foreach(var e in Events(w))
        {
            bool own=e.House?.Value==mine||e.Other?.Value==mine||e.Person==viewer||e.Person2==viewer;
            if(!own&&e.Kind is "scout" or "rank-ask" or "proposal")continue;
            string? where=e.Site!=null&&e.Site.StartsWith("site:",StringComparison.Ordinal)?e.Site:e.House is {} h?HomeOf(w,h.Value):null;
            long learned=e.Arrives is long a&&own?a:own?e.At:where is {Length:>0}?e.At+HistoricalNews.DelayFrom(w,viewer,new(where),e.At):e.At+Day;
            if(learned<=at)rows.Add(new(e.Id,e.Text,e.At,learned,e.House));
        }
        return rows.OrderBy(r=>r.LearnedAt).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
    }
    internal static IEnumerable<PersonalChronicleRow> ChronicleRows(ContractWorld w,EntityId viewer)=>
        Heard(w,viewer,w.ElapsedSeconds).Select(r=>new PersonalChronicleRow("politics:"+r.Id,"外交・家中",r.Text,r.OccurredAt,r.LearnedAt,"噂",r.House?.Value));

    internal static void Validate(ContractWorld w,StrategyState s,StrategyState? prior)
    {
        if(s.Politics==null&&s.Terms==null&&s.Factions==null)return;
        if(!On(w))throw new InvalidDataException("外交・家中・国人の交渉は一覧に Politics2 のある世界だけです");
        if(prior?.Terms is {} ot&&!(s.Terms??[]).Take(ot.Length).SequenceEqual(ot))throw new InvalidDataException("過去の約束の条項が変更されています");
        foreach(var f in s.Factions??[])if(f.Kin+f.Outsiders!=100||f.Kin<0)throw new InvalidDataException("派閥の記録が不正です");
    }
}

public sealed partial class WorldSession
{
    // 本人（当主）が同盟の申し入れに答える。
    public CommandResult AnswerProposal(string proposal,bool accept,long revision)
    {
        lock(gate)
        {
            var id=new EntityId("request:proposal/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(proposal+"/"+accept+"/"+revision))[..16]);
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
            var trial=ContractJson.Read<ContractWorld>(ContractJson.Write(state));
            try{Politics2.Answer(trial,trial.PlayerControl,proposal,accept);}catch(InvalidDataException e){return new(ResultCode.NotAvailable,id,state.CommitSequence,e.Message);}
            Change(w=>Politics2.Answer(w,w.PlayerControl,proposal,accept));
            return new(ResultCode.Accepted,id,state.CommitSequence,accept?"同盟を結ぶと返した":"断ると返した");
        }
    }
}
