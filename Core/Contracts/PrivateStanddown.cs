namespace HakutaiKagami.Core.Contracts;
public enum StanddownAction { Ask, Accept }
public sealed record StanddownInput(EntityId Id,EntityId Person,StanddownAction Action,EntityId? Quote=null,int Hours=0);
public sealed record StanddownQuote(EntityId Id,EntityId Person,EntityId Site,int Hours,long Fee,EntityId Payment,long At,long Revision,FactionStanceAuthority Authority);
public sealed record StanddownOrder(StanddownInput Input,long At,long Revision);
public sealed record StanddownState(int Schema,string Contract,StanddownQuote[] Quotes,StanddownOrder[] Orders);
public sealed record StanddownCard(EntityId Quote,EntityId Person,int Hours,long Fee,string Stage,long? Until,bool CanAccept);
// A witnessed private undertaking by an idle head about his next personal muster only.
public static class PrivateStanddown
{
    public const string Policy="private-head-next-muster-delay-fee100-known-paid-contact-v1";public const long Fee=100,OfferSeconds=30;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>NewGameWorld.Enabled(w)&&w.NewGame?.StanddownPolicy==Policy;
    static StanddownState State(ContractWorld w)=>w.Standdown??new(1,Policy,[],[]);
    static bool Before(long t,long r,long at,long rev)=>t<at||t==at&&r<rev;
    public static bool OwnIdUsed(ContractWorld w,EntityId id)=>State(w).Orders.Any(o=>o.Input.Id==id);
    static StanddownOrder? Acceptance(ContractWorld w,EntityId quote,long at,long rev)=>State(w).Orders.SingleOrDefault(o=>o.Input.Action==StanddownAction.Accept&&o.Input.Quote==quote&&Before(o.At,o.Revision,at,rev));
    public static bool ShouldWait(ContractWorld w,EntityId person,long at,long revision)
    {if(!Enabled(w))return false;return State(w).Quotes.Where(q=>q.Person==person).Any(q=>Acceptance(w,q.Id,at,revision+1) is {} o&&at<checked(o.At+q.Hours*3600L));}
    static FactionStanceAuthority Authority(ContractWorld w,EntityId person,long at,long revision)
    {var f=HouseSuccession.Factions(w,at,revision).SingleOrDefault(f=>f.Head==person.Value);Need(person!=w.PlayerControl&&f!=null,"この条件は当主本人の次の軍引受だけを扱います");var a=FactionStances.ResolveAuthority(w,person,new(f.Id),at,revision);Need(a!=null,"本人に現地の当主権限がありません");return a;}
    static void Available(ContractWorld w,EntityId person,EntityId site,long at,long revision)
    {Need(PrivateInformation.Local(w,person,site,at,revision),"現地で本人に会って相談してください");Need(!NpcArmies.Musters(w).Any(m=>m.Commander==person&&Before(m.At,m.Revision,at,revision))&&!NewGameMilitary.LedgerStates(w).Any(s=>s.Terms.Principal==person&&s.Requests.Any(r=>Before(r.At,r.Revision,at,revision))),"既に引き受けた軍や他人が預かる兵は、この約束で止めません");Need(!ShouldWait(w,person,at,revision-1),"本人との見合わせ協力は既に有効です");}
    static StanddownQuote Prepare(ContractWorld w,StanddownOrder o,EntityId site)
    {var i=o.Input;Need(i.Action==StanddownAction.Ask&&i.Quote==null&&i.Hours is >=1 and <=24,"見合わせは1〜24時間で提示してください");var a=Authority(w,i.Person,o.At,o.Revision);Need(site==a.Home,"本人の本拠で相談してください");Available(w,i.Person,site,o.At,o.Revision);var payment=w.PrivateInformation?.Orders.LastOrDefault(p=>p.Input.Action==InformationAction.Pay&&p.Input.Person==i.Person&&Before(p.At,p.Revision,o.At,o.Revision));Need(payment!=null,"まず本人との情報交換を精算し、面識を作ってください");return new(i.Id,i.Person,site,i.Hours,Fee,payment.Input.Id,o.At,o.Revision,a);}
    static void CheckAccept(ContractWorld w,StanddownOrder o,StanddownQuote q)
    {Need(o.Input.Action==StanddownAction.Accept&&o.Input.Person==q.Person&&o.Input.Quote==q.Id&&o.Input.Hours==0&&Before(q.At,q.Revision,o.At,o.Revision)&&o.At<=q.At+OfferSeconds,"本人が提示した同じ条件を期限内に確認してください");Need(Acceptance(w,q.Id,o.At,o.Revision)==null,"既に合意した条件です");Authority(w,q.Person,o.At,o.Revision);Available(w,q.Person,q.Site,o.At,o.Revision);Need(TransportWork.WalletBefore(w,w.PlayerControl,o.At,o.Revision)>=q.Fee,"私財が足りません。家の蔵や軍用備蓄は使いません");}
    internal static void Apply(ContractWorld w,StanddownInput i)
    {Need(Enabled(w)&&EntityId.IsValid(i.Id.Value)&&Enum.IsDefined(i.Action),"この保存で扱える協力条件ではありません");var o=new StanddownOrder(i,w.ElapsedSeconds,w.CommitSequence);if(i.Action==StanddownAction.Ask){var q=Prepare(w,o,w.Locations.Single(l=>l.Actor==w.PlayerControl).Site??throw new InvalidDataException("到着してから相談してください"));w.Standdown=State(w) with{Quotes=[..State(w).Quotes,q],Orders=[..State(w).Orders,o]};}else{var q=State(w).Quotes.SingleOrDefault(q=>q.Id==i.Quote);Need(q!=null,"聞いた条件がありません");CheckAccept(w,o,q);w.Standdown=State(w) with{Orders=[..State(w).Orders,o]};}}
    public static long Net(ContractWorld w,EntityId lot,long at=long.MaxValue,long revision=long.MaxValue)
    {long n=0;foreach(var o in State(w).Orders.Where(o=>o.Input.Action==StanddownAction.Accept&&Before(o.At,o.Revision,at,revision))){var q=State(w).Quotes.Single(q=>q.Id==o.Input.Quote);if(lot==NewGameWork.Wallet(w.PlayerControl))n=checked(n-q.Fee);if(lot==NewGameWork.Wallet(q.Person))n=checked(n+q.Fee);}return n;}
    internal static long? NextBoundary(ContractWorld w,long target)=>!Enabled(w)?null:State(w).Quotes.Select(q=>Acceptance(w,q.Id,w.ElapsedSeconds,w.CommitSequence+1) is {} o?(long?)(o.At+q.Hours*3600L):null).Where(t=>t>w.ElapsedSeconds&&t<=target).Order().FirstOrDefault();
    public static StanddownCard[] View(ContractWorld w)=>!Enabled(w)?[]:State(w).Quotes.Select(q=>{var a=Acceptance(w,q.Id,w.ElapsedSeconds,w.CommitSequence+1);long? until=a==null?null:a.At+q.Hours*3600L;bool local=PrivateInformation.Local(w,q.Person,q.Site,w.ElapsedSeconds,w.CommitSequence+1);return new StanddownCard(q.Id,q.Person,q.Hours,q.Fee,a==null?w.ElapsedSeconds<=q.At+OfferSeconds?"現地で条件を確認":"条件の確認期限を経過":w.ElapsedSeconds<until?"本人の新規軍引受を見合わせ中":"約束の期間終了",until,a==null&&local&&w.ElapsedSeconds<=q.At+OfferSeconds);}).ToArray();
    internal static void Rewind(ContractWorld w,long at,long rev){if(w.Standdown is {} s)w.Standdown=s with{Quotes=s.Quotes.Where(q=>Before(q.At,q.Revision,at,rev+1)).ToArray(),Orders=s.Orders.Where(o=>Before(o.At,o.Revision,at,rev+1)).ToArray()};}
    internal static void Validate(ContractWorld w)
    {if(!Enabled(w)){Need(w.Standdown==null,"旧世界へ秘密の約束を追加できません");return;}var s=State(w);Need(w.Standdown!=null&&s.Schema==1&&s.Contract==Policy&&s.Quotes!=null&&s.Orders!=null,"秘密の約束の原本がありません");Need(s.Orders.Select(o=>o.Input.Id).Distinct().Count()==s.Orders.Length&&s.Orders.Zip(s.Orders.Skip(1)).All(p=>p.First.Revision<p.Second.Revision&&p.First.At<=p.Second.At),"約束の原本・順序が不正です");foreach(var o in s.Orders){Need(o.At>=0&&o.At<=w.ElapsedSeconds&&o.Revision>0&&o.Revision<=w.CommitSequence&&!FactionStances.OtherIdUsed(w,o.Input.Id,includeStanddown:false)&&!FactionStances.OwnIdUsed(w,o.Input.Id),"約束の時点・IDが不正です");var q=s.Quotes.Single(q=>q.Id==(o.Input.Action==StanddownAction.Ask?o.Input.Id:o.Input.Quote));if(o.Input.Action==StanddownAction.Ask)Need(q==Prepare(w,o,q.Site),"本人の条件・面識・権限が原本と違います");else CheckAccept(w,o,q);}Need(s.Quotes.Select(q=>q.Id).Distinct().Count()==s.Quotes.Length&&s.Quotes.All(q=>s.Orders.Any(o=>o.Input.Id==q.Id&&o.Input.Action==StanddownAction.Ask)),"条件の原本が重複・欠落しています");}
}
