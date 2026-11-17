using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// S08（2026-11-17）：全国の水運・交易・補給と名物。一覧（NationalSystems）に Logistics2 のある新規の全国開始だけ（全国の家の内政 Domestic2 の帳面を使う）。
// ・名物：通説で知られる茶器と刀を、開始の年の持ち主の家（または堺・博多の商人）と置き場所つきで入れる（通説・要確認の試作の目録）。一つだけの物で、落城で奪われ、
//   当主が勲功の多い家臣に恩賞として与え、商人の手の売り物は、毎年1月に近くの豊かな家が買う。持ち主の履歴を残す。
// ・探物：本人のいる市の人物が名物を探す仕事を出す。本人は名物のある拠点へ行って見つけ（売り物なら預り金で買い）、依頼主のところへ戻って精算する。
// ・水軍：海に面した国の城と水軍の技能から家の水軍を決める。海を渡る出陣（本州・四国・九州・島どうし）は、水軍が足りなければ力が落ち、無ければ渡れない。
//   同盟の家の水軍は護衛に加わり、相手の水軍が上回れば海路の兵糧を封鎖する。
// ・交易船：港の国を持つ家は3か月ごとに交易船を出し、航海の日数のあと銭と品（硝石・南蛮の鉄砲）を持ち帰る。台風の季節は難破しやすく、瀬戸内の水軍の家が通行銭を取るか奪う。
// ・陣への補給：出陣の兵糧（ひと月分）は家の蔵米から持ち出し、本拠から遠い（10日を超える）分は運ぶ荷になる。道のりと荷の量で遅れ（30日を超えれば届く分が減る）、
//   10日ごとに1%が傷み、賊の巣の近くでは1割、海路を封鎖されれば半分が失われる。届いた分だけで戦う。守る城の兵糧（家の蔵米を城の数で分けた分）が尽きかければ守りが弱る。
//   封鎖だけで在庫を消さない。軍馬は飼料の米を食べ、無ければ減る。工房の鉄は武具になり、合戦で傷む。足りなければ力が落ちる。
// ・隊商：3か月ごとに、品（軍馬・鉄砲・金銀・武具）の余る家から、無い家へ商人の隊商が複数の荷を運ぶ。通る家の境で護衛を引き継ぎ、遅れ・傷み・賊は陣への補給と同じ。
//   買い手と戦っている家の領地を通れば荷を差し押さえられる。
public sealed record Treasure(string Id,string Name,string Kind,EntityId? House,EntityId? Person,string Site,bool ForSale,long Price,string Basis,string[] History);
public sealed record LogisticsHouse(EntityId House,long Gear,long Fleet,long At);
public sealed record LogisticsEvent(string Id,string Kind,string? Site,EntityId? House,EntityId? Other,long At,long Revision,long Amount,string Text)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? Arrives {get;init;}
    // 荷（種類=数 を「、」でつないだもの）・名物の ID・仕事の ID。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Cargo {get;init;}
}
public sealed record TreasureJob(string Id,EntityId Issuer,EntityId Worker,string Treasure,string Home,string Target,bool Buy,long Advance,long Fee,string Phase);

