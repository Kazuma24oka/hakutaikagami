namespace HakutaiKagami.Core.Contracts;

public enum EconomyAction { CollectLevy, RelieveVillage, ImproveWater }
public sealed record EconomyInput(EntityId Id,EconomyAction Action,long Amount);
public sealed record EconomyOrder(EconomyInput Input,EntityId Actor,long At,long Revision,FactionStanceAuthority Authority);
public sealed record EconomyEvent(string Kind,long At,long Amount,int Water);
public sealed record EconomyAccounts(long VillageRice,long HouseRice,long Assessed,long Collected,long Relief,long WorksSpent,long Harvested,int Water,long? WorkDue);
public sealed record RegionalEconomyState(int Schema,string Contract,EconomyOrder[] Orders,EconomyEvent[] Events,EconomyAccounts Accounts);
public sealed record RegionalEconomyView(EntityId Site,EntityId Owner,long ObservedAt,EconomyAccounts Accounts,long NextHarvest,bool CanManage,string Explanation);

// A versioned, explicit pilot village covenant, not tax power inferred from an office.
// All quantities are kg of pilot rice. Private mon and preallocated military provisions are separate stores.
public static class RegionalEconomy
{
    public const string Policy="kiyosu-village-covenant-seasonal-rice-v1";
    public static readonly EntityId Site=new("site:castle");
    public const long InitialVillage=800,InitialHouse=300,ProtectedReserve=500,WorksCost=200,WorksSeconds=7*86400;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string text){if(!ok)throw new InvalidDataException(text);}
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.EconomyPolicy==Policy;
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Economy?.Orders.Any(q=>q.Input.Id==id)==true;
    public static EntityId? Owner(ContractWorld w)=>w.NewGame?.Start.Castles.SingleOrDefault(c=>c.Id==Site.Value)?.Faction is {} f?new(f):null;
    public static long HarvestAt(ContractWorld w,int year)=>checked((DateOnly.ParseExact(year+"-10-01","yyyy-MM-dd").DayNumber-DateOnly.ParseExact(w.EpochDate,"yyyy-MM-dd").DayNumber)*86400L);
    public static long NextHarvest(ContractWorld w,long at){int y=DateOnly.ParseExact(w.EpochDate,"yyyy-MM-dd").AddDays(checked((int)(at/86400))).Year;long n=HarvestAt(w,y);return n>at?n:HarvestAt(w,y+1);}
    static bool LocalAt(ContractWorld w,EntityId actor,long at,long revision)
    {
        var node=BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==Site);
        if(RegionalCampaign.RoadActorPointAt(w,actor,at)!=node.Position)return false;
        if(w.Branches!.Journeys.Any(j=>j.Actor==actor&&w.Tasks.Any(t=>t.Id==j.Task&&w.Requests.Any(r=>r.Id==t.RequestId&&r.CommitSequence<revision))&&j.Legs.Any(l=>l.StartAt<=at&&at<l.EndAt)))return false;
        var last=NewGameMilitary.LedgerStates(w).SelectMany(s=>s.Requests).Where(q=>q.Revision<revision&&(q.At<at||q.At==at)).OrderBy(q=>q.Revision).LastOrDefault(q=>q.Input.Action is NewMilitaryAction.Deploy or NewMilitaryAction.Enter);
        return actor!=w.PlayerControl||last?.Input.Action!=NewMilitaryAction.Deploy;
    }
    static FactionStanceAuthority Authority(ContractWorld w,EntityId actor,long at,long revision)
    {
        Need(Owner(w) is {} owner&&FactionStances.ResolveAuthority(w,actor,owner,at,revision) is {} auth&&auth.Home==Site,"清洲の試作年貢契約と現地支配を持つ当主だけが扱えます");
        return FactionStances.ResolveAuthority(w,actor,Owner(w)!.Value,at,revision)!;
    }
    public static RegionalEconomyView? View(ContractWorld w,EntityId observer)
    {
        if(!Enabled(w)||observer!=w.PlayerControl||!w.Locations.Any(l=>l.Actor==observer&&l.Kind==LocationKind.AtSite&&l.Site==Site)||!LocalAt(w,observer,w.ElapsedSeconds,w.CommitSequence+1))return null;
        bool manage=false;try{Authority(w,observer,w.ElapsedSeconds,w.CommitSequence);manage=true;}catch(InvalidDataException){}
        // Local accounts are shown only to the controlling covenant head; other visitors have no automatic access.
        if(!manage)return null;
        return new(Site,Owner(w)!.Value,w.ElapsedSeconds,w.Economy!.Accounts,NextHarvest(w,w.ElapsedSeconds),true,"清洲一地域の試作契約。家の蔵と村の米を分離。私財・軍用の既存備蓄とは別。数量は調整用kg。");
    }
    static RegionalEconomyState Replay(ContractWorld w,EconomyOrder[] orders)
    {
        long village=InitialVillage,house=InitialHouse,assessed=0,collected=0,relief=0,works=0,harvested=0;int water=100;long? due=null;
        var events=new List<EconomyEvent>();long next=HarvestAt(w,DateOnly.ParseExact(w.EpochDate,"yyyy-MM-dd").Year);long cursor=0,priorRev=0;var ids=new HashSet<EntityId>();
        void Advance(long until){while(Math.Min(next,due??long.MaxValue)<=until){long at=Math.Min(next,due??long.MaxValue);if(due==at){water+=10;events.Add(new("water-complete",at,0,water));due=null;}
            if(next==at){long amount=1000L*water/100;if(due!=null)amount=amount*9/10;village=checked(village+amount);harvested=checked(harvested+amount);assessed=checked(assessed+amount*3/10);events.Add(new("harvest",at,amount,water));next=NextHarvest(w,at);}}}
        foreach(var q in orders){Need(q!=null&&q.Input!=null&&EntityId.IsValid(q.Input.Id.Value)&&ids.Add(q.Input.Id)&&q.Actor==w.PlayerControl&&q.At>=cursor&&q.At<=w.ElapsedSeconds&&q.Revision>priorRev&&q.Revision<=w.CommitSequence,"地域経済の操作原本が不正です");Need(!FactionStances.OtherIdUsed(w,q.Input.Id,includeEconomy:false)&&!FactionStances.OwnIdUsed(w,q.Input.Id)&&w.Military?.Requests.Any(r=>r.Input.Id==q.Input.Id)!=true&&w.Career?.Requests.Any(r=>r.Input.Id==q.Input.Id)!=true,"別の操作で使われた経済原本IDです");cursor=q.At;priorRev=q.Revision;Advance(q.At);
            Need(q.Authority==Authority(w,q.Actor,q.At,q.Revision)&&LocalAt(w,q.Actor,q.At,q.Revision),"地域経済の権限・現地受渡しが一致しません");var i=q.Input;Need(Enum.IsDefined(i.Action)&&i.Amount>0,"操作と数量が不正です");
            switch(i.Action){
                case EconomyAction.CollectLevy:Need(i.Amount<=assessed-collected&&i.Amount<=village-ProtectedReserve,"未徴収年貢と村の保護備蓄を超えて徴収できません");village-=i.Amount;house=checked(house+i.Amount);collected=checked(collected+i.Amount);break;
                case EconomyAction.RelieveVillage:Need(i.Amount<=house,"家の蔵の米が不足しています");house-=i.Amount;village=checked(village+i.Amount);relief=checked(relief+i.Amount);break;
                case EconomyAction.ImproveWater:Need(i.Amount==WorksCost&&house>=WorksCost&&due==null&&water<130,"水利工事は米200kg・7日、同時に一件、水利130までです");house-=WorksCost;works=checked(works+WorksCost);due=checked(q.At+WorksSeconds);break;
            }
        }
        Advance(w.ElapsedSeconds);return new(1,Policy,orders,events.ToArray(),new(village,house,assessed,collected,relief,works,harvested,water,due));
    }
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Economy=Replay(w,[]);}
    internal static void Tick(ContractWorld w){if(Enabled(w))w.Economy=Replay(w,w.Economy!.Orders);}
    internal static long? NextBoundary(ContractWorld w,long target){if(!Enabled(w))return null;long at=Math.Min(NextHarvest(w,w.ElapsedSeconds),w.Economy!.Accounts.WorkDue??long.MaxValue);return at<=target?at:null;}
    internal static void Apply(ContractWorld w,EconomyInput input,EntityId actor){Need(Enabled(w),"この保存では地域経済が有効ではありません");var q=new EconomyOrder(input,actor,w.ElapsedSeconds,w.CommitSequence,Authority(w,actor,w.ElapsedSeconds,w.CommitSequence));w.Economy=Replay(w,[..w.Economy!.Orders,q]);}
    internal static void Validate(ContractWorld w){if(!Enabled(w)){Need(w.Economy==null,"旧世界に地域経済を混在できません");return;}Need(w.Economy is {Schema:1,Contract:Policy} s&&s.Orders!=null&&s.Events!=null&&s.Accounts!=null,"地域経済の原本がありません");Need(ContractJson.CanonicalHash(w.Economy)==ContractJson.CanonicalHash(Replay(w,w.Economy!.Orders)),"収穫・年貢・支出の帳尻が原本と一致しません");}
}
