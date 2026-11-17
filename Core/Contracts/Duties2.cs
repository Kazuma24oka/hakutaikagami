using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// S09E（2026-11-17）：任務・配送・命令の全般と、人物と家の残り。一覧（NationalSystems）に Duties2 のある新規の全国開始だけ（Cases2 までを使う）。
// ・併用の表：捕らわれ・療養（意思表示できない）の人は、家の役・軍団長・客将と重なれない。重なれば代わりの者に移し、戻れば元に戻して、代わりを務めた者へ補償（20貫）を記録する。
// ・書状の届け方：家どうしの書状は、敵（この半年に戦った家）の領地を避ける道の日数で届く。避けられなければ2割で途中で失われ、使者を継いで送り直す（日数が倍）。
//   同じ家が同じ月に三通を超えて出せば、一通ごとに1日遅れる。宛先の当主が不在か意思表示できなければ家老が代わりに開封する。
// ・命令の競合：従う家が、自分の当主の出陣と主家の出陣が同じ月に重なれば、自分の当主の命が効き、主家の加勢の命は退けた理由とともに記録する。
// ・子の形成：未成年の子は、毎年1月に親の得意と家の方針から興味が育ち、元服のとき志として定まる。
// ・手強さと先読み：手強さの設定が「強い」なら、NPCの当主は相手の同盟の家の援軍（半分）まで見込んで攻めるかを決める。
// ・役以外の仕事：取次の交渉の技能が60以上なら従属への変更を断れ、当主の兵站の技能は陣への兵糧の傷みを減らし、調練は雇い兵を増やす。
// ・後見人：当主が15歳未満の家は家老を後見人とし、15歳で後見を終えて帳簿を返す。
// ・代理の決裁と追認：当主が不在か意思表示できない月の方針・布告は家老が代わりに決め、当主が戻った月に追認するか改める。
// ・国人連合：包囲網の盟約の家は、1・4・7・10月に合議し、半数以上が賛成すれば的の家の城へ共同で出陣し、反対した家は賛成した家へ負担（100貫）を払う。
// ・配下の攻撃の責任：放火が見つかれば、相手の家が抗議し、当主は否む（2倍以上強い）・償う（倹約の家、200貫）・罰するのどれかを決める。
// ・婚姻の再確認：夫婦の家どうしが戦い、両家の約束が無ければ離縁、約束が続けば婚姻を続ける。成人の養子は本人と両家の当主の合意を記録し、本人の志が天下なら断る。
// ・国人の鞍替え：従う家の離反は、新しい主（境の一番強い家）への接近と、元の主への関係を解く通知として記録し、通知が届いた月に効く。
// ・恩と忠誠：家臣の主への忠誠＝50＋勲功の数×5（恩賞があれば×10）−給付の削減20＋一族20。辞去する人と、独立の時に付く軍団長の判断に使う。
// ・身代金と医療：捕らえた家は捕虜の家へ身代金（300貫、当主の一族は1000貫）を求め、銭があれば払われて放つ。捕らわれの間は捕らえた家が月に5貫の手当を負う。
// ・兵糧の見聞：陣への兵糧の運びを、隣の家の目付が見聞きし、見た日と誤差（±3割）のある量として記録する。
public sealed record DutyEvent(string Id,string Kind,string? Site,EntityId? House,EntityId? Other,EntityId? Person,long At,long Revision,long Amount,string Text)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public long? Arrives {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Detail {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public EntityId? Person2 {get;init;}
}

public static class Duties2
{
    public const string Name=NationalSystems.Duties2;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name)&&Cases2.On(w);
    const long Day=86400,Year=365*Day;
    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"duties-v1/{w.Seed}/{key}")))%1000);
    static string Hash(string s)=>ContractJson.Hash(Encoding.UTF8.GetBytes(s))[..12];
    static string HouseName(ContractWorld w,EntityId h)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==h)?.Name??h.Value;
    static string PersonName(ContractWorld w,EntityId p)=>w.Definitions.Characters.SingleOrDefault(c=>c.Id==p)?.Name??p.Value;
    static string HomeOf(ContractWorld w,string house)=>w.NewGame!.Start.Factions.FirstOrDefault(f=>f.Id==house)?.Home??"";
    static bool Player(ContractWorld w,string house)=>HouseSuccession.Head(w,new(house))==w.PlayerControl;
    public static DutyEvent[] Events(ContractWorld w)=>w.Strategy?.Duties??[];

    // ---- 併用の表 ----
    public static readonly IReadOnlyDictionary<(string State,string Role),bool> Concurrency=new Dictionary<(string,string),bool>{
        [("捕らわれ","家の役")]=false,[("捕らわれ","軍団長")]=false,[("捕らわれ","客将")]=false,
        [("療養","家の役")]=false,[("療養","軍団長")]=false,[("療養","客将")]=false,
        [("出陣中","家の役")]=true,[("出陣中","軍団長")]=true,[("出陣中","客将")]=true};
    static string? Hindered(ContractWorld w,EntityId p,long at)=>BattleCaptivity.HeldAt(w,p,at,w.CommitSequence+1)?"捕らわれ":MortalSuccession.On(w)&&MortalSuccession.Incapacitated(w,p,at)?"療養":null;

    // ---- 書状 ----
    // 書状の日数：敵の領地を避ける道。避けられなければ2割で失われて継ぎ直す。同じ月に三通を超えれば混む。
    public sealed record Courier(long Days,bool Lost,int Relays,bool Detour);
    public static Courier Route(ContractWorld w,StrategyState s,string sender,string fromSite,string toSite,int sentThisMonth,long now)
    {
        var owners=NationalStrategy.Owners(w,s);
        var enemies=s.Campaigns.Where(c=>now-c.At<180*Day&&(c.Attacker.Value==sender||c.Defender.Value==sender)).Select(c=>c.Attacker.Value==sender?c.Defender.Value:c.Attacker.Value).ToHashSet(StringComparer.Ordinal);
        var prev=new Dictionary<string,string>(StringComparer.Ordinal){[fromSite]=""};var q=new Queue<string>();q.Enqueue(fromSite);
        while(q.Count>0){var x=q.Dequeue();if(x==toSite)break;foreach(var n in NationalStrategy.Neighbors(x))if(!(owners.GetValueOrDefault(n) is {} o&&enemies.Contains(o))&&prev.TryAdd(n,x))q.Enqueue(n);}
        long direct=Domestic2.Days(fromSite,toSite);long jam=Math.Max(0,sentThisMonth-3);
        if(prev.ContainsKey(toSite))
        {
            int hops=0;for(var x=toSite;prev[x]!="";x=prev[x])hops++;
            long days=Math.Max(direct,hops*2)+jam;return new(days,false,Math.Max(0,hops/3),hops*2>direct);
        }
        bool lost=Draw(w,$"courier/{sender}/{fromSite}/{toSite}/{now}")<200;
        return new((lost?direct*2:direct)+jam,lost,lost?1:0,false);
    }
    // 開封する人：当主が本拠にいて意思表示できればその人、そうでなければ家老。
    public static EntityId Opener(ContractWorld w,string house,long at)
    {
        var head=HouseSuccession.Head(w,new(house));bool away=SocialWorld.Site(w,head)?.Value!=HomeOf(w,house)||MortalSuccession.On(w)&&MortalSuccession.Incapacitated(w,head,at);
        if(!away)return head;return Domestic2.House(w,new(house))?.Posts.FirstOrDefault(p=>p.Post==Domestic2.Elder)?.Person??head;
    }

    // ---- 手強さと先読み ----
    public static long Expected(ContractWorld w,StrategyState s,string defender,Dictionary<string,long> power)
    {
        long d=power.GetValueOrDefault(defender,50);
        if(!On(w)||w.NewGame!.Judgment?.Challenge!=NpcChallenge.Strong)return d;
        long now=w.ElapsedSeconds;
        foreach(var p in (s.Pacts??[]).Where(p=>p.Kind=="alliance"&&p.Broken==null&&p.From<=now&&now<p.Until&&(p.A.Value==defender||p.B.Value==defender)))
            d+=power.GetValueOrDefault(p.A.Value==defender?p.B.Value:p.A.Value)/2;
        return d;
    }
    // ---- 役以外の仕事への技能 ----
    public static int Skill(ContractWorld w,EntityId p,DevelopmentSkill s)=>Persons2.Skill(w,p,s,w.ElapsedSeconds);
    public static bool NegotiatesAway(ContractWorld w,string house)=>On(w)&&Domestic2.House(w,new(house))?.Posts.FirstOrDefault(p=>p.Post==Domestic2.Envoy) is {} e&&Skill(w,e.Person,DevelopmentSkill.Negotiation)>=60;
    public static int LogisticsCare(ContractWorld w,string house)=>!On(w)?0:Math.Min(80,Skill(w,HouseSuccession.Head(w,new(house)),DevelopmentSkill.Logistics));
    public static int DrillBonus(ContractWorld w,string house)=>!On(w)?0:Skill(w,HouseSuccession.Head(w,new(house)),DevelopmentSkill.Drill)/2;
    // ---- 恩と忠誠 ----
    public static int Loyalty(ContractWorld w,StrategyState s,EntityId person,string house)
    {
        var head=HouseSuccession.Head(w,new(house));int merits=(s.Merits??[]).Count(m=>m.Person==person&&m.House.Value==house);
        bool rewarded=(s.Fiefs??[]).Any(f=>f.Holder==person)||(s.Honors??[]).Any(h=>h.Person==person&&h.Kind is "偏諱" or "与姓")||(s.Treasures??[]).Any(t=>t.Person==person);
        bool kin=FamilyBirth.People(w).SingleOrDefault(p=>p.Id==person.Value)?.BirthParents?.Contains(head.Value)==true;
        int cut=s.Houses?.FirstOrDefault(h=>h.House.Value==house)?.StipendCut>0?20:0;
        return Math.Clamp(50+merits*(rewarded?10:5)-cut+(kin?20:0),0,100);
    }
    // ---- 身代金で放たれた人 ----
    public static bool Ransomed(ContractWorld w,EntityId person,long capturedRevision,long at,long rev)=>On(w)&&Events(w).Any(e=>e.Kind=="ransom"&&e.Person==person&&e.Amount>0&&e.Revision>=capturedRevision&&(e.At<at||e.At==at&&e.Revision<rev));
    // 断られた養子。
    public static bool AdoptionRefused(ContractWorld w,EntityId adoptee)=>On(w)&&Events(w).Any(e=>e.Kind=="adopt-consent"&&e.Person==adoptee&&e.Amount==0);
    // 離反の通知（StrategyDefections.Decide から）：すぐには効かせず、通知が届いた月に効く。
    internal static bool Defer(ContractWorld w,List<DutyEvent> pending,string house,string overlord,long now,long rev,Dictionary<string,long> power)
    {
        if(!On(w))return false;
        if(pending.Any(e=>e.Kind=="defect-notice"&&e.House?.Value==house&&e.Other?.Value==overlord&&(e.Arrives??0)>now))return true;
        var owners=NationalStrategy.Owners(w,w.Strategy);
        var next=owners.Where(x=>x.Value==house).SelectMany(x=>NationalStrategy.Neighbors(x.Key)).Select(n=>owners.GetValueOrDefault(n)).Where(o=>o!=null&&o!=house&&o!=overlord).Distinct().OrderByDescending(o=>power.GetValueOrDefault(o!)).ThenBy(o=>o,StringComparer.Ordinal).FirstOrDefault();
        long days=Domestic2.Days(HomeOf(w,house),HomeOf(w,overlord));
        pending.Add(new($"duty:defect-notice/{Hash(house+"/"+overlord+"/"+now)}","defect-notice",null,new(house),new(overlord),null,now,rev,days,
            $"{HouseName(w,new(house))}が{(next!=null?HouseName(w,new(next))+"に近づき、":"")}{HouseName(w,new(overlord))}へ関係を解くと通知した"){Arrives=now+days*Day,Detail=next});
        return true;
    }

    // ---- 月の初め（NationalStrategy.Apply から） ----
    internal static StrategyState Month(ContractWorld w,StrategyState s,Dictionary<string,string?> owners)
    {
        if(!On(w))return s;
        long now=w.ElapsedSeconds,rev=w.CommitSequence;var date=WorldCalendar.At(w.EpochDate,now);int year=date.Year,month=date.Month;
        var events=(s.Duties??[]).ToList();var houses=(s.Houses??[]).ToDictionary(h=>h.House.Value,h=>h,StringComparer.Ordinal);
        var corps=(s.Corps??[]).ToList();var campaigns=s.Campaigns.ToList();var defections=(s.Defections??[]).ToList();var persons=(s.Persons??[]).ToList();
        if(month==1)events=events.Where(e=>now-e.At<Year||e.Kind is "suspend" or "guardian" or "defect-notice" or "adopt-consent" or "ransom"||(e.Arrives??0)>now).ToList();
        void Add(string kind,string? site,EntityId? house,EntityId? other,EntityId? person,long amount,string text,long? arrives=null,string? detail=null,EntityId? person2=null)=>
            events.Add(new($"duty:{kind}/{date:yyyyMM}/{Hash(kind+"/"+site+"/"+house?.Value+"/"+other?.Value+"/"+person?.Value+"/"+events.Count)}",kind,site,house,other,person,now,rev,amount,text){Arrives=arrives,Detail=detail,Person2=person2});
        var pm=date.AddMonths(-1);long prevStart=NationalStrategy.MonthStart(w,pm.Year,pm.Month);
        var power=NationalStrategy.Powers(w,owners);var alive=w.Characters.Where(c=>c.Life==LifeState.Adult).Select(c=>c.Id).ToHashSet();
        var people=FamilyBirth.People(w).GroupBy(p=>p.Id,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.Ordinal);
        bool Live(string h)=>owners.ContainsValue(h);

        // 併用の表：役の人が捕らわれ・療養なら代わりに移す。戻れば元に戻し、代わりに補償。
        // この区切りの中では世界は変わらないので、人ごとの捕らわれ・療養は一度だけ調べる（S10）。
        var hinderedNow=new Dictionary<EntityId,string?>();string? HinderedNow(EntityId p){if(!hinderedNow.TryGetValue(p,out var why))hinderedNow[p]=why=Hindered(w,p,now);return why;}
        foreach(var (key,h) in houses.OrderBy(x=>x.Key,StringComparer.Ordinal).ToArray())
        {
            if(Player(w,key))continue;var posts=h.Posts.ToList();long coin=h.Coin;
            var used=posts.Select(p=>p.Person).ToHashSet();
            for(int i=0;i<posts.Count;i++)
            {
                var p=posts[i];
                if(HinderedNow(p.Person) is {} why&&!Concurrency[(why,"家の役")])
                {
                    var sub=people.Values.Where(x=>x.Faction==key&&alive.Contains(new EntityId(x.Id))&&!used.Contains(new(x.Id))&&x.Id!=HouseSuccession.Head(w,h.House).Value&&HinderedNow(new(x.Id))==null).OrderBy(x=>x.Id,StringComparer.Ordinal).FirstOrDefault();
                    if(sub==null)continue;
                    posts[i]=p with{Person=new(sub.Id),Since=now};used.Add(new(sub.Id));
                    Add("suspend",p.Place,h.House,null,p.Person,0,$"{PersonName(w,p.Person)}が{why}のため、{sub.Name}が{p.Post}を代わりに務める",null,p.Post+"/"+p.Place,new(sub.Id));
                }
            }
            foreach(var e in events.Where(e=>e.Kind=="suspend"&&e.House==h.House&&!events.Any(x=>x.Kind=="resume"&&x.Detail==e.Id)).ToArray())
            {
                if(e.Person is not {} orig||!alive.Contains(orig)||HinderedNow(orig)!=null)continue;
                int i=posts.FindIndex(p=>p.Person==e.Person2);if(i<0)continue;
                posts[i]=posts[i] with{Person=orig};coin=Math.Max(0,coin-20);
                Add("resume",e.Site,h.House,null,orig,20,$"{PersonName(w,orig)}が役に戻り、代わりを務めた{PersonName(w,e.Person2!.Value)}へ20貫を与えた",null,e.Id,e.Person2);
            }
            houses[key]=h with{Posts=posts.ToArray(),Coin=coin};
        }
        for(int i=0;i<corps.Count;i++)
        {
            var c=corps[i];if(Hindered(w,c.Commander,now) is not {} why||Player(w,c.House.Value))continue;
            if(corps.Skip(i+1).Any(x=>x.House==c.House&&x.Province==c.Province))continue;
            var sub=people.Values.Where(x=>x.Faction==c.House.Value&&alive.Contains(new EntityId(x.Id))&&Hindered(w,new(x.Id),now)==null&&!corps.Any(k=>k.Commander.Value==x.Id)&&x.Id!=HouseSuccession.Head(w,c.House).Value).OrderBy(x=>x.Id,StringComparer.Ordinal).FirstOrDefault();
            if(sub==null)continue;corps.Add(c with{Commander=new(sub.Id),Kin=false,At=now,Revision=rev});
            Add("suspend",c.Province,c.House,null,c.Commander,0,$"{PersonName(w,c.Commander)}が{why}のため、{sub.Name}が{c.Province}の軍団を代わりに率いる",null,"軍団長/"+c.Province,new(sub.Id));
        }
        // 命令の競合：前の月に、従う家が自分の出陣をし、主家も出陣していれば、自分の当主の命が効く。
        foreach(var c in s.Campaigns.Where(c=>c.At==prevStart||c.At>prevStart&&c.At<now))
        {
            if(NationalStrategy.DbOverlord(w,c.Attacker.Value) is not {} lord||!s.Campaigns.Any(x=>x.Attacker.Value==lord&&x.At==c.At))continue;
            if(events.Any(e=>e.Kind=="order"&&e.Site==c.Id.Value))continue;
            events.Add(new($"duty:order/{Hash(c.Id.Value)}","order",c.Id.Value,c.Attacker,new(lord),null,now,rev,0,$"{HouseName(w,c.Attacker)}には自分の当主の出陣と{HouseName(w,new(lord))}の加勢の命が重なり、自分の当主の命に従った（主家の命は退けた）"));
        }
        // 子の形成と元服の志。
        if(month==1)
            foreach(var p in people.Values.Where(p=>w.Characters.Any(c=>c.Id.Value==p.Id&&c.Life==LifeState.Minor)&&p.Faction!=null).OrderBy(p=>p.Id,StringComparer.Ordinal))
            {
                var parent=p.BirthParents?.FirstOrDefault();var policy=houses.GetValueOrDefault(p.Faction!)?.Policy;
                var interest=policy==Domestic2.Arms?DevelopmentSkill.Tactics:policy==Domestic2.Trade?DevelopmentSkill.Commerce:parent!=null?CharacterDevelopment.Skills(w).OrderByDescending(k=>CharacterDevelopment.StartLevel(w,new(parent),k)).ThenBy(k=>k).First():DevelopmentSkill.Agriculture;
                Add("growth",null,new(p.Faction!),null,new(p.Id),0,$"{p.Name}は{CharacterDevelopment.Name(interest)}に興味を持って育っている",null,interest.ToString());
            }
        foreach(var g in persons.Where(e=>e.Kind=="genpuku"&&e.At==now).ToArray())
        {
            var last=events.Where(e=>e.Kind=="growth"&&e.Person==g.Person).Select(e=>e.Detail).LastOrDefault();
            string asp=last switch{nameof(DevelopmentSkill.Tactics) or nameof(DevelopmentSkill.Drill)=>"武名",nameof(DevelopmentSkill.Writing) or nameof(DevelopmentSkill.Tea) or nameof(DevelopmentSkill.Renga)=>"文名",nameof(DevelopmentSkill.Commerce) or nameof(DevelopmentSkill.Agriculture)=>"立身",_=>"家の存続"};
            persons.Add(new($"person:aspiration-formed/{Hash(g.Person.Value)}","aspiration-formed",g.Person,g.House,now,rev,asp));
        }
        // 後見人：当主が15歳未満の家は家老を後見人とし、15歳で終えて帳簿を返す。
        foreach(var (key,h) in houses.OrderBy(x=>x.Key,StringComparer.Ordinal))
        {
            var head=HouseSuccession.Head(w,h.House);int age=people.GetValueOrDefault(head.Value)?.BirthYear is int b?year-b:30;
            var g=events.LastOrDefault(e=>e.Kind=="guardian"&&e.House==h.House&&e.Person2==head&&!events.Any(x=>x.Kind=="guardian-end"&&x.Detail==e.Id));
            var elder=h.Posts.FirstOrDefault(p=>p.Post==Domestic2.Elder)?.Person;
            if(age<15&&g==null&&elder!=null)Add("guardian",null,h.House,null,elder,15-age,$"{PersonName(w,elder.Value)}が幼い当主{PersonName(w,head)}の後見人となった（{15-age}年）",now+(15-age)*Year,null,head);
            else if(age>=15&&g!=null)Add("guardian-end",null,h.House,null,g.Person,0,$"{PersonName(w,head)}が15歳になり、後見人{PersonName(w,g.Person!.Value)}が帳簿を返した",null,g.Id,head);
        }
        // 代理の決裁と追認：前の月の方針・布告のうち、当主が不在か意思表示できなかった家のものは家老の代理。当主が戻った月に追認するか改める。
        foreach(var e in (s.Domestic??[]).Where(e=>e.Kind is "policy" or "decree"&&e.At==prevStart&&e.House!=null&&!Player(w,e.House.Value.Value)))
        {
            var house=e.House!.Value;var opener=Opener(w,house.Value,e.At);var head=HouseSuccession.Head(w,house);
            if(opener==head||events.Any(x=>x.Kind=="proxy"&&x.Detail==e.Id))continue;
            Add("proxy",null,house,null,opener,0,$"{PersonName(w,opener)}（家老）が当主に代わり「{e.Text}」を決めた",null,e.Id);
        }
        foreach(var e in events.Where(e=>e.Kind=="proxy"&&!events.Any(x=>x.Kind is "ratify" or "overturn"&&x.Detail==e.Id)).ToArray())
        {
            var house=e.House!.Value;if(Opener(w,house.Value,now)!=HouseSuccession.Head(w,house))continue;
            bool keep=Draw(w,"ratify/"+e.Id)<800;
            Add(keep?"ratify":"overturn",null,house,null,HouseSuccession.Head(w,house),0,$"{PersonName(w,HouseSuccession.Head(w,house))}は家老の代理の決定を{(keep?"追認した":"改めた")}",null,e.Id);
        }
        // 国人連合：包囲網の盟約の家は、1・4・7・10月に合議して共同で出陣する。
        if(month is 1 or 4 or 7 or 10)
            foreach(var co in (s.Coalitions??[]).Where(c=>now-c.At<2*Year&&Live(c.Target.Value)).OrderBy(c=>c.Id.Value,StringComparer.Ordinal))
            {
                var members=co.Members.Where(m=>m!=co.Target&&Live(m.Value)&&!Player(w,m.Value)).ToArray();if(members.Length<2)continue;
                bool Yes(EntityId m)=>power.GetValueOrDefault(m.Value)*5>=power.GetValueOrDefault(co.Target.Value)&&!(s.Pacts??[]).Any(p=>p.Kind=="truce"&&p.Broken==null&&p.From<=now&&now<p.Until&&(p.A==m&&p.B==co.Target||p.B==m&&p.A==co.Target));
                var yes=members.Where(Yes).ToArray();var no=members.Except(yes).ToArray();
                Add("council",null,co.Target,null,null,yes.Length,$"{HouseName(w,co.Target)}への包囲網の合議：賛成{yes.Length}・反対{no.Length}");
                if(yes.Length*2<members.Length)continue;
                foreach(var m in yes)
                {
                    var site=owners.Where(x=>x.Value==co.Target.Value).Select(x=>x.Key).Where(t=>NationalStrategy.Neighbors(t).Any(n=>owners.GetValueOrDefault(n)==m.Value)).OrderBy(x=>x,StringComparer.Ordinal).FirstOrDefault();
                    if(site==null||campaigns.Any(c=>c.Attacker==m&&c.At==now))continue;
                    campaigns.Add(new(new($"campaign:national/{date:yyyyMM}/coalition/{Hash(co.Id.Value+"/"+m.Value)}"),m,co.Target,new(site),now,rev,NationalStrategy.Border));
                }
                foreach(var n in no)if(houses.TryGetValue(n.Value,out var nh)&&nh.Coin>=100&&yes.Length>0)
                {houses[n.Value]=nh with{Coin=nh.Coin-100};var r=yes[0].Value;if(houses.TryGetValue(r,out var rh))houses[r]=rh with{Coin=rh.Coin+100};Add("burden",null,n,yes[0],null,100,$"{HouseName(w,n)}は共同の出陣に加わらず、負担100貫を{HouseName(w,yes[0])}へ払った");}
            }
        // 配下の攻撃の責任：今月見つかった放火に、相手が抗議し、当主が否む・償う・罰するを決める。
        foreach(var e in (s.Domestic??[]).Where(e=>e.Kind=="caught"&&e.At==now&&e.House!=null&&e.Other!=null))
        {
            var victim=e.House!.Value;var culprit=e.Other!.Value;if(Player(w,culprit.Value))continue;
            string answer=power.GetValueOrDefault(culprit.Value)>=2*power.GetValueOrDefault(victim.Value)?"否んだ":houses.GetValueOrDefault(culprit.Value)?.Policy==Domestic2.Thrift?"償った（200貫）":"放火した配下を罰した";
            if(answer.StartsWith("償",StringComparison.Ordinal)&&houses.TryGetValue(culprit.Value,out var ch)&&houses.TryGetValue(victim.Value,out var vh)){houses[culprit.Value]=ch with{Coin=Math.Max(0,ch.Coin-200)};houses[victim.Value]=vh with{Coin=vh.Coin+200};}
            Add("incident",e.Site,victim,culprit,null,0,$"{HouseName(w,victim)}が放火に抗議し、{HouseName(w,culprit)}は{answer}");
        }
        // 婚姻の再確認：夫婦の家どうしが戦い、約束が無ければ離縁。
        foreach(var m in (s.Marriages??[]))
        {
            string? hm=people.GetValueOrDefault(m.Man.Value)?.Faction,hw=people.GetValueOrDefault(m.Woman.Value)?.Faction;if(hm==null||hw==null||hm==hw)continue;
            bool war=s.Campaigns.Any(c=>c.At>prevStart&&c.At<=now&&(c.Attacker.Value==hm&&c.Defender.Value==hw||c.Attacker.Value==hw&&c.Defender.Value==hm));if(!war)continue;
            if(events.Any(e=>e.Kind=="marriage-check"&&e.Person==m.Man&&e.Person2==m.Woman))continue;
            bool pact=(s.Pacts??[]).Any(p=>p.Broken==null&&p.From<=now&&now<p.Until&&(p.A.Value==hm&&p.B.Value==hw||p.A.Value==hw&&p.B.Value==hm));
            Add("marriage-check",null,new(hm),new(hw),m.Man,pact?1:0,$"両家が戦い、{PersonName(w,m.Man)}と{PersonName(w,m.Woman)}は{(pact?"婚姻を続けた":"離縁した")}",null,null,m.Woman);
        }
        // 成人の養子の合意。
        foreach(var e in (s.Politics??[]).Where(e=>e.Kind=="adopt"&&e.At==now))
        {
            if(e.Person is not {} who||events.Any(x=>x.Kind=="adopt-consent"&&x.Person==who))continue;
            bool yes=Persons2.Aspiration(w,who)?.Value!="天下";
            Add("adopt-consent",null,e.House,e.Other,who,yes?1:0,$"{PersonName(w,who)}は{HouseName(w,e.House!.Value)}への養子入りを{(yes?"受け、両家の当主も合意した":"断った")}");
        }
        // 国人の鞍替え：届いた通知の離反を効かせる。
        foreach(var e in events.Where(e=>e.Kind=="defect-notice"&&e.Arrives is long a&&a>prevStart&&a<=now).ToArray())
            if(!defections.Any(d=>d.House==e.House&&d.From==e.Other)){defections.Add(new(e.House!.Value,e.Other!.Value,now,rev));Add("defect",null,e.House,e.Other,null,0,$"{HouseName(w,e.Other!.Value)}は{HouseName(w,e.House!.Value)}の通知を受け取り、従属が解けた");}
        // 身代金と医療：NPCどうしの捕虜。
        foreach(var cap in BattleCaptivity.AllCaptures(w).Select(c=>c.Person).Distinct().Select(p=>BattleCaptivity.CaptivityAt(w,p,now,rev+1)).Where(c=>c!=null).Select(c=>c!).OrderBy(c=>c.Person.Value,StringComparer.Ordinal))
        {
            if(Player(w,cap.CaptorOwner.Value)||Player(w,cap.FromOwner.Value)||events.Any(e=>e.Kind=="ransom"&&e.Person==cap.Person&&e.Amount>0))continue;
            if(houses.TryGetValue(cap.CaptorOwner.Value,out var ch))houses[cap.CaptorOwner.Value]=ch with{Coin=Math.Max(0,ch.Coin-5)};
            bool kin=people.GetValueOrDefault(cap.Person.Value)?.BirthParents?.Contains(HouseSuccession.Head(w,cap.FromOwner).Value)==true;long price=kin?1000:300;
            if(!houses.TryGetValue(cap.FromOwner.Value,out var fh)||fh.Coin<price*2){if(!events.Any(e=>e.Kind=="ransom"&&e.Person==cap.Person))Add("ransom",cap.Site.Value,cap.CaptorOwner,cap.FromOwner,cap.Person,0,$"{HouseName(w,cap.CaptorOwner)}が{PersonName(w,cap.Person)}の身代金{price}貫を求めた（まだ払われない。手当は月に5貫）");continue;}
            houses[cap.FromOwner.Value]=fh with{Coin=fh.Coin-price};if(houses.TryGetValue(cap.CaptorOwner.Value,out var ch2))houses[cap.CaptorOwner.Value]=ch2 with{Coin=ch2.Coin+price};
            Add("ransom",cap.Site.Value,cap.CaptorOwner,cap.FromOwner,cap.Person,price,$"{HouseName(w,cap.FromOwner)}が身代金{price}貫を払い、{PersonName(w,cap.Person)}が放たれた");
        }
        // 兵糧の見聞：今月決着した出陣の兵糧の運びを、目的の城の隣の家の目付が見聞きする（誤差つき）。
        foreach(var o in s.Outcomes.Where(o=>o.At>prevStart&&o.At<=now))
        {
            var c=s.Campaigns.Single(x=>x.Id==o.Campaign);
            var watcher=NationalStrategy.Neighbors(c.Target.Value).Select(n=>owners.GetValueOrDefault(n)).Where(x=>x!=null&&x!=c.Attacker.Value&&x!=c.Defender.Value&&houses.GetValueOrDefault(x!)?.Posts.Any(p=>p.Post==Domestic2.Inspector)==true).OrderBy(x=>x,StringComparer.Ordinal).FirstOrDefault();
            if(watcher==null)continue;
            var line=Logistics2.Supply(w,s,c,power.GetValueOrDefault(c.Attacker.Value),power.GetValueOrDefault(c.Defender.Value));
            int err=Draw(w,$"seen-supply/{watcher}/{c.Id.Value}")%61-30;long seen=Math.Max(0,line.Delivered*(100+err)/100);
            Add("seen-supply",c.Target.Value,new(watcher),c.Attacker,null,seen,$"{HouseName(w,new(watcher))}の目付が{HouseName(w,c.Attacker)}の陣へ運ばれる兵糧を約{seen}石と見た（見た時の見立て）");
        }
        return s with{Duties=events.Count==0?null:events.ToArray(),Houses=houses.Count==0?s.Houses:houses.Values.OrderBy(h=>h.House.Value,StringComparer.Ordinal).ToArray(),
            Corps=corps.Count==0?null:corps.ToArray(),Campaigns=campaigns.ToArray(),Defections=defections.Count==0?null:defections.ToArray(),Persons=persons.Count==0?null:persons.ToArray()};
    }

    public static DomesticRumor[] Heard(ContractWorld w,EntityId viewer,long at)
    {
        if(!On(w))return [];var rows=new List<DomesticRumor>();var mine=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==viewer.Value)?.Faction;
        foreach(var e in Events(w))
        {
            bool own=e.House?.Value==mine||e.Other?.Value==mine||e.Person==viewer||e.Person2==viewer;
            if(!own&&e.Kind is "seen-supply" or "proxy" or "ratify" or "overturn" or "growth" or "suspend" or "resume")continue;
            string? where=e.Site!=null&&e.Site.StartsWith("site:",StringComparison.Ordinal)?e.Site:e.House is {} h?HomeOf(w,h.Value):null;
            long learned=own&&e.Kind=="defect-notice"&&e.Arrives is long a?a:own?e.At:where is {Length:>0}?e.At+HistoricalNews.DelayFrom(w,viewer,new(where),e.At):e.At+Day;
            if(learned<=at)rows.Add(new(e.Id,e.Text,e.At,learned,e.House));
        }
        return rows.OrderBy(r=>r.LearnedAt).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
    }
    internal static IEnumerable<PersonalChronicleRow> ChronicleRows(ContractWorld w,EntityId viewer)=>
        Heard(w,viewer,w.ElapsedSeconds).Select(r=>new PersonalChronicleRow("duties:"+r.Id,"家中と使者",r.Text,r.OccurredAt,r.LearnedAt,"噂",r.House?.Value));
    internal static void Validate(ContractWorld w,StrategyState s,StrategyState? prior)
    {
        if(s.Duties==null)return;
        if(!On(w))throw new InvalidDataException("任務・配送・命令の全般は一覧に Duties2 のある世界だけです");
        if(s.Duties.Select(e=>e.Id).Distinct(StringComparer.Ordinal).Count()!=s.Duties.Length)throw new InvalidDataException("家中と使者の記録が重複しています");
    }
}
