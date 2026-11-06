namespace HakutaiKagami.Core.Contracts;

public enum CouncilRule { Majority, TwoThirds, Unanimous }
public sealed record CouncilCharter(EntityId Origin,CouncilRule Rule);

public sealed record CouncilBallot(CouncilCharter Charter,EntityId Demand,EntityId DemandingHouse,SubmissionConditions Conditions,bool ChairSupports);

public sealed record CouncilDepartureCopy(EntityId Original,CouncilCharter Charter,EntityId DepartingHouse);

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

    // 契状の解除予告に引用できるのは、離脱当主が成立を確認した自家の個別従属だけ。
    internal static TreatyFormation? SeparateSubmission(ContractWorld w,TreatyFormation charter,EntityId head,long at,long revision)
    {
        if(charter.Terms.Charter==null)return null;
        return TreatyNegotiations.Known(w,head,at,revision).FirstOrDefault(f=>
            f.Terms.Submission!=null && f.Terms.B==charter.Terms.B && f.Terms.A!=charter.Terms.A &&
            f.At>=charter.At && at<f.Terms.Until && !TreatyNegotiations.KnowsEnding(w,head,f.Acceptance,at,revision) &&
            !(w.Treaties?.Papers.Any(p=>p.Input.Ballot?.Demand==f.Terms.Origin)??false));
    }

    public static TreatyOperationInput? Departure(ContractWorld w,TreatyFormation charter,EntityId head,long at,long revision)
    {
        if(at+charter.Terms.NoticeSeconds>=charter.Terms.Until ||
            w.Treaties!.Operations.Any(o=>o.Input.Acceptance==charter.Acceptance && o.Kind==TreatyOperation.Terminate && o.Author==head))return null;
        if(SeparateSubmission(w,charter,head,at,revision) is not {} submission)return null;
        return new(HouseholdCourt.Id("council-departure",charter.Acceptance.Value+"/"+submission.Acceptance.Value),
            TreatyOperation.Terminate,charter.Acceptance,charter.Terms.B,charter.Terms.A,
            EffectiveAt:at+charter.Terms.NoticeSeconds,Evidence:submission.Acceptance.Value);
    }

    public static bool KnowsSeparateDeparture(ContractWorld w,EntityId observer,EntityId house,long at,long revision)
    {
        var state=w.Treaties;
        if(state==null)return false;
        if(state.Papers.Any(p=>p.Input.DepartureCopy?.DepartingHouse==house && p.Recipient==observer &&
            state.Openings.Any(o=>o.Message==p.Message && o.Actor==observer && o.At<=at && o.Revision<=revision)))return true;
        return state.Operations.Any(p=>p.Kind==TreatyOperation.Terminate && p.Terms.Charter!=null &&
            p.Input.Owner==house && p.Intent.Evidence!=null && p.Recipient==observer && p.At<=at && p.Revision<=revision &&
            state.OperationOpenings.Any(o=>o.Message==p.Message && o.Actor==observer && o.At<=at && o.Revision<=revision));
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
