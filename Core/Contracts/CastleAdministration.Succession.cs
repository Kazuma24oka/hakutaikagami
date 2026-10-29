namespace HakutaiKagami.Core.Contracts;
public sealed record CastellanReauthorizationCard(EntityId Original,EntityId Site,EntityId Head,EntityId[] Agents,long Until);
public static partial class CastleAdministration
{
    internal static bool IsAppointment(CastellanAction a)=>a is CastellanAction.Appoint or CastellanAction.Reauthorize;
    static bool Effective(ContractWorld w,CastellanEntry e,long at,long rev)=>FactionStances.ResolveAuthority(w,e.Principal,e.Owner,at,Math.Min(rev,w.CommitSequence))!=null;
    internal static bool SharedBudget(ContractWorld w,EntityId site,long at,long rev)=>w.Castellan?.Entries.Any(e=>e.Input.Site==site&&e.Input.Action==CastellanAction.Reauthorize&&Before(e,at,rev))==true;
    static CastellanEntry Reauthorize(ContractWorld w,CastellanInput i,long at,long rev,EntityId owner,EntityId head)
    {
        Need(HouseSuccession.Enabled(w)&&i.Appointment!=null&&i.Person!=null&&i.Days is >=1 and <=30,"元の任命・担当者・期限を指定してください");
        var old=Prefix(w,at,rev).LastOrDefault(e=>e.Input.Site==i.Site&& (IsAppointment(e.Input.Action)||e.Input.Action==CastellanAction.Release));
        Need(old!=null&&IsAppointment(old.Input.Action)&&old.Input.Id==i.Appointment&&old.Principal!=head&&at<old.Until&&HouseSuccession.Jobs(w,at,rev-1).Any(j=>j.Applied!=null&&j.Terms.Owner==owner&&j.Terms.Incumbent==old.Principal),"期限内の旧当主の最新任命を一度だけ再確認してください");
        var agent=i.Person!.Value;
        Need(agent!=head&&(w.PlayerControl==head||w.PlayerControl==agent||w.PlayerControl==old.Principal||w.PlayerControl==old.Agent)&&Local(w,head,i.Site,at,rev)&&Local(w,agent,i.Site,at,rev)&&Bound(w,agent,head,owner,at,rev)&&FactionStances.ResolveAuthority(w,head,owner,at,Math.Min(rev-1,w.CommitSequence))!=null,"現当主と新しい成人直属家臣の現地立会いが必要です");
        Need((head==w.PlayerControl||w.Characters.Single(c=>c.Id==head).WillObey)&&(agent==w.PlayerControl||w.Characters.Single(c=>c.Id==agent).WillObey),"当主と担当者自身の同意が必要です");
        Need(!w.Stewardship!.Grants.Any(g=>g.Site==i.Site&&CommunityStewardship.Active(w,g,at,rev)),"有効な代理行政委任と重複できません");
        return new(i,head,agent,owner,at,rev,Math.Min(old.Until,checked(at+i.Days*86400L)),null);
    }
    public static CastellanReauthorizationCard[] ReauthorizationView(ContractWorld w)
    {
        if(!HouseSuccession.Enabled(w)||w.Locations.Single(l=>l.Actor==w.PlayerControl) is not {Kind:LocationKind.AtSite,Site:{} site})return [];
        long at=w.ElapsedSeconds,rev=w.CommitSequence+1;var old=Prefix(w,at,rev).LastOrDefault(e=>e.Input.Site==site&&(IsAppointment(e.Input.Action)||e.Input.Action==CastellanAction.Release));if(old==null||!IsAppointment(old.Input.Action))return [];
        var head=HouseSuccession.Head(w,old.Owner);var agents=new List<EntityId>();foreach(var p in w.Characters.Where(c=>c.Id!=head)){try{Prepare(w,new(new("request:castellan-succession-preview"),CastellanAction.Reauthorize,site,p.Id,old.Input.Id),at,rev);agents.Add(p.Id);}catch(InvalidDataException){}}
        return agents.Count==0?[]:[new(old.Input.Id,site,head,agents.ToArray(),old.Until)];
    }
}
public sealed partial class WorldSession
{
    public CastellanReauthorizationCard[] CastellanReauthorizationView(){lock(gate)return CastleAdministration.ReauthorizationView(state);}
}
