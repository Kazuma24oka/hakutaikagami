namespace HakutaiKagami.Core.Contracts;

public enum CouncilRule { Majority, TwoThirds, Unanimous }
public sealed record CouncilCharter(EntityId Origin,CouncilRule Rule);

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
