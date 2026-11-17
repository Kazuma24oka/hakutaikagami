using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
using Xunit;

namespace HakutaiKagami.Tests;

// S11（2026-11-17）：画面と文面の仕上げ。書状の帳面・自動の停止・人物帳・判断の比較・ルールのつまみ・史実の機会・家督と約束と軍勢の帳面・呼び方。
[Trait("Tier","Heavy")]
public sealed class Screens2Tests
{
    static readonly Lazy<ContractWorld> start=new(()=>NationalStart.Create(NationalStartTests.Nobunaga(),timePolicy:WorldTimeControl.Policy,history:HistoryMode.Historical));
    static ContractWorld World()=>ContractJson.Copy(start.Value);
    static string House(ContractWorld w)=>Screens2.HouseOf(w,w.PlayerControl)!;
    static EntityId Retainer(ContractWorld w)=>Screens2.Commanders(w,w.PlayerControl).First();
    static void Send(ContractWorld w,string id,string kind,EntityId from,EntityId to,string body)
    {
        MessageFlow.Send(w.Messaging,new MessageEnvelope(id,id,kind,from.Value,to.Value,body,FamilyBirth.People(w).Single(p=>p.Id==to.Value).Site,w.ElapsedSeconds),w.ElapsedSeconds,1);
        MessageFlow.Advance(w.Messaging,w.ElapsedSeconds+1);
        // 封をした書状は、届いても本人が開くまで中身を知らない（ゲームの開封と同じ）。
        w.SealedMessages=[..w.SealedMessages,new(id)];w.Messaging.Knowledge=w.Messaging.Knowledge.Where(k=>k.MessageId!=id||k.CharacterId!=to.Value).ToArray();
    }
    static void Learn(ContractWorld w,string id)=>w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(id,w.PlayerControl.Value,w.ElapsedSeconds,id)];

    [Fact]
    public void TheLetterBookShowsOnlyLettersThePlayerHasAndHidesUnopenedText()
    {
        var w=World();Assert.True(Screens2.On(w));var me=w.PlayerControl;var other=Retainer(w);
        Send(w,"message:test/a","strategy-outcome",other,me,"合戦は勝ち");Send(w,"message:test/b",FamilyNotice.Kind,other,me,"父上ご逝去");
        Send(w,"message:test/c","contract-order",me,other,"見回りを頼む");Send(w,"message:test/d","contract-order",other,Screens2.Commanders(w,me).Skip(1).First(),"よそ");
        w.ElapsedSeconds+=1;Learn(w,"message:test/a");
        var rows=Screens2.Letters(w,me);Assert.True(rows.Length>=3,string.Join(" | ",rows.Select(r=>r.ToString())));
        Assert.Contains(rows,r=>r.Id=="message:test/a"&&r.Incoming&&r.Opened&&r.Body=="合戦は勝ち"&&r.Category=="軍");
        Assert.Contains(rows,r=>r.Id=="message:test/b"&&!r.Opened&&r.Body==null&&r.Category=="家族"&&r.Kind=="訃報");
        Assert.Contains(rows,r=>r.Id=="message:test/c"&&!r.Incoming&&r.Category=="仕事");
        Assert.DoesNotContain(rows,r=>r.Id=="message:test/d");
        Assert.All(Screens2.Letters(w,me,"家族"),r=>Assert.Equal("家族",r.Category));
        // 一覧に Screens2 の無い世界では帳面を出さない。
        var old=ContractJson.Copy(w);old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(x=>x!=NationalSystems.Screens2).ToArray()};
        Assert.Empty(Screens2.Letters(old,me));
    }

    [Fact]
    public void AnOpenedObituaryAndAKnownGraveIllnessStopTheClock()
    {
        var w=World();Assert.Contains(w.TimeControl!.Rules,r=>r.Kind==TimeNoticeKind.SevereIllness);
        var prior=ContractJson.Copy(w);var other=Retainer(w);
        Send(w,"message:test/obituary",FamilyNotice.Kind,other,w.PlayerControl,"父上ご逝去");w.ElapsedSeconds+=1;
        // 届いただけ（開いていない）では止まらない。
        WorldTimeControl.Observe(prior,w);Assert.DoesNotContain(w.TimeControl!.Notices,n=>n.Kind==TimeNoticeKind.Death);
        Learn(w,"message:test/obituary");WorldTimeControl.Observe(prior,w);
        Assert.Contains(w.TimeControl!.Notices,n=>n.Kind==TimeNoticeKind.Death&&n.Source=="message:test/obituary"&&n.Response==TimeResponse.Pause);Assert.NotNull(WorldTimeControl.Pending(w));
        // 本人の重い病の始まりで止まる（重さ3の病のある人を本人にして確かめる）。
        var sick=FamilyBirth.People(start.Value).Take(400).Select(p=>new EntityId(p.Id)).SelectMany(p=>PersonalHealth.Conditions(start.Value,p,start.Value.ElapsedSeconds+40*365*86400L)).First(c=>c.Severity>=3);
        var x=World();x.PlayerControl=sick.Person;x.ElapsedSeconds=sick.OnsetAt;
        Assert.Contains(sick.Id.Value,Screens2.SevereIllness(x));
        x.ElapsedSeconds+=3600;Assert.DoesNotContain(sick.Id.Value,Screens2.SevereIllness(x));
        // 設定の無い（前の版の）時間の設定でも検証は通る。
        var old=World();old.NewGame=old.NewGame! with{NationalSystems=old.NewGame.NationalSystems!.Where(n=>n!=NationalSystems.Screens2).ToArray()};old.TimeControl=WorldTimeControl.Initial();WorldTimeControl.Validate(old,true);
        Assert.ThrowsAny<InvalidDataException>(()=>{var bad=World();bad.TimeControl=WorldTimeControl.Initial();WorldTimeControl.Validate(bad,true);});
    }

    [Fact]
    public void ThePersonBookShowsOwnValuesByFieldAndOnlyEstimatesForOthers()
    {
        var w=World();var me=w.PlayerControl;
        var own=Screens2.Book(w,me,me)!;
        Assert.True(own.Own);Assert.Equal(6,own.Fields.Length);Assert.Equal(32,own.Fields.Sum(f=>f.Skills.Length));Assert.All(own.Fields.SelectMany(f=>f.Skills),s=>Assert.NotNull(s.Value));
        Assert.Equal(["面会","書状","政務","移動","武芸","修行"],own.Care.Select(c=>c.Action));Assert.True(own.Care.Single(c=>c.Action=="政務").Can); // 当主は政務の権限がある
        var other=Retainer(w);var book=Screens2.Book(w,me,other)!;
        Assert.False(book.Own);Assert.All(book.Abilities.Concat(book.Fields.SelectMany(f=>f.Skills)),v=>Assert.Null(v.Value));Assert.Empty(book.Care);Assert.Empty(book.Growth);
        // 家臣の政務は権限がない（役がない）。
        var x=ContractJson.Copy(w);x.PlayerControl=other;Assert.Contains(Screens2.Care(x,other),c=>c.Action=="政務"&&(c.Can||c.Reason=="権限"));
    }

    [Fact]
    public void TheDecisionSupportSettingChangesOnlyTheDetailOfTheComparison()
    {
        var w=World();var c=w.NewGame!.Start.Factions.First(f=>f.Head!=w.PlayerControl.Value);
        // 誘いを開いた本人に、包囲網の誘いの危機が出る。
        var coalition=new StrategyCoalition(new("coalition:test"),new(c.Id),[new(c.Id),new(c.Id)],w.ElapsedSeconds,w.CommitSequence){Invites=[new(new(House(w)),w.PlayerControl,new("message:test/invite"),Retainer(w))]};
        w.Strategy=(w.Strategy??new StrategyState(1,NationalStrategy.Policy,[],[])) with{Coalitions=[coalition]};
        Send(w,"message:test/invite",NationalStrategy.InviteKind,Retainer(w),w.PlayerControl,"誘い");Learn(w,"message:test/invite");
        ContractWorld With(DecisionSupport s){var x=ContractJson.Copy(w);x.NewGame=x.NewGame! with{Judgment=x.NewGame.Judgment! with{Support=s}};return x;}
        var detailed=Screens2.Crises(With(DecisionSupport.Detailed),w.PlayerControl).Single(x=>x.Kind=="包囲網の誘い");
        var brief=Screens2.Crises(With(DecisionSupport.Brief),w.PlayerControl).Single(x=>x.Kind=="包囲網の誘い");
        var self=Screens2.Crises(With(DecisionSupport.Independent),w.PlayerControl).Single(x=>x.Kind=="包囲網の誘い");
        Assert.All(detailed.Options,o=>Assert.False(string.IsNullOrEmpty(o.Basis)||string.IsNullOrEmpty(o.Uncertainty)||string.IsNullOrEmpty(o.Preparation)));
        Assert.All(brief.Options,o=>Assert.True(o.Basis==""&&o.Cost!=""));
        Assert.All(self.Options,o=>Assert.True(o.Cost==""&&o.Time==""));
        // どの設定でも基本の情報と警告は同じ。
        Assert.Equal(detailed.What,self.What);Assert.Equal(detailed.Warning,self.Warning);Assert.Equal(detailed.Options.Select(o=>o.Name),self.Options.Select(o=>o.Name));
    }

    [Fact]
    public void RuleKnobsAreSavedAndTheStandardCourseKeepsTheOldValues()
    {
        var w=World();
        Assert.Equal(900,Screens2.EventChance(w,900));Assert.Equal(350,Screens2.CaptureChance(w));Assert.Equal(15,Screens2.WoundChance(w));Assert.Equal(96,Screens2.BirthThreshold(w));Assert.Equal(200,Screens2.ThreatChance(w,200));
        var standard=Screens2.Courses.Single(c=>c.Name=="標準").Knobs;var stormy=Screens2.Courses.Single(c=>c.Name=="波乱").Knobs with{Births=RuleLevel.Low};
        ContractWorld With(RuleKnobs k){var x=ContractJson.Copy(w);x.NewGame=x.NewGame! with{Judgment=x.NewGame.Judgment! with{Rules=k}};return x;}
        var s=With(standard);Assert.Equal(900,Screens2.EventChance(s,900));Assert.Equal(350,Screens2.CaptureChance(s));Assert.Equal(96,Screens2.BirthThreshold(s));
        var r=With(stormy);Assert.Equal(540,Screens2.EventChance(r,900));Assert.Equal(300,Screens2.ThreatChance(r,200));Assert.Equal(500,Screens2.CaptureChance(r));Assert.Equal(25,Screens2.WoundChance(r));Assert.Equal(48,Screens2.BirthThreshold(r));
        // 新規開始で選んだつまみは保存に残り、一覧の外の世界では認めない。
        var knobbed=NationalStart.Create(NationalStartTests.Nobunaga(),knobs:new(NpcAttention.Standard,NpcChallenge.Strong,DecisionSupport.Independent,stormy));
        Assert.Equal(stormy,ContractJson.Copy(knobbed).NewGame!.Judgment!.Rules);
        var bad=knobbed.NewGame! with{NationalSystems=knobbed.NewGame.NationalSystems!.Where(n=>n!=NationalSystems.Screens2).ToArray()};
        Assert.ThrowsAny<InvalidDataException>(()=>Screens2.ValidateKnobs(bad));
    }

    static (ContractWorld W,HistoricalCampaign E) Opportunity()
    {
        var w=World();var house=House(w);var owners=NationalStrategy.Owners(w,w.Strategy);
        var e=NationalStrategy.HistoryFor(w).First(e=>NationalStart.FactionId(e.Attacker)==house&&owners.Where(x=>x.Value==house).Any(x=>NationalStrategy.Neighbors(x.Key).Any(n=>owners.GetValueOrDefault(n)==NationalStart.FactionId(e.Defender))));
        w.ElapsedSeconds=NationalStrategy.MonthStart(w,1560,6);w.CommitSequence+=5;
        var offers=new List<HistoryOpportunity>();Assert.True(Screens2.Offer(w,e,house,owners,offers));
        w.Strategy=(w.Strategy??new StrategyState(1,NationalStrategy.Policy,[],[])) with{Opportunities=offers.ToArray()};
        MessageFlow.Advance(w.Messaging,w.ElapsedSeconds+1);w.ElapsedSeconds+=1;Learn(w,offers[0].Letter.Value);
        return (w,e);
    }
    static ContractWorld NextMonth(ContractWorld w){w.ElapsedSeconds=NationalStrategy.MonthStart(w,1560,7);w.CommitSequence+=5;NationalStrategy.Apply(w);return w;}

    [Fact]
    public void AHistoricalOpportunityReachesThePlayerAndItsAnswerDecidesTheEvent()
    {
        var (w,e)=Opportunity();var letter=w.Strategy!.Opportunities!.Single().Letter;
        var body=w.Messaging.Messages.Single(m=>m.Id==letter.Value).Body;Assert.Contains("様",body);Assert.Contains("恐惶謹言",body); // 家老から当主への書状
        Assert.Contains(Screens2.Crises(w,w.PlayerControl),c=>c.Kind=="出陣の機会");
        var accepted=ContractJson.Copy(w);Screens2.Answer(accepted,letter,true);Assert.Contains(Screens2.Promises(accepted,accepted.PlayerControl).Private,p=>p.Kind=="出陣の約束");
        NextMonth(accepted);
        Assert.Contains(accepted.Strategy!.Campaigns,c=>c.Basis=="history:"+e.Id&&c.Attacker.Value==House(accepted));
        Assert.Equal("fired",accepted.Strategy.Opportunities!.Single().Result);Assert.Contains(accepted.Strategy.History!,h=>h.Event==e.Id&&h.Result=="fired");
        NationalStrategy.Validate(accepted,null);
        // 相手の家と約束があれば、応じても競合で不発。
        var bound=ContractJson.Copy(w);Screens2.Answer(bound,letter,true);var defender=NationalStart.FactionId(e.Defender);
        bound.Strategy=bound.Strategy! with{Pacts=[..bound.Strategy.Pacts??[],new(new("pact:test"),new(House(bound)),new(defender),"truce",NationalStrategy.MonthStart(bound,1560,6),NationalStrategy.MonthStart(bound,1561,6),bound.CommitSequence,"test")]};
        NextMonth(bound);Assert.Equal("conflict",bound.Strategy!.Opportunities!.Single().Result);Assert.DoesNotContain(bound.Strategy.Campaigns,c=>c.Basis=="history:"+e.Id);
        // 断れば見送り。
        var declined=ContractJson.Copy(w);Screens2.Answer(declined,letter,false);NextMonth(declined);Assert.Equal("player-declined",declined.Strategy!.Opportunities!.Single().Result);
    }

    // 画面の検査（Game/Scenes/Screens2Check.tscn）の入力。HAKUTAI_SCREENS2_FIXTURE があるときだけ書く。
    [Fact]
    public void WritesTheScreenCheckInput()
    {
        if(Environment.GetEnvironmentVariable("HAKUTAI_SCREENS2_FIXTURE") is not {} dir)return;
        // 開始の時点の世界に、史実の機会の書状を出した（まだ道中）ものを画面の検査の入力にする。
        var w=World();var house=House(w);var owners=NationalStrategy.Owners(w,w.Strategy);var offers=new List<HistoryOpportunity>();
        var e=NationalStrategy.HistoryFor(w).First(e=>NationalStart.FactionId(e.Attacker)==house&&Screens2.Offer(ContractJson.Copy(w),e,house,owners,[]));
        Assert.True(Screens2.Offer(w,e,house,owners,offers));w.Strategy=(w.Strategy??new StrategyState(1,NationalStrategy.Policy,[],[])) with{Opportunities=offers.ToArray()};
        ContractValidation.Validate(w);Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"ready.json"),ContractJson.Write(w));
    }

    [Fact]
    public void TheHeadOrdersACorpsAndTheHouseStrategyMustersItByPriority()
    {
        var w=World();w.ElapsedSeconds=NationalStrategy.MonthStart(w,1560,6);w.CommitSequence+=5;NationalStrategy.Apply(w);
        var provinces=Screens2.Provinces(w,w.PlayerControl);Assert.NotEmpty(provinces);var commander=Retainer(w);
        foreach(var (p,n) in provinces.Select((p,n)=>(p,n)))Screens2.Order(w,p,commander,n+1);
        Assert.ThrowsAny<InvalidDataException>(()=>Screens2.Order(w,provinces[0],commander,1)); // 同じ国に二度は命じない
        Assert.Equal(provinces.Length,Screens2.Armies(w,w.PlayerControl).Count(a=>a.State=="命じた"));
        NextMonth(w);
        var used=w.Strategy!.Orders!.Where(o=>o.UsedAt!=null).ToArray();Assert.Single(used);
        {
            // 優先度の高い（数の小さい）命から、攻められる城のある最初の国。
            var c=w.Strategy.Campaigns.Single(c=>c.Basis=="order:"+used[0].Id.Value);Assert.Equal(commander,c.Commander);
            Assert.Contains(Screens2.Armies(w,w.PlayerControl),a=>a.Id==c.Id.Value&&a.Priority==used[0].Priority&&a.State=="出陣中");
        }
        NationalStrategy.Validate(w,null);
        // 当主でない者は命じられない。
        var x=ContractJson.Copy(w);x.PlayerControl=commander;Assert.ThrowsAny<InvalidDataException>(()=>Screens2.Order(x,provinces[0],commander,1));
    }

    [Fact]
    public void SuccessionPromisesAndAddressesFollowWhatThePlayerKnows()
    {
        var w=World();var me=w.PlayerControl;var retainer=Retainer(w);
        var book=Screens2.Succession(w,me)!;Assert.True(book.ViewerIsHead);Assert.Equal(me,book.Head);
        Assert.All(book.Candidates,c=>Assert.Contains(HeirDesignation.KnownChildren(w,book.House),k=>k.Person==c.Person));
        var x=ContractJson.Copy(w);x.PlayerControl=retainer;var seen=Screens2.Succession(x,retainer)!;Assert.False(seen.ViewerIsHead);Assert.Empty(seen.Candidates);
        // 家の約束は当主だけが持ち、私的な約束と分ける。
        var (house,mine)=Screens2.Promises(w,me);Assert.All(house,p=>Assert.Contains(p.Kind,new[]{"同盟","和睦"}));Assert.DoesNotContain(mine,p=>p.Kind is "同盟" or "和睦");
        Assert.Empty(Screens2.Promises(x,retainer).House);
        // 宛名と結び：家臣から当主へは「様」と「恐惶謹言」、当主から家臣へは「へ」と「謹言」。
        Assert.EndsWith("様",Screens2.Address(w,retainer,me));Assert.Equal("恐惶謹言",Screens2.Closing(w,retainer,me));
        Assert.EndsWith("へ",Screens2.Address(w,me,retainer));Assert.Equal("謹言",Screens2.Closing(w,me,retainer));
        Assert.StartsWith(Screens2.Title(w,me),Screens2.Address(w,retainer,me));
        Assert.EndsWith(".png",Screens2.StageFor(me,"talk").Portrait);
    }
}
