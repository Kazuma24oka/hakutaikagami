namespace HakutaiKagami.Core.Contracts;

// A bounded personal peace profile. Old fixtures and saved characters retain their identities.
public static class PersonalStart
{
    public const string Ruleset="created-person-peace-v1", Contract="independent-created-ronin-self-travel-local-voluntary-work-v1";
    public static readonly EntityId Person=new("person:created/player");
    public static readonly StartBudgetPolicy Policy=new(0,1,1,1,[]);
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    public static string Fingerprint(ContractWorld w)=>ContractJson.CanonicalHash(new[]{Contract,w.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),ContractJson.CanonicalHash(w.Definitions.Characters.Single(c=>c.Id==Person))});
    public static ContractWorld Create(string name="作成武将",ulong seed=20261020)
    {
        if(string.IsNullOrWhiteSpace(name)||name.Length>24||name.Any(char.IsControl)||name!=name.Trim())throw new InvalidDataException("名前は前後空白のない1～24文字で指定してください");
        var w=NpcWork.Create();w.PlayerControl=Person;w.WorldId=new("world:"+Ruleset);w.Seed=seed;
        w.Definitions=w.Definitions with{Id=new("scenario:"+Ruleset),Ruleset=Ruleset,Characters=[..w.Definitions.Characters,new(Person,name)]};
        w.Characters=[..w.Characters,new(Person,LifeState.Adult,new("appearance:fixture"),0)];
        w.Locations=[..w.Locations,new(Person,LocationKind.AtSite,ContractFixture.Castle,null,0,0,null,null,null,null,0)];
        var basis=new EntityId("basis:personal-start");
        w.Definitions=w.Definitions with{AuthorityBases=[..w.Definitions.AuthorityBases,new(basis,"試作用の本人移動と任意点検。所属/主従/徴税/指揮権を与えない")]};
        w.Grants=[..w.Grants,new(new("grant:personal-start-move"),Person,Person,ContractFixture.Castle,[ActionKind.Move],basis,0,long.MaxValue,0,[],false,0)];
        foreach(var role in RegionalWork.Roles)w.Grants=[..w.Grants,new(new("grant:personal-inspect/"+role.Node),role.Person,Person,RegionalCampaign.Site(role.Node),[ActionKind.Inspect],basis,0,long.MaxValue,0,[],false,0)];
        // Independent streams derive from the chosen seed, never from the old character's memory.
        w.RandomStreams=w.RandomStreams.Select(s=>new RandomStream(s.Purpose,ContractRandom.InitialState(seed,s.Purpose),0)).ToArray();
        w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};
        ContractValidation.Validate(w);
        var selection=StartConfiguration.Describe(w,Person,StartTemplate.Ronin,PersonOrigin.Created,Policy);
        if(!StartConfiguration.Check(w,selection,Policy).Eligible)throw new InvalidDataException("作成人物の開始設定が不整合です");
        return w;
    }
    public static void Validate(ContractWorld w)
    {
        if(!Enabled(w))return;ValidatePeaceIdentity(w,false);
    }
    internal static void ValidatePeaceIdentity(ContractWorld w,bool relationsAllowed,bool armyAllowed=false)
    {
        if(w.PlayerControl!=Person||w.Characters.SingleOrDefault(c=>c.Id==Person)?.Life!=LifeState.Adult||
            !relationsAllowed&&(w.Memberships.Any(m=>m.Subject==Person)||w.ServiceBonds.Any(b=>b.Subject==Person||b.Target==Person))||
            w.Offices.Any(o=>o.Subject==Person)||w.LandRights.Any(l=>l.Subject==Person)||
            !armyAllowed&&w.Units.Any(u=>u.Commander==Person)||w.ResourceLots.Any(l=>l.Owner==Person))
            throw new InvalidDataException("Personal peace start cannot silently gain political or military rights");
        var grants=w.Grants.Where(g=>g.Agent==Person).ToArray();
        if(grants.Length!=4||grants.Any(g=>g.Principal!=Person&&!RegionalWork.Roles.Any(r=>r.Person==g.Principal)||g.Actions.Length!=1||g.Redelegate||g.Limit!=0||g.Assets.Length!=0||g.From!=0||g.Until!=long.MaxValue)||
            grants.Count(g=>g.Principal==Person&&g.Scope==ContractFixture.Castle&&g.Actions[0]==ActionKind.Move)!=1||
            RegionalWork.Roles.Any(r=>grants.Count(g=>g.Principal==r.Person&&g.Scope==RegionalCampaign.Site(r.Node)&&g.Actions[0]==ActionKind.Inspect)!=1))
            throw new InvalidDataException("Personal start has an unregistered grant");
    }
}
