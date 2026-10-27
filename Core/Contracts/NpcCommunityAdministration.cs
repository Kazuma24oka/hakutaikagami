namespace HakutaiKagami.Core.Contracts;
public sealed record CommunityDecision(EntityId Id,EntityId Actor,EntityId Site,long At,long Revision,string InputsHash,EconomyAction? Action,long Amount,string Reason,EntityId? Representative);
public sealed record CommunityDecisionInputs(EntityId Actor,EntityId Site,long At,bool Local,bool Authority,CommunityAccounts? Account,long Reserved);
public sealed record CommunityAdministrationState(int Schema,string Policy,CommunityDecision[] Decisions);
// Small local administration policy; no remote stock reads, generic omniscience or player-house automation.
public static class NpcCommunityAdministration
{
    public const string Policy="village-local-npc-community-administration-v6";
    const long Day=86400;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or CommunityStewardship.Policy or MaterialMarkets.Policy or RegionalDomains.Policy or HouseLogistics.Policy or HouseFreightReceiving.Policy or MilitaryEconomy.Policy or NpcFreight.Policy or FreightOperations.Policy or FieldConvoy.Policy or ArmyRoadMarch.Policy;
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.CommunityAdministration?.Decisions.Any(d=>d.Id==id)==true;
    static DateOnly Date(ContractWorld w,long at)=>DateOnly.Parse(w.EpochDate).AddDays(checked((int)(at/Day)));
    static int Period(ContractWorld w,long at){var d=Date(w,at);return d.Year*12+d.Month;}
    static EntityId? Principal(ContractWorld w,EntityId site)=>w.NewGame!.Start.Castles.SingleOrDefault(c=>c.Id==site.Value)?.Faction is {} owner&&w.NewGame.Start.Factions.SingleOrDefault(f=>f.Id==owner) is {} f?new(f.Head):null;
    static EntityId Id(ContractWorld w,EntityId site,long at,long revision=long.MaxValue)=>new("decision:npc-community/"+site.Value.Replace(':','/')+"/"+(!CommunityStewardship.Enabled(w)?Period(w,at).ToString():CommunityStewardship.GrantAt(w,site,at,revision)!=null?"d"+(at/Day):"m"+Period(w,at)));
    static EntityId Effect(EntityId id)=>new("request:npc-community/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(id.Value)));
    static CommunityDecision Decide(ContractWorld w,EntityId site,long at,long revision)
    {
        var actor=CommunityStewardship.Delegate(w,site,at,revision)??Principal(w,site)??throw new InvalidDataException("No configured principal");bool adult=w.Characters.Any(c=>c.Id==actor&&c.Life==LifeState.Adult);
        bool local=adult&&CommunityEconomy.Local(w,actor,site,at,revision);bool authority=(FactionStances.ResolveAuthority(w,actor,new(w.NewGame!.Start.Castles.Single(c=>c.Id==site.Value).Faction!),at,revision)!=null||CommunityStewardship.Authority(w,actor,new(w.NewGame!.Start.Castles.Single(c=>c.Id==site.Value).Faction!),site,at,revision)!=null)&&w.LandRights.Any(r=>r.Subject.Value==w.NewGame.Start.Castles.Single(c=>c.Id==site.Value).Faction&&r.Target==site&&r.Kind=="control"&&r.From<=at&&at<r.Until);
        CommunityAccounts? account=local&&authority?CommunityEconomy.AccountAt(w,site,at,revision):null;long reserved=account==null?0:HouseSalary.Reserved(w,site,at,revision);
        string hash=ContractJson.CanonicalHash(new CommunityDecisionInputs(actor,site,at,local,authority,account,reserved));EconomyAction? action=null;long amount=0;string reason="当主が現地で確認できる状態になるまで待機";EntityId? representative=null;
        if(account!=null){long free=account.HouseRice-reserved;
            if(account.Hunger>0){amount=Math.Min(account.Hunger,free);if(amount>0){action=EconomyAction.RelieveVillage;reason="現地で確認した食糧不足を家の自由米で救済";}else reason="食糧不足を確認、処分できる米がないため待機";}
            else if(account.Assessed>account.Collected&&account.VillageRice>CommunityEconomy.Population*5){amount=Math.Min(1000,Math.Min(account.Assessed-account.Collected,account.VillageRice-CommunityEconomy.Population*5));action=EconomyAction.CollectLevy;reason="既定年貢のうち村の一月分を残せる現物だけ徴収";}
            else if(account.Water<130&&account.Reserved==0&&account.Workforce>=CommunityEconomy.Crew&&free>=CommunityEconomy.WorkCost+500){action=EconomyAction.ImproveWater;amount=CommunityEconomy.WorkCost;representative=VillageConsultation.Representative(site,at);reason="代表の食糧・空き労働条件に合意し家に500kgを残して水利工事";}
            else reason="不足なし、追加工事または徴収の条件なしのため待機";
        }
        if(action==EconomyAction.ImproveWater&&MaterialMarkets.WoodAvailable(w,site,at,revision)<MaterialMarkets.WaterWood){action=null;amount=0;representative=null;reason="家の木材が不足しているため工事を待機";}
        if(action is {} a){amount=Math.Min(amount,CommunityStewardship.Budget(w,actor,site,a,at,revision));if(amount==0||a==EconomyAction.ImproveWater&&amount<CommunityEconomy.WorkCost){action=null;amount=0;representative=null;reason="合意した今月の処分枠を使い切ったため待機";}}
        return new(Id(w,site,at,revision),actor,site,at,revision,hash,action,amount,reason,representative);
    }
    internal static bool Authorized(ContractWorld w,CommunityOrder order)
    {
        if(!Enabled(w)||order.Actor==w.PlayerControl)return false;
        return w.CommunityAdministration?.Decisions.Any(d=>Effect(d.Id)==order.Input.Id&&d.Actor==order.Actor&&d.Site==order.Input.Site&&d.At==order.At&&d.Revision==order.Revision&&d.Action==order.Input.Action&&d.Amount==order.Input.Amount)==true;
    }
    internal static bool WaterAuthorized(ContractWorld w,EntityId effect,EntityId site,long at,long revision)=>Enabled(w)&&w.CommunityAdministration?.Decisions.Any(d=>Effect(d.Id)==effect&&d.Site==site&&d.At==at&&d.Revision==revision&&d.Action==EconomyAction.ImproveWater&&d.Representative==VillageConsultation.Representative(site,at))==true;
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.CommunityAdministration=new(1,Policy,[]);}
    internal static void Apply(ContractWorld w,EntityId site)
    {
        var d=Decide(w,site,w.ElapsedSeconds,w.CommitSequence);Need(d.Actor!=w.PlayerControl,"プレイヤーの家は代行しません");
        w.CommunityAdministration=w.CommunityAdministration! with{Decisions=[..w.CommunityAdministration!.Decisions,d]};
        if(d.Action is {} action)CommunityEconomy.ApplyFor(w,new(Effect(d.Id),site,action,d.Amount),d.Actor);
    }
    internal static void TakeTurns(WorldSession session)
    {
        var w=session.DiagnosticSnapshot();if(!Enabled(w)||w.ElapsedSeconds==0||w.ElapsedSeconds%Day!=0)return;
        foreach(var site in CommunityEconomy.SitesFor(w)){w=session.DiagnosticSnapshot();if(Date(w,w.ElapsedSeconds).Day!=1&&CommunityStewardship.GrantAt(w,site,w.ElapsedSeconds,w.CommitSequence+1)==null)continue;var controller=CommunityStewardship.Delegate(w,site,w.ElapsedSeconds,w.CommitSequence+1)??Principal(w,site);if(controller==null||controller==w.PlayerControl||w.CommunityAdministration!.Decisions.Any(d=>d.Site==site&&d.Id==Id(w,site,w.ElapsedSeconds)))continue;session.RunCommunityAdministration(site,w.CommitSequence);}
    }
    internal static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;var d=Date(w,w.ElapsedSeconds);var next=new DateOnly(d.Year,d.Month,1).AddMonths(1);long at=(next.DayNumber-DateOnly.Parse(w.EpochDate).DayNumber)*Day;if(CommunityStewardship.Enabled(w)&&w.Stewardship!.Grants.Any(g=>CommunityStewardship.Active(w,g,w.ElapsedSeconds,w.CommitSequence+1)))at=Math.Min(at,(w.ElapsedSeconds/Day+1)*Day);return at<=target?at:null;
    }
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.CommunityAdministration==null,"旧世界へNPC地域運営を追加できません");return;}
        Need(w.CommunityAdministration is {Schema:1,Policy:Policy,Decisions:not null},"地域運営の原本がありません");var ids=new HashSet<EntityId>();var periods=new HashSet<EntityId>();long prior=0,priorAt=0;
        foreach(var d in w.CommunityAdministration.Decisions){Need(d.At>0&&d.At%Day==0&&d.At<=w.ElapsedSeconds&&(Date(w,d.At).Day==1||CommunityStewardship.GrantAt(w,d.Site,d.At,d.Revision)!=null)&&d.Revision>prior&&d.Revision<=w.CommitSequence&&d.At>=priorAt&&CommunityEconomy.SitesFor(w).Contains(d.Site)&&d.Actor!=w.PlayerControl&&ids.Add(d.Id)&&periods.Add(Id(w,d.Site,d.At,d.Revision)),"月次判断が重複または不正です");prior=d.Revision;priorAt=d.At;Need(d==Decide(w,d.Site,d.At,d.Revision),"NPC判断が本人の現地入力と一致しません");Need(!FactionStances.OtherIdUsed(w,d.Id,includeAdministration:false)&&!FactionStances.OwnIdUsed(w,d.Id),"NPC判断IDが別操作と重複しています");
            var order=w.Communities!.Orders.SingleOrDefault(o=>o.Input.Id==Effect(d.Id));Need(d.Action==null?order==null:order!=null&&Authorized(w,order),"判断と現物台帳が一致しません");}
        Need(w.Communities!.Orders.Where(o=>o.Actor!=w.PlayerControl).All(o=>Authorized(w,o)),"NPCの現物操作に判断原本がありません");
    }
    internal static string LocalOutcome(ContractWorld w,EntityId site)
    {
        if(!Enabled(w))return "";var d=w.CommunityAdministration!.Decisions.LastOrDefault(d=>d.Site==site&&d.Action!=null);
        return d==null?"":Date(w,d.At).ToString("yyyy-MM-dd")+"の現地履行："+(d.Action==EconomyAction.RelieveVillage?"救済米の受渡し":d.Action==EconomyAction.CollectLevy?"既定年貢の徴収":"村と合意した水利工事に着手");
    }
}
public sealed partial class WorldSession
{
    internal void RunCommunityAdministration(EntityId site,long revision){lock(gate){if(revision!=state.CommitSequence)throw new InvalidOperationException("地域運営の状態が変わりました");Change(w=>NpcCommunityAdministration.Apply(w,site));}}
}
