using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace HakutaiKagami.Tests;

// 全国の開始で、地方の版の仕組み（生活と目標・人間関係・婚姻・養子・密書・天候・治安）が動くこと（2026-11-13）。
[Trait("Tier","Heavy")]
public sealed class NationalSystemsTests(ITestOutputHelper o)
{
    [Fact]
    public void LifeAndSocialSystemsRunInTheNationalStartAndRestoreToTheSameFuture()
    {
        var sw=System.Diagnostics.Stopwatch.StartNew();
        var w=NationalStart.Create(NationalStartTests.Nobunaga());o.WriteLine($"作成 {sw.ElapsedMilliseconds}ms");
        Assert.True(PersonalLife.Enabled(w));Assert.True(CharacterRelations.Enabled(w));Assert.True(AdultMarriage.Enabled(w));Assert.True(AdultAdoption.Enabled(w));
        Assert.True(PrivateInformation.Enabled(w));Assert.True(RegionalWeather.Enabled(w));Assert.True(LocalSecurity.Enabled(w));Assert.True(HouseSuccession.Enabled(w));Assert.True(AbilityEffects.Enabled(w));
        Assert.True(ClinicalHealth.Enabled(w));Assert.True(HealthMortality.Enabled(w));Assert.True(TruceNegotiations.Enabled(w));Assert.True(TreatyNegotiations.Enabled(w));Assert.NotNull(w.Truces);
        var s=new WorldSession(w);
        var site=SocialWorld.Site(s.DiagnosticSnapshot(),s.PlayerControl)!.Value;
        var goal=s.ManageLife(new(new("request:national-goal"),LifeAction.SetGoal,site,Goal:LifeGoal.KeepHouse,Target:1),s.PlayerControl,s.Revision);
        Assert.True(goal.Code==ResultCode.Accepted,goal.VisibleReason);
        sw.Restart();NewGameWorldTests.At(s,s.Seconds+6*3600);o.WriteLine($"6時間 {sw.ElapsedMilliseconds}ms"); // 重さは PC が空いたときに別に測る
        var mid=s.DiagnosticSnapshot();
        Assert.Contains(PersonalLife.Goals(mid),g=>g.Goal==LifeGoal.KeepHouse);
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(mid)));
        foreach(var x in new[]{s,restored})NewGameWorldTests.At(x,mid.ElapsedSeconds+3600);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
    // NPC が自分から手当を受ける：発症の1日後に城や町にいれば手当を受け、回復が早まる。一覧に NpcSelfCare のない世界では受けない。
    [Fact]
    public void NpcsTakeCareOfTheirIllnessWhereTheyStayADayAfterOnset()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());w.ElapsedSeconds=90*86400;
        var people=FamilyBirth.People(w).Select(p=>new EntityId(p.Id)).Where(p=>p!=w.PlayerControl).Take(400).ToArray();
        var c=people.SelectMany(p=>PersonalHealth.Conditions(w,p,w.ElapsedSeconds-PersonalHealth.NpcCareDelay)).First();
        var care=PersonalHealth.Treatment(w,c);Assert.NotNull(care);
        Assert.Equal(c.OnsetAt+PersonalHealth.NpcCareDelay,care.At);Assert.Equal(PersonalCompanions.PhysicalSiteAt(w,c.Person,care.At,0),care.Site);
        Assert.True(PersonalHealth.RecoverAt(w,c)<c.OnsetAt+c.BaseSeconds||care.At>=c.OnsetAt+c.BaseSeconds);
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!="NpcSelfCare").ToArray()};
        Assert.Null(PersonalHealth.Treatment(old,c));
        Assert.Null(PersonalHealth.Treatment(w,c with{Person=w.PlayerControl,Id=new("condition:test/player")}));
    }
    // 全国の開始では、家中の書状の受け渡しで付き合いの記録ができても、NPCの日課の付き合い（訪問・噂・贈り物・寄進・茶会）は動かない。一覧に QuietNpcSocial のない世界（入れる前の保存）では今までどおり動く。
    [Fact]
    public void NationalStartKeepsTheRegionalNpcSocialRoutineOff()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.Contains(NationalSystems.QuietNpcSocial,w.NewGame!.NationalSystems!);Assert.False(SocialWorld.NpcRoutine(w));
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.QuietNpcSocial).ToArray()};
        Assert.True(SocialWorld.NpcRoutine(old));
    }
    // 1560年の官位（S01、百代鏡DBの追加の版）：就いた年の記録が1560年以前の官位を、名乗りと朝廷の序列の始まりに入れる。
    // 年の無い官位は在職にしない。一覧に DbOffices1560 のない世界（入れる前の保存）では今までどおり無い。
    [Fact]
    public void NationalStartHoldsDatedOfficesOf1560()
    {
        HakutaiDbSupplement.Validate(HakutaiDbSupplement.Current);
        Assert.All(HakutaiDbSupplement.Current.People.Where(p=>p.Held1560!=null).SelectMany(p=>p.Held1560!),h=>Assert.True(h.From<=1560));
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.Contains(NationalSystems.DbOffices1560,w.NewGame!.NationalSystems!);
        var chokei=w.Characters.Select(c=>c.Id).Single(id=>NationalStart.DbPerson(id.Value)?.Name=="三好長慶");
        var held=CentralTitles.Held(w,chokei);
        Assert.Contains(held,t=>t.Name=="修理大夫"&&t.Basis==CentralTitles.DbHeldBasis);
        Assert.Equal(CentralTitles.ByName("従四位下")!.Order,CentralTitles.FormalRank(w,chokei,w.ElapsedSeconds,w.CommitSequence+1));
        // 年の記録の無い官位だけの人は、在職にしない（織田信長の官位は1575年以後の記録だけ）。
        var nobunaga=new EntityId(NationalStartTests.Nobunaga());
        Assert.Empty(CentralTitles.Held1560(w,nobunaga));Assert.Equal(CentralTitles.NoRank,CentralTitles.FormalRank(w,nobunaga,w.ElapsedSeconds,w.CommitSequence+1));
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.DbOffices1560).ToArray()};
        Assert.DoesNotContain(CentralTitles.Held(old,chokei),t=>t.Basis==CentralTitles.DbHeldBasis);
        Assert.Equal(CentralTitles.NoRank,CentralTitles.FormalRank(old,chokei,old.ElapsedSeconds,old.CommitSequence+1));
    }
    // 百代鏡DBの追加の版の寺社と郡の人口（S01）：寺社は戦国DBの台帳から（宗派は記事、国は推定）、郡の人口は石高の推定×0.65（推定と分かる決め方つき）。
    [Fact]
    public void SupplementCarriesTemplesAndEstimatedDistrictPopulation()
    {
        var e=HakutaiDbSupplement.Current;HakutaiDbSupplement.Validate(e);
        Assert.True(e.Temples.Length>=50);
        Assert.Contains(e.Temples,t=>t.Name=="延暦寺"&&t.Religion=="天台宗"&&t.Province=="近江");
        Assert.Contains(e.Temples,t=>t.Name=="石山本願寺"&&t.Religion=="浄土真宗"&&t.Province=="摂津");
        foreach(var d in e.DistrictPopulation)
        {
            Assert.Equal((long)Math.Round(HakutaiDb.District(d.Id)!.Kokudaka*0.65,MidpointRounding.ToEven),d.Population);
            Assert.Contains("推定",d.Basis);
        }
    }
    // 史実人物の家系と信仰（S01）：百代鏡DBの本姓（記事の氏族の欄）を作成武将と同じ家系の一覧に、宗派の候補を信仰の一覧に当てる。
    // 根拠の文を持ち、分からなければ null。一覧に DbLineageFaith のない世界では引かない。
    [Fact]
    public void HistoricalPeopleHaveLineageAndFaithFromRecords()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.Contains(NationalSystems.DbLineageFaith,w.NewGame!.NationalSystems!);
        EntityId Who(string name)=>w.Characters.Select(c=>c.Id).Single(id=>NationalStart.DbPerson(id.Value)?.Name==name);
        var shingen=PersonIdentity.Lineage(w,Who("武田信玄"));Assert.Equal("源氏",shingen!.Value);Assert.Contains("清和源氏",shingen.Basis);
        Assert.Equal("その他",PersonIdentity.Lineage(w,Who("毛利元就"))!.Value);
        Assert.Equal("浄土真宗",PersonIdentity.Faith(w,Who("顕如"))!.Value);
        Assert.Null(PersonIdentity.Faith(w,Who("大村純忠"))); // キリスト教は洗礼の年が分からないので1560年の信仰にしない
        Assert.Null(PersonIdentity.Faith(w,Who("大友宗麟")));  // 候補が2つ（改宗）
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.DbLineageFaith).ToArray()};
        Assert.Null(PersonIdentity.Lineage(old,Who("武田信玄")));
    }
    // 史実人物の開始時の技能（S01）：百代鏡DBの技能・専門の候補（経歴の手がかり）がある技能を得意に入れ、表示の根拠を分ける。
    // 一覧に DbSkillHints のない世界（入れる前の保存）では今までどおり（手がかりを使わない）。
    [Fact]
    public void HistoricalPeopleStartWithSkillsFromTheirRecordedCareer()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        Assert.Contains(NationalSystems.DbSkillHints,w.NewGame!.NationalSystems!);
        var takakage=w.Characters.Select(c=>c.Id).Single(id=>NationalStart.DbPerson(id.Value)?.Name=="村上武吉");
        Assert.Contains(DevelopmentSkill.Navy,CharacterDevelopment.Hints(w,takakage));
        Assert.True(CharacterDevelopment.StartLevel(w,takakage,DevelopmentSkill.Navy)>=5);
        Assert.Contains(CharacterDevelopment.View(w,takakage,takakage)!.Skills,v=>v.Name==CharacterDevelopment.Name(DevelopmentSkill.Navy)&&v.Source.StartsWith("経歴の記録からの推定",StringComparison.Ordinal));
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.DbSkillHints).ToArray()};
        Assert.Empty(CharacterDevelopment.Hints(old,takakage));
    }
    // 他人の能力の推定（BD-16）：家の当主と同じ家の者は評判で段階と幅を知り、真の値は渡さない。それ以外の家の者は不明。一覧にない世界では不明。
    [Fact]
    public void OthersAbilitiesShowReputationRangesWithoutTrueValues()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());var me=w.PlayerControl;
        var head=new EntityId(HouseSuccession.Factions(w).First(f=>f.Head!=me.Value).Head);
        var card=CharacterDevelopment.View(w,me,head)!;var truth=CharacterDevelopment.View(w,head,head)!;
        Assert.False(card.Own);
        foreach(var (seen,real) in card.Abilities.Zip(truth.Abilities))
        {
            Assert.Null(seen.Value);Assert.Equal("評判",seen.Source);o.WriteLine($"{seen.Name} {seen.Confidence}");
            var range=System.Text.RegularExpressions.Regex.Match(seen.Confidence,@"（(\d+)〜(\d+)）");Assert.True(range.Success);
            Assert.InRange(real.Value!.Value,int.Parse(range.Groups[1].Value),int.Parse(range.Groups[2].Value));
        }
        var houseOf=w.Memberships.Where(m=>m.Subject==me).Select(m=>m.Target).ToHashSet();
        var stranger=w.Memberships.First(m=>!houseOf.Contains(m.Target)&&!HouseSuccession.Factions(w).Any(f=>f.Head==m.Subject.Value)&&m.Subject!=me).Subject;
        Assert.All(CharacterDevelopment.View(w,me,stranger)!.Abilities,v=>{Assert.Null(v.Value);Assert.Equal("不明",v.Confidence);});
        var old=ContractJson.Read<ContractWorld>(ContractJson.Write(w));old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!="AbilityReputation").ToArray()};
        Assert.All(CharacterDevelopment.View(old,me,head)!.Abilities,v=>Assert.Equal("不明",v.Confidence));
    }
    // 全国の開始の歴史モード：新規ゲームの画面で選んだモードを持ち、地方の版には付けられない。
    [Fact]
    public void TheNationalStartKeepsTheChosenHistoryMode()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga(),history:HistoryMode.Fictional);
        Assert.Equal(HistoryMode.Fictional,w.NewGame!.History);
        Assert.Equal(ContractJson.Write(w),ContractJson.Write(ContractJson.Read<ContractWorld>(ContractJson.Write(w))));
        Assert.Null(NationalStart.Create(NationalStartTests.Nobunaga()).NewGame!.History);
        var start=NewGameWorldTests.Setup([NewGameWorldTests.Officer()]);
        Assert.Throws<InvalidDataException>(()=>NewGameWorld.Create(start.World,start.Registered,"created:test",0,history:HistoryMode.Historical));
    }
}

