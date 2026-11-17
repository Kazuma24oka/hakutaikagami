using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// S09A（2026-11-17）：NPCの判断と人物の一生。一覧（NationalSystems）に Persons2 のある新規の全国開始だけ（全国の家の内政 Domestic2 を使う）。
// ・志：史実人物は立場と経歴の手がかりから推定（印つき）、作成武将は本人が選ぶ。兵科適性は槍・弓・騎馬・鉄砲の技能から。
// ・家の目標と計画：NPCの当主は家の目標（天下・国の統一・家の存続・奉公）を節目（家督・京の主・大きな敗北・毎年1月）で見直し、次に取る国を計画する。出陣の的の選び方に効く。
// ・断る条件：裏切られた・本拠を奪われた事実から、相手との同盟を理由つきで断り、期限が来ると考え直す。
// ・自発登用と役の目標：役の空いた家は、領地にいる主のない成人を一人ずつ登用する。役ごとの目標を毎年1月に振り返り、果たせなければ替える。
// ・実務の技能：開始の熟練＋役を務めた年数＋子のうちに家中で教わった分−55歳からの衰え。家の役の選び方・普請・監督・合戦に効く。
// ・隠居：60歳を過ぎるか長く判断できない当主は、成人の後継がいれば存命のうちに家督を譲り、相談役として残る。
// ・元服：子は15歳の年の1月に、幼いころの興味から表明した希望を振り返り、修行か役の道を選ぶ。
public sealed record HouseGoal(EntityId House,EntityId Head,string Goal,string? Province,string Reason,long At,long Revision);
public sealed record HouseRefusal(EntityId House,EntityId Other,string Kind,string Reason,long Since,long Until);
public sealed record PersonEvent(string Id,string Kind,EntityId Person,EntityId? House,long At,long Revision,string Text);

