namespace HakutaiKagami.Core.Contracts;

public sealed record FormerSeatObservation(EntityId House, EntityId Site, long ObservedAt);

public static class HouseRestoration
{
    // 以前本人が仕えていた時に現地で見た支配と、現在地で確かめられる喪失だけを扱う。
    public static FormerSeatObservation[] ObserveFormerSeat(ContractWorld world, EntityId observer)
    {
        if (world.NewGame == null || SocialWorld.Site(world, observer) is not {} site)
            return [];

        var now = world.ElapsedSeconds;
        var observations = new List<FormerSeatObservation>();
        var service = world.Memberships.Where(row => row.Subject == observer && row.Kind == "membership");
        foreach (var house in service.Select(row => row.Target).Distinct())
        {
            var rights = world.LandRights.Where(row =>
                row.Subject == house && row.Target == site && row.Kind == "control").ToArray();
            if (rights.Any(row => row.From <= now && now < row.Until))
                continue;

            var witnessed = rights.Any(right => service.Any(member =>
            {
                var since = Math.Max(member.From, right.From);
                var until = Math.Min(member.Until, right.Until);
                return member.Target == house && since < until && until <= now &&
                    PersonalCompanions.SiteAt(world, observer, since, world.CommitSequence) == site;
            }));
            if (witnessed)
                observations.Add(new(house, site, now));
        }
        return observations.OrderBy(row => row.House.Value, StringComparer.Ordinal).ToArray();
    }
}
