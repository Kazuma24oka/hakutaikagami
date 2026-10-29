namespace HakutaiKagami.Core.Contracts;
public sealed record MaterialStock(string Kind,long Quantity);
public sealed record CommodityQuote(string Kind,string Name,string Unit,int MassKg,long BuyPrice,long SellPrice,long Available);
public sealed record MaterialProfile(EntityId Site,EntityId Merchant,EntityId Facility,EntityId? Residents,string Basis);
public sealed record WorkshopMonth(EntityId Site,long At,long Revision,long WoodMade,long WeaponsMade,long Wages,long WoodBought,long WeaponsBought,long Income,string InputsHash);
public sealed record WorkshopInputs(EntityId Site,long At,CommunityAccounts Community,TradeMarket Merchant,long CashFree,long WoodFree,long WeaponsFree,long WorkersCash,long RawWood,long RawWeapons,long CivilWeapons);
public sealed record MaterialHouse(EntityId Site,long Wood,long Weapons,long WoodUsed);
public sealed record MaterialState(int Schema,string Policy,MaterialProfile[] Profiles,WorkshopMonth[] Months,MaterialHouse[] Houses);
public static class MaterialMarkets
{
    public const string Policy="timber-weapons-local-workshops-national-hub-markets-v8";
    public const long InitialWood=200,InitialWeapons=40,RawWood=1000,RawWeapons=100,WorkerCash=300,HouseWood=20,HouseWeapons=10,WaterWood=10;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string text){if(!ok)throw new InvalidDataException(text);}
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or RegionalDomains.Policy or HouseLogistics.Policy or HouseFreightReceiving.Policy or MilitaryEconomy.Policy or NpcFreight.Policy or FreightOperations.Policy or FieldConvoy.Policy or ArmyRoadMarch.Policy or RoadConvoy.Policy or PersonalLife.Policy or CastleHandover.Policy or CastleAdministration.Policy or HouseSuccession.Policy;
    static readonly EntityId[] hubs=CommunityEconomy.Sites.Concat(NationalRoutes.Catalog.Nodes.Where(n=>n.Kind=="historic-site-candidate"||n.Id is "kyoto" or "sakai").Select(n=>new EntityId("site:"+n.Id))).Distinct().ToArray();
    public static EntityId[] Sites(ContractWorld w)=>Enabled(w)?[..hubs]:CommunityEconomy.Sites;
    public static bool Supported(string? kind)=>kind is null or "wood" or "weapons";
    public static string Name(string? kind)=>kind=="wood"?"木材":kind=="weapons"?"武具一式":"米";
    public static string Unit(string? kind)=>kind=="weapons"?"組":"kg";
    public static int MassKg(string? kind)=>kind=="weapons"?5:1;
    public static long Initial(string kind)=>kind=="wood"?InitialWood:InitialWeapons;
    public static long Stock(TradeMarket market,string? kind)=>kind==null?market.Rice:market.Goods!.Single(g=>g.Kind==kind).Quantity;
    internal static TradeMarket Change(TradeMarket m,string? kind,long delta)=>kind==null?m with{Rice=checked(m.Rice+delta)}:m with{Goods=m.Goods!.Select(g=>g.Kind==kind?g with{Quantity=checked(g.Quantity+delta)}:g).ToArray()};
    internal static MaterialStock[] InitialGoods()=>[new("wood",InitialWood),new("weapons",InitialWeapons)];
    static bool Before(long at,long rev,long t,long r)=>at<t||at==t&&rev<r;
    static MaterialProfile[] Profiles(ContractWorld w)=>Sites(w).Select(site=>new MaterialProfile(site,new("org:market/"+site.Value.Replace(':','/')),new("facility:market/"+site.Value.Replace(':','/')),CommunityEconomy.SitesFor(w).Contains(site)?new EntityId("organization:village/"+site.Value.Replace(':','/')):null,"Authored trial market on an existing map node; not historical market/stock/price evidence")).ToArray();
    static long Supplied(ContractWorld w,EntityId site,string kind,long at,long rev)=>w.Trade?.Entries.Where(e=>e.Input.Action==TradeAction.SupplyHouse&&e.Input.Site==site&&Before(e.At,e.Revision,at,rev)).Where(e=>w.Trade.Entries.Single(q=>q.Input.Id==e.Input.Contract).Input.Commodity==kind).Sum(e=>w.Trade.Entries.Single(q=>q.Input.Id==e.Input.Contract).Input.Amount)??0;
    internal static long WoodAvailable(ContractWorld w,EntityId site,long at,long rev)=>!Enabled(w)?long.MaxValue:(RegionalDomains.HasHouse(w,site)?HouseWood:0)+Supplied(w,site,"wood",at,rev)+RegionalDomains.Purchased(w,site,"wood",at,rev)+HouseLogistics.Supplied(w,site,"wood",at,rev)-w.Communities!.Orders.Where(o=>o.Input.Site==site&&o.Input.Action==EconomyAction.ImproveWater&&Before(o.At,o.Revision,at,rev)).LongCount()*WaterWood;
    internal static MaterialHouse HouseAt(ContractWorld w,EntityId site,long at,long rev)=>new(site,WoodAvailable(w,site,at,rev),(RegionalDomains.HasHouse(w,site)?HouseWeapons:0)+Supplied(w,site,"weapons",at,rev)+RegionalDomains.Purchased(w,site,"weapons",at,rev)+HouseLogistics.Supplied(w,site,"weapons",at,rev),w.Communities!.Orders.LongCount(o=>o.Input.Site==site&&o.Input.Action==EconomyAction.ImproveWater&&(o.At<at||o.At==at&&o.Revision<rev))*WaterWood);
    static MaterialHouse[] Houses(ContractWorld w)=>CommunityEconomy.SitesFor(w).Select(site=>HouseAt(w,site,w.ElapsedSeconds,w.CommitSequence+1)).ToArray();
    static WorkshopMonth Compute(ContractWorld w,EntityId site,long at,long revision)
    {
        var previous=w.Materials!.Months.Where(m=>m.Site==site&&Before(m.At,m.Revision,at,revision)).ToArray();var account=CommunityEconomy.AccountAt(w,site,at,revision);var trade=RegionalTrade.SnapshotAt(w,at,revision);var m=trade.Markets.Single(m=>m.Site==site);
        long woodFree=RegionalTrade.StockFree(trade,site,"wood"),armsFree=RegionalTrade.StockFree(trade,site,"weapons"),cashFree=RegionalTrade.CashFree(trade,site);
        long funds=WorkerCash+previous.Sum(m=>m.Wages-m.Income),wood=RawWood-previous.Sum(m=>m.WoodMade),arms=RawWeapons-previous.Sum(m=>m.WeaponsMade),civil=previous.Sum(m=>m.WeaponsBought);
        var input=new WorkshopInputs(site,at,account,m,cashFree,woodFree,armsFree,funds,wood,arms,civil);long madeWood=0,madeArms=0,wages=0,boughtWood=0,boughtArms=0,income=0;
        if(account.Hunger==0){int labor=account.Workforce-account.Reserved;long woodLimit=MilitaryEconomy.Enabled(w)?20L*labor/CommunityEconomy.Workers:20,weaponLimit=MilitaryEconomy.Enabled(w)?2L*labor/CommunityEconomy.Workers:2;
            if(labor>=10){madeWood=Math.Max(0,Math.Min(Math.Min(woodLimit,220-Stock(m,"wood")),Math.Min(wood,cashFree*2)));if(madeWood>0){wages=(madeWood+1)/2;labor-=10;}}
            if(labor>=5){madeArms=Math.Max(0,Math.Min(Math.Min(weaponLimit,42-Stock(m,"weapons")),Math.Min(arms,(cashFree-wages)/5)));wages+=madeArms*5;}
            var after=Change(Change(m,"wood",madeWood),"weapons",madeArms);long price=RegionalTrade.QuotePrice(after,true,"wood");boughtWood=Math.Min(2,Math.Min(woodFree+madeWood,(funds+wages)/price));income=boughtWood*price;
            price=RegionalTrade.QuotePrice(after,true,"weapons");boughtArms=Math.Min(1,Math.Min(Math.Max(0,10-civil),Math.Min(armsFree+madeArms,(funds+wages-income)/price)));income+=boughtArms*price;
        }
        return new(site,at,revision,madeWood,madeArms,wages,boughtWood,boughtArms,income,ContractJson.CanonicalHash(input));
    }
    internal static WorkshopMonth[] Production(ContractWorld w,long at,long rev)=>w.Materials?.Months.Where(m=>Before(m.At,m.Revision,at,rev)).OrderBy(m=>m.Revision).ThenBy(m=>m.Site.Value,StringComparer.Ordinal).ToArray()??[];
    internal static TradeMarket Apply(TradeMarket m,WorkshopMonth p)=>Change(Change(m,"wood",p.WoodMade-p.WoodBought),"weapons",p.WeaponsMade-p.WeaponsBought) with{Cash=checked(m.Cash-p.Wages+p.Income)};
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Materials=new(1,Policy,Profiles(w),[],Houses(w));}
    internal static void Tick(ContractWorld w)
    {
        if(!Enabled(w))return;var d=DateOnly.Parse(w.EpochDate).AddDays((int)(w.ElapsedSeconds/86400));if(w.ElapsedSeconds>0&&w.ElapsedSeconds%86400==0&&d.Day==1){foreach(var site in CommunityEconomy.SitesFor(w)){if(w.Materials!.Months.Any(m=>m.Site==site&&m.At==w.ElapsedSeconds))continue;var month=Compute(w,site,w.ElapsedSeconds,w.CommitSequence);w.Materials=w.Materials! with{Months=[..w.Materials.Months,month]};}}
        w.Materials=w.Materials! with{Houses=Houses(w)};
    }
    internal static void Validate(ContractWorld w,ContractWorld? prior=null)
    {
        if(!Enabled(w)){Need(w.Materials==null,"旧世界へ物資市場を追加できません");return;}
        Need(w.Materials is {Schema:1,Policy:Policy,Profiles:not null,Months:not null,Houses:not null},"物資市場の原本がありません");var s=w.Materials;bool reuse=RegionalDomains.PastUnchanged(w,prior)&&s.Months.Length>=prior!.Materials!.Months.Length&&s.Months.Take(prior.Materials.Months.Length).SequenceEqual(prior.Materials.Months);Need(ContractJson.CanonicalHash(s.Profiles)==ContractJson.CanonicalHash(Profiles(w))&&ContractJson.CanonicalHash(s.Houses)==ContractJson.CanonicalHash(Houses(w))&&s.Houses.All(h=>h.Wood>=0&&h.Weapons>=0),"市場所在地または家の物資が原本と違います");
        var epoch=DateOnly.Parse(w.EpochDate);var current=epoch.AddDays((int)(w.ElapsedSeconds/86400));int elapsedMonths=(current.Year-epoch.Year)*12+current.Month-epoch.Month;Need(s.Months.Length==elapsedMonths*CommunityEconomy.SitesFor(w).Length,"月次生産原本の欠落があります");
        var seen=new HashSet<(EntityId,long)>();long priorAt=0,priorRev=0;foreach(var p in s.Months){Need(p.At>0&&p.At<=w.ElapsedSeconds&&p.At%86400==0&&DateOnly.Parse(w.EpochDate).AddDays((int)(p.At/86400)).Day==1&&p.Revision>0&&p.Revision<=w.CommitSequence&&p.At>=priorAt&&p.Revision>=priorRev&&CommunityEconomy.SitesFor(w).Contains(p.Site)&&seen.Add((p.Site,p.At)),"工房の月次原本が重複・不正です");Need(reuse&&prior!.Materials!.Months.Contains(p)||p==Compute(w,p.Site,p.At,p.Revision),"工房の生産・賃金・住民購入が当時の入力と一致しません");priorAt=p.At;priorRev=p.Revision;}
        foreach(var site in CommunityEconomy.SitesFor(w)){var ms=s.Months.Where(m=>m.Site==site);Need(ms.Sum(m=>m.WoodMade)<=RawWood&&ms.Sum(m=>m.WeaponsMade)<=RawWeapons&&WorkerCash+ms.Sum(m=>m.Wages-m.Income)>=0&&ms.Sum(m=>m.WeaponsBought)<=10,"加工前在庫・住民資金・武具保有が不正です");}
    }
    internal static void CheckBalances(ContractWorld w,IEnumerable<TradeMarket> markets,IEnumerable<TradeDeal> deals,long until,long revision)
    {
        if(!Enabled(w))return;var ms=markets.ToArray();var ds=deals.ToArray();var production=Production(w,until,revision+1);
        foreach(var kind in new[]{"wood","weapons"}){long carried=ds.Where(d=>d.Commodity==kind&&d.Phase is TradePhase.Carrying or TradePhase.SaleQuoted or TradePhase.ReturnRequired).Sum(d=>d.Amount),supplied=ds.Where(d=>d.Commodity==kind&&d.Phase==TradePhase.Supplied).Sum(d=>d.Amount);long made=production.Sum(p=>kind=="wood"?p.WoodMade:p.WeaponsMade),consumed=production.Sum(p=>kind=="wood"?p.WoodBought:p.WeaponsBought);Need(ms.Sum(m=>Stock(m,kind))+carried+supplied+consumed+HouseLogistics.MarketPurchases(w,until,revision+1).Where(e=>e.Input.Commodity==kind).Sum(e=>e.Input.Quantity)+RegionalDomains.Purchases(w,until,revision+1).Where(e=>e.Input.Commodity==kind).Sum(e=>e.Input.Quantity)==Initial(kind)*ms.Length+made,"商品別の現物収支が一致しません");}
    }
    public static MaterialHouse? LocalHouse(ContractWorld w)=>Enabled(w)&&CommunityEconomy.View(w,w.PlayerControl) is {} view?w.Materials!.Houses.Single(h=>h.Site==view.Site):null;
}
