using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// S09D（2026-11-17）：領地と寺社と朝廷の個別の案件。一覧（NationalSystems）に Cases2 のある新規の全国開始だけ（Domestic2・Persons2・Armies2・Politics2 を使う）。
// ・水利の争い：8月の作柄が凶作の国で、隣り合う二つの拠点の村が用水を争う。郡代が調べ（10日）、両方が自分の家の村なら裁定、他家の村となら仮の合意を提案する。
//   裁定・合意は普請（村の協力・人手20人・米・30日・給付）で直して初めて効き、勝った村は不信が減り、負けた村は増える（他家との合意は両方とも減る）。
// ・寺社の案件：本人は寺社の代表者に、寄進・安堵・禁制・免税・通行・取次・仲裁・噂を願い出る。寺社は関係と権益から応じるかを決め、関係の事実が残る。
//   キリスト教：南蛮の港の国（肥前・豊後・薩摩）を持つ商の方針の家は布教を許し、南蛮船の品が増え、その国の寺社との関係が下がる。
// ・惣国と徳政：一揆の拠点が一つの国の城の半分を一年持てば惣国になり、領主の家はその国の年貢を取れない（一揆が収まるまで）。
//   借銀のある家の領地で一揆が起きれば、家は徳政（借銀の棒引き）を出すかを決め、出せば一揆が静まり、商人との関係（次の借銀）が悪くなる。
// ・客将：強い同盟の家は、弱い同盟の家に一年の客将（戦術の高い家臣）を貸し、客将は決めた国の戦だけを率いる。期限で延長を申し入れ、元の家が応じなければ帰る（道のりの日数）。
// ・家督の後の任命の再承認：家督が代わると、新しい当主が役を改めて認め、家督を争った相手の役は解く。
// ・朝廷の官職：国司の名乗りの当主はその国を攻めるときに力が5%増え、京官の当主の官位の願いは通りやすい。
// ・幕府への申請：本人は幕府職（探題など）と栄典を書状で申請し、京に届いた後、将軍と幕府奉行人が家の城の数で決め、返事が本人に届く。
// ・中央の仲裁：この半年に城を二つ以上失った家（操作する人物の家でない）は将軍に仲裁を願い、両家に届いて、攻め手が強すぎなければ和睦し、攻め手は攻城の区画を解く。
public sealed record CaseEvent(string Id,string Kind,string? Site,EntityId? House,EntityId? Other,EntityId? Person,long At,long Revision,long Amount,string Text)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? Arrives {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Detail {get;init;}
}

