namespace HakutaiKagami.Core.Contracts;
public sealed record RepresentativeObservation(EntityId Owner,EntityId Person,EntityId Home,long ObservedAt,long LearnedAt,long Revision,string Source,string Route);
// Personal dated evidence, not a view of the live foreign faction table. Arrival is not observation.
public static class RepresentativeKnowledge
{
    static bool Before(long t,long r,long at,long rev)=>t<at||t==at&&r<=rev;
    public static RepresentativeObservation[] Observations(ContractWorld w,EntityId actor,long? at=null,long? revision=null)
    {
        long t=at??w.ElapsedSeconds,r=revision??w.CommitSequence;
        if(t<0||t>w.ElapsedSeconds||r<0||r>w.CommitSequence||!w.Characters.Any(c=>c.Id==actor))throw new InvalidDataException("未確定の代表者認識です");
        var rows=new List<RepresentativeObservation>();
        foreach(var f in w.NewGame?.Start.Factions??[])rows.Add(new(new(f.Id),new(f.Head),new(f.Home),0,0,0,"scenario:"+f.Id,"開始時点の公知情報"));
        foreach(var j in HouseSuccession.Jobs(w,t,r).Where(j=>j.Applied!=null&&(j.Terms.Incumbent==actor||j.Terms.Heir==actor))){var e=j.Applied!;rows.Add(new(j.Terms.Owner,j.Terms.Heir,j.Terms.Home,e.At,e.At,e.Revision,j.Terms.Id.Value,"現地立会"));}
        void Signed(FactionStanceAuthority? a,string id,IEnumerable<FactionNoticeOpening> openings){
            if(a==null||!Before(a.At,a.Revision,t,r))return;
            var o=openings.SingleOrDefault(o=>o.Message==id&&o.Actor==actor&&Before(o.At,o.Revision,t,r));
            if(o!=null)rows.Add(new(a.Owner,a.Issuer,a.Home,a.At,o.At,a.Revision,id,"本人開封した署名"));
        }
        foreach(var p in w.Truces?.Papers??[])if(Before(p.At,p.Revision,t,r))Signed(p.Authority,p.Message,w.Truces!.Openings);
        foreach(var p in w.FactionNotices?.Requests??[])if(Before(p.At,p.Revision,t,r))Signed(p.Authority,p.Message,w.FactionNotices!.Openings);
        foreach(var p in w.TruceConfirmations?.Papers??[])if(Before(p.At,p.Revision,t,r))Signed(p.Authority,p.Id,w.TruceConfirmations!.Openings);
        return rows.OrderBy(x=>x.ObservedAt).ThenBy(x=>x.Revision).ThenBy(x=>x.Source,StringComparer.Ordinal).ToArray();
    }
    public static RepresentativeObservation? Known(ContractWorld w,EntityId actor,EntityId owner,long? at=null,long? revision=null)=>Observations(w,actor,at,revision).LastOrDefault(o=>o.Owner==owner);
}
