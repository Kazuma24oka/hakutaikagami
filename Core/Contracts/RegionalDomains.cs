namespace HakutaiKagami.Core.Contracts;
public sealed record DomainProfile(EntityId Site,EntityId? Owner,int Population,int Workforce,long VillageRice,long HouseRice,long InitialCash,string Basis);
public enum TreasuryAction { SetBudget,Fund,Buy }
public sealed record TreasuryInput(EntityId Id,TreasuryAction Action,EntityId Site,string? Commodity=null,long Quantity=0,long Amount=0);
public sealed record SpotQuote(EntityId Id,string? Commodity,long Quantity,long UnitPrice,long At,long Revision);
public sealed record SpotReceipt(EntityId Id,EntityId Quote,EntityId Owner,EntityId Site,string? Commodity,long Quantity,long At,long Revision);
public sealed record SpotPayment(EntityId Id,EntityId Quote,EntityId FromHouse,EntityId ToMerchant,long Amount,long At,long Revision);
public sealed record TreasuryEntry(TreasuryInput Input,EntityId Actor,long At,long Revision,SpotQuote? Quote,SpotReceipt? Receipt,SpotPayment? Payment);
public sealed record ProcurementInputs(EntityId Actor,EntityId Site,bool Local,bool Authority,CommunityAccounts? Community,MaterialHouse? Materials,TradeMarket? Merchant,long RiceFree,long WoodFree,long Cash,long Budget,EntityId? Grant);
public sealed record ProcurementDecision(EntityId Id,EntityId Actor,EntityId Site,long At,long Revision,string InputsHash,string? Commodity,long Quantity,string Reason);
public sealed record RegionalObservation(EntityId Site,long ObservedAt,long HouseCash,long MonthlyBudget,long BudgetRemaining,MaterialHouse Materials);
public sealed record DomainState(int Schema,string Policy,DomainProfile[] Profiles,TreasuryEntry[] Entries,ProcurementDecision[] Decisions);
public sealed record TreasuryView(EntityId Site,long HouseCash,long PrivateCash,long MonthlyBudget,long Remaining,bool CanSetBudget,bool CanBuy,MaterialHouse Materials);
public static class RegionalDomains
{
    public const string Policy="nationwide-trial-domain-ledgers-bounded-local-house-procurement-v9";
    const long Day=86400;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string text){if(!ok)throw new InvalidDataException(text);}
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or HouseLogistics.Policy or HouseFreightReceiving.Policy or MilitaryEconomy.Policy or NpcFreight.Policy;
    internal static EntityId? Owner(ContractWorld w,EntityId site)=>w.NewGame!.Start.Castles.SingleOrDefault(c=>c.Id==site.Value)?.Faction is {} f?new(f):null;
    internal static EntityId? Head(ContractWorld w,EntityId site)=>Owner(w,site) is {} owner&&w.NewGame!.Start.Factions.SingleOrDefault(f=>f.Id==owner.Value) is {} f?new(f.Head):null;
    internal static bool HasHouse(ContractWorld w,EntityId site)=>!Enabled(w)||Owner(w,site)!=null;
    static bool Before(long at,long rev,long t,long r)=>at<t||at==t&&rev<r;
    static DateOnly Date(ContractWorld w,long at)=>DateOnly.Parse(w.EpochDate).AddDays((int)(at/Day));
    static bool Month(ContractWorld w,long a,long b){var x=Date(w,a);var y=Date(w,b);return x.Year==y.Year&&x.Month==y.Month;}
    static DomainProfile[] Profiles(ContractWorld w)=>MaterialMarkets.Sites(w).Select(site=>new DomainProfile(site,Owner(w,site),200,100,10000,Owner(w,site)!=null?3000:0,Owner(w,site)!=null?500:0,"Authored trial population and starting balances; historical demographics/ownership NOT inferred from a map point")).ToArray();
    static TreasuryEntry[] Prefix(ContractWorld w,long at,long rev)=>w.Domains?.Entries.Where(e=>Before(e.At,e.Revision,at,rev)).ToArray()??[];
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Domains?.Entries.Any(e=>e.Input.Id==id||e.Quote?.Id==id||e.Receipt?.Id==id||e.Payment?.Id==id)==true||w.Domains?.Decisions.Any(d=>d.Id==id)==true;
    public static long Net(ContractWorld w,EntityId wallet,long at=long.MaxValue,long rev=long.MaxValue)=>Prefix(w,at,rev).Where(e=>e.Input.Action==TreasuryAction.Fund&&NewGameWork.Wallet(e.Actor)==wallet).Sum(e=>-e.Input.Amount);
    internal static TreasuryEntry[] Purchases(ContractWorld w,long at,long rev)=>Prefix(w,at,rev).Where(e=>e.Input.Action==TreasuryAction.Buy).ToArray();
    internal static long Purchased(ContractWorld w,EntityId site,string? kind,long at,long rev)=>Purchases(w,at,rev).Where(e=>e.Input.Site==site&&e.Input.Commodity==kind).Sum(e=>e.Input.Quantity);
    internal static long SpentAll(ContractWorld w,long at,long rev)=>Purchases(w,at,rev).Sum(e=>e.Payment!.Amount);
    internal static long Cash(ContractWorld w,EntityId site,long at,long rev)=>(HasHouse(w,site)?500:0)+HouseLogistics.CashNet(w,site,at,rev)+Prefix(w,at,rev).Where(e=>e.Input.Site==site).Sum(e=>e.Input.Action==TreasuryAction.Fund?e.Input.Amount:e.Input.Action==TreasuryAction.Buy?-e.Payment!.Amount:0);
    internal static long Budget(ContractWorld w,EntityId site,long at,long rev)=>Prefix(w,at,rev).LastOrDefault(e=>e.Input.Site==site&&e.Input.Action==TreasuryAction.SetBudget)?.Input.Amount??200;
    internal static long Remaining(ContractWorld w,EntityId actor,EntityId site,long at,long rev)
    {
        var es=Purchases(w,at,rev).Where(e=>e.Input.Site==site&&Month(w,e.At,at)).ToArray();long n=Math.Max(0,Budget(w,site,at,rev)-es.Sum(e=>e.Payment!.Amount)-HouseLogistics.BudgetUsed(w,actor,site,at,rev,false));
        if(Head(w,site)!=actor)n=Math.Min(n,Math.Max(0,200-es.Where(e=>e.Actor==actor).Sum(e=>e.Payment!.Amount)-HouseLogistics.BudgetUsed(w,actor,site,at,rev,true)));return n;
    }
    static bool HeadAuthority(ContractWorld w,EntityId actor,EntityId site,long at,long rev)=>Owner(w,site) is {} owner&&FactionStances.ResolveAuthority(w,actor,owner,at,Math.Min(rev,w.CommitSequence))!=null&&w.LandRights.Any(r=>r.Subject==owner&&r.Target==site&&r.Kind=="control"&&r.From<=at&&at<r.Until);
    internal static bool Authority(ContractWorld w,EntityId actor,EntityId site,long at,long rev)=>HeadAuthority(w,actor,site,at,rev)||Owner(w,site) is {} owner&&CommunityStewardship.Authority(w,actor,owner,site,at,rev)!=null;
    static EntityId Child(string kind,EntityId id)=>new(kind+":house/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(id.Value)));
    static TreasuryEntry Make(ContractWorld w,TreasuryInput i,EntityId actor,long at,long rev)
    {
        Need(Enabled(w)&&CommunityEconomy.SitesFor(w).Contains(i.Site)&&CommunityStewardship.Local(w,actor,i.Site,at,rev)&&Authority(w,actor,i.Site,at,rev)&&Enum.IsDefined(i.Action),"現地の家の処分権がありません");
        if(i.Action!=TreasuryAction.Buy){Need(actor==w.PlayerControl&&HeadAuthority(w,actor,i.Site,at,rev)&&i.Commodity==null&&i.Quantity==0,"予算と拠出は現地当主本人が決めます");Need(i.Action==TreasuryAction.SetBudget?i.Amount>=0&&i.Amount<=1000:i.Amount>0&&i.Amount<=TransportWork.WalletBefore(w,actor,at,rev),"予算範囲または本人の自由な私財を確認してください");return new(i,actor,at,rev,null,null,null);}
        Need(MaterialMarkets.Supported(i.Commodity)&&i.Quantity>0&&i.Quantity<=1000&&i.Amount==0,"物資・数量が不正です");var trade=RegionalTrade.SnapshotAt(w,at,rev);var merchant=trade.Markets.Single(m=>m.Site==i.Site);long price=RegionalTrade.QuotePrice(merchant,true,i.Commodity),cost=checked(price*i.Quantity);
        Need(i.Quantity<=RegionalTrade.StockFree(trade,i.Site,i.Commodity)&&cost<=Cash(w,i.Site,at,rev)&&cost<=Remaining(w,actor,i.Site,at,rev),"商人の自由在庫・家の資金・今月の調達枠が不足しています");var quote=new SpotQuote(Child("quote",i.Id),i.Commodity,i.Quantity,price,at,rev);var owner=Owner(w,i.Site)!.Value;var m=w.Materials!.Profiles.Single(p=>p.Site==i.Site);
        return new(i,actor,at,rev,quote,new(Child("receipt",i.Id),quote.Id,owner,i.Site,i.Commodity,i.Quantity,at,rev),new(Child("payment",i.Id),quote.Id,owner,m.Merchant,cost,at,rev));
    }
    static EntityId? Controller(ContractWorld w,EntityId site,long at,long rev)=>CommunityStewardship.Delegate(w,site,at,rev)??Head(w,site);
    static EntityId DecisionId(ContractWorld w,EntityId site,long at,long rev){string root="decision:house-procure/"+site.Value.Replace(':','/')+"/"+(CommunityStewardship.GrantAt(w,site,at,rev)!=null?"d"+(at/Day):"m"+(Date(w,at).Year*12+Date(w,at).Month));var id=new EntityId(root);int n=0;while(FactionStances.OtherIdUsed(w,id,includeDomains:false)||FactionStances.OwnIdUsed(w,id))id=new(root+"/n"+(++n));return id;}
    static ProcurementDecision Decide(ContractWorld w,EntityId site,long at,long rev)
    {
        var actor=Controller(w,site,at,rev);Need(actor!=null,"この地域に当主・代理官を設定していません");bool local=CommunityStewardship.Local(w,actor.Value,site,at,rev),authority=Authority(w,actor.Value,site,at,rev);CommunityAccounts? community=null;MaterialHouse? materials=null;TradeMarket? merchant=null;long rice=0,wood=0,cash=0,budget=0,n=0;string? kind=null;string reason="現地で権限者が確認できるまで調達を待機";
        if(local&&authority){community=CommunityEconomy.AccountAt(w,site,at,rev);materials=MaterialMarkets.HouseAt(w,site,at,rev);var trade=RegionalTrade.SnapshotAt(w,at,rev);merchant=trade.Markets.Single(m=>m.Site==site);rice=RegionalTrade.StockFree(trade,site,null);wood=RegionalTrade.StockFree(trade,site,"wood");cash=Cash(w,site,at,rev);budget=Remaining(w,actor.Value,site,at,rev);long free=community.HouseRice-HouseSalary.Reserved(w,site,at,rev);
            if(community.Hunger>free){n=Math.Min(500,Math.Min(community.Hunger-free,rice));reason="救済用の米を家の資金で現地調達";}
            else if(community.Hunger==0&&community.Water<130&&materials.Wood<10){kind="wood";n=Math.Min(10-materials.Wood,wood);reason="合意できる水利工事に必要な木材を現地調達";}
            else reason="必要な米・工事木材が足りているため調達を待機";
            if(n>0)n=Math.Min(n,Math.Min(cash,budget)/RegionalTrade.QuotePrice(merchant,true,kind));if(n==0&&reason.Contains("調達")&&!reason.Contains("待機"))reason="必要物資はあるが在庫・資金・予算不足で調達を待機";
        }
        var input=new ProcurementInputs(actor.Value,site,local,authority,community,materials,merchant,rice,wood,cash,budget,CommunityStewardship.GrantAt(w,site,at,rev)?.Id);
        return new(DecisionId(w,site,at,rev),actor.Value,site,at,rev,ContractJson.CanonicalHash(input),kind,n,reason);
    }
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Domains=new(1,Policy,Profiles(w),[],[]);}
    internal static void Apply(ContractWorld w,TreasuryInput i,EntityId actor){var e=Make(w,i,actor,w.ElapsedSeconds,w.CommitSequence);w.Domains=w.Domains! with{Entries=[..w.Domains!.Entries,e]};NewGameWork.Sync(w);}
    internal static void ApplyNpc(ContractWorld w,EntityId site){var d=Decide(w,site,w.ElapsedSeconds,w.CommitSequence);Need(d.Actor!=w.PlayerControl,"本人の買物は自動化しません");w.Domains=w.Domains! with{Decisions=[..w.Domains!.Decisions,d]};if(d.Quantity>0)Apply(w,new(d.Id,TreasuryAction.Buy,site,d.Commodity,d.Quantity),d.Actor);}
    internal static void TakeTurns(WorldSession session)
    {
        var w=session.DiagnosticSnapshot();if(!Enabled(w)||w.ElapsedSeconds==0||w.ElapsedSeconds%Day!=0)return;
        foreach(var site in CommunityEconomy.SitesFor(w)){w=session.DiagnosticSnapshot();if(Date(w,w.ElapsedSeconds).Day!=1&&CommunityStewardship.GrantAt(w,site,w.ElapsedSeconds,w.CommitSequence+1)==null)continue;var actor=Controller(w,site,w.ElapsedSeconds,w.CommitSequence+1);if(actor==null||actor==w.PlayerControl||w.Domains!.Decisions.Any(d=>d.Id==DecisionId(w,site,w.ElapsedSeconds,w.CommitSequence+1)))continue;session.RunHouseProcurement(site,w.CommitSequence);}
    }
    internal static bool Authorized(ContractWorld w,TreasuryEntry e)=>w.Domains?.Decisions.Any(d=>d.Id==e.Input.Id&&d.Actor==e.Actor&&d.At==e.At&&d.Revision==e.Revision&&d.Quantity==e.Input.Quantity&&d.Commodity==e.Input.Commodity&&e.Input.Action==TreasuryAction.Buy)==true;
    static bool AppendOnly<T>(T[] before,T[] after,Func<T,long> at,Func<T,long> rev,ContractWorld prior)=>after.Length>=before.Length&&after.Take(before.Length).SequenceEqual(before)&&after.Skip(before.Length).All(e=>at(e)>=prior.ElapsedSeconds&&rev(e)>prior.CommitSequence);
    internal static bool PastUnchanged(ContractWorld w,ContractWorld? prior)
    {
        if(!MilitaryEconomy.PastUnchanged(w,prior))return false;
        if(HouseLogistics.Enabled(w)&&ContractJson.CanonicalHash(w.HouseFreight)!=ContractJson.CanonicalHash(prior?.HouseFreight))return false;
        if(prior==null||!Enabled(w)||!Enabled(prior)||w.WorldId!=prior.WorldId||w.PlayerControl!=prior.PlayerControl||w.Seed!=prior.Seed||w.EpochDate!=prior.EpochDate||w.ElapsedSeconds<prior.ElapsedSeconds||w.CommitSequence<=prior.CommitSequence)return false;
        if(!w.Characters.SequenceEqual(prior.Characters)||!w.Locations.SequenceEqual(prior.Locations)||!w.Memberships.SequenceEqual(prior.Memberships)||!w.ServiceBonds.SequenceEqual(prior.ServiceBonds)||!w.Offices.SequenceEqual(prior.Offices)||!w.LandRights.SequenceEqual(prior.LandRights)||ContractJson.CanonicalHash(w.NewGame)!=ContractJson.CanonicalHash(prior.NewGame)||ContractJson.CanonicalHash(w.Tasks)!=ContractJson.CanonicalHash(prior.Tasks)||ContractJson.CanonicalHash(w.Branches)!=ContractJson.CanonicalHash(prior.Branches))return false;
        return AppendOnly(prior.Communities!.Orders,w.Communities!.Orders,e=>e.At,e=>e.Revision,prior)&&AppendOnly(prior.Trade!.Entries,w.Trade!.Entries,e=>e.At,e=>e.Revision,prior)&&AppendOnly(prior.Salary!.Entries,w.Salary!.Entries,e=>e.At,e=>e.Revision,prior)&&AppendOnly(prior.Domains!.Entries,w.Domains!.Entries,e=>e.At,e=>e.Revision,prior)&&w.Domains.Profiles.SequenceEqual(prior.Domains.Profiles);
    }
    internal static void Validate(ContractWorld w,ContractWorld? prior=null)
    {
        if(!Enabled(w)){Need(w.Domains==null,"旧世界へ全国地域台帳を追加できません");return;}Need(w.Domains is {Schema:1,Policy:Policy,Profiles:not null,Entries:not null,Decisions:not null},"地域台帳の原本がありません");var s=w.Domains;bool reuse=PastUnchanged(w,prior);Need(ContractJson.CanonicalHash(s.Profiles)==ContractJson.CanonicalHash(Profiles(w)),"地域の人口・初期資金・所有者が開始条件と一致しません");var ids=new HashSet<EntityId>();long rev=0,at=0;
        foreach(var e in s.Entries){Need(e.At>=at&&e.At<=w.ElapsedSeconds&&e.Revision>rev&&e.Revision<=w.CommitSequence&&EntityId.IsValid(e.Input.Id.Value)&&ids.Add(e.Input.Id)&&!FactionStances.OtherIdUsed(w,e.Input.Id,includeDomains:false)&&!FactionStances.OwnIdUsed(w,e.Input.Id),"家の資金操作の順序・IDが不正です");Need(e.Actor==w.PlayerControl||Authorized(w,e),"NPC調達に判断原本がありません");Need(reuse&&prior!.Domains!.Entries.Contains(e)||e==Make(w,e.Input,e.Actor,e.At,e.Revision),"見積・受領・支払が当時の入力と一致しません");if(e.Quote!=null){Need(ids.Add(e.Quote.Id)&&ids.Add(e.Receipt!.Id)&&ids.Add(e.Payment!.Id)&&new[]{e.Quote.Id,e.Receipt.Id,e.Payment.Id}.All(id=>!FactionStances.OtherIdUsed(w,id,includeDomains:false)&&!FactionStances.OwnIdUsed(w,id)),"取引証拠IDが重複しています");}rev=e.Revision;at=e.At;}
        ids.Clear();rev=0;at=0;foreach(var d in s.Decisions){Need(d.At>0&&d.At%Day==0&&d.At<=w.ElapsedSeconds&&d.Revision>rev&&d.Revision<=w.CommitSequence&&d.At>=at&&d.Actor!=w.PlayerControl&&ids.Add(d.Id)&&(Date(w,d.At).Day==1||CommunityStewardship.GrantAt(w,d.Site,d.At,d.Revision)!=null),"NPC調達の時点・重複が不正です");Need(!FactionStances.OtherIdUsed(w,d.Id,includeDomains:false)&&!FactionStances.OwnIdUsed(w,d.Id),"NPC調達IDが別操作と重複しています");Need(reuse&&prior!.Domains!.Decisions.Contains(d)||d==Decide(w,d.Site,d.At,d.Revision),"NPCが当時確認した調達入力と一致しません");Need(d.Quantity==0?!s.Entries.Any(e=>e.Input.Id==d.Id):s.Entries.Any(e=>Authorized(w,e)&&e.Input.Id==d.Id),"調達判断と取引原本が一致しません");rev=d.Revision;at=d.At;}
        foreach(var site in CommunityEconomy.SitesFor(w))Need(Cash(w,site,w.ElapsedSeconds,w.CommitSequence+1)>=0,"家の資金が負です");
    }
    internal static RegionalObservation? Observation(ContractWorld w,EntityId site,EntityId actor,long at,long rev)=>Enabled(w)?new(site,at,Cash(w,site,at,rev),Budget(w,site,at,rev),Remaining(w,actor,site,at,rev),MaterialMarkets.HouseAt(w,site,at,rev)):null;
    public static TreasuryView? View(ContractWorld w)
    {if(!Enabled(w))return null;var site=w.Locations.Single(l=>l.Actor==w.PlayerControl).Site;if(site==null||!CommunityEconomy.SitesFor(w).Contains(site.Value)||!CommunityStewardship.Local(w,w.PlayerControl,site.Value,w.ElapsedSeconds,w.CommitSequence+1)||!Authority(w,w.PlayerControl,site.Value,w.ElapsedSeconds,w.CommitSequence+1))return null;return new(site.Value,Cash(w,site.Value,w.ElapsedSeconds,w.CommitSequence+1),NewGameWork.Balance(w,w.PlayerControl),Budget(w,site.Value,w.ElapsedSeconds,w.CommitSequence+1),Remaining(w,w.PlayerControl,site.Value,w.ElapsedSeconds,w.CommitSequence+1),HeadAuthority(w,w.PlayerControl,site.Value,w.ElapsedSeconds,w.CommitSequence+1),true,MaterialMarkets.HouseAt(w,site.Value,w.ElapsedSeconds,w.CommitSequence+1));}
}
public sealed partial class WorldSession
{
    public TreasuryView? HouseTreasuryView(){lock(gate)return RegionalDomains.View(state);}
    public CommandResult ManageHouseTreasury(TreasuryInput input,EntityId actor,long revision)
    {lock(gate){if(actor!=state.PlayerControl||!RegionalDomains.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"本人の家の操作ではありません");var old=state.Domains!.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"操作済みです");if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の原本で使用済みです");if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"現地の予算を読み直してください");try{Change(w=>RegionalDomains.Apply(w,input,actor));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"家の予算・現物受領・支払を記録しました");}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}}}
    internal void RunHouseProcurement(EntityId site,long revision){lock(gate){if(revision!=state.CommitSequence)throw new InvalidOperationException("調達の状態が変わりました");Change(w=>RegionalDomains.ApplyNpc(w,site));}}
}
