namespace HakutaiKagami.Core.Contracts;

public enum CouncilRule { Majority, TwoThirds, Unanimous }
public sealed record CouncilCharter(EntityId Origin,CouncilRule Rule);

public sealed record CouncilBallot(CouncilCharter Charter,EntityId Demand,EntityId DemandingHouse,SubmissionConditions Conditions,bool ChairSupports);

public static class KokujinCouncil
{
    // 開封した承諾と成立確認だけから、その武将が加盟を確認できる家を返す。
    public static EntityId[] KnownMembers(ContractWorld world,EntityId observer,CouncilCharter charter,long at,long revision)
    {
        return TreatyNegotiations.Known(world,observer,at,revision)
            .Where(f=>f.Terms.Charter==charter && at<f.Terms.Until &&
                !TreatyNegotiations.KnowsEnding(world,observer,f.Acceptance,at,revision))
            .SelectMany(f=>new[]{f.Terms.A,f.Terms.B}).Distinct().OrderBy(id=>id.Value,StringComparer.Ordinal).ToArray();
    }

    public sealed record Count(int Houses,int Yes,int No,int Required)
    {
        public TreatyAction? Verdict=>Yes>=Required?TreatyAction.Accept:Houses-No<Required?TreatyAction.Decline:null;
    }
    public static Count? Tally(ContractWorld w,EntityId chair,EntityId demand,long at,long revision)
    {
        var state=w.Treaties;
        if(state==null)return null;
        var papers=state.Papers.Where(p=>p.At<=at && p.Revision<=revision).ToArray();
        var questions=papers.Where(p=>p.Author==chair && p.Input.Ballot?.Demand==demand).ToArray();
        if(questions.Length==0)return null;
        var first=questions[0];var ballot=first.Input.Ballot!;
        var members=KnownMembers(w,chair,ballot.Charter,first.At,first.Revision-1);
        int yes=ballot.ChairSupports?1:0,no=ballot.ChairSupports?0:1;
        foreach(var q in questions.Where(q=>members.Contains(q.Input.Target))){
            var vote=papers.SingleOrDefault(p=>p.Input.Source==q.Input.Id && p.Recipient==chair &&
                state.Openings.Any(o=>o.Actor==chair && o.Message==p.Message && o.At<=at && o.Revision<=revision));
            if(vote?.Input.Action==TreatyAction.Accept)yes++;
            if(vote?.Input.Action==TreatyAction.Decline)no++;
        }
        return new(members.Length,yes,no,RequiredVotes(ballot.Charter.Rule,members.Length));
    }

    public static int RequiredVotes(CouncilRule rule,int houses)
    {
        if(houses<2)throw new ArgumentOutOfRangeException(nameof(houses));
        return rule switch {
            CouncilRule.Majority=>houses/2+1,
            CouncilRule.TwoThirds=>houses-houses/3,
            CouncilRule.Unanimous=>houses,
            _=>throw new ArgumentOutOfRangeException(nameof(rule))
        };
    }
}
