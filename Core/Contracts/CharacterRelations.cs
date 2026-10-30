using System.Text;
namespace HakutaiKagami.Core.Contracts;
public enum SocialTrait { Ambition, Duty, Caution, Courage, Affection, GainSeeking }
public enum LoyaltyTarget { Person, House, Organization }
public sealed record SocialReaction(string Source,EntityId Observer,EntityId Subject,long ObservedAt,long LearnedAt,long Revision,int Affection,int Trust,int Respect,int Fear,int Hostility,string Kind);
public sealed record SocialState(int Schema,string Policy,SocialReaction[] Reactions);
public sealed record SocialScores(int Affection,int Trust,int Respect,int Fear,int Hostility);
public sealed record SocialKnownInteraction(EntityId Person,string Description,long ObservedAt,long LearnedAt,string Source);
public sealed record SocialCard(EntityId Person,SocialKnownInteraction[] Known,string Note);
public static class CharacterRelations
{
    public const string Policy="stable-six-traits-directed-known-reactions-consent-v1";
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.SocialPolicy==Policy;
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Social=new(1,Policy,[]);}
    // Diagnostic calculations are for Core decisions/tests, never an actor-view payload.
    public static int DiagnosticTrait(ContractWorld w,EntityId actor,SocialTrait trait){
        Need(Enabled(w)&&w.Characters.Any(c=>c.Id==actor)&&Enum.IsDefined(trait),"Unknown social profile");
        string h=ContractJson.Hash(Encoding.UTF8.GetBytes(Policy+"/"+w.NewGame!.Seed+"/"+actor.Value+"/"+trait));return 10+(int)(Convert.ToUInt32(h[..8],16)%81);
    }
    static bool Before(SocialReaction r,long at,long revision)=>r.LearnedAt<at||r.LearnedAt==at&&r.Revision<revision;
    public static SocialScores DiagnosticScores(ContractWorld w,EntityId observer,EntityId subject,long at=long.MaxValue,long revision=long.MaxValue){
        var rows=(w.Social?.Reactions??[]).Where(r=>r.Observer==observer&&r.Subject==subject&&Before(r,at,revision));
        int Sum(Func<SocialReaction,int> f)=>Math.Clamp(rows.Sum(f),-100,100);
        return new(Sum(r=>r.Affection),Sum(r=>r.Trust),Sum(r=>r.Respect),Sum(r=>r.Fear),Sum(r=>r.Hostility));
    }
    public static int DiagnosticLoyalty(ContractWorld w,EntityId actor,LoyaltyTarget kind,EntityId target,long at,long revision){
        Need(Enabled(w)&&Enum.IsDefined(kind),"Unknown loyalty target");var p=w.NewGame!.Start.People.Single(p=>p.Id==actor.Value);int duty=DiagnosticTrait(w,actor,SocialTrait.Duty);
        // Initial affiliations are separate targets. No relationship event changes legal membership.
        int basis=kind==LoyaltyTarget.Person?(p.Lord==target.Value?30+duty/2:0):p.Faction==target.Value?(kind==LoyaltyTarget.House?25+duty/2:15+duty/3):0;
        if(kind==LoyaltyTarget.Person)basis+=DiagnosticScores(w,actor,target,at,revision).Trust/2;
        return Math.Clamp(basis,0,100);
    }
    static SocialReaction[] Sources(ContractWorld w){
        var rows=new List<SocialReaction>();
        foreach(var q in w.Companions?.Orders.Where(q=>q.Input.Action is CompanionAction.Agree or CompanionAction.Pay or CompanionAction.PrivateInspect&&q.Input.Amount>0)??[]){
            if(q.Input.Amount<=0)continue;
            rows.Add(new("companion-payment:"+q.Input.Id.Value,q.Input.Person,w.PlayerControl,q.At,q.At,q.Revision,3,5,1,0,0,"local-payment"));
        }
        foreach(var p in w.WorkCycle?.Payments??[]){var t=w.Tasks.Single(t=>t.Id==p.Task);rows.Add(new("work-payment:"+p.Id.Value,t.Executor,t.Issuer,p.At,p.At,p.Revision,3,5,1,0,0,"local-payment"));}
        foreach(var k in w.Messaging.Knowledge){
            var m=w.Messaging.Messages.Single(m=>m.Id==k.MessageId);if(m.SenderId==m.RecipientId||k.CharacterId!=m.RecipientId)continue;
            bool supported=m.Kind=="contract-report"||m.Kind==PersonalCompanions.ReportKind;
            if(!supported)continue;bool success=m.Body=="任務完了"||m.Body=="頼まれた現地の点検を終えました。"||m.Body=="指定された拠点へ到着しました。";
            bool failure=m.Body=="履行できず・照会が必要"||m.Body=="現地点検を完了できませんでした。"||m.Body=="指定された拠点まで進めませんでした。";
            if(!success&&!failure)continue;
            // The recipient's dated impression, not a global determination of fault.
            rows.Add(new("known-report:"+m.Id+"/"+k.CharacterId,new(k.CharacterId),new(m.SenderId),m.CreatedAt,k.LearnedAt,0,success?1:0,success?4:-2,success?2:0,0,0,success?"known-completion":"known-noncompletion"));
        }
        return rows.GroupBy(r=>r.Source,StringComparer.Ordinal).Select(g=>g.First()).OrderBy(r=>r.LearnedAt).ThenBy(r=>r.Source,StringComparer.Ordinal).ToArray();
    }
    internal static void Sync(ContractWorld w){
        if(!Enabled(w))return;var rows=w.Social!.Reactions.ToList();foreach(var r in Sources(w))if(rows.All(x=>x.Source!=r.Source))rows.Add(r with{Revision=r.Revision==0?w.CommitSequence:r.Revision});w.Social=w.Social with{Reactions=rows.ToArray()};
    }
    public static bool AcceptsCompanion(ContractWorld w,EntityId person,long amount,long at,long revision){
        if(!Enabled(w))return true;if(person==w.PlayerControl)return false;
        var relation=DiagnosticScores(w,person,w.PlayerControl,at,revision);
        int score=45+(DiagnosticTrait(w,person,SocialTrait.Duty)-50)/5+(DiagnosticTrait(w,person,SocialTrait.Affection)-50)/5+(DiagnosticTrait(w,person,SocialTrait.GainSeeking)-50)/5
            -DiagnosticTrait(w,person,SocialTrait.Caution)/10+(DiagnosticTrait(w,person,SocialTrait.Courage)-50)/10+(DiagnosticTrait(w,person,SocialTrait.Ambition)-50)/10
            +relation.Trust/2-relation.Hostility/2+DiagnosticLoyalty(w,person,LoyaltyTarget.Person,w.PlayerControl,at,revision)/10+(int)Math.Min(10,amount/2);
        return score>=50;
    }
    public static SocialCard? View(ContractWorld w,EntityId person){
        if(!Enabled(w)||!w.Characters.Any(c=>c.Id==person))return null;
        var known=w.Social!.Reactions.Where(r=>r.Observer==w.PlayerControl&&r.Subject==person).Select(r=>new SocialKnownInteraction(person,r.Kind=="local-payment"?"この相手から実際に手当を受け取った":r.Kind=="known-completion"?"この相手の完了報告を受け取った":"この相手から未完了の報告を受け取った（責任は未確定）",r.ObservedAt,r.LearnedAt,r.Source)).ToArray();
        return new(person,known,"相手の内心・忠誠の数値は未確認です。自分が知った出来事と、相手が自分をどう思うかは別です。関係や派閥は正式な所属・権限を変更しません。");
    }
    public static void Validate(ContractWorld w,ContractWorld? prior=null){
        if(!Enabled(w)){Need(w.Social==null,"Social data without policy");return;}
        Need(w.Social is {Schema:1,Policy:Policy,Reactions:not null},"Missing social state");var rows=w.Social.Reactions;Need(rows.All(r=>r!=null)&&rows.Select(r=>r.Source).Distinct().Count()==rows.Length,"Duplicate social reaction");
        var expected=Sources(w);Need(expected.Length==rows.Length,"Missing social reaction");foreach(var r in rows){var source=expected.SingleOrDefault(s=>s.Source==r.Source);Need(source!=null&&r.Revision>0&&r.Revision<=w.CommitSequence&&r.LearnedAt<=w.ElapsedSeconds&&r.ObservedAt<=r.LearnedAt&&r==(source with{Revision=source.Revision==0?r.Revision:source.Revision}),"Reaction lacks personally known source");}
        if(prior?.Social is {} old)Need(rows.Take(old.Reactions.Length).SequenceEqual(old.Reactions),"Prior social observations changed");
    }
}
