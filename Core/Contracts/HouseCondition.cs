namespace HakutaiKagami.Core.Contracts;

public sealed record SurveyObservation(EntityId Site,SurveyBook Book);
public sealed record HouseConditionView(EntityId House,EntityId Site,long ObservedAt,bool ControlsLocalSite,
    EntityId[] LocalLandholders,EntityId[] LocalCastellans,SurveyObservation[] KnownSurveys);

public static class HouseCondition
{
    public static HouseConditionView? Observe(ContractWorld w,EntityId viewer,EntityId house)
    {
        if(w.NewGame==null || SocialWorld.Site(w,viewer) is not {} site ||
            DefectionPacts.HouseAt(w,viewer,w.ElapsedSeconds,w.CommitSequence)!=house)return null;
        var members=w.Memberships.Where(Active).Where(m=>m.Target==house).Select(m=>m.Subject).ToHashSet();
        var rights=w.LandRights.Where(Active).Where(r=>r.Target==site && r.Kind=="control").ToArray();
        var holders=rights.Where(r=>members.Contains(r.Subject)).Select(r=>r.Subject).Distinct().ToArray();
        var officers=w.Offices.Where(Active).Where(o=>o.Target==site && members.Contains(o.Subject) &&
            o.Kind is "castellan" or "house-castellan").Select(o=>o.Subject).ToList();
        if(CastleAdministration.Grant(w,site,w.ElapsedSeconds,w.CommitSequence+1) is {} grant && grant.Owner==house)
            officers.Add(grant.Agent);
        var jobs=(w.Surveys?.Entries??[]).Where(e=>e.Input.Owner==house &&
            e.Input.Action is SurveyAction.Measurement or SurveyAction.Statement).ToDictionary(e=>e.Input.Id);
        var surveys=LandSurvey.KnownBooks(w,viewer).Where(b=>jobs.ContainsKey(b.Job))
            .Select(b=>new SurveyObservation(jobs[b.Job].Input.Site,b)).OrderByDescending(e=>e.Book.At).ToArray();
        return new(house,site,w.ElapsedSeconds,rights.Any(r=>r.Subject==house) &&
            !CastleAdministration.Blocked(w,site,w.ElapsedSeconds,w.CommitSequence+1),holders,officers.Distinct().ToArray(),surveys);
        bool Active(TimedRelation r)=>r.From<=w.ElapsedSeconds && w.ElapsedSeconds<r.Until;
    }
}