public static class Persons2
{
    public const string Name=NationalSystems.Persons2;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name)&&Domestic2.On(w);
    const long Day=86400,Year=365*Day;
    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"persons-v1/{w.Seed}/{key}")))%1000);
    static string Hash(string s)=>ContractJson.Hash(Encoding.UTF8.GetBytes(s))[..12];
    static string HouseName(ContractWorld w,EntityId h)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==h)?.Name??h.Value;
    static string PersonName(ContractWorld w,EntityId p)=>w.Definitions.Characters.SingleOrDefault(c=>c.Id==p)?.Name??p.Value;
    public const string Tenka="天下",Unify="国の統一",Survive="家の存続",Serve="奉公";
    public static readonly string[] Aspirations=["天下","家の存続","立身","武名","文名","信仰"];

    // ---- 志と兵科適性 ----
    public sealed record AspirationValue(string Value,string Basis);
    public static AspirationValue? Aspiration(ContractWorld w,EntityId person)
    {
        if(!On(w))return null;
        if((w.Strategy?.Persons??[]).LastOrDefault(e=>e.Kind=="aspiration"&&e.Person==person) is {} chosen)return new(chosen.Text,"本人が選んだもの");
        var p=FamilyBirth.People(w).SingleOrDefault(x=>x.Id==person.Value);if(p==null)return null;
        if(w.NewGame!.Registered.Any(r=>r.Id==person.Value))return null;
        bool head=p.Faction!=null&&w.NewGame.Start.Factions.Any(f=>f.Id==p.Faction&&f.Head==p.Id);
        var hints=CharacterDevelopment.Hints(w,person);var faith=PersonIdentity.Faith(w,person)?.Value;
        string v=head?(NationalStrategy.Cap(w,p.Faction!)>=5000?"天下":"家の存続"):
            faith is "浄土真宗" or "日蓮宗"&&hints.Length==0?"信仰":
            hints.Any(h=>h is DevelopmentSkill.Tea or DevelopmentSkill.Renga or DevelopmentSkill.Writing or DevelopmentSkill.Teaching)?"文名":
            hints.Any(h=>h is DevelopmentSkill.Tactics or DevelopmentSkill.Spear or DevelopmentSkill.Swordsmanship or DevelopmentSkill.Bow or DevelopmentSkill.Cavalry or DevelopmentSkill.Gun)?"武名":"立身";
        return new(v,"推定（立場・技能の手がかり・信仰から。史料の志ではない）");
    }
    public static string Aptitude(ContractWorld w,EntityId person)
    {
        string G(DevelopmentSkill s){int l=Skill(w,person,s,w.ElapsedSeconds);return l>=40?"A":l>=20?"B":"C";}
        return $"槍{G(DevelopmentSkill.Spear)}　弓{G(DevelopmentSkill.Bow)}　騎馬{G(DevelopmentSkill.Cavalry)}　鉄砲{G(DevelopmentSkill.Gun)}";
    }
    public static void Choose(ContractWorld w,EntityId actor,string aspiration)
    {
        if(!On(w)||!Aspirations.Contains(aspiration)||!w.NewGame!.Registered.Any(r=>r.Id==actor.Value))throw new InvalidDataException("作成武将だけが志を選べます");
        var s=w.Strategy??=new StrategyState(1,NationalStrategy.Policy,[],[]);
        w.Strategy=s with{Persons=[..s.Persons??[],new($"person:aspiration/{Hash(actor.Value+"/"+w.ElapsedSeconds+"/"+w.CommitSequence)}","aspiration",actor,null,w.ElapsedSeconds,w.CommitSequence,aspiration)]};
    }

    // ---- 実務の技能 ----
    // 月ごとに引ける表（人物・役・家中の師）。全国の戦略の状態が変わるたびに作り直す。
    sealed record Tables(Dictionary<string,SetupPerson> People,Dictionary<EntityId,DomesticPost[]> Posts,System.Collections.Concurrent.ConcurrentDictionary<(string,DevelopmentSkill),bool> Teachers);
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object,Tables> tables=new();
    static Tables T(ContractWorld w)=>tables.GetValue((object?)w.Strategy??w.NewGame!,_=>new(FamilyBirth.People(w).GroupBy(p=>p.Id,StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.Ordinal),
        (w.Strategy?.Houses??[]).SelectMany(h=>h.Posts).GroupBy(p=>p.Person).ToDictionary(g=>g.Key,g=>g.ToArray()),new()));
    static int? BirthYear(ContractWorld w,EntityId p)=>T(w).People.GetValueOrDefault(p.Value)?.BirthYear;
    public static int Skill(ContractWorld w,EntityId person,DevelopmentSkill skill,long at)
    {
        int lv=CharacterDevelopment.StartLevel(w,person,skill);if(!On(w))return lv;
        int year=WorldCalendar.At(w.EpochDate,at).Year;var t=T(w);
        // 役を務めた年数（関係する技能だけ、1年に2、20まで）。
        foreach(var p in t.Posts.GetValueOrDefault(person)??[])if(Uses(p.Post).Contains(skill))lv+=(int)Math.Min(20,(at-p.Since)/Year*2);
        // 子のうちに家中で教わった分（開始の時に15歳未満で、家中にその技能40以上の人がいれば、15歳までに10）。
        if(BirthYear(w,person) is int born&&born+15>NationalSystems.StartYear(w)&&year>=born+15&&t.People.GetValueOrDefault(person.Value)?.Faction is {} faction)
        {
            if(t.Teachers.GetOrAdd((faction,skill),k=>t.People.Values.Any(x=>x.Faction==k.Item1&&CharacterDevelopment.StartLevel(w,new(x.Id),k.Item2)>=40&&(x.BirthYear??0)+15<=NationalSystems.StartYear(w))))lv+=10;
        }
        // 55歳からの衰え（1年に2）。
        if(BirthYear(w,person) is int b&&year-b>55)lv-=(year-b-55)*2;
        return Math.Clamp(lv,0,100);
    }
    static DevelopmentSkill[] Uses(string post)=>post switch{
        Domestic2.Elder=>[DevelopmentSkill.Adjudication,DevelopmentSkill.TalentAssessment],Domestic2.Treasurer=>[DevelopmentSkill.Finance,DevelopmentSkill.Commerce],
        Domestic2.WorksMagistrate=>[DevelopmentSkill.Construction,DevelopmentSkill.Irrigation],Domestic2.Inspector=>[DevelopmentSkill.IntelligenceGathering,DevelopmentSkill.Adjudication],
        Domestic2.Envoy=>[DevelopmentSkill.Negotiation,DevelopmentSkill.Etiquette],Domestic2.TempleMagistrate=>[DevelopmentSkill.Etiquette,DevelopmentSkill.Writing],
        Domestic2.Intendant=>[DevelopmentSkill.Agriculture,DevelopmentSkill.Adjudication],Domestic2.Deputy=>[DevelopmentSkill.Agriculture],_=>[]};
    // 合戦：当主の戦術が力に効く（50を境に、1につき0.2%）。
    internal static (long A,long D) Effective(ContractWorld w,StrategyState s,StrategyCampaign c,long a,long d)
    {
        if(!On(w))return (a,d);
        int Tac(EntityId house)=>Skill(w,HouseSuccession.Head(w,house),DevelopmentSkill.Tactics,w.ElapsedSeconds);
        return (a*(1000+(Tac(c.Attacker)-50)*2)/1000,d*(1000+(Tac(c.Defender)-50)*2)/1000);
    }

    // ---- 家の目標・計画・断る条件 ----
    public static HouseGoal? Goal(ContractWorld w,EntityId house)=>w.Strategy?.Goals?.LastOrDefault(g=>g.House==house);
    public static bool Refuses(ContractWorld w,string a,string b,long at)=>On(w)&&(w.Strategy?.Refusals??[]).Any(r=>r.Since<=at&&at<r.Until&&(r.House.Value==a&&r.Other.Value==b||r.House.Value==b&&r.Other.Value==a));
    // 出陣の的：目標の国の城を先に、家の存続を目指す家は3倍以上の相手だけ、天下を目指す家は1.2倍から。
    internal static (string Site,string? Owner)? Choose(ContractWorld w,string house,(string Site,string? Owner)[] targets,Func<string,long> ratio)
    {
        var g=Goal(w,new(house));if(g==null)return null;
        int need=g.Goal switch{Survive=>300,Tenka=>120,_=>150};
        return targets.Select(t=>(t,R:ratio(t.Owner!),Plan:g.Province!=null&&CentralStanding.ProvinceOf(new(t.Site))==g.Province)).Where(x=>x.R>=need)
            .OrderByDescending(x=>x.Plan).ThenByDescending(x=>x.R).ThenBy(x=>x.t.Site,StringComparer.Ordinal).Select(x=>((string,string?)?)x.t).FirstOrDefault();
    }

    // ---- 月の初め（NationalStrategy.Apply から） ----
    internal static StrategyState Month(ContractWorld w,StrategyState s,Dictionary<string,string?> owners)
    {
        if(!On(w))return s;
        long now=w.ElapsedSeconds,rev=w.CommitSequence;var date=WorldCalendar.At(w.EpochDate,now);int year=date.Year,month=date.Month;
        var goals=(s.Goals??[]).ToList();var refusals=(s.Refusals??[]).ToList();var events=(s.Persons??[]).ToList();
        var successions=(s.Successions??[]).ToList();var houses=(s.Houses??[]).ToDictionary(h=>h.House.Value,h=>h,StringComparer.Ordinal);
        void Add(string kind,EntityId person,EntityId? house,string text)=>events.Add(new($"person:{kind}/{date:yyyyMM}/{Hash(kind+"/"+person.Value+"/"+events.Count)}",kind,person,house,now,rev,text));
        var alive=w.Characters.Where(c=>c.Life==LifeState.Adult).Select(c=>c.Id).ToHashSet();
        var power=NationalStrategy.Powers(w,owners);var capital=NationalStrategy.Capital(w)?.Value;
        var lastMonth=now-35*Day;var settled=s.Outcomes.Where(o=>o.At>lastMonth&&o.At<=now).ToArray();

        // 断る条件：裏切り（約束を破って攻めた）と本拠の喪失。期限が来たら考え直す。
        foreach(var p in (s.Pacts??[]).Where(p=>p.Broken is long b&&b>lastMonth&&b<=now))
        {
            var betrayer=s.Campaigns.Where(c=>c.Basis=="betrayal"&&c.At==p.Broken).Select(c=>c.Attacker).FirstOrDefault(x=>x==p.A||x==p.B);if(betrayer.Value==null)continue;
            var victim=betrayer==p.A?p.B:p.A;
            refusals.Add(new(victim,betrayer,"alliance",$"{HouseName(w,betrayer)}に約束を破られた",now,now+10*Year));
        }
        foreach(var o in settled.Where(o=>o.Captured))
        {
            var c=s.Campaigns.Single(x=>x.Id==o.Campaign);
            if(w.NewGame!.Start.Factions.FirstOrDefault(f=>f.Id==c.Defender.Value)?.Home==c.Target.Value)refusals.Add(new(c.Defender,c.Attacker,"alliance",$"{HouseName(w,c.Attacker)}に本拠を奪われた",now,now+5*Year));
        }
        foreach(var r in refusals.Where(r=>r.Until>lastMonth&&r.Until<=now).ToArray())Add("reconsider",HouseSuccession.Head(w,r.House),r.House,$"{HouseName(w,r.House)}は{HouseName(w,r.Other)}との同盟を考え直した（{r.Reason}から年月がたった）");

        foreach(var f in w.NewGame!.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            var house=new EntityId(f.Id);var head=HouseSuccession.Head(w,house);
            if(head==w.PlayerControl||!alive.Contains(head)||!owners.ContainsValue(f.Id))continue;
            // 隠居：60歳を過ぎた当主（または意思表示できないまま半年たった当主）に成人の後継がいれば、毎年1月に家督を譲る。
            int age=BirthYear(w,head) is int by?year-by:0;
            bool ill=MortalSuccession.On(w)&&MortalSuccession.Incapacitated(w,head,now)&&MortalSuccession.Incapacitated(w,head,now-180*Day);
            if((month==1&&age>=60&&Draw(w,$"retire/{head.Value}/{year}")<300||ill)&&MortalSuccession.On(w)&&MortalSuccession.Successor(w,house,head,f.Id) is {} pick&&pick.Heir!=head&&alive.Contains(pick.Heir)&&pick.Heir!=w.PlayerControl)
            {
                successions.Add(new(house,head,pick.Heir,now,rev){Retired=true});
                Add("retire",head,house,$"{PersonName(w,head)}が隠居し、{PersonName(w,pick.Heir)}が家督を継いだ");
                goals.Add(new(house,pick.Heir,Goal(w,house)?.Goal??Survive,null,"家督を継いだ",now,rev));
                continue;
            }
            // 目標の見直し：毎年1月、家督の後、京の主になった月、大きな敗北（城を二つ以上失った月）。
            var last=goals.LastOrDefault(g=>g.House==house);
            int lost=settled.Count(o=>o.Captured&&s.Campaigns.Single(c=>c.Id==o.Campaign).Defender==house);
            string? why=last==null?"初めの方針":last.Head!=head?"家督を継いだ":capital==f.Id&&last.Goal!=Tenka?"京の主になった":lost>=2?"城を二つ失った":month==1?"年の初めの見直し":null;
            if(why!=null)
            {
                long mine=power.GetValueOrDefault(f.Id);var top=power.Values.OrderByDescending(x=>x).Take(10).LastOrDefault();
                bool vassal=NationalStrategy.DbOverlord(w,f.Id) is {} ov&&owners.ContainsValue(ov);
                string goal=capital==f.Id||mine>=top&&Persons2.Aspiration(w,head)?.Value=="天下"?Tenka:vassal?Serve:lost>=2||mine<500?Survive:Unify;
                // 計画：目標が国の統一・天下なら、境の城の多い国（本拠の国を先に）を次に取る国とする。
                var own=owners.Where(x=>x.Value==f.Id).Select(x=>x.Key).ToArray();
                var homeProvince=CentralStanding.ProvinceOf(new(f.Home));
                string? plan=goal is Unify or Tenka?own.SelectMany(NationalStrategy.Neighbors).Where(n=>owners.GetValueOrDefault(n) is {} o&&o!=f.Id).Select(n=>CentralStanding.ProvinceOf(new(n))).Where(p=>p!=null)
                    .GroupBy(p=>p!).OrderByDescending(g=>g.Key==homeProvince).ThenByDescending(g=>g.Count()).ThenBy(g=>g.Key,StringComparer.Ordinal).Select(g=>g.Key).FirstOrDefault():null;
                if(last==null||last.Goal!=goal||last.Province!=plan||last.Head!=head)goals.Add(new(house,head,goal,plan,why,now,rev));
            }
        }

        // 自発登用：人手の足りない家は、領地にいる主のない成人を一人登用する。
        var hired=events.Where(e=>e.Kind=="recruit").Select(e=>e.Person).ToHashSet();
        var resigned=Recruits(w).Where(x=>x.Value==null).Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
        var ronin=FamilyBirth.People(w).Where(p=>(p.Faction==null||resigned.Contains(p.Id))&&alive.Contains(new EntityId(p.Id))&&!hired.Contains(new EntityId(p.Id))&&p.Id!=w.PlayerControl.Value).ToArray();
        foreach(var h in houses.Values.OrderBy(h=>h.House.Value,StringComparer.Ordinal))
        {
            if(HouseSuccession.Head(w,h.House)==w.PlayerControl)continue;
            var own=owners.Where(x=>x.Value==h.House.Value).Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
            int provinces=own.Select(x=>CentralStanding.ProvinceOf(new(x))).Where(p=>p!=null).Distinct().Count();
            // 人手の足りない家：家中の成人（登用した人を含む）が、国ごとの郡代と代官、ほかの役6つに足りない。
            int staff=FamilyBirth.People(w).Count(p=>(p.Faction==h.House.Value||Recruits(w).GetValueOrDefault(p.Id)==h.House.Value)&&alive.Contains(new EntityId(p.Id)));
            if(staff>=provinces*2+6)continue;
            // 登用の条件（一覧の Politics2）：給付を削った家・裏切りの悪名の主の誘いには応じない。
            if(Politics2.On(w)&&(h.StipendCut>0||s.Campaigns.Any(c=>c.Basis=="betrayal"&&c.Attacker==h.House&&now-c.At<2*Year)))continue;
            var r=ronin.FirstOrDefault(p=>own.Contains(p.Site)||own.Any(x=>NationalStrategy.Neighbors(x).Contains(p.Site)));if(r==null)continue;
            ronin=ronin.Where(x=>x!=r).ToArray();Add("recruit",new(r.Id),h.House,$"{HouseName(w,h.House)}が{r.Name}を召し抱えた");
        }

        // 役の目標の振り返り（毎年1月）：郡代は国で一揆・連合を起こさせない、普請奉行は被害を残さない、勘定奉行は赤字・借銀を出さない。
        if(month==1)
            foreach(var (key,h) in houses.OrderBy(x=>x.Key,StringComparer.Ordinal).ToArray())
            {
                if(HouseSuccession.Head(w,h.House)==w.PlayerControl)continue;
                var dom=Domestic2.Events(w);var keep=new List<DomesticPost>();
                foreach(var p in h.Posts)
                {
                    bool failed=p.Post switch{
                        Domestic2.Intendant=>dom.Any(e=>e.Kind is "ikki" or "unrest"&&e.House==h.House&&e.Amount>=4&&now-e.At<Year&&CentralStanding.ProvinceOf(new(e.Site??""))==p.Place),
                        Domestic2.WorksMagistrate=>(s.Sites??[]).Count(x=>owners.GetValueOrDefault(x.Site)==key&&x.Agriculture+x.Commerce>=30)>=3,
                        Domestic2.Treasurer=>h.Debt>0||h.Deficit>0,_=>false};
                    if(failed&&now-p.Since>=Year){Add("review",p.Person,h.House,$"{PersonName(w,p.Person)}は{p.Post}の務めを果たせず、役を解かれた");continue;}
                    keep.Add(p);
                }
                if(keep.Count!=h.Posts.Length)houses[key]=h with{Posts=keep.ToArray()};
            }

        // 元服：15歳の年の1月、幼いころの興味（親の得意な技能）から表明した希望を振り返り、家中に教える人がいれば修行の道、いなければ役の道を選ぶ。
        if(month==1)
            foreach(var p in FamilyBirth.People(w).Where(p=>p.BirthYear==year-15&&p.Faction!=null&&alive.Contains(new EntityId(p.Id))&&p.Id!=w.PlayerControl.Value).OrderBy(p=>p.Id,StringComparer.Ordinal))
            {
                var parent=p.BirthParents?.FirstOrDefault();var interest=parent!=null?CharacterDevelopment.Skills(w).OrderByDescending(k=>CharacterDevelopment.StartLevel(w,new(parent),k)).ThenBy(k=>k).First():DevelopmentSkill.Agriculture;
                bool teacher=FamilyBirth.People(w).Any(x=>x.Faction==p.Faction&&x.Id!=p.Id&&CharacterDevelopment.StartLevel(w,new(x.Id),interest)>=30);
                Add("genpuku",new(p.Id),new(p.Faction!),$"{p.Name}が元服し、幼いころ望んだ{CharacterDevelopment.Name(interest)}の道を{(teacher?"家中の師について修行する":"役を務めながら学ぶ")}と決めた");
            }

        return s with{Goals=goals.Count==0?null:goals.ToArray(),Refusals=refusals.Count==0?null:refusals.ToArray(),Persons=events.Count==0?null:events.ToArray(),
            Successions=successions.Count==0?null:successions.ToArray(),Houses=houses.Count==0?s.Houses:houses.Values.OrderBy(h=>h.House.Value,StringComparer.Ordinal).ToArray()};
    }
    // 登用した人の家（全国の家の内政の家中に数える）。
    // 辞去（一覧の Politics2）した人は主のない人（null）。
    public static Dictionary<string,string?> Recruits(ContractWorld w)=>(w.Strategy?.Persons??[]).Where(e=>e.Kind=="recruit"&&e.House!=null||e.Kind=="resign").GroupBy(e=>e.Person.Value).ToDictionary(g=>g.Key,g=>g.Last().Kind=="resign"?null:g.Last().House!.Value.Value,StringComparer.Ordinal);
    public static bool Retired(ContractWorld w,EntityId person)=>On(w)&&(w.Strategy?.Successions??[]).Any(x=>x.Retired==true&&x.Previous==person);

    // ---- 知ること ----
    public static DomesticRumor[] Heard(ContractWorld w,EntityId viewer,long at)
    {
        if(!On(w))return [];var rows=new List<DomesticRumor>();var mine=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==viewer.Value)?.Faction;
        foreach(var e in w.Strategy?.Persons??[])
        {
            if(e.Kind=="aspiration")continue;
            string? home=e.House is {} h?w.NewGame.Start.Factions.FirstOrDefault(f=>f.Id==h.Value)?.Home:null;
            long learned=e.House?.Value==mine||e.Person==viewer?e.At:home!=null?e.At+HistoricalNews.DelayFrom(w,viewer,new(home),e.At):e.At+Day;
            if(learned<=at)rows.Add(new(e.Id,e.Text,e.At,learned,e.House));
        }
        foreach(var r in w.Strategy?.Refusals??[])
        {
            string? home=w.NewGame.Start.Factions.FirstOrDefault(f=>f.Id==r.House.Value)?.Home;
            long learned=r.House.Value==mine||r.Other.Value==mine?r.Since:home!=null?r.Since+HistoricalNews.DelayFrom(w,viewer,new(home),r.Since):r.Since+Day;
            if(learned<=at)rows.Add(new("refusal:"+r.House.Value+"/"+r.Other.Value+"/"+r.Since,$"{HouseName(w,r.House)}は{HouseName(w,r.Other)}との同盟を断っている（{r.Reason}）",r.Since,learned,r.House));
        }
        return rows.OrderBy(r=>r.LearnedAt).ThenBy(r=>r.Id,StringComparer.Ordinal).ToArray();
    }
    internal static IEnumerable<PersonalChronicleRow> ChronicleRows(ContractWorld w,EntityId viewer)=>
        Heard(w,viewer,w.ElapsedSeconds).Select(r=>new PersonalChronicleRow("persons:"+r.Id,"人物",r.Text,r.OccurredAt,r.LearnedAt,"噂",r.House?.Value));

    internal static void Validate(ContractWorld w,StrategyState s,StrategyState? prior)
    {
        if(s.Goals==null&&s.Refusals==null&&s.Persons==null)return;
        if(!On(w))throw new InvalidDataException("NPCの判断と人物の一生は一覧に Persons2 のある世界だけです");
        if(prior?.Persons is {} old&&!(s.Persons??[]).Take(old.Length).SequenceEqual(old))throw new InvalidDataException("過去の人物の記録が変更されています");
        if(prior?.Goals is {} og&&!(s.Goals??[]).Take(og.Length).SequenceEqual(og))throw new InvalidDataException("過去の家の目標が変更されています");
    }
}

public sealed partial class WorldSession
{
    // 作成武将の本人が志を選ぶ。
    public CommandResult ChooseAspiration(string aspiration,long revision)
    {
        lock(gate)
        {
            var id=new EntityId("request:aspiration/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(aspiration+"/"+revision))[..16]);
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
            var trial=ContractJson.Read<ContractWorld>(ContractJson.Write(state));
            try{Persons2.Choose(trial,trial.PlayerControl,aspiration);}catch(InvalidDataException e){return new(ResultCode.NotAvailable,id,state.CommitSequence,e.Message);}
            Change(w=>Persons2.Choose(w,w.PlayerControl,aspiration));
            return new(ResultCode.Accepted,id,state.CommitSequence,"志を定めた");
        }
    }
}
