using HakutaiKagami.Core.Messaging;
using System.Globalization;
namespace HakutaiKagami.Core.Contracts;
public sealed record CommunityInput(EntityId Id,EntityId Site,EconomyAction Action,long Amount);
public sealed record CommunityOrder(CommunityInput Input,EntityId Actor,long At,long Revision,FactionStanceAuthority Authority);
public sealed record CommunityEvent(EntityId Site,string Kind,long At,long Amount,string Detail);
public sealed record CommunityAccounts(EntityId Site,int Population,int Workforce,int Reserved,long VillageRice,long HouseRice,long Assessed,long Collected,long Relief,long WorksSpent,long Harvested,long Consumed,long Hunger,int Water,long? WorkDue,long WorkRemaining,string Climate,long? ClimateAt);
public sealed record CommunityNotice(EntityId Site,EntityId Sender,EntityId Recipient,EntityId Address,long At,long Revision,long Need,int Period,string Message);
public sealed record CommunityState(int Schema,string Contract,CommunityOrder[] Orders,CommunityEvent[] Events,CommunityAccounts[] Accounts,CommunityNotice[] Notices);
public sealed record CommunityView(EntityId Site,EntityId Owner,long ObservedAt,CommunityAccounts Accounts,long NextHarvest,string Explanation);
public sealed record CommunityReport(EntityId Site,long Need,long ObservedAt);
// Authored trial quantities, not historical demographic or climate claims. Rice kg is separate from military person-seconds/private mon.
public static class CommunityEconomy
{
    public const string Policy="three-community-population-labor-calendar-harvest-shortage-relief-v2",Kind="community-food-report";
    public const int Population=200,Workers=100,Crew=20;public const long InitialVillage=10000,InitialHouse=3000,BaseHarvest=20000,WorkCost=200,WorkSeconds=7*86400;
    public static readonly EntityId[] Sites=[new("site:castle"),new("site:kofu"),new("site:odawara")];
    public static EntityId[] SitesFor(ContractWorld w)=>RegionalDomains.Enabled(w)?MaterialMarkets.Sites(w):Sites;
    static long InitialHouseFor(ContractWorld w,EntityId site)=>RegionalDomains.HasHouse(w,site)?InitialHouse:0;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string text){if(!ok)throw new InvalidDataException(text);}
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.EconomyPolicy is Policy or RegionalTrade.Policy or HouseSalary.Policy or VillageConsultation.Policy or NpcCommunityAdministration.Policy or CommunityStewardship.Policy or MaterialMarkets.Policy or RegionalDomains.Policy or HouseLogistics.Policy or HouseFreightReceiving.Policy;
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Communities?.Orders.Any(q=>q.Input.Id==id)==true;
    static DateOnly Epoch(ContractWorld w)=>DateOnly.ParseExact(w.EpochDate,"yyyy-MM-dd",CultureInfo.InvariantCulture);
    static long Time(ContractWorld w,DateOnly date)=>checked((date.DayNumber-Epoch(w).DayNumber)*86400L);
    static DateOnly Date(ContractWorld w,long at)=>Epoch(w).AddDays(checked((int)(at/86400)));
    static int Period(ContractWorld w,long at){var d=Date(w,at);return d.Year*12+d.Month;}
    static (string Name,int Factor) Season(ContractWorld w,EntityId site,int year){var h=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(w.Seed+"/community-weather/"+site.Value+"/"+year));int n=h[0]%100;return n<20?("干ばつ",50):n>=90?("洪水",70):("平年",100);}
    static EntityId? Owner(ContractWorld w,EntityId site)=>w.NewGame!.Start.Castles.SingleOrDefault(c=>c.Id==site.Value)?.Faction is {} f?new(f):null;
    internal static bool Local(ContractWorld w,EntityId actor,EntityId site,long at,long rev)
    {
        var point=BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==site).Position;if(RegionalCampaign.RoadActorPointAt(w,actor,at)!=point)return false;
        if(w.Branches!.Journeys.Any(j=>j.Actor==actor&&w.Tasks.Any(t=>t.Id==j.Task&&w.Requests.Any(r=>r.Id==t.RequestId&&r.CommitSequence<rev))&&j.Legs.Any(l=>l.StartAt<=at&&at<l.EndAt)))return false;
        var last=NewGameMilitary.LedgerStates(w).SelectMany(s=>s.Requests).Where(q=>q.Revision<rev&&q.At<=at).OrderBy(q=>q.Revision).LastOrDefault(q=>q.Input.Action is NewMilitaryAction.Deploy or NewMilitaryAction.Enter);return actor!=w.PlayerControl||last?.Input.Action!=NewMilitaryAction.Deploy;
    }
    static FactionStanceAuthority Authority(ContractWorld w,EntityId actor,EntityId site,long at,long rev)
    {
        var owner=Owner(w,site);Need(owner!=null&&(FactionStances.ResolveAuthority(w,actor,owner.Value,at,Math.Min(rev,w.CommitSequence))??CommunityStewardship.Authority(w,actor,owner.Value,site,at,rev)) is {} a&&w.LandRights.Any(r=>r.Subject==owner&&r.Target==site&&r.Kind=="control"&&r.From<=at&&at<r.Until),"その地域の試作年貢契約と現地支配を持つ当主ではありません");return (FactionStances.ResolveAuthority(w,actor,owner.Value,at,Math.Min(rev,w.CommitSequence))??CommunityStewardship.Authority(w,actor,owner.Value,site,at,rev))!;
    }
    static CommunityState Replay(ContractWorld w,CommunityOrder[] orders,CommunityNotice[] notices,long until,long revision,EntityId? onlySite=null)
    {
        var ids=new HashSet<EntityId>();long prior=0,priorAt=0;foreach(var q in orders){Need((q.Actor==w.PlayerControl||NpcCommunityAdministration.Authorized(w,q))&&q.At>=priorAt&&q.At<=until&&q.Revision>prior&&q.Revision<=revision&&EntityId.IsValid(q.Input.Id.Value)&&ids.Add(q.Input.Id)&&SitesFor(w).Contains(q.Input.Site)&&Enum.IsDefined(q.Input.Action)&&q.Input.Amount>0,"地域帳簿の入力・時点・順序が不正です");Need(!FactionStances.OtherIdUsed(w,q.Input.Id,includeCommunities:false)&&!FactionStances.OwnIdUsed(w,q.Input.Id),"その原本IDは別の操作で使用済みです");Need(q.Authority==Authority(w,q.Actor,q.Input.Site,q.At,q.Revision)&&Local(w,q.Actor,q.Input.Site,q.At,q.Revision),"現地権限・引受原本が一致しません");prior=q.Revision;priorAt=q.At;}
        var events=new List<CommunityEvent>();var accounts=new List<CommunityAccounts>();
        foreach(var site in SitesFor(w).Where(site=>onlySite==null||site==onlySite)){long imports=0,payout=0;long village=InitialVillage,house=InitialHouseFor(w,site),assessed=0,collected=0,relief=0,spent=0,harvested=0,consumed=0,hunger=0,remaining=0;int water=100,reserved=0,factor=100;long? due=null,climateAt=null;string climate="未観測";var epoch=Epoch(w);long month=Time(w,new DateOnly(epoch.Year,epoch.Month,1).AddMonths(1)),weather=Time(w,new(epoch.Year,9,1)),harvest=Time(w,new(epoch.Year,10,1));
            void Event(string kind,long at,long n,string detail="")=>events.Add(new(site,kind,at,n,detail));
            void Adjust(long at){if(reserved>0&&hunger>0&&due is {} end){remaining=end-at;due=null;Event("work-paused",at,remaining);}if(reserved>0&&hunger==0&&due==null&&remaining>0){due=checked(at+remaining);remaining=0;Event("work-resumed",at,due.Value);}}
            void Feed(long at){long n=Math.Min(village,hunger);if(n>0){village-=n;hunger-=n;consumed=checked(consumed+n);Event("food",at,n);}Adjust(at);}
            void Advance(long target){while(true){long at=Math.Min(Math.Min(month,weather),Math.Min(harvest,due??long.MaxValue));if(at>target)break;
                if(due==at){water+=10;reserved=0;due=null;remaining=0;Event("water-complete",at,water);}
                if(weather==at){var s=Season(w,site,Date(w,at).Year);climate=s.Name;factor=s.Factor;climateAt=at;Event("weather",at,factor,climate);weather=Time(w,new(Date(w,at).Year+1,9,1));}
                if(harvest==at){long n=BaseHarvest*water/100*(Workers-reserved)/Workers*factor/100;if(hunger>0)n/=2;village=checked(village+n);harvested=checked(harvested+n);assessed=checked(assessed+n*3/10);Event("harvest",at,n,climate);Feed(at);harvest=Time(w,new(Date(w,at).Year+1,10,1));}
                if(month==at){hunger=checked(hunger+Population*5);Feed(at);Event(hunger>0?"food-shortage":"monthly-food",at,hunger);month=Time(w,new DateOnly(Date(w,at).Year,Date(w,at).Month,1).AddMonths(1));}
            }}
            var operations=orders.Where(q=>q.Input.Site==site).Select(q=>(q.At,q.Revision,Order:(CommunityOrder?)q,Amount:0L)).Concat(RegionalTrade.Supplies(w,site,until,revision).Select(s=>(s.At,s.Revision,Order:(CommunityOrder?)null,s.Amount))).Concat(HouseSalary.Payouts(w,site,until,revision).Select(s=>(s.At,s.Revision,Order:(CommunityOrder?)null,Amount:-s.Amount))).Concat(RegionalDomains.Purchases(w,until,revision+1).Where(e=>e.Input.Site==site&&e.Input.Commodity==null).Select(e=>(e.At,e.Revision,Order:(CommunityOrder?)null,Amount:e.Input.Quantity))).Concat(HouseLogistics.Deliveries(w,site,until,revision+1).Select(e=>(e.At,e.Revision,Order:(CommunityOrder?)null,Amount:e.Amount))).OrderBy(x=>x.Revision);
            foreach(var op in operations){Advance(op.At);if(op.Order==null){house=checked(house+op.Amount);if(op.Amount>=0){imports=checked(imports+op.Amount);Event("private-rice-supplied",op.At,op.Amount);}else{payout=checked(payout-op.Amount);Event("salary-paid",op.At,-op.Amount);}continue;}var q=op.Order;var i=q.Input;Need(i.Amount<=CommunityStewardship.Budget(w,q.Actor,i.Site,i.Action,q.At,q.Revision),"今月の委任処分枠を超えています");long free=house-HouseSalary.Reserved(w,site,q.At,q.Revision);switch(i.Action){
                case EconomyAction.CollectLevy:Need(hunger==0&&i.Amount<=assessed-collected&&i.Amount<=village-Population*5,"未徴収額と村の一月分の保護備蓄を超えた徴収はできません");village-=i.Amount;house=checked(house+i.Amount);collected=checked(collected+i.Amount);Event("levy-collected",q.At,i.Amount);break;
                case EconomyAction.RelieveVillage:Need(i.Amount<=free,"家の蔵の米が不足しています");house-=i.Amount;village=checked(village+i.Amount);relief=checked(relief+i.Amount);Event("relief",q.At,i.Amount);Feed(q.At);break;
                case EconomyAction.ImproveWater:Need(MaterialMarkets.WoodAvailable(w,i.Site,q.At,q.Revision)>=MaterialMarkets.WaterWood,"水利工事には家の木材10kgが必要です");Need(!VillageConsultation.Enabled(w)||VillageConsultation.WorkAuthorized(w,i.Id,i.Site,q.At,q.Revision),"先に村との水利工事の合意を確認してください");Need(i.Amount==WorkCost&&free>=WorkCost&&reserved==0&&water<130&&hunger==0,"水利工事は米200kg/20人/7日、同時一件で、先に食糧不足を解消します");house-=WorkCost;spent=checked(spent+WorkCost);reserved=Crew;due=checked(q.At+WorkSeconds);Event("water-start",q.At,WorkCost);break;
            }}Advance(until);Need(village>=0&&house>=0&&village+house+consumed+spent+payout==InitialVillage+InitialHouseFor(w,site)+harvested+imports,"地域の米収支が保存則に一致しません");accounts.Add(new(site,Population,Workers,reserved,village,house,assessed,collected,relief,spent,harvested,consumed,hunger,water,due,remaining,climate,climateAt));
        }return new(1,Policy,orders,events.OrderBy(e=>e.At).ThenBy(e=>e.Site.Value,StringComparer.Ordinal).ToArray(),accounts.ToArray(),notices);
    }
    internal static CommunityAccounts AccountAt(ContractWorld w,EntityId site,long at,long revision)=>Replay(w,w.Communities!.Orders.Where(q=>q.Input.Site==site&&(q.At<at||q.At==at&&q.Revision<revision)).ToArray(),[],at,revision-1,site).Accounts.Single(a=>a.Site==site);
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Communities=Replay(w,[],[],w.ElapsedSeconds,w.CommitSequence);}
    internal static void Apply(ContractWorld w,CommunityInput input)=>ApplyFor(w,input,w.PlayerControl);
    internal static void ApplyFor(ContractWorld w,CommunityInput input,EntityId actor){Need(Enabled(w),"この保存では複数地域の帳簿は使用できません");var q=new CommunityOrder(input,actor,w.ElapsedSeconds,w.CommitSequence,Authority(w,actor,input.Site,w.ElapsedSeconds,w.CommitSequence));w.Communities=Replay(w,[..w.Communities!.Orders,q],w.Communities.Notices,w.ElapsedSeconds,w.CommitSequence);}
    internal static void Tick(ContractWorld w)
    {
        if(!Enabled(w))return;w.Communities=Replay(w,w.Communities!.Orders,w.Communities.Notices,w.ElapsedSeconds,w.CommitSequence);int period=Period(w,w.ElapsedSeconds);
        foreach(var a in w.Communities.Accounts.Where(a=>a.Hunger>0)){
            if(w.Communities.Notices.Any(n=>n.Site==a.Site&&n.Period==period)||Owner(w,a.Site) is not {} owner)continue;var faction=w.NewGame!.Start.Factions.SingleOrDefault(f=>f.Id==owner.Value);if(faction==null)continue;var recipient=new EntityId(faction.Head);
            var witness=w.Characters.Where(c=>c.Life==LifeState.Adult&&(c.Id!=w.PlayerControl||recipient==w.PlayerControl)&&w.Memberships.Any(r=>r.Subject==c.Id&&r.Target==owner&&r.From<=w.ElapsedSeconds&&w.ElapsedSeconds<r.Until)&&Local(w,c.Id,a.Site,w.ElapsedSeconds,w.CommitSequence+1)).OrderBy(c=>c.Id==recipient?1:0).ThenBy(c=>c.Id.Value,StringComparer.Ordinal).FirstOrDefault();if(witness==null||witness.Id==recipient&&recipient!=w.PlayerControl)continue;
            var id="message:community/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(a.Site.Value+"/"+period));var address=new EntityId(faction.Home);var body=System.Text.Encoding.UTF8.GetString(ContractJson.Write(new CommunityReport(a.Site,a.Hunger,w.ElapsedSeconds)));MessageFlow.Send(w.Messaging,new(id,a.Site.Value,Kind,witness.Id.Value,recipient.Value,body,address.Value,w.ElapsedSeconds),w.ElapsedSeconds,1);w.SealedMessages=[..w.SealedMessages,new(id)];w.Communities=w.Communities with{Notices=[..w.Communities.Notices,new(a.Site,witness.Id,recipient,address,w.ElapsedSeconds,w.CommitSequence,a.Hunger,period,id)]};
        }
    }
    public static CommunityView? View(ContractWorld w,EntityId actor)
    {
        if(!Enabled(w)||actor!=w.PlayerControl)return null;var l=w.Locations.Single(l=>l.Actor==actor);if(l.Kind!=LocationKind.AtSite||l.Site==null||!SitesFor(w).Contains(l.Site.Value)||!Local(w,actor,l.Site.Value,w.ElapsedSeconds,w.CommitSequence+1))return null;try{var a=Authority(w,actor,l.Site.Value,w.ElapsedSeconds,w.CommitSequence+1);return new(l.Site.Value,a.Owner,w.ElapsedSeconds,w.Communities!.Accounts.Single(a=>a.Site==l.Site),RegionalEconomy.NextHarvest(w,w.ElapsedSeconds),(RegionalDomains.Enabled(w)?"全国30地域":"3地域")+"の試作契約。人口/米量/作柄は調整用データで史実値ではありません。村/家/私財/既存軍用備蓄は別。工事20人/200kg/7日。");}catch(InvalidDataException){return null;}
    }
    public static string? Address(ContractWorld w,MessageEnvelope m)=>w.Communities?.Notices.SingleOrDefault(n=>n.Message==m.Id) is {} n?NewGameWorld.Node(n.Address.Value):null;
    internal static long? NextBoundary(ContractWorld w,long target)
    {if(!Enabled(w))return null;var d=Date(w,w.ElapsedSeconds);var times=new List<long>{Time(w,new DateOnly(d.Year,d.Month,1).AddMonths(1)),Time(w,new(d.Year,9,1)),Time(w,new(d.Year+1,9,1)),Time(w,new(d.Year,10,1)),Time(w,new(d.Year+1,10,1))};times.AddRange(w.Communities!.Accounts.Where(a=>a.WorkDue!=null).Select(a=>a.WorkDue!.Value));return times.Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();}
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.Communities==null,"旧世界へ複数地域経済を混在できません");return;}var s=w.Communities;Need(s is {Schema:1,Contract:Policy}&&s.Orders!=null&&s.Events!=null&&s.Accounts!=null&&s.Notices!=null,"地域経済の原本がありません");Need(ContractJson.CanonicalHash(s)==ContractJson.CanonicalHash(Replay(w,s.Orders,s.Notices,w.ElapsedSeconds,w.CommitSequence)),"地域の労働/食糧/収支が原本と一致しません");
        Need(s.Notices.Select(n=>(n.Site,n.Period)).Distinct().Count()==s.Notices.Length,"同じ月の不足報告が重複しています");foreach(var n in s.Notices){Need(n.At>=0&&n.At<=w.ElapsedSeconds&&n.Revision>0&&n.Revision<=w.CommitSequence&&SitesFor(w).Contains(n.Site)&&n.Period==Period(w,n.At)&&Local(w,n.Sender,n.Site,n.At,n.Revision+1),"不足報告の現地観測根拠がありません");var a=Replay(w,s.Orders.Where(q=>q.At<n.At||q.At==n.At&&q.Revision<=n.Revision).ToArray(),[],n.At,n.Revision).Accounts.Single(a=>a.Site==n.Site);var owner=Owner(w,n.Site);Need(a.Hunger==n.Need&&n.Need>0&&owner!=null&&w.Memberships.Any(r=>r.Subject==n.Sender&&r.Target==owner&&r.From<=n.At&&(n.At<r.Until||n.At==r.Until&&n.Revision<r.Revision)),"不足量/観測者の所属が一致しません");var f=w.NewGame!.Start.Factions.Single(f=>f.Id==owner.Value.Value);Need(n.Recipient.Value==f.Head&&n.Address.Value==f.Home,"報告先の当主/固定宛先が違います");var id="message:community/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(n.Site.Value+"/"+n.Period));var body=System.Text.Encoding.UTF8.GetString(ContractJson.Write(new CommunityReport(n.Site,n.Need,n.At)));Need(n.Message==id&&w.Messaging.Messages.Contains(new(id,n.Site.Value,Kind,n.Sender.Value,n.Recipient.Value,body,n.Address.Value,n.At))&&w.SealedMessages.Contains(new(id)),"不足報告の原本が一致しません");}
        Need(w.Messaging.Messages.Count(m=>m.Kind==Kind)==s.Notices.Length,"不足報告に観測原本がありません");
    }
}
public sealed partial class WorldSession
{
    public CommunityView? CommunityEconomyView(){lock(gate)return CommunityEconomy.View(state,state.PlayerControl);}
    public CommandResult ManageCommunity(CommunityInput input,EntityId actor,long revision){lock(gate){if(actor!=state.PlayerControl||!CommunityEconomy.Enabled(state)||!CommunityEconomy.SitesFor(state).Contains(input.Site))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"この地域の操作ではありません");var old=state.Communities!.Orders.SingleOrDefault(q=>q.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"操作済みです");if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の操作で使用済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"帳簿を確認し直してください");try{Change(w=>CommunityEconomy.Apply(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"村と家の現物帳簿へ記録しました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}}
}
