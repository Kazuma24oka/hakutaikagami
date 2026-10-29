namespace HakutaiKagami.Core.Contracts;
public sealed record StewardReauthorizationCard(EntityId Original,EntityId Site,EntityId Head,EntityId[] Agents,long Until);
public static partial class CommunityStewardship
{
    static bool Reauthorized(ContractWorld w,StewardGrant g,long at,long rev)=>w.Stewardship!.Entries.Any(e=>e.Input.Action==StewardAction.Reauthorize&&e.Input.Id==g.Id&&Before(e.At,e.Revision,at,rev));
    static bool Superseded(ContractWorld w,StewardGrant g,long at,long rev)=>w.Stewardship!.Entries.Any(e=>e.Input.Action==StewardAction.Reauthorize&&e.Input.PriorGrant==g.Id&&Before(e.At,e.Revision,at,rev));
    static bool Member(ContractWorld w,EntityId who,EntityId owner,long at,long rev)=>w.Characters.Any(c=>c.Id==who&&c.Life==LifeState.Adult)&&w.Memberships.Any(m=>m.Subject==who&&m.Target==owner&&m.From<=at&&(at<m.Until||at==m.Until&&rev<m.Revision));
    static bool LiveSource(ContractWorld w,StewardGrant g,long at,long rev)=>FactionStances.ResolveAuthority(w,g.Principal,g.Owner,at,Math.Min(rev,w.CommitSequence))!=null;
    static bool PeriodClaim(ContractWorld w,StewardGrant g,long at,long rev)=>Before(g.From,g.Revision,at,rev)&&at<g.Until&&!Stopped(w,g,at,rev)&&!Superseded(w,g,at,rev);
    static bool Assignment(StewardAction a)=>a is StewardAction.Assign or StewardAction.Reauthorize;
    static bool Overlap(ContractWorld w,StewardGrant g,StewardEntry e)=>w.Stewardship!.Grants.Any(q=>q.Site==g.Site&&PeriodClaim(w,q,e.At,e.Revision)&&(e.Input.Action!=StewardAction.Reauthorize||q.Id!=e.Input.PriorGrant));
    static StewardGrant MakeReauthorization(ContractWorld w,StewardEntry e)
    {
        var i=e.Input;Need(HouseSuccession.Enabled(w)&&i.PriorGrant!=null&&i.Grant==null&&i.Other!=null&&i.Operation==null&&i.Amount==0&&i.Report==null&&i.ReturnAddress!=null&&i.Days is >=1 and <=30,"元の行政原本・同意する担当者・返報先を指定してください");
        var old=w.Stewardship!.Grants.Single(g=>g.Id==i.PriorGrant&&g.Revision<e.Revision);var head=HouseSuccession.Head(w,old.Owner,e.At,e.Revision-1);var agent=head==w.PlayerControl?i.Other!.Value:i.Other==head?w.PlayerControl:i.Other!.Value;
        Need(i.Site==old.Site&&head!=old.Principal&&agent!=head&&HouseSuccession.Jobs(w,e.At,e.Revision-1).Any(j=>j.Applied!=null&&j.Terms.Owner==old.Owner&&j.Terms.Incumbent==old.Principal)&&e.At<old.Until&&!Stopped(w,old,e.At,e.Revision)&&!Superseded(w,old,e.At,e.Revision),"期限内の旧当主の委任を一度だけ再確認してください");
        Need((w.PlayerControl==head||w.PlayerControl==agent||w.PlayerControl==old.Principal||w.PlayerControl==old.Agent)&&Local(w,w.PlayerControl,old.Site,e.At,e.Revision)&&Local(w,head,old.Site,e.At,e.Revision)&&Local(w,agent,old.Site,e.At,e.Revision)&&Member(w,agent,old.Owner,e.At,e.Revision)&&!PersonalLife.Resting(w,e.At,e.Revision)&&!CastleAdministration.Blocked(w,old.Site,e.At,e.Revision),"当主・担当者・引継ぎ当事者の現地立会いが必要です");
        Need((head==w.PlayerControl||w.Characters.Single(c=>c.Id==head).WillObey)&&(agent==w.PlayerControl||w.Characters.Single(c=>c.Id==agent).WillObey)&&FactionStances.ResolveAuthority(w,head,old.Owner,e.At,Math.Min(e.Revision-1,w.CommitSequence))!=null&&w.Definitions.Sites.Any(s=>s.Id==i.ReturnAddress),"新当主と担当者自身の同意・実権・固定返報先を確認してください");
        var castle=CastleAdministration.Grant(w,old.Site,e.At,e.Revision);Need(castle==null||!LiveSource(w,castle,e.At,e.Revision),"有効な城主任命と重複できません");
        return new(i.Id,head,agent,old.Owner,old.Site,i.ReturnAddress!.Value,e.At,Math.Min(old.Until,checked(e.At+i.Days*Day)),e.Revision);
    }
    internal static bool SharedDelegationBudget(ContractWorld w,EntityId site,long at,long rev)=>w.Stewardship?.Entries.Any(e=>e.Input.Action==StewardAction.Reauthorize&&e.Input.Site==site&&Before(e.At,e.Revision,at,rev))==true||CastleAdministration.SharedBudget(w,site,at,rev);
    internal static bool DelegatedCharge(ContractWorld w,EntityId actor,EntityId site,long at,long rev)=>GrantAt(w,site,at,rev) is {} g&&g.Agent==actor;
    public static StewardReauthorizationCard[] ReauthorizationView(ContractWorld w)
    {
        if(!HouseSuccession.Enabled(w)||w.Locations.Single(l=>l.Actor==w.PlayerControl) is not {Kind:LocationKind.AtSite,Site:{} site})return [];
        var cards=new List<StewardReauthorizationCard>();foreach(var old in w.Stewardship!.Grants.Where(g=>g.Site==site)){
            var head=HouseSuccession.Head(w,old.Owner);var people=w.Characters.Where(c=>c.Id!=head&&Member(w,c.Id,old.Owner,w.ElapsedSeconds,w.CommitSequence+1)&&Local(w,c.Id,site,w.ElapsedSeconds,w.CommitSequence+1)).Select(c=>c.Id).ToArray();var agents=new List<EntityId>();
            foreach(var a in people){var i=new StewardInput(new("request:steward-succession-preview"),StewardAction.Reauthorize,site,head==w.PlayerControl?a:a==w.PlayerControl?head:a,ReturnAddress:site){PriorGrant=old.Id};try{var e=new StewardEntry(i,w.ElapsedSeconds,w.CommitSequence+1);var g=MakeReauthorization(w,e);if(!Overlap(w,g,e))agents.Add(a);}catch(InvalidDataException){} }
            if(agents.Count>0)cards.Add(new(old.Id,site,head,agents.ToArray(),old.Until));
        }return cards.ToArray();
    }
}
public sealed partial class WorldSession
{
    public StewardReauthorizationCard[] StewardReauthorizationView(){lock(gate)return CommunityStewardship.ReauthorizationView(state);}
}