public static class Logistics2
{
    public const string Name=NationalSystems.Logistics2;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name)&&Domestic2.On(w);
    const long Day=86400;
    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"logistics-v1/{w.Seed}/{key}")))%1000);
    static string Hash(string s)=>ContractJson.Hash(Encoding.UTF8.GetBytes(s))[..12];
    static string HouseName(ContractWorld w,EntityId h)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==h)?.Name??h.Value;
    static string PersonName(ContractWorld w,EntityId p)=>w.Definitions.Characters.SingleOrDefault(c=>c.Id==p)?.Name??p.Value;
    static string SiteName(ContractWorld w,string site)=>w.Definitions.Sites.SingleOrDefault(s=>s.Id.Value==site)?.Name??site;
    static string? Province(string site)=>CentralStanding.ProvinceOf(new(site));
    static string HomeOf(ContractWorld w,string house)=>w.NewGame!.Start.Factions.FirstOrDefault(f=>f.Id==house)?.Home??"";

    // ---- 名物の目録（通説。持ち主・時期は要確認の試作値） ----
    sealed record TreasureSeed(string Id,string Name,string Kind,string? Holder1467,string? Holder1560,string? Holder1600,long Price,string Basis);
    const string Sakai="site:sakai",Hakata="site:hakata";
    static readonly TreasureSeed[] Seeds=[
        new("dojigiri","童子切安綱","刀","足利将軍家","足利将軍家","松平（徳川）",3000,"通説：足利将軍家の重宝、のち豊臣・徳川へ（要確認）"),
        new("onimaru","鬼丸国綱","刀","足利将軍家","足利将軍家",null,3000,"通説：足利将軍家の重宝（要確認）"),
        new("odenta","大典太光世","刀","足利将軍家","足利将軍家",null,3000,"通説：足利将軍家の重宝（要確認）"),
        new("honebami","骨喰藤四郎","刀","足利将軍家","足利将軍家",null,2500,"通説：足利将軍家の重宝、のち豊臣へ（要確認）"),
        new("mikazuki","三日月宗近","刀","足利将軍家","足利将軍家",null,3000,"通説：足利将軍家の重宝（要確認）"),
        new("tsukumo","九十九髪茄子","茶入","足利将軍家","松永",null,2000,"通説：東山御物、のち松永久秀（1560年頃の持ち主は要確認）"),
        new("hiragumo","平蜘蛛","茶釜",null,"松永",null,1500,"通説：松永久秀の所持（要確認）"),
        new("hatsuhana","初花肩衝","茶入","足利将軍家","@sakai","松平（徳川）",2000,"通説：東山御物、のち堺の商人の手を経て信長・家康へ（要確認）"),
        new("nitta","新田肩衝","茶入",null,"@sakai",null,1500,"通説：堺の商人の手（要確認）"),
        new("narashiba","楢柴肩衝","茶入",null,"@hakata",null,1500,"通説：博多の商人の手、のち秋月・豊臣（要確認）"),
        new("mikazuki-tsubo","三日月の茶壺","茶壺",null,"三好",null,1500,"通説：三好家の所持（要確認）"),
        new("matsushima","松島の茶壺","茶壺",null,"@sakai",null,1200,"通説：堺の商人の手（要確認）"),
    ];
    // 開始の時の名物（持ち主の家の本拠に置く。商人の物は堺・博多の売り物）。
    public static Treasure[] Initial(ContractWorld w)
    {
        int year=NationalSystems.StartYear(w);var rows=new List<Treasure>();
        foreach(var t in Seeds)
        {
            string? holder=year switch{1467=>t.Holder1467,1600=>t.Holder1600,_=>t.Holder1560};if(holder==null)continue;
            if(holder.StartsWith('@')){string site=holder=="@sakai"?Sakai:Hakata;rows.Add(new(t.Id,t.Name,t.Kind,null,null,site,true,t.Price,t.Basis,[$"{year}年　{(site==Sakai?"堺":"博多")}の商人"]));continue;}
            var f=w.NewGame!.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal).FirstOrDefault(f=>f.Name==holder||f.Name.StartsWith(holder,StringComparison.Ordinal));
            if(f==null)continue;
            rows.Add(new(t.Id,t.Name,t.Kind,new(f.Id),null,f.Home,false,t.Price,t.Basis,[$"{year}年　{f.Name}"]));
        }
        return rows.ToArray();
    }
    static readonly HashSet<string> NearSakai=new(["山城","大和","河内","和泉","摂津","近江","丹波","播磨","紀伊","伊勢","伊賀"],StringComparer.Ordinal);
    static readonly HashSet<string> NearHakata=new(["筑前","筑後","豊前","豊後","肥前","肥後"],StringComparer.Ordinal);
    public static Treasure[] Treasures(ContractWorld w)=>!On(w)?[]:w.Strategy?.Treasures??Initial(w);
    public static Treasure[] TreasuresOf(ContractWorld w,EntityId house)=>Treasures(w).Where(t=>t.House==house).ToArray();
    public static LogisticsEvent[] Events(ContractWorld w)=>w.Strategy?.Logistics??[];
    public static LogisticsHouse? Store(ContractWorld w,EntityId house)=>w.Strategy?.Stores?.FirstOrDefault(x=>x.House==house);

    // ---- 水軍と海 ----
    static readonly HashSet<string> Landlocked=new(["甲斐","信濃","飛騨","美濃","近江","大和","山城","上野","下野","伊賀","丹波","美作"],StringComparer.Ordinal);
    static readonly HashSet<string> Shikoku=new(["阿波","讃岐","伊予","土佐"],StringComparer.Ordinal);
    static readonly HashSet<string> Kyushu=new(["筑前","筑後","豊前","豊後","肥前","肥後","日向","大隅","薩摩"],StringComparer.Ordinal);
    static string Island(string? p)=>p==null?"本州":Shikoku.Contains(p)?"四国":Kyushu.Contains(p)?"九州":p is "佐渡" or "隠岐" or "淡路" or "壱岐" or "対馬"?p:"本州";
    public static bool Coastal(string site)=>Province(site) is {} p&&!Landlocked.Contains(p);
    static readonly HashSet<string> Inland=new(["伊予","安芸","備後","周防","備中","讃岐"],StringComparer.Ordinal);
    // 家の水軍：海に面した国の城の数×（1＋家中で一番の水軍の技能/25）。
    public static long Fleet(ContractWorld w,string house,Dictionary<string,string?> owners)
    {
        long coast=owners.Count(x=>x.Value==house&&Coastal(x.Key));if(coast==0)return 0;
        int best=FamilyBirth.People(w).Where(p=>p.Faction==house).Select(p=>CharacterDevelopment.StartLevel(w,new(p.Id),DevelopmentSkill.Navy)).DefaultIfEmpty(0).Max();
        return coast*(25+best)/25;
    }
    // 海を渡る出陣：攻める家の本拠と目標が、別の島（本州・四国・九州・佐渡・隠岐・淡路・壱岐・対馬）。
    public static bool Crossing(ContractWorld w,StrategyCampaign c)=>Island(Province(HomeOf(w,c.Attacker.Value)))!=Island(Province(c.Target.Value));
    static IEnumerable<string> Allies(StrategyState s,string house,long at)=>(s.Pacts??[]).Where(p=>p.Kind=="alliance"&&p.Broken==null&&p.From<=at&&at<p.Until&&(p.A.Value==house||p.B.Value==house)).Select(p=>p.A.Value==house?p.B.Value:p.A.Value);
    static long Escorted(ContractWorld w,StrategyState s,string house,Dictionary<string,string?> owners,long at)=>Fleet(w,house,owners)+Allies(s,house,at).Sum(a=>Fleet(w,a,owners)/2);

    // ---- 陣への補給（合戦の決着の時に NationalStrategy.Settle から。月の初めにも同じ値で記録する） ----
    public sealed record SupplyLine(long Need,long Carried,long Delivered,long Days,long Lost,bool Blockaded,long DefenderStock,long DefenderNeed);
    public static SupplyLine Supply(ContractWorld w,StrategyState s,StrategyCampaign c,long attacker,long defender)
    {
        var owners=NationalStrategy.Owners(w,s);long at=c.At;
        long need=attacker*15/100;var house=s.Houses?.FirstOrDefault(h=>h.House==c.Attacker);
        long carried=house==null?need:Math.Min(need,house.Rice/2);
        // 追跡補給（一覧の Armies2）：実際に動く軍勢へは、その時の軍勢の位置までの日数。
        long days=Armies2.TrackingDays(w,c)??Domestic2.Days(HomeOf(w,c.Attacker.Value),c.Target.Value);
        long far=days>10?carried*(days-10)/days:0,near=carried-far;
        long delay=days+far/2000;long arrive=delay>30?far*30/delay:far;
        long broken=arrive*(delay/10)/100;arrive-=broken;
        bool bandits=Territory2.IsCamp(w,c.Target)||NationalStrategy.Neighbors(c.Target.Value).Any(n=>Territory2.IsCamp(w,new(n)));
        long stolen=bandits?arrive/10:0;arrive-=stolen;
        bool blockaded=Crossing(w,c)&&Escorted(w,s,c.Defender.Value,owners,at)>Escorted(w,s,c.Attacker.Value,owners,at);
        long blocked=blockaded?arrive/2:0;arrive-=blocked;
        var dh=s.Houses?.FirstOrDefault(h=>h.House==c.Defender);long castles=Math.Max(1,owners.Count(x=>x.Value==c.Defender.Value));
        long stock=dh==null?defender:dh.Rice/castles;long dneed=defender*15/100;
        return new(need,carried,near+arrive,delay,(far-arrive-broken-stolen-blocked)+broken+stolen+blocked,blockaded,stock,dneed);
    }
    internal static (long A,long D) Effective(ContractWorld w,StrategyState s,StrategyCampaign c,long a,long d)
    {
        if(!On(w))return (a,d);
        var owners=NationalStrategy.Owners(w,s);
        // 海を渡る：水軍が無ければ渡れず、相手の水軍が倍を超えれば力が半分。
        if(Crossing(w,c))
        {
            long mine=Escorted(w,s,c.Attacker.Value,owners,c.At),theirs=Escorted(w,s,c.Defender.Value,owners,c.At);
            a=mine==0?a*3/10:theirs>mine*2?a/2:a*9/10;
        }
        var line=Supply(w,s,c,a,d);
        if(line.Need>0&&line.Delivered<line.Need)a=a*(60+40*line.Delivered/line.Need)/100;
        if(line.DefenderNeed>0&&line.DefenderStock<line.DefenderNeed)d=d*(70+30*line.DefenderStock/line.DefenderNeed)/100;
        long gear=Store(w,c.Attacker)?.Gear??long.MaxValue;if(gear<a/10)a=a*9/10;
        long dgear=Store(w,c.Defender)?.Gear??long.MaxValue;if(dgear<d/10)d=d*9/10;
        return (a,d);
    }

    // ---- 月の初め（NationalStrategy.Apply から、Domestic2.Month の後） ----
    internal static StrategyState Month(ContractWorld w,StrategyState s,Dictionary<string,string?> owners)
    {
        if(!On(w))return s;
        long now=w.ElapsedSeconds,rev=w.CommitSequence;var date=WorldCalendar.At(w.EpochDate,now);int month=date.Month,year=date.Year;
        var events=(s.Logistics??[]).ToList();
        if(month==1)events=events.Where(e=>now-e.At<365*Day||e.Kind is "treasure" or "job"||e.Arrives is long a&&a>now).ToList();
        void Add(string kind,string? site,EntityId? house,EntityId? other,long amount,string text,long? arrives=null,string? cargo=null)=>
            events.Add(new($"logistics:{kind}/{date:yyyyMM}/{Hash(kind+"/"+site+"/"+house?.Value+"/"+other?.Value+"/"+events.Count)}",kind,site,house,other,now,rev,amount,text){Arrives=arrives,Cargo=cargo});
        var treasures=(s.Treasures??Initial(w)).ToList();
        var houses=(s.Houses??[]).ToDictionary(h=>h.House.Value,h=>h,StringComparer.Ordinal);
        var stores=(s.Stores??[]).ToDictionary(x=>x.House.Value,x=>x,StringComparer.Ordinal);
        // 届く物は、前の月の初めより後から今までに届いたもの（月の初めごとに一度だけ数える）。
        var pm=date.AddMonths(-1);long prevStart=NationalStrategy.MonthStart(w,pm.Year,pm.Month);
        var lastMonth=now-35*Day;var settled=s.Outcomes.Where(o=>o.At>lastMonth&&o.At<=now).ToArray();
        var power=NationalStrategy.Powers(w,owners);
        bool AtWar(string a,string b)=>Politics2.Sanctioning(w,a,b,now)||s.Campaigns.Any(c=>now-c.At<180*Day&&(c.Attacker.Value==a&&c.Defender.Value==b||c.Attacker.Value==b&&c.Defender.Value==a));

        // 名物：落城で奪われる。
        foreach(var o in settled.Where(o=>o.Captured))
        {
            var c=s.Campaigns.Single(x=>x.Id==o.Campaign);
            for(int i=0;i<treasures.Count;i++)if(treasures[i] is var t&&t.Site==c.Target.Value&&t.House==c.Defender)
            {treasures[i]=t with{House=c.Attacker,Person=null,History=[..t.History,$"{year}年　{HouseName(w,c.Attacker)}（落城で）"]};Add("treasure",t.Site,c.Attacker,c.Defender,0,$"{t.Name}が{HouseName(w,c.Attacker)}の手に渡った（落城）",cargo:t.Id);}
        }
        // 名物：家が城を失えば、本拠を取った家の物になる（本拠に置いているため）。持ち主の家が無くなった物はそのまま。
        // 恩賞：12月に、名物を二つ以上持つ家の当主が、勲功の一番多い家臣（3度以上）に一つ与える。
        if(month==12)
            foreach(var g in treasures.Where(t=>t.House!=null&&t.Person==null).GroupBy(t=>t.House!.Value.Value).Where(g=>g.Count()>=2).OrderBy(g=>g.Key,StringComparer.Ordinal))
            {
                var house=new EntityId(g.Key);var head=HouseSuccession.Head(w,house);if(head==w.PlayerControl)continue;
                var who=(s.Merits??[]).Where(m=>m.House==house&&m.Person!=head).GroupBy(m=>m.Person).Where(x=>x.Count()>=3).OrderByDescending(x=>x.Count()).ThenBy(x=>x.Key.Value,StringComparer.Ordinal).Select(x=>x.Key).FirstOrDefault();
                if(who.Value==null||treasures.Any(t=>t.Person==who))continue;
                var gift=g.OrderBy(t=>t.Price).ThenBy(t=>t.Id,StringComparer.Ordinal).First();int i=treasures.IndexOf(gift);
                treasures[i]=gift with{Person=who,History=[..gift.History,$"{year}年　{PersonName(w,who)}（恩賞）"]};Add("treasure",gift.Site,house,who,0,$"{PersonName(w,head)}が{PersonName(w,who)}に{gift.Name}を与えた",cargo:gift.Id);
            }
        // 売り物：毎年1月、銭が値の2倍以上ある家（操作する人物の家でない）のうち、京に近い家（本拠からの日数の短い順）が買う。
        if(month==1)
            for(int i=0;i<treasures.Count;i++)
            {
                var t=treasures[i];if(!t.ForSale)continue;
                // 買い手は、その市の近くの国（堺は畿内と周り、博多は九州の北）に城を持つ家のうち、銭の一番多い家。
                var near=t.Site==Hakata?NearHakata:NearSakai;
                var buyer=houses.Values.Where(h=>h.Coin>=t.Price*2&&HouseSuccession.Head(w,h.House)!=w.PlayerControl&&owners.Any(o=>o.Value==h.House.Value&&Province(o.Key) is {} q&&near.Contains(q))).OrderByDescending(h=>h.Coin).ThenBy(h=>h.House.Value,StringComparer.Ordinal).FirstOrDefault();
                if(buyer==null||Draw(w,$"buy/{t.Id}/{year}")>=300)continue;
                houses[buyer.House.Value]=buyer with{Coin=buyer.Coin-t.Price};
                treasures[i]=t with{House=buyer.House,ForSale=false,Site=HomeOf(w,buyer.House.Value),History=[..t.History,$"{year}年　{HouseName(w,buyer.House)}（{t.Price}貫で）"]};
                Add("treasure",t.Site,buyer.House,null,t.Price,$"{HouseName(w,buyer.House)}が{t.Name}を{t.Price}貫で買った",cargo:t.Id);
            }

        // 家ごと：水軍、武具（工房の鉄）、軍馬の飼料、合戦での武具の傷み、陣の兵糧。
        foreach(var f in w.NewGame!.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            if(!houses.TryGetValue(f.Id,out var h))continue;var house=h.House;
            var st=stores.GetValueOrDefault(f.Id)??new(house,0,0,now);
            long gear=st.Gear+h.Goods.Iron;var goods=h.Goods with{Iron=0};
            long fodder=goods.Horses*5/100;long rice=h.Rice;
            if(rice>=fodder)rice-=fodder;else{goods=goods with{Horses=goods.Horses*9/10};Add("fodder",null,house,null,fodder-rice,$"{HouseName(w,house)}は飼料が足りず軍馬が減った");rice=0;}
            foreach(var o in settled)
            {
                var c=s.Campaigns.Single(x=>x.Id==o.Campaign);if(c.Attacker!=house&&c.Defender!=house)continue;
                long soldiers=o.Battle is {} b?(c.Attacker==house?b.AttackerSoldiers:b.DefenderSoldiers):power.GetValueOrDefault(f.Id)/2;
                gear=Math.Max(0,gear-soldiers/20);
                if(c.Attacker==house)
                {
                    var line=Supply(w,s,c,power.GetValueOrDefault(f.Id),power.GetValueOrDefault(c.Defender.Value));
                    rice=Math.Max(0,rice-line.Carried);
                    if(line.Delivered<line.Need||line.Lost>0)Add("supply",c.Target.Value,house,c.Defender,line.Delivered,$"{HouseName(w,house)}の陣へ兵糧{line.Delivered}石が届いた（要る分{line.Need}石・道中で{line.Lost}石を失う{(line.Blockaded?"・海路を封鎖された":"")}）",cargo:c.Id.Value);
                }
            }
            houses[f.Id]=h with{Rice=rice,Goods=goods};
            stores[f.Id]=st with{Gear=gear,Fleet=Fleet(w,f.Id,owners),At=now};
        }

        // 交易船：3か月ごと（1・4・7・10月）、港の国を持つ家が出す。帰りの月に結果を決める。
        string[] ports=["和泉","筑前","肥前","豊後","薩摩","摂津","越前","出羽","伊勢"];
        string[] nanban=["肥前","豊後","薩摩"];
        foreach(var e in events.Where(e=>e.Kind=="voyage"&&e.Arrives is long a&&a>prevStart&&a<=now).ToArray())
        {
            var house=e.House!.Value;if(!houses.TryGetValue(house.Value,out var h))continue;
            int departed=WorldCalendar.At(w.EpochDate,e.At).Month;
            if(Draw(w,"wreck/"+e.Id)<(departed is >=6 and <=9?150:30)){Add("wreck",e.Site,house,null,0,$"{HouseName(w,house)}の交易船が難破した");continue;}
            long profit=e.Amount;string got="";var g=h.Goods;
            // 瀬戸内を通る船（摂津・和泉と九州の間）は、水軍の家に通行銭を取られるか奪われる。
            var pirate=e.Cargo=="inland"?houses.Keys.Where(x=>x!=house.Value&&owners.Any(o=>o.Value==x&&Province(o.Key) is {} q&&Inland.Contains(q))&&(stores.GetValueOrDefault(x)?.Fleet??0)>=3&&!Allies(s,x,now).Contains(house.Value)).OrderByDescending(x=>stores.GetValueOrDefault(x)?.Fleet??0).ThenBy(x=>x,StringComparer.Ordinal).FirstOrDefault():null;
            if(pirate!=null)
            {
                if(Draw(w,"plunder/"+e.Id)<300){Add("plunder",e.Site,new(pirate),house,profit,$"{HouseName(w,new(pirate))}の水軍が{HouseName(w,house)}の交易船を奪った");houses[pirate]=houses[pirate] with{Coin=houses[pirate].Coin+profit};continue;}
                long toll=profit/10;profit-=toll;houses[pirate]=houses[pirate] with{Coin=houses[pirate].Coin+toll};Add("toll",e.Site,new(pirate),house,toll,$"{HouseName(w,new(pirate))}の水軍が{HouseName(w,house)}の交易船から通行銭{toll}貫を取った");
            }
            if(e.Site!=null&&nanban.Contains(e.Site)&&year>=1543){g=g with{Saltpeter=g.Saltpeter+20,Guns=g.Guns+10};got="・硝石20・鉄砲10";}
            houses[house.Value]=h with{Coin=h.Coin+profit,Goods=g};
            Add("return",e.Site,house,null,profit,$"{HouseName(w,house)}の交易船が帰った（銭{profit}貫{got}）");
        }
        if(month is 1 or 4 or 7 or 10)
            foreach(var (house,h) in houses.OrderBy(x=>x.Key,StringComparer.Ordinal).ToArray())
            {
                var port=owners.Where(x=>x.Value==house&&Province(x.Key) is {} q&&ports.Contains(q)).Select(x=>Province(x.Key)!).OrderBy(q=>q,StringComparer.Ordinal).FirstOrDefault();
                if(port==null||h.Coin<100)continue;
                bool far=nanban.Contains(port)||port is "筑前";long days=far?60:30;
                bool inland=port is "和泉" or "摂津"||far&&Draw(w,$"route/{house}/{date:yyyyMM}")<500;
                houses[house]=h with{Coin=h.Coin-100};
                Add("voyage",port,new(house),null,far?400:180,$"{HouseName(w,new(house))}が{port}から交易船を出した",now+days*Day,inland?"inland":null);
            }

        // 隊商：3か月ごと、品の余る家から無い家へ。通る家の境で護衛を引き継ぐ。
        foreach(var e in events.Where(e=>e.Kind=="caravan"&&e.Arrives is long a&&a>prevStart&&a<=now).ToArray())
        {
            if(e.Other is not {} buyer||!houses.TryGetValue(buyer.Value,out var bh)||e.House is not {} seller)continue;
            var g=bh.Goods;long gear=0;
            foreach(var part in (e.Cargo??"").Split('、',StringSplitOptions.RemoveEmptyEntries)){var kv=part.Split('=');long n=long.Parse(kv[1]);
                g=kv[0] switch{"軍馬"=>g with{Horses=g.Horses+n},"鉄砲"=>g with{Guns=g.Guns+n},"金"=>g with{Gold=g.Gold+n},_=>g};if(kv[0]=="武具")gear+=n;}
            houses[buyer.Value]=bh with{Goods=g,Coin=Math.Max(0,bh.Coin-e.Amount)};
            if(houses.TryGetValue(seller.Value,out var sh))houses[seller.Value]=sh with{Coin=sh.Coin+e.Amount};
            if(gear>0&&stores.TryGetValue(buyer.Value,out var bs))stores[buyer.Value]=bs with{Gear=bs.Gear+gear};
            Add("delivered",null,buyer,seller,e.Amount,$"{HouseName(w,seller)}からの隊商が{HouseName(w,buyer)}に着いた（{e.Cargo}）");
        }
        if(month is 2 or 5 or 8 or 11)
        {
            int sent=0;
            foreach(var (sellerId,sh) in houses.Where(x=>owners.ContainsValue(x.Key)).OrderByDescending(x=>x.Value.Goods.Horses+x.Value.Goods.Guns*5).ThenBy(x=>x.Key,StringComparer.Ordinal).ToArray())
            {
                if(sent>=10)break;
                var buyer=houses.Values.Where(h=>h.House.Value!=sellerId&&owners.ContainsValue(h.House.Value)&&h.Coin>=300&&h.Goods.Horses==0&&!AtWar(sellerId,h.House.Value)&&HouseSuccession.Head(w,h.House)!=w.PlayerControl)
                    .OrderBy(h=>Domestic2.Days(HomeOf(w,sellerId),HomeOf(w,h.House.Value))).ThenBy(h=>h.House.Value,StringComparer.Ordinal).FirstOrDefault();
                if(buyer==null)continue;
                var cargo=new List<(string Kind,long N)>();var g=sh.Goods;
                if(g.Horses>=100)cargo.Add(("軍馬",g.Horses/5));if(g.Guns>=50)cargo.Add(("鉄砲",g.Guns/5));if(g.Gold>=50)cargo.Add(("金",g.Gold/5));
                long sgear=stores.GetValueOrDefault(sellerId)?.Gear??0;if(sgear>=500)cargo.Add(("武具",sgear/5));
                if(cargo.Count==0)continue;
                houses[sellerId]=sh with{Goods=g with{Horses=g.Horses-cargo.Where(x=>x.Kind=="軍馬").Sum(x=>x.N),Guns=g.Guns-cargo.Where(x=>x.Kind=="鉄砲").Sum(x=>x.N),Gold=g.Gold-cargo.Where(x=>x.Kind=="金").Sum(x=>x.N)}};
                if(sgear>=500)stores[sellerId]=stores[sellerId] with{Gear=sgear-sgear/5};
                // 道のり：本拠から本拠へ、城の網をたどる。通る家の境で護衛を引き継ぐ。
                var path=Path(HomeOf(w,sellerId),HomeOf(w,buyer.House.Value));
                var through=path.Select(x=>owners.GetValueOrDefault(x)).Where(x=>x!=null&&x!=sellerId&&x!=buyer.House.Value).Select(x=>x!).Distinct(StringComparer.Ordinal).ToArray();
                long days=Math.Max(1,path.Count)+cargo.Sum(x=>x.N)/500;
                var seizer=through.FirstOrDefault(x=>AtWar(x,buyer.House.Value));
                long camps=path.Count(x=>Territory2.IsCamp(w,new(x)));
                var delivered=cargo.Select(x=>(x.Kind,N:x.N-x.N*(days/10)/100-x.N*Math.Min(30,camps*10)/100)).Where(x=>x.N>0).ToArray();
                long price=cargo.Sum(x=>x.Kind switch{"軍馬"=>x.N*3,"鉄砲"=>x.N*5,"金"=>x.N*25,_=>x.N});
                string list(IEnumerable<(string Kind,long N)> xs)=>string.Join("、",xs.Select(x=>$"{x.Kind}={x.N}"));
                if(seizer!=null){Add("seized",null,new(seizer),buyer.House,0,$"{HouseName(w,new(seizer))}が{HouseName(w,buyer.House)}へ向かう隊商の荷を差し押さえた（{list(cargo)}）",cargo:list(cargo));sent++;continue;}
                Add("caravan",null,new(sellerId),buyer.House,price,$"{HouseName(w,new(sellerId))}から{HouseName(w,buyer.House)}へ隊商が出た{(through.Length>0?"（"+string.Join("・",through.Select(x=>HouseName(w,new(x))))+"の境で護衛を引き継ぐ）":"")}",now+days*Day,list(delivered));
                if(delivered.Sum(x=>x.N)<cargo.Sum(x=>x.N))Add("loss",null,buyer.House,new(sellerId),cargo.Sum(x=>x.N)-delivered.Sum(x=>x.N),$"{HouseName(w,buyer.House)}へ向かう隊商の荷が道中で傷み・賊に奪われた");
                sent++;
            }
        }
        return s with{Houses=houses.Values.OrderBy(h=>h.House.Value,StringComparer.Ordinal).ToArray(),Stores=stores.Values.OrderBy(x=>x.House.Value,StringComparer.Ordinal).ToArray(),
            Treasures=treasures.ToArray(),Logistics=events.Count==0?null:events.ToArray()};
    }
    // 城の網の最短の道（隣り合う拠点をたどる幅優先）。
    static List<string> Path(string from,string to)
    {
        var prev=new Dictionary<string,string>(StringComparer.Ordinal){[from]=""};var q=new Queue<string>();q.Enqueue(from);
        while(q.Count>0){var x=q.Dequeue();if(x==to)break;foreach(var n in NationalStrategy.Neighbors(x))if(prev.TryAdd(n,x))q.Enqueue(n);}
        var path=new List<string>();if(!prev.ContainsKey(to))return path;for(var x=to;x!="";x=prev[x])path.Add(x);path.Reverse();return path;
    }

    // ---- 探物（本人の仕事） ----
    public static TreasureJob[] Jobs(ContractWorld w,long at=long.MaxValue,long rev=long.MaxValue)
    {
        var jobs=new Dictionary<string,TreasureJob>(StringComparer.Ordinal);
        foreach(var e in Events(w).Where(e=>e.Kind=="job"&&(e.At<at||e.At==at&&e.Revision<rev)))
        {
            var parts=e.Cargo!.Split('|');string id=parts[0],step=parts[1];
            if(step=="accept")jobs[id]=new(id,e.Other!.Value,e.House!.Value,parts[2],parts[3],parts[4],parts[5]=="buy",long.Parse(parts[6]),long.Parse(parts[7]),"引受中");
            else if(jobs.TryGetValue(id,out var j))jobs[id]=j with{Phase=step=="find"?"見つけた":"精算済み"};
        }
        return jobs.Values.ToArray();
    }
    // 本人のいる市の人物が出す探物（ほかの家の名物の所在か、商人の売り物）。
    public static (EntityId Issuer,Treasure Treasure,long Fee)[] Offers(ContractWorld w,EntityId viewer)
    {
        if(!On(w)||SocialWorld.Site(w,viewer) is not {} here||!MaterialMarkets.Sites(w).Contains(here)||Jobs(w).Any(j=>j.Worker==viewer&&j.Phase!="精算済み"))return [];
        var issuer=w.Characters.Where(c=>c.Id!=viewer&&c.Life==LifeState.Adult&&SocialWorld.Site(w,c.Id)==here).Select(c=>c.Id).OrderBy(c=>c.Value,StringComparer.Ordinal).FirstOrDefault();
        if(issuer.Value==null)return [];
        var mine=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==viewer.Value)?.Faction;
        return Treasures(w).Where(t=>t.House?.Value!=mine&&t.Person!=viewer&&t.Site!=here.Value).OrderByDescending(t=>t.ForSale).ThenBy(t=>t.Id,StringComparer.Ordinal).Take(3).Select(t=>(issuer,t,t.ForSale?300L:150L)).ToArray();
    }
    // 引き受け・見つける（売り物なら買う）・精算。本人がその場所にいることが要る。
    public static void Work(ContractWorld w,EntityId actor,string step,string treasureOrJob)
    {
        if(!On(w))throw new InvalidDataException("探物のない世界です");
        var here=SocialWorld.Site(w,actor)??throw new InvalidDataException("移動中はできません");
        long now=w.ElapsedSeconds,rev=w.CommitSequence;var s=w.Strategy??=new StrategyState(1,NationalStrategy.Policy,[],[]);
        var events=(s.Logistics??[]).ToList();var treasures=(s.Treasures??Initial(w)).ToList();
        string Id(string k)=>$"logistics:job/{Hash(k+"/"+now+"/"+events.Count)}";
        switch(step)
        {
            case "accept":
            {
                var offer=Offers(w,actor).FirstOrDefault(o=>o.Treasure.Id==treasureOrJob);if(offer.Treasure==null)throw new InvalidDataException("その探物はありません");
                string job="job:"+Hash(actor.Value+"/"+offer.Treasure.Id+"/"+now);
                events.Add(new(Id("accept"),"job",here.Value,actor,offer.Issuer,now,rev,offer.Treasure.ForSale?offer.Treasure.Price:0,$"{PersonName(w,offer.Issuer)}から{offer.Treasure.Name}の探物を引き受けた"){Cargo=$"{job}|accept|{offer.Treasure.Id}|{here.Value}|{offer.Treasure.Site}|{(offer.Treasure.ForSale?"buy":"find")}|{(offer.Treasure.ForSale?offer.Treasure.Price:0)}|{offer.Fee}"});
                break;
            }
            case "find":
            {
                var j=Jobs(w).SingleOrDefault(j=>j.Id==treasureOrJob&&j.Worker==actor&&j.Phase=="引受中")??throw new InvalidDataException("引受中の探物ではありません");
                var t=treasures.Single(t=>t.Id==j.Treasure);
                if(here.Value!=t.Site)throw new InvalidDataException($"{t.Name}は{SiteName(w,t.Site)}にあります");
                if(j.Buy)
                {
                    if(!t.ForSale)throw new InvalidDataException($"{t.Name}はもう売り物ではありません");
                    int i=treasures.IndexOf(t);treasures[i]=t with{ForSale=false,House=null,Person=j.Issuer,History=[..t.History,$"{WorldCalendar.At(w.EpochDate,now).Year}年　{PersonName(w,j.Issuer)}（探物で）"]};
                }
                events.Add(new(Id("find"),"job",here.Value,actor,j.Issuer,now,rev,j.Buy?j.Advance:0,$"{t.Name}を{(j.Buy?"買った":"見つけた")}"){Cargo=$"{j.Id}|find"});
                break;
            }
            case "settle":
            {
                var j=Jobs(w).SingleOrDefault(j=>j.Id==treasureOrJob&&j.Worker==actor&&j.Phase=="見つけた")??throw new InvalidDataException("見つけた探物ではありません");
                if(here.Value!=j.Home||SocialWorld.Site(w,j.Issuer)!=here)throw new InvalidDataException("依頼主のところへ戻ってください");
                events.Add(new(Id("settle"),"job",here.Value,actor,j.Issuer,now,rev,j.Fee,$"探物の礼{j.Fee}文を受け取った"){Cargo=$"{j.Id}|settle"});
                break;
            }
            default:throw new InvalidDataException("探物の操作を確かめてください");
        }
        w.Strategy=s with{Logistics=events.ToArray(),Treasures=treasures.ToArray()};
    }
    // 財布：引き受けで預り金（買う探物）を受け取り、買うときに払い、精算で礼を受け取る。
    public static long Net(ContractWorld w,EntityId wallet,long at,long rev)
    {
        if(!On(w))return 0;long n=0;
        foreach(var e in Events(w).Where(e=>e.Kind=="job"&&(e.At<at||e.At==at&&e.Revision<rev)))
        {
            if(e.House is not {} worker||NewGameWork.Wallet(worker)!=wallet)continue;
            var step=e.Cargo!.Split('|')[1];
            n=checked(n+(step=="accept"?e.Amount:step=="find"?-e.Amount:e.Amount));
        }
        return n;
    }

    // ---- 知ること ----
    public static DomesticRumor[] Heard(ContractWorld w,EntityId viewer,long at)
    {
        if(!On(w))return [];var mine=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==viewer.Value)?.Faction;var rows=new List<DomesticRumor>();
        foreach(var e in Events(w))
        {
            if(e.Kind is "job" or "voyage" or "fodder")continue;
            bool own=e.House?.Value==mine||e.Other?.Value==mine;
            string? where=e.Site!=null&&e.Site.StartsWith("site:",StringComparison.Ordinal)?e.Site:e.House is {} h?HomeOf(w,h.Value):null;
            long learned=own?e.At:where is {Length:>0}?e.At+HistoricalNews.DelayFrom(w,viewer,new(where),e.At):e.At+Day;
            if(learned<=at)rows.Add(new(e.Id,e.Text,e.At,learned,e.House));
        }
        return rows.OrderBy(r=>r.LearnedAt).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
    }
    internal static IEnumerable<PersonalChronicleRow> ChronicleRows(ContractWorld w,EntityId viewer)=>
        Heard(w,viewer,w.ElapsedSeconds).Select(r=>new PersonalChronicleRow("logistics:"+r.Id,"水運・交易",r.Text,r.OccurredAt,r.LearnedAt,"噂",r.House?.Value));

    internal static void Validate(ContractWorld w,StrategyState s,StrategyState? prior)
    {
        if(s.Logistics==null&&s.Treasures==null&&s.Stores==null)return;
        if(!On(w))throw new InvalidDataException("全国の水運・交易は一覧に Logistics2 のある世界だけです");
        if(prior?.Logistics is {} old)
        {
            var map=old.ToDictionary(e=>e.Id,StringComparer.Ordinal);long latest=old.Length==0?0:old.Max(e=>e.At);
            foreach(var e in s.Logistics??[])if(map.TryGetValue(e.Id,out var o)?o!=e:e.At<latest)throw new InvalidDataException("過去の水運・交易の記録が変更されています");
        }
        if((s.Treasures??[]).Select(t=>t.Id).Distinct(StringComparer.Ordinal).Count()!=(s.Treasures?.Length??0))throw new InvalidDataException("名物が重複しています");
        foreach(var x in s.Stores??[])if(x.Gear<0||x.Fleet<0)throw new InvalidDataException("家の武具・水軍が不正です");
    }
}

public sealed partial class WorldSession
{
    // 本人の探物：accept（名物の ID）・find・settle（仕事の ID）。
    public CommandResult TreasureWork(string step,string target,long revision)
    {
        lock(gate)
        {
            var id=new EntityId("request:treasure/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(step+"/"+target+"/"+revision))[..16]);
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
            var trial=ContractJson.Read<ContractWorld>(ContractJson.Write(state));
            try{Logistics2.Work(trial,trial.PlayerControl,step,target);}catch(InvalidDataException e){return new(ResultCode.NotAvailable,id,state.CommitSequence,e.Message);}
            Change(w=>Logistics2.Work(w,w.PlayerControl,step,target));
            return new(ResultCode.Accepted,id,state.CommitSequence,"受け付けました");
        }
    }
}
