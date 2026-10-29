namespace HakutaiKagami.Core.Contracts;
public enum VillageAction { ProposeRelief,ProposeWater,Confirm,Deliver,Cancel }
public sealed record VillageInput(EntityId Id,VillageAction Action,EntityId Site,EntityId? Proposal=null,long Amount=0);
public sealed record VillageEntry(VillageInput Input,long At,long Revision);
public sealed record VillageMember(EntityId Id,string Name);
public sealed record VillageOrganization(EntityId Id,EntityId Site,VillageMember[] Members);
public sealed record VillageProposal(EntityId Id,EntityId Site,EntityId Representative,int Term,VillageAction Kind,long Amount,long At,bool Agreed,string Reply);
public sealed record VillagePromise(EntityId Id,EntityId Site,VillageAction Kind,long Amount,long ConfirmedAt,long DeliverBy,long Delivered,long? StartedAt,long? CancelledAt);
public sealed record VillageState(int Schema,string Policy,VillageEntry[] Entries,VillageOrganization[] Organizations,VillageProposal[] Proposals,VillagePromise[] Promises);
public sealed record VillageCard(EntityId Id,string Kind,long Amount,long Delivered,long DeliverBy,string Status);
public sealed record VillageLocalView(EntityId Site,EntityId Organization,string Representative,int Term,long ObservedAt,bool CanPromise,string Reaction,string Administration,VillageProposal[] Proposals,VillageCard[] Promises);
// Trial village delegates are roles, not invented historical officers or player-controlled organizations.
public static class VillageConsultation
{
    public const string Policy="village-representative-relief-water-performance-v5";
    const long Day=86400;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or NpcCommunityAdministration.Policy or CommunityStewardship.Policy or MaterialMarkets.Policy or RegionalDomains.Policy or HouseLogistics.Policy or HouseFreightReceiving.Policy or MilitaryEconomy.Policy or NpcFreight.Policy or FreightOperations.Policy or FieldConvoy.Policy or ArmyRoadMarch.Policy or RoadConvoy.Policy or PersonalLife.Policy or CastleHandover.Policy or CastleAdministration.Policy;
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>w.Villages?.Entries.Any(e=>e.Input.Id==id)==true;
    static int Term(long at)=>checked((int)(at/(30*Day)));
    static VillageOrganization Organization(EntityId site)=>new(new("organization:village/"+site.Value.Replace(':','/') ),site,[new(new("person:village/"+site.Value.Replace(':','/')+"/a"),"村役人"),new(new("person:village/"+site.Value.Replace(':','/')+"/b"),"水利世話人")]);
    internal static EntityId Representative(EntityId site,long at)=>Organization(site).Members[Term(at)%2].Id;
    internal static EntityId Effect(EntityId id)=>new("request:village-effect/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(id.Value)));
    static bool Local(ContractWorld w,EntityId site,long at,long rev)=>TransportWork.At(w,w.PlayerControl,site,at,rev,stableCompletion:true);
    static bool Authority(ContractWorld w,EntityId site,long at,long rev)
    {
        var castle=w.NewGame!.Start.Castles.Single(c=>c.Id==site.Value);
        if(castle.Faction is not {} f)return false;var owner=new EntityId(f);
        return FactionStances.ResolveAuthority(w,w.PlayerControl,owner,at,rev)!=null&&w.LandRights.Any(r=>r.Subject==owner&&r.Target==site&&r.Kind=="control"&&r.From<=at&&at<r.Until);
    }
    static string Status(ContractWorld w,VillagePromise p,long at,long rev)
    {
        if(p.Kind==VillageAction.ProposeRelief&&p.Delivered==p.Amount)return "救済受領済み";
        if(p.StartedAt is {} start){return w.Communities!.Events.Any(e=>e.Site==p.Site&&e.Kind=="water-complete"&&e.At>start&&e.At<=at)?"工事完了":"工事中（食糧不足時は休止）";}
        if(p.CancelledAt!=null)return p.Delivered>0?"一部履行で終了":"未履行で終了";
        return at>p.DeliverBy?(p.Delivered>0?"一部履行・期限超過":"未履行・期限超過"):"約束の履行待ち";
    }
    static int Trust(ContractWorld w,IEnumerable<VillagePromise> promises,EntityId site,long at)
    {
        int n=70;foreach(var p in promises.Where(p=>p.Site==site)){
            bool done=p.Kind==VillageAction.ProposeWater?p.StartedAt!=null:p.Delivered==p.Amount;
            if(done){if(p.Kind!=VillageAction.ProposeWater||w.Communities!.Events.Any(e=>e.Site==p.Site&&e.Kind=="water-complete"&&e.At>p.StartedAt&&e.At<=at))n+=5;}else if(p.CancelledAt!=null||at>p.DeliverBy)n-=20;
        }return Math.Clamp(n,0,100);
    }
    internal static bool WorkAuthorized(ContractWorld w,EntityId effect,EntityId site,long at,long revision)
    {
        if(!Enabled(w)||NpcCommunityAdministration.WaterAuthorized(w,effect,site,at,revision)||CommunityStewardship.WaterAuthorized(w,effect,site,at,revision))return true;var e=w.Villages?.Entries.SingleOrDefault(e=>e.Revision==revision&&e.At==at&&e.Input.Site==site&&e.Input.Action==VillageAction.Deliver&&Effect(e.Input.Id)==effect);
        return e!=null&&w.Villages!.Proposals.Any(q=>q.Id==e.Input.Proposal&&q.Kind==VillageAction.ProposeWater)&&w.Villages.Entries.Any(c=>c.Input.Action==VillageAction.Confirm&&c.Input.Proposal==e.Input.Proposal&&c.Revision<revision);
    }
    static VillageState Replay(ContractWorld w,VillageEntry[] entries,long until,long revision)
    {
        var proposals=new Dictionary<EntityId,VillageProposal>();var promises=new Dictionary<EntityId,VillagePromise>();var ids=new HashSet<EntityId>();long prev=0,priorAt=0;
        foreach(var e in entries){var i=e.Input;
            Need(EntityId.IsValid(i.Id.Value)&&ids.Add(i.Id)&&e.Revision>prev&&e.Revision<=revision&&e.At>=priorAt&&e.At<=until&&CommunityEconomy.SitesFor(w).Contains(i.Site)&&Enum.IsDefined(i.Action),"村の原本・順序・地域が不正です");prev=e.Revision;priorAt=e.At;
            Need(!FactionStances.OtherIdUsed(w,i.Id,includeVillages:false)&&!FactionStances.OwnIdUsed(w,i.Id),"別の操作で使ったIDです");
            Need(Local(w,i.Site,e.At,e.Revision)&&Authority(w,i.Site,e.At,e.Revision),"現地で家の処分権を持つ本人が相談してください");
            if(i.Action is VillageAction.ProposeWater or VillageAction.ProposeRelief){
                Need(i.Proposal==null&&(i.Action==VillageAction.ProposeWater?i.Amount==CommunityEconomy.WorkCost:i.Amount>0&&i.Amount<=3000),"救済量または工事条件が不正です");
                Need(!promises.Values.Any(p=>p.Site==i.Site&&p.CancelledAt==null&&p.StartedAt==null&&p.Delivered<p.Amount&&e.At<=p.DeliverBy),"先に現在の約束を履行するか終了してください");
                var account=CommunityEconomy.AccountAt(w,i.Site,e.At,e.Revision);bool water=i.Action==VillageAction.ProposeWater;
                bool agreed=!water||account.Hunger==0&&account.Reserved==0&&account.Water<130&&Trust(w,promises.Values,i.Site,e.At)>=40;
                string reply=!water?"その量の救済を村の食糧として受け取ります。七日以内の現物受渡しをお願いします。":account.Hunger>0?"先に食糧不足への救済をお願いします。工事は引き受けられません。":account.Reserved>0?"働き手は既に工事に出ています。":account.Water>=130?"今は追加の水利工事を必要としていません。":!agreed?"前の約束が守られていません。先に救済を実行してください。":"米200kg・働き手20人で七日間の工事に協力します。七日以内に着手してください。";
                proposals.Add(i.Id,new(i.Id,i.Site,Representative(i.Site,e.At),Term(e.At),i.Action,i.Amount,e.At,agreed,reply));
            }else{
                Need(i.Proposal is {} id&&proposals.ContainsKey(id),"相談原本がありません");var q=proposals[i.Proposal!.Value];Need(q.Site==i.Site,"別の村の原本です");
                if(i.Action==VillageAction.Confirm){
                    Need(i.Amount==0&&q.Agreed&&e.At<=q.At+Day&&q.Representative==Representative(i.Site,e.At)&&q.Term==Term(e.At)&&!promises.ContainsKey(q.Id),"同じ代表の有効な返答を確認し直してください");
                    Need(!promises.Values.Any(p=>p.Site==i.Site&&p.CancelledAt==null&&p.StartedAt==null&&p.Delivered<p.Amount&&e.At<=p.DeliverBy),"別の約束が成立済みです");
                    var a=CommunityEconomy.AccountAt(w,i.Site,e.At,e.Revision);Need(q.Kind!=VillageAction.ProposeWater||a.Hunger==0&&a.Reserved==0&&a.Water<130,"村の食糧・労働条件が変わりました");
                    promises.Add(q.Id,new(q.Id,q.Site,q.Kind,q.Amount,e.At,e.At+7*Day,0,null,null));
                }else{
                    Need(promises.TryGetValue(q.Id,out var p)&&p.CancelledAt==null&&p.StartedAt==null&&p.Delivered<p.Amount,"履行待ちの約束ではありません");
                    if(i.Action==VillageAction.Cancel){Need(i.Amount==0,"取消に数量は指定できません");promises[q.Id]=p! with{CancelledAt=e.At};}
                    else{
                        Need(e.At<=p!.DeliverBy&&i.Amount>0&&i.Amount<=p.Amount-p.Delivered,"期限と未履行量を確認してください");
                        Need(p.Kind!=VillageAction.ProposeWater||i.Amount==CommunityEconomy.WorkCost,"工事は一式で着手します");
                        var effect=w.Communities!.Orders.SingleOrDefault(o=>o.Input.Id==Effect(i.Id));
                        Need(effect!=null&&effect.At==e.At&&effect.Revision==e.Revision&&effect.Actor==w.PlayerControl&&effect.Input.Site==i.Site&&effect.Input.Amount==i.Amount&&effect.Input.Action==(p.Kind==VillageAction.ProposeWater?EconomyAction.ImproveWater:EconomyAction.RelieveVillage),"約束に対応する現物の履行原本がありません");
                        promises[q.Id]=p with{Delivered=p.Delivered+i.Amount,StartedAt=p.Kind==VillageAction.ProposeWater?e.At:null};
                    }
                }
            }
        }
        Need(w.Communities!.Orders.Where(o=>o.Input.Id.Value.StartsWith("request:village-effect/",StringComparison.Ordinal)).All(o=>entries.Any(e=>e.Input.Action==VillageAction.Deliver&&Effect(e.Input.Id)==o.Input.Id)),"現物原本に対応する村との約束がありません");
        return new(1,Policy,entries,CommunityEconomy.SitesFor(w).Select(Organization).ToArray(),proposals.Values.ToArray(),promises.Values.ToArray());
    }
    internal static void Initialize(ContractWorld w){if(Enabled(w))w.Villages=Replay(w,[],w.ElapsedSeconds,w.CommitSequence);}
    internal static void Tick(ContractWorld w){if(Enabled(w))w.Villages=Replay(w,w.Villages!.Entries,w.ElapsedSeconds,w.CommitSequence);}
    internal static void Apply(ContractWorld w,VillageInput i)
    {
        var entries=w.Villages!.Entries.Append(new VillageEntry(i,w.ElapsedSeconds,w.CommitSequence)).ToArray();
        // The change runs on WorldSession's private copy: either both ledgers publish or neither does.
        w.Villages=w.Villages with{Entries=entries};
        if(i.Action==VillageAction.Deliver){var q=w.Villages.Proposals.SingleOrDefault(q=>q.Id==i.Proposal);Need(q!=null,"相談原本がありません");CommunityEconomy.Apply(w,new(Effect(i.Id),i.Site,q.Kind==VillageAction.ProposeWater?EconomyAction.ImproveWater:EconomyAction.RelieveVillage,i.Amount));}
        w.Villages=Replay(w,entries,w.ElapsedSeconds,w.CommitSequence);
    }
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.Villages==null,"旧世界に村落相談を追加できません");return;}
        Need(w.Villages is {Schema:1,Policy:Policy,Entries:not null,Organizations:not null,Proposals:not null,Promises:not null},"村落原本の版が不正です");
        Need(ContractJson.CanonicalHash(w.Villages)==ContractJson.CanonicalHash(Replay(w,w.Villages.Entries,w.ElapsedSeconds,w.CommitSequence)),"村の返答・合意・履行が原本と一致しません");
    }
    internal static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;var times=w.Villages!.Promises.Select(p=>p.DeliverBy+1).Concat(w.Villages.Proposals.Select(q=>q.At+Day+1)).Append((Term(w.ElapsedSeconds)+1)*30*Day);
        return times.Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    }
    public static VillageLocalView? View(ContractWorld w)
    {
        if(!Enabled(w))return null;var site=w.Locations.Single(l=>l.Actor==w.PlayerControl).Site;
        if(site==null||!CommunityEconomy.SitesFor(w).Contains(site.Value)||!Local(w,site.Value,w.ElapsedSeconds,w.CommitSequence+1))return null;
        var s=w.Villages!;var org=Organization(site.Value);var rep=org.Members[Term(w.ElapsedSeconds)%2];int trust=Trust(w,s.Promises,site.Value,w.ElapsedSeconds);
        var cards=s.Promises.Where(p=>p.Site==site).Select(p=>new VillageCard(p.Id,p.Kind==VillageAction.ProposeWater?"水利工事":"救済",p.Amount,p.Delivered,p.DeliverBy,Status(w,p,w.ElapsedSeconds,w.CommitSequence+1))).ToArray();
        return new(site.Value,org.Id,rep.Name,Term(w.ElapsedSeconds),w.ElapsedSeconds,Authority(w,site.Value,w.ElapsedSeconds,w.CommitSequence),trust<40?"前の約束の不履行について不信が残っています。":trust>70?"実際に約束を果たしたことを評価しています。":"まずは条件と実際の履行を確かめたいと話しています。",NpcCommunityAdministration.LocalOutcome(w,site.Value),s.Proposals.Where(q=>q.Site==site).ToArray(),cards);
    }
}
public sealed partial class WorldSession
{
    public VillageLocalView? VillageView(){lock(gate)return VillageConsultation.View(state);}
    public CommandResult ConsultVillage(VillageInput input,EntityId actor,long revision)
    {
        lock(gate){if(actor!=state.PlayerControl||!VillageConsultation.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"この保存の村落相談ではありません");
            var old=state.Villages!.Entries.SingleOrDefault(e=>e.Input.Id==input.Id);
            if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"操作済みです");
            if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"別の原本で使用済みです");
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"相談を読み直してください");
            try{Change(w=>VillageConsultation.Apply(w,input));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"村との相談と履行を記録しました");}
            catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
        }
    }
}
