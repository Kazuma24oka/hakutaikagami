namespace HakutaiKagami.Core.Contracts;

// Explicit synthetic integration fixture, not adopted historical geography or game balance.
public static class ContractFixture
{
    public static readonly EntityId Lord = new("person:lord"), Officer = new("person:officer"), Clerk = new("person:clerk");
    public static readonly EntityId Castle = new("site:castle"), Village = new("site:village");
    public static readonly EntityId Source = new("lot:village-rice"), Treasury = new("lot:treasury-rice");
    public static ContractWorld Create()
    {
        var house = new EntityId("org:house"); var village = new EntityId("org:village");
        EntityId Basis(string name) => new("basis:" + name);
        var people = new[] { Lord, Officer, Clerk };
        var w = new ContractWorld {
            Schema = 1, WorldId = new("world:common-fixture"), TimeDomain = "GameSecondsV1", EpochDate = "1560-01-01",
            ElapsedSeconds = 0, CommitSequence = 0, Seed = 20261020, Paused = false,
            Definitions = new(new("scenario:common-fixture"), "1", "common-core-fixture-v1",
                [new(Lord,"試作城主"),new(Officer,"試作奉行"),new(Clerk,"試作書役")],
                [new(house,"試作主家"),new(village,"試作村落")],
                [new(Castle,"検証城（架空）",0,0,0),new(Village,"検証村（架空）",0,1,0)],3,
                new[]{"membership","service","office","council","explicit-fixture-levy-right"}.Select(n=>new DefinitionEntry(Basis(n),n)).ToArray(),[]),
            Characters = people.Select(p => new CharacterState(p,LifeState.Adult,new("appearance:fixture"),0)).ToArray(),
            Memberships = people.Select((p,i) => new TimedRelation(new("membership:"+i),p,house,"membership",Basis("membership"),0,long.MaxValue,0)).ToArray(),
            ServiceBonds = [new(new("service:officer"),Officer,Lord,"service",Basis("service"),0,long.MaxValue,0)],
            Offices = [new(new("office:lord"),Lord,Castle,"castellan",Basis("office"),0,long.MaxValue,0)],
            LandRights = [],
            Grants = people.SelectMany((p,i) => new[] {
                new Grant(new("grant:ordinary/"+i),house,p,Castle,[ActionKind.Inspect,ActionKind.Move,ActionKind.Cancel],Basis("council"),0,long.MaxValue,long.MaxValue,[],false,0),
                new Grant(new("grant:tax/"+i),village,p,Castle,[ActionKind.Transfer],Basis("explicit-fixture-levy-right"),0,long.MaxValue,100,[Source],false,0)
            }).ToArray(),
            Locations = people.Select(p => new LocationState(p,LocationKind.AtSite,Castle,null,0,0,null,null,null,null,0)).ToArray(),
            Units = [], Tasks = [], Actions = [], ResourceLots = [
                new(Source,"rice",village,house,Castle,"fixture-rice-unit",100,100,0),
                new(Treasury,"rice",house,house,Castle,"fixture-rice-unit",0,0,0)],
            Reservations = [],Transfers = [],Debts = [],Messaging = new(),SealedMessages = [],Evidence = [],Knowledge = [],
            PendingEvents = [],EffectLedger = [],Outbox = [],Compensations = [],RandomStreams = [new("decisions",ContractRandom.InitialState(20261020,"decisions"),0)],
            RandomDraws = [],IdCounters = new(StringComparer.Ordinal),ConflictDecisions = [],PlayerControl = Officer,Chronicle = [],Requests = []
        };
        ContractValidation.Validate(w); return w;
    }
    public static Intent Order(string id, ActionKind action = ActionKind.Inspect, long amount = 0, bool important = false,
        EntityId? executor = null, long deadline = 60, TaskKind kind = TaskKind.BindingOrder) =>
        new(new(id),Lord,executor??Officer,Castle,action,action==ActionKind.Transfer?Source:null,action==ActionKind.Transfer?Treasury:null,
            amount,action==ActionKind.Move?Village:null,new(DeadlineCriterion.Completion,deadline,false,null,deadline),0,2,4,2,important,kind);
}