public static class Cases2
{
    public const string Name=NationalSystems.Cases2;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name)&&Politics2.On(w);
    const long Day=86400,Year=365*Day;
    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"cases-v1/{w.Seed}/{key}")))%1000);
    static string Hash(string s)=>ContractJson.Hash(Encoding.UTF8.GetBytes(s))[..12];
    static string HouseName(ContractWorld w,EntityId h)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==h)?.Name??h.Value;
    static string PersonName(ContractWorld w,EntityId p)=>w.Definitions.Characters.SingleOrDefault(c=>c.Id==p)?.Name??p.Value;
    static string SiteName(ContractWorld w,string site)=>w.Definitions.Sites.SingleOrDefault(s=>s.Id.Value==site)?.Name??site;
    static string HomeOf(ContractWorld w,string house)=>w.NewGame!.Start.Factions.FirstOrDefault(f=>f.Id==house)?.Home??"";
    static bool Player(ContractWorld w,string house)=>HouseSuccession.Head(w,new(house))==w.PlayerControl;
    static string? Province(string site)=>CentralStanding.ProvinceOf(new(site));
    public static readonly string[] TempleRequests=["寄進","安堵","禁制","免税","通行","取次","仲裁","噂"];
    public static readonly string[] ShogunateRequests=["奥州探題","羽州探題","九州探題","御相伴衆","国持衆","准国主","御供衆"];

    public static CaseEvent[] Events(ContractWorld w)=>w.Strategy?.Cases??[];
    // 惣国の国（その国の領主の家は年貢を取れない）。
    public static bool Sokoku(ContractWorld w,string province,long at)=>On(w)&&Events(w).Where(e=>e.Site==province&&(e.Kind=="sokoku"||e.Kind=="sokoku-end")&&e.At<=at).OrderBy(e=>e.At).LastOrDefault()?.Kind=="sokoku";
    // 客将：その国の戦を率いる人（受け入れた家の出陣の大将になる）。
    public static EntityId? Guest(ContractWorld w,string house,string? province,long at)=>!On(w)||province==null?null:
        Events(w).LastOrDefault(e=>e.Kind=="guest"&&e.House?.Value==house&&e.Site==province&&e.At<=at&&at<(e.Arrives??0)&&!Events(w).Any(x=>x.Kind=="guest-return"&&x.Person==e.Person&&x.At>e.At&&x.At<=at))?.Person;
    // 朝廷の官職：当主の名乗りの国司の国。
    static IEnumerable<TitleEntry> TitlesOf(ContractWorld w,EntityId person)
    {
        var names=CentralTitles.Held1560(w,person).Select(h=>h.Office).Concat(CentralTitles.Held(w,person).Select(h=>h.Name)).ToHashSet(StringComparer.Ordinal);
        return CentralTitles.Titles.Where(t=>names.Contains(t.Name));
    }
    public static bool ProvinceTitle(ContractWorld w,EntityId head,string? province)=>On(w)&&province!=null&&TitlesOf(w,head).Any(t=>t.Kind=="kokushi"&&t.Province==province);
    public static bool CourtOffice(ContractWorld w,EntityId head)=>On(w)&&TitlesOf(w,head).Any(t=>t.Kind=="kyokan");
    // 布教を許した家（南蛮船の品が増える）。
    public static bool Mission(ContractWorld w,string house)=>On(w)&&Events(w).Any(e=>e.Kind=="mission"&&e.House?.Value==house);
    internal static (long A,long D) Effective(ContractWorld w,StrategyState s,StrategyCampaign c,long a,long d)
    {
        if(!On(w))return (a,d);
        if(ProvinceTitle(w,HouseSuccession.Head(w,c.Attacker),Province(c.Target.Value)))a=a*105/100;
        // 客将がその国の戦を率いれば、客将の戦術が効く（当主の戦術の代わり）。
        if(Guest(w,c.Attacker.Value,Province(c.Target.Value),c.At) is {} g)
        {int tg=Persons2.Skill(w,g,DevelopmentSkill.Tactics,c.At),th=Persons2.Skill(w,HouseSuccession.Head(w,c.Attacker),DevelopmentSkill.Tactics,c.At);a=a*(1000+(tg-th)*2)/1000;}
        return (a,d);
    }
    // 幕府奉行人：将軍家の成人のうち、文筆と裁許の合計の高い二人。
    public static EntityId[] Bugyonin(ContractWorld w)=>!On(w)?[]:FamilyBirth.People(w).Where(p=>p.Faction==NationalStart.FactionId(NationalStrategy.Shogun)&&w.Characters.Any(c=>c.Id.Value==p.Id&&c.Life==LifeState.Adult)&&SupremeOffice.ShogunAt(w,w.ElapsedSeconds,w.CommitSequence)?.Value!=p.Id)
        .OrderByDescending(p=>Persons2.Skill(w,new(p.Id),DevelopmentSkill.Writing,w.ElapsedSeconds)+Persons2.Skill(w,new(p.Id),DevelopmentSkill.Adjudication,w.ElapsedSeconds)).ThenBy(p=>p.Id,StringComparer.Ordinal).Take(2).Select(p=>new EntityId(p.Id)).ToArray();

    // ---- 月の初め（NationalStrategy.Apply から） ----
    internal static StrategyState Month(ContractWorld w,StrategyState s,Dictionary<string,string?> owners)
    {
        if(!On(w))return s;
        long now=w.ElapsedSeconds,rev=w.CommitSequence;var date=WorldCalendar.At(w.EpochDate,now);int year=date.Year,month=date.Month;
        var events=(s.Cases??[]).ToList();var houses=(s.Houses??[]).ToDictionary(h=>h.House.Value,h=>h,StringComparer.Ordinal);
        var temples=(s.Temples??[]).ToList();var sites=(s.Sites??[]).ToDictionary(x=>x.Site,x=>x,StringComparer.Ordinal);var pacts=(s.Pacts??[]).ToList();var honors=(s.Honors??[]).ToList();var sieges=(s.Sieges??[]).ToList();
        if(month==1)events=events.Where(e=>now-e.At<Year||e.Kind is "sokoku" or "sokoku-end" or "guest" or "mission"||(e.Arrives??0)>now).ToList();
        void Add(string kind,string? site,EntityId? house,EntityId? other,EntityId? person,long amount,string text,long? arrives=null,string? detail=null)=>
            events.Add(new($"case:{kind}/{date:yyyyMM}/{Hash(kind+"/"+site+"/"+house?.Value+"/"+other?.Value+"/"+person?.Value+"/"+events.Count)}",kind,site,house,other,person,now,rev,amount,text){Arrives=arrives,Detail=detail});
        var pm=date.AddMonths(-1);long prevStart=NationalStrategy.MonthStart(w,pm.Year,pm.Month);
        var power=NationalStrategy.Powers(w,owners);var alive=w.Characters.Where(c=>c.Life==LifeState.Adult).Select(c=>c.Id).ToHashSet();
        DomesticSite Get(string site)=>sites.GetValueOrDefault(site)??new(site,0,0,0,0,0,null,0,null,0,0,null,0);

        // 水利の争い：8月の凶作の国で、隣り合う二つの拠点が争う（国ごとに一つ）。
        if(month==8)
            foreach(var g in (s.Domestic??[]).Where(e=>e.Kind=="harvest"&&e.Amount<100&&e.At==now).Select(e=>e.Site!).Distinct().OrderBy(x=>x,StringComparer.Ordinal))
            {
                var pair=owners.Where(x=>x.Value!=null&&Province(x.Key)==g).SelectMany(x=>NationalStrategy.Neighbors(x.Key).Where(n=>Province(n)==g&&owners.GetValueOrDefault(n)!=null&&string.CompareOrdinal(x.Key,n)<0).Select(n=>(A:x.Key,B:n))).OrderBy(p=>p.A,StringComparer.Ordinal).FirstOrDefault();
                if(pair.A==null)continue;string ha=owners[pair.A]!,hb=owners[pair.B]!;
                Add("water",pair.A,new(ha),new(hb),null,0,$"{SiteName(w,pair.A)}と{SiteName(w,pair.B)}の村が用水を争っている",null,pair.B);
            }
        // 水利の争いの進み方：郡代が調べ（10日）、同じ家なら裁定、他家となら仮の合意。普請（30日、人手20・米200・給付20貫）が終われば効く。
        foreach(var e in events.Where(e=>e.Kind=="water"&&!events.Any(x=>x.Kind is "ruling" or "accord"&&x.Detail==e.Id)).ToArray())
        {
            var lord=e.House!.Value;if(Player(w,lord.Value))continue;
            var h=houses.GetValueOrDefault(lord.Value);var intendant=h?.Posts.FirstOrDefault(p=>p.Post==Domestic2.Intendant&&p.Place==Province(e.Site!));
            if(h==null||h.Rice<200||h.Coin<20)continue;
            bool same=e.House==e.Other;var winner=Draw(w,"water/"+e.Id)<500?e.Site!:e.Detail!;
            houses[lord.Value]=h with{Rice=h.Rice-200,Coin=h.Coin-20};
            string who=intendant!=null?$"{PersonName(w,intendant.Person)}（郡代）":HouseName(w,lord);
            Add(same?"ruling":"accord",e.Site,lord,e.Other,intendant?.Person,same?1:0,same?$"{who}が調べ、用水を{SiteName(w,winner)}の村に分ける裁定をした（普請30日）":$"{who}が{HouseName(w,e.Other!.Value)}と、用水を両村で分ける仮の合意を結んだ（普請30日）",now+40*Day,e.Id);
        }
        foreach(var e in events.Where(e=>e.Kind is "ruling" or "accord"&&e.Arrives is long a&&a>prevStart&&a<=now).ToArray())
        {
            var dispute=events.FirstOrDefault(x=>x.Id==e.Detail);if(dispute==null)continue;
            var a=dispute.Site!;var b=dispute.Detail!;
            foreach(var x in new[]{a,b})
            {
                var d=Get(x);bool won=e.Kind=="accord"||e.Text.Contains(SiteName(w,x)+"の村に分ける");
                sites[x]=d with{Agriculture=Math.Max(0,d.Agriculture-10),Distrust=Math.Max(0,d.Distrust+(won?-1:1))};
            }
            Add("water-done",a,e.House,e.Other,e.Person,0,$"{SiteName(w,a)}と{SiteName(w,b)}の用水の普請が終わった");
        }
        // キリスト教：南蛮の港の国を持つ商の方針の家（操作する人物の家でない）は布教を許す。
        foreach(var (key,h) in houses.Where(x=>x.Value.Policy==Domestic2.Trade&&!Player(w,x.Key)).OrderBy(x=>x.Key,StringComparer.Ordinal))
        {
            if(events.Any(e=>e.Kind=="mission"&&e.House?.Value==key))continue;
            var port=owners.Where(x=>x.Value==key&&Province(x.Key) is "肥前" or "豊後" or "薩摩").Select(x=>Province(x.Key)).FirstOrDefault();
            if(port==null||year<1549)continue;
            Add("mission",port,h.House,null,null,0,$"{HouseName(w,h.House)}が{port}で宣教師の布教を許した");
            // その国の寺社との関係が下がる。
            temples=temples.Select(t=>Province(t.Site)==port?t with{Relation=t.Relation-2}:t).ToList();
        }
        // 惣国：一揆の拠点がある国の城の半分以上で、その状態が一年続けば惣国。一揆が半分を下回れば終わる。
        foreach(var g in owners.Where(x=>x.Value!=null&&Province(x.Key)!=null).GroupBy(x=>Province(x.Key)!).OrderBy(g=>g.Key,StringComparer.Ordinal))
        {
            int total=g.Count(),ikki=g.Count(x=>Get(x.Key).Ikki!=null);bool held=ikki*2>=total&&total>=2;bool now2=Sokoku(w,g.Key,now);
            var since=(s.Domestic??[]).Where(e=>e.Kind=="ikki"&&Province(e.Site??"")==g.Key).Select(e=>e.At).DefaultIfEmpty(now).Min();
            if(held&&!now2&&now-since>=Year)Add("sokoku",g.Key,null,null,null,ikki,$"{g.Key}は一揆の惣国となった");
            else if(!held&&now2)Add("sokoku-end",g.Key,null,null,null,0,$"{g.Key}の惣国が崩れた");
        }
        // 惣国の国の年貢は、全国の家の内政の10月の年貢で取れない（Domestic2 が Sokoku を見る）。
        // 徳政：借銀のある家の領地で一揆が起きた月、家は徳政を出すかを決める（借銀が年貢の半分を超えれば出す）。
        foreach(var e in (s.Domestic??[]).Where(e=>e.Kind=="ikki"&&e.At>prevStart&&e.At<=now&&e.House!=null))
        {
            if(!houses.TryGetValue(e.House!.Value.Value,out var h)||h.Debt==0||Player(w,h.House.Value)||events.Any(x=>x.Kind=="tokusei"&&x.House==h.House&&now-x.At<Year))continue;
            long yearly=owners.Where(x=>x.Value==h.House.Value).Sum(x=>Domestic2.Koku(x.Key))*h.TaxRate/100;
            bool issue=h.Debt*2>yearly;
            Add("tokusei",e.Site,h.House,null,null,h.Debt,$"{HouseName(w,h.House)}は一揆の求めに{(issue?"徳政を出し、借銀を棒引きにした":"徳政を出さなかった")}");
            if(issue){houses[h.House.Value]=h with{Debt=0};var d=Get(e.Site!);sites[e.Site!]=d with{Unrest=Math.Max(0,d.Unrest-2),Ikki=null};}
        }
        // 客将：強い同盟の家（2倍以上）は、戦っている弱い同盟の家に、一年の客将を貸す（年に一度）。期限で延長を申し入れ、元の家が応じなければ帰る。
        if(month==4)
            foreach(var p in pacts.Where(p=>p.Kind=="alliance"&&p.Broken==null&&p.From<=now&&now<p.Until).OrderBy(p=>p.Id.Value,StringComparer.Ordinal))
            {
                var (strong,weak)=power.GetValueOrDefault(p.A.Value)>=2*power.GetValueOrDefault(p.B.Value)?(p.A,p.B):power.GetValueOrDefault(p.B.Value)>=2*power.GetValueOrDefault(p.A.Value)?(p.B,p.A):(default,default);
                if(strong.Value==null||Player(w,strong.Value)||Player(w,weak.Value))continue;
                var front=s.Campaigns.Where(c=>now-c.At<180*Day&&(c.Attacker==weak||c.Defender==weak)).Select(c=>Province(c.Target.Value)).FirstOrDefault(x=>x!=null);if(front==null)continue;
                if(events.Any(e=>e.Kind=="guest"&&e.House==weak&&(e.Arrives??0)>now))continue;
                var used=houses.GetValueOrDefault(strong.Value)?.Posts.Select(x=>x.Person).ToHashSet()??[];
                var guest=FamilyBirth.People(w).Where(x=>x.Faction==strong.Value&&alive.Contains(new EntityId(x.Id))&&x.Id!=HouseSuccession.Head(w,strong).Value&&!used.Contains(new(x.Id))).OrderByDescending(x=>Persons2.Skill(w,new(x.Id),DevelopmentSkill.Tactics,now)).ThenBy(x=>x.Id,StringComparer.Ordinal).FirstOrDefault();
                if(guest==null)continue;
                Add("guest",front,weak,strong,new(guest.Id),0,$"{HouseName(w,strong)}が{guest.Name}を客将として{HouseName(w,weak)}に貸した（{front}の戦を一年率いる）",now+Year);
            }
        foreach(var e in events.Where(e=>e.Kind=="guest"&&e.Arrives is long a&&a>prevStart&&a<=now).ToArray())
        {
            bool extend=!events.Any(x=>x.Kind=="guest-extend"&&x.Person==e.Person)&&power.GetValueOrDefault(e.Other!.Value.Value)>=3*power.GetValueOrDefault(e.House!.Value.Value);
            if(extend){Add("guest-extend",e.Site,e.House,e.Other,e.Person,0,$"{HouseName(w,e.House!.Value)}の延長の申し入れに{HouseName(w,e.Other!.Value)}が応じ、{PersonName(w,e.Person!.Value)}は半年残る");
                events.Add(e with{Id=e.Id+"/extended",At=now,Revision=rev,Arrives=now+Year/2});continue;}
            long days=Domestic2.Days(HomeOf(w,e.House!.Value.Value),HomeOf(w,e.Other!.Value.Value));
            Add("guest-return",e.Site,e.House,e.Other,e.Person,days,$"{PersonName(w,e.Person!.Value)}が客将の務めを終え、{HouseName(w,e.Other!.Value)}へ帰った",now+days*Day);
        }
        // 家督の後の任命の再承認：前の月に家督が代わった家は、役を改めて認め、家督を争った相手の役は解く。
        foreach(var x in (s.Successions??[]).Where(x=>x.At>prevStart&&x.At<=now))
        {
            if(!houses.TryGetValue(x.House.Value,out var h)||Player(w,x.House.Value))continue;
            var rivals=(s.Disputes??[]).Where(d=>d.House==x.House&&d.Revision==x.Revision).SelectMany(d=>d.Claimants).Where(c=>c!=x.Heir).ToHashSet();
            var keep=h.Posts.Where(p=>!rivals.Contains(p.Person)).ToArray();
            houses[x.House.Value]=h with{Posts=keep};
            Add("reconfirm",null,x.House,null,x.Heir,keep.Length,$"{PersonName(w,x.Heir)}が家中の役を改めて認めた{(keep.Length<h.Posts.Length?$"（家督を争った{h.Posts.Length-keep.Length}人の役を解いた）":"")}");
        }
        // 中央の仲裁：この半年に城を二つ以上失った家は、取次がいれば将軍に仲裁を願う。両家に届き、攻め手が3倍以上強くなければ和睦し、攻城の区画を解く。
        if(SupremeOffice.ShogunAt(w,now,rev) is {} shogun&&shogun!=w.PlayerControl)
        {
            var kyoto=CentralCourtCatalog.KyotoIn(w).Value;
            foreach(var g in s.Outcomes.Where(o=>o.Captured&&now-o.At<180*Day).Select(o=>s.Campaigns.Single(c=>c.Id==o.Campaign)).GroupBy(c=>(c.Defender,c.Attacker)).Where(g=>g.Count()>=2).OrderBy(g=>g.Key.Defender.Value,StringComparer.Ordinal))
            {
                var (loser,winner)=g.Key;if(Player(w,loser.Value)||Player(w,winner.Value)||!owners.ContainsValue(loser.Value))continue;
                if(houses.GetValueOrDefault(loser.Value)?.Posts.Any(p=>p.Post==Domestic2.Envoy)!=true||events.Any(e=>e.Kind=="mediation"&&e.House==loser&&now-e.At<Year))continue;
                bool ok=power.GetValueOrDefault(winner.Value)<3*power.GetValueOrDefault(loser.Value);
                // 既存の盟約・包囲網との抵触（一覧の Finish2）：背くなら理由を添えて断る。
                var conflict=Finish2.MediationConflict(w,s,winner,loser,now);if(conflict!=null)ok=false;
                // 書状の道（一覧の Duties2）：将軍の案は敵の領地を避けて両家へ届く。
                long arrive=now+(Duties2.On(w)?Math.Max(Duties2.Route(w,s,NationalStart.FactionId(NationalStrategy.Shogun),kyoto,HomeOf(w,loser.Value),0,now).Days,Duties2.Route(w,s,NationalStart.FactionId(NationalStrategy.Shogun),kyoto,HomeOf(w,winner.Value),0,now).Days):Math.Max(Domestic2.Days(kyoto,HomeOf(w,loser.Value)),Domestic2.Days(kyoto,HomeOf(w,winner.Value))))*Day;
                Add("mediation",HomeOf(w,loser.Value),loser,winner,shogun,ok?1:0,$"{HouseName(w,loser)}の願いで将軍が{HouseName(w,winner)}との仲裁に入り、{(ok?"両家は和睦した":HouseName(w,winner)+"は応じなかった"+(conflict!=null?"（"+conflict+"に背くため）":""))}",arrive);
                if(!ok)continue;
                if(!pacts.Any(p=>p.Broken==null&&p.From<=now&&now<p.Until&&(p.A==loser&&p.B==winner||p.A==winner&&p.B==loser)))
                    pacts.Add(new(new($"pact:national/{date:yyyyMM}/{Hash(loser.Value+"/"+winner.Value+"/mediation")}"),loser,winner,"truce",now,now+Year,rev,"mediation:shogun"));
                int before=sieges.Count;sieges.RemoveAll(x=>x.Attacker==winner&&owners.GetValueOrDefault(x.Site)==loser.Value);
                if(sieges.Count<before)Add("withdraw",HomeOf(w,loser.Value),winner,loser,null,before-sieges.Count,$"{HouseName(w,winner)}は和睦に従い、{HouseName(w,loser)}の城の囲みを解いた");
            }
            // 幕府への申請（本人）：京に届いた願いを、将軍と幕府奉行人が家の城の数で決め、返事が本人に届く。
            foreach(var e in events.Where(e=>e.Kind=="shogunate-ask"&&e.Arrives is long a&&a>prevStart&&a<=now).ToArray())
            {
                var house=e.House!.Value;int castles=owners.Count(x=>x.Value==house.Value);
                int need=e.Detail is "奥州探題" or "羽州探題" or "九州探題"?8:e.Detail is "御相伴衆"?10:e.Detail is "国持衆" or "准国主"?5:2;
                bool grant=castles>=need;var bug=Bugyonin(w);
                long back=now+Domestic2.Days(kyoto,HomeOf(w,house.Value))*Day;
                Add("shogunate-answer",null,house,null,e.Person,grant?1:0,$"将軍{(bug.Length>0?"（奉行人 "+string.Join("・",bug.Select(b=>PersonName(w,b)))+"）":"")}は{e.Detail}の願いを{(grant?"許した":"退けた")}",back,e.Detail);
                if(grant)honors.Add(new(house,e.Person!.Value,e.Detail!,shogun,null,now,rev));
            }
        }
        return s with{Cases=events.Count==0?null:events.ToArray(),Houses=houses.Count==0?s.Houses:houses.Values.OrderBy(h=>h.House.Value,StringComparer.Ordinal).ToArray(),
            Sites=sites.Count==0?null:sites.Values.OrderBy(x=>x.Site,StringComparer.Ordinal).ToArray(),Pacts=pacts.Count==0?null:pacts.ToArray(),Honors=honors.Count==0?null:honors.ToArray(),Sieges=sieges.Count==0?null:sieges.ToArray(),Temples=temples.Count==0?s.Temples:temples.ToArray()};
    }

    // ---- 本人の願い出 ----
    // 寺社の案件：本人のいる拠点の寺社（または本人の家の領地の大寺社）に願い出る。寺社は関係（寄進・安堵で上がる）と権益から応じるかを決める。
    public static void AskTemple(ContractWorld w,EntityId actor,string temple,string request)
    {
        if(!On(w)||!TempleRequests.Contains(request))throw new InvalidDataException("寺社への願いを確かめてください");
        var s=w.Strategy??=new StrategyState(1,NationalStrategy.Policy,[],[]);
        var t=Domestic2.Temples(w).FirstOrDefault(x=>x.Temple==temple)??throw new InvalidDataException("その寺社はありません");
        var house=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==actor.Value)?.Faction;bool head=house!=null&&HouseSuccession.Head(w,new(house))==actor;
        if(request is "安堵" or "禁制" or "免税"&&!(head&&t.Lord?.Value==house))throw new InvalidDataException("寺領の安堵・禁制・免税は、その寺社の領主の当主が出します");
        int rel=t.Relation+Events(w).Count(e=>e.Kind=="temple-case"&&e.Site==temple&&e.Person==actor&&e.Amount==1&&e.Detail is "寄進" or "安堵" or "禁制" or "免税");
        bool yes=request switch{"寄進" or "安堵" or "禁制" or "免税"=>true,"通行" or "噂"=>rel>=1,"取次"=>rel>=3,"仲裁"=>rel>=4,_=>false};
        long now=w.ElapsedSeconds,rev=w.CommitSequence;
        string text=request switch{
            "寄進"=>$"{temple}に寄進した",
            "安堵"=>$"{temple}の寺領を安堵した",
            "禁制"=>$"{temple}に禁制を与えた",
            "免税"=>$"{temple}の寺領の年貢を免じた",
            "通行"=>yes?$"{temple}が寺領の通行を許した":$"{temple}は寺領の通行を断った",
            "噂"=>yes?$"{temple}の僧が遠国の噂を教えた":$"{temple}の僧は口を閉ざした",
            "取次"=>yes?$"{temple}が朝廷への取次を引き受けた":$"{temple}は朝廷への取次を断った",
            _=>yes?$"{temple}が和睦の仲裁を引き受けた":$"{temple}は仲裁を断った"};
        w.Strategy=s with{Cases=[..s.Cases??[],new($"case:temple/{Hash(actor.Value+"/"+temple+"/"+request+"/"+now+"/"+rev)}","temple-case",temple,house!=null?new(house):null,t.Lord,actor,now,rev,yes?1:0,text){Detail=request}]};
    }
    // 幕府への申請：本人（当主）が幕府職・栄典を書状で願う。京に届くまで道のりの日数がかかる。
    public static void AskShogunate(ContractWorld w,EntityId actor,string post)
    {
        if(!On(w)||!ShogunateRequests.Contains(post))throw new InvalidDataException("願える幕府職・栄典を確かめてください");
        var house=w.NewGame!.Start.Factions.Select(f=>new EntityId(f.Id)).FirstOrDefault(h=>HouseSuccession.Head(w,h)==actor);
        if(house.Value==null)throw new InvalidDataException("当主だけが幕府に願えます");
        if(Events(w).Any(e=>e.Kind=="shogunate-ask"&&e.Person==actor&&e.Detail==post&&w.ElapsedSeconds-e.At<Year))throw new InvalidDataException("同じ願いの返事を待っています");
        var s=w.Strategy??=new StrategyState(1,NationalStrategy.Policy,[],[]);long now=w.ElapsedSeconds,rev=w.CommitSequence;
        long arrive=now+Domestic2.Days(HomeOf(w,house.Value),CentralCourtCatalog.KyotoIn(w).Value)*Day;
        w.Strategy=s with{Cases=[..s.Cases??[],new($"case:shogunate/{Hash(actor.Value+"/"+post+"/"+now+"/"+rev)}","shogunate-ask",null,house,null,actor,now,rev,0,$"{post}を幕府に願った"){Arrives=arrive,Detail=post}]};
    }

    public static DomesticRumor[] Heard(ContractWorld w,EntityId viewer,long at)
    {
        if(!On(w))return [];var rows=new List<DomesticRumor>();var mine=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==viewer.Value)?.Faction;
        foreach(var e in Events(w))
        {
            bool own=e.House?.Value==mine||e.Other?.Value==mine||e.Person==viewer;
            if(!own&&e.Kind is "temple-case" or "shogunate-ask" or "reconfirm")continue;
            string? where=e.Site!=null&&e.Site.StartsWith("site:",StringComparison.Ordinal)?e.Site:e.House is {} h?HomeOf(w,h.Value):null;
            long learned=own&&e.Arrives is long a&&e.Kind is "shogunate-answer" or "mediation" or "guest-return"?a:own?e.At:where is {Length:>0}?e.At+HistoricalNews.DelayFrom(w,viewer,new(where),e.At):e.At+Day;
            if(learned<=at)rows.Add(new(e.Id,e.Text,e.At,learned,e.House));
        }
        return rows.OrderBy(r=>r.LearnedAt).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
    }
    internal static IEnumerable<PersonalChronicleRow> ChronicleRows(ContractWorld w,EntityId viewer)=>
        Heard(w,viewer,w.ElapsedSeconds).Select(r=>new PersonalChronicleRow("cases:"+r.Id,"領地と寺社",r.Text,r.OccurredAt,r.LearnedAt,"噂",r.House?.Value));
    internal static void Validate(ContractWorld w,StrategyState s,StrategyState? prior)
    {
        if(s.Cases==null)return;
        if(!On(w))throw new InvalidDataException("領地と寺社と朝廷の個別の案件は一覧に Cases2 のある世界だけです");
        if(s.Cases.Select(e=>e.Id).Distinct(StringComparer.Ordinal).Count()!=s.Cases.Length)throw new InvalidDataException("案件の記録が重複しています");
    }
}

public sealed partial class WorldSession
{
    // 本人の寺社への願い・幕府への申請。
    public CommandResult AskTemple(string temple,string request,long revision)=>CaseCommand("temple/"+temple+"/"+request,revision,w=>Cases2.AskTemple(w,w.PlayerControl,temple,request));
    public CommandResult AskShogunate(string post,long revision)=>CaseCommand("shogunate/"+post,revision,w=>Cases2.AskShogunate(w,w.PlayerControl,post));
    CommandResult CaseCommand(string key,long revision,Action<ContractWorld> act)
    {
        lock(gate)
        {
            var id=new EntityId("request:case/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(key+"/"+revision))[..16]);
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
            var trial=ContractJson.Read<ContractWorld>(ContractJson.Write(state));
            try{act(trial);}catch(InvalidDataException e){return new(ResultCode.NotAvailable,id,state.CommitSequence,e.Message);}
            Change(act);
            return new(ResultCode.Accepted,id,state.CommitSequence,"願い出た");
        }
    }
}