[Trait("Tier","Heavy")]
[Collection("NationalFieldSupplyIsolation")]
public sealed class NationalTruceRoundTests(ITestOutputHelper o)
{
    // 全国の開始での和睦交渉の一巡：本人（織田信長）が今川家の当主へ不戦の条件を書状で送り、相手が開封して返書し、本人が開封して成立を知る。
    [Fact]
    public void ATruceLetterTravelsToANeighbouringHeadAndTheReplyFormsTheTruce()
    {
        // 全国の世界は毎手番の写しの照合（検査だけの重い確かめ）を入れると何時間もかかるので切る（実際のゲームと同じ）。切り替えが他の検査に響かないよう、並走しない組で流す。
        bool guard=WorldSession.GuardTurnSnapshots;WorldSession.GuardTurnSnapshots=false;
        try{TruceRound();}finally{WorldSession.GuardTurnSnapshots=guard;}
    }
    void TruceRound()
    {
        var w=NationalStart.Create(NationalStartTests.Nobunaga());var s=new WorldSession(w);
        var own=new EntityId(NationalStart.FactionId("G-000131"));var imagawa=new EntityId(NationalStart.FactionId("G-000123"));
        var sent=s.NegotiateTruce(new(new("request:national-truce"),TruceAction.Propose,own,imagawa,null,20),s.PlayerControl,s.Revision);
        Assert.True(sent.Code==ResultCode.Accepted,sent.VisibleReason);
        var sw=System.Diagnostics.Stopwatch.StartNew();
        for(int n=0;n<60&&!TruceNegotiations.Known(s.DiagnosticSnapshot(),s.PlayerControl).Any();n++)
        {
            NewGameWorldTests.At(s,s.Seconds+6*3600);
            foreach(var m in s.GetActorView(s.PlayerControl).Messages.Where(m=>m.KnownBody==null).ToArray())s.Open(s.PlayerControl,m.Id,s.Revision);
        }
        var done=s.DiagnosticSnapshot();o.WriteLine($"{(done.ElapsedSeconds)/3600}時間 {sw.ElapsedMilliseconds}ms 段階={string.Join("、",s.TruceView().Select(c=>c.Stage))}");
        var known=Assert.Single(TruceNegotiations.Known(done,s.PlayerControl));
        Assert.Equal(new[]{own,imagawa}.OrderBy(x=>x.Value),new[]{known.Terms.A,known.Terms.B}.OrderBy(x=>x.Value));
        Assert.True(TruceNegotiations.WillNotInitiate(done,imagawa,own,done.ElapsedSeconds,done.CommitSequence)||!TruceNegotiations.Known(done,new(HouseSuccession.Head(done,imagawa).Value)).Any(),"相手の当主は成立を知れば出陣しない");
        var restored=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(done)));
        foreach(var x in new[]{s,restored})NewGameWorldTests.At(x,done.ElapsedSeconds+3600);
        Assert.Equal(ContractJson.Write(s.DiagnosticSnapshot()),ContractJson.Write(restored.DiagnosticSnapshot()));
    }
}
