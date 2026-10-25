namespace HakutaiKagami.Core.Contracts;

public sealed record FactionNoticeReaction(EntityId Source,EntityId Actor,FactionStanceInput Decision,long At,long Revision);
// One bounded defensive choice per NPC head per clock boundary, using actually opened papers.
public static class FactionNoticeReactions
{
    static string Key(string value)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(value));
    static bool Before(long t,long r,long at,long rev)=>t<at||t==at&&r<=rev;
    static bool Active(FactionStanceClaim c,long at)=>c.EffectiveFrom<=at&&at<c.Until;
    static FactionNoticeRequest[] Latest(ContractWorld w,long at,long rev)
    {
        var s=w.FactionNotices;if(s==null)return [];
        return s.Requests.Where(q=>q.Recipient!=w.PlayerControl&&s.Openings.Any(o=>o.Message==q.Message&&o.Actor==q.Recipient&&Before(o.At,o.Revision,at,rev)))
            .GroupBy(q=>(q.Recipient,q.Claim.Declaration)).Select(g=>g.OrderByDescending(q=>q.Claim.ObservedAt).ThenBy(q=>q.Claim.Until).ThenBy(q=>q.Input.Id.Value,StringComparer.Ordinal).First()).ToArray();
    }
    static FactionStanceInput? Decision(ContractWorld w,FactionNoticeRequest source,long at,long rev,IEnumerable<FactionNoticeReaction> prior)
    {
        var faction=w.NewGame!.Start.Factions.SingleOrDefault(f=>f.Head==source.Recipient.Value&&f.Id==source.Claim.ToOwner.Value);if(faction==null||source.Recipient==w.PlayerControl)return null;
        var owner=new EntityId(faction.Id);if(FactionStances.ResolveAuthority(w,source.Recipient,owner,at,rev)==null)return null;
        var state=w.FactionNotices!;var response=prior.SingleOrDefault(r=>r.Actor==source.Recipient&&r.Decision.Action==FactionStanceAction.DeclareHostile&&state.Requests.Single(q=>q.Input.Id==r.Source).Claim.Declaration==source.Claim.Declaration);
        if(Active(source.Claim,at)){
            if(response!=null||FactionStances.ClaimAt(w,owner,source.Claim.FromOwner,at,rev)!=null)return null;
            return new(new("decision:received-threat/"+Key(source.Claim.Declaration.Value+"/"+owner.Value)),FactionStanceAction.DeclareHostile,owner,source.Claim.FromOwner,at,source.Claim.Until);
        }
        if(response==null||response.Decision.Until<=at||w.FactionStances!.Decisions.Any(d=>d.Input.Action==FactionStanceAction.WithdrawOwnDeclaration&&d.Input.Original==response.Decision.Id&&Before(d.At,d.Revision,at,rev)))return null;
        if(Latest(w,at,rev).Any(q=>q.Recipient==source.Recipient&&q.Claim.FromOwner==source.Claim.FromOwner&&Active(q.Claim,at)))return null;
        return new(new("decision:threat-withdraw/"+Key(response.Decision.Id.Value+"/"+source.Input.Id.Value)),FactionStanceAction.WithdrawOwnDeclaration,owner,source.Claim.FromOwner,at,0,response.Decision.Id);
    }
    static FactionNoticeInput Reply(FactionStanceInput decision)=>new(new("request:faction-response/"+Key(decision.Id.Value)),decision.Action==FactionStanceAction.DeclareHostile?decision.Id:decision.Original!.Value);
    internal static (FactionNoticeRequest Source,FactionStanceInput Input)? Next(ContractWorld w,EntityId actor,long at,long rev)
    {
        var prior=w.FactionNotices?.Reactions.Where(r=>Before(r.At,r.Revision,at,rev)).ToArray()??[];
        foreach(var source in Latest(w,at,rev).Where(q=>q.Recipient==actor).OrderBy(q=>q.Input.Id.Value,StringComparer.Ordinal))if(Decision(w,source,at,rev,prior) is {} input)return(source,input);
        return null;
    }
    internal static int BoundaryCost(ContractWorld w)=>w.FactionNotices==null?0:2*Latest(w,w.ElapsedSeconds,w.CommitSequence).Select(q=>q.Recipient).Distinct().Count();
    internal static long? NextBoundary(ContractWorld w,long target)
    {
        if(w.FactionNotices==null)return null;var times=new List<long>();
        foreach(var actor in Latest(w,w.ElapsedSeconds,w.CommitSequence).Select(q=>q.Recipient).Distinct())if(Next(w,actor,w.ElapsedSeconds,w.CommitSequence)!=null)times.Add(w.ElapsedSeconds+1);
        times.AddRange(Latest(w,w.ElapsedSeconds,w.CommitSequence).SelectMany(q=>new[]{q.Claim.EffectiveFrom,q.Claim.Until}));
        return times.Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    }
    internal static void Apply(ContractWorld w,FactionNoticeRequest source,FactionStanceInput decision)
    {
        var expected=Next(w,source.Recipient,w.ElapsedSeconds,w.CommitSequence-1);
        if(expected==null||expected.Value.Source!=source||expected.Value.Input!=decision)throw new InvalidDataException("受領した原本からのNPC自身の判断ではありません");
        FactionStances.Apply(w,decision,source.Recipient);w.FactionNotices=w.FactionNotices! with{Reactions=[..w.FactionNotices.Reactions,new(source.Input.Id,source.Recipient,decision,w.ElapsedSeconds,w.CommitSequence)]};FactionNotices.Dispatch(w,Reply(decision),source.Recipient);
    }
    internal static void TakeTurns(WorldSession session)
    {
        var w=session.DiagnosticSnapshot();if(w.Paused||w.FactionNotices==null)return;
        foreach(var actor in Latest(w,w.ElapsedSeconds,w.CommitSequence).Select(q=>q.Recipient).Distinct().OrderBy(a=>a.Value,StringComparer.Ordinal)){
            w=session.DiagnosticSnapshot();if(Next(w,actor,w.ElapsedSeconds,w.CommitSequence) is {} choice)session.ReactToFactionNotice(choice.Source,choice.Input);
        }
    }
    public static void Validate(ContractWorld w)
    {
        if(w.FactionNotices is not {} s)return;
        for(int n=0;n<s.Reactions.Length;n++){
            var r=s.Reactions[n];if(r.At<0||r.At>w.ElapsedSeconds||r.Revision<=0||r.Revision>w.CommitSequence||n>0&&(r.At<s.Reactions[n-1].At||r.Revision<=s.Reactions[n-1].Revision))throw new InvalidDataException("NPC政治判断の時刻・順序が不正です");
            var expected=Next(w,r.Actor,r.At,r.Revision-1);
            if(expected==null||expected.Value.Source.Input.Id!=r.Source||expected.Value.Input!=r.Decision||!w.FactionStances!.Decisions.Any(d=>d.Input==r.Decision&&d.At==r.At&&d.Revision==r.Revision&&d.Authority.Issuer==r.Actor)||!s.Requests.Any(q=>q.Input==Reply(r.Decision)&&q.Sender==r.Actor&&q.At==r.At&&q.Revision==r.Revision))throw new InvalidDataException("開封済み原本・NPC自身の判断・返書が一致しません");
        }
    }
}
