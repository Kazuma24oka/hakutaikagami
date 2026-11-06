using System.Security.Cryptography;
using System.Text;
namespace HakutaiKagami.Core.Contracts;
public enum CastleSection { OuterMoat, Gate, SecondBailey, Keep }
public sealed record CastleDefense(CastleSection Section,int Maximum,int Damage,int Strength);
public static class CastleWorks
{
 public const string Policy="territory-physical-castle-district-works-stonemasons-v2";
 public static bool Enabled(ContractWorld w)=>w.NewGame?.TerritoryPolicy==Policy;
 public static bool IsFort(WorksKind k)=>k is WorksKind.Moat or WorksKind.Rampart or WorksKind.StoneWall or WorksKind.Gatehouse or WorksKind.Tower or WorksKind.RepairSection;
 public static string Name(CastleSection s)=>s switch{CastleSection.OuterMoat=>"外堀",CastleSection.Gate=>"城門",CastleSection.SecondBailey=>"二の丸",_=>"本丸"};
 public static string Name(WorksKind k)=>k switch{WorksKind.Moat=>"堀",WorksKind.Rampart=>"土塁",WorksKind.StoneWall=>"石垣",WorksKind.Gatehouse=>"門",WorksKind.Tower=>"櫓",WorksKind.RepairSection=>"区画修繕",_=>throw new ArgumentException("城の工事ではありません")};
 public static bool Fits(CastleSection s,WorksKind k)=>Enum.IsDefined(s)&&k switch{WorksKind.Moat=>s==CastleSection.OuterMoat,WorksKind.Gatehouse=>s==CastleSection.Gate,WorksKind.Rampart or WorksKind.StoneWall=>true,WorksKind.Tower=>s is CastleSection.Gate or CastleSection.SecondBailey or CastleSection.Keep,WorksKind.RepairSection=>true,_=>false};
 static int Bonus(WorksKind k)=>k switch{WorksKind.Moat=>14,WorksKind.Rampart=>8,WorksKind.StoneWall=>20,WorksKind.Gatehouse=>12,WorksKind.Tower=>10,_=>0};
 public static CastleSection HitSection(RegionalDisaster e)=>(CastleSection)(SHA256.HashData(Encoding.UTF8.GetBytes(e.Id))[0]%4);
 public static CastleDefense Defense(ContractWorld w,EntityId site,CastleSection section,long at,long rev){if(!Enabled(w)||!Enum.IsDefined(section)||!MaterialMarkets.Sites(w).Contains(site))throw new InvalidDataException("版付きの城と区画が必要です");var works=PublicWorks.Orders(w).Where(q=>q.Input.Site==site&&q.Due<=at&&q.Revision<rev).ToArray();int maximum=(section switch{CastleSection.OuterMoat=>20,CastleSection.Gate=>30,CastleSection.SecondBailey=>35,_=>40})+works.Where(q=>q.Input.Section==section).Sum(q=>Bonus(q.Input.Kind));var events=RegionalWeather.Events(w,site,at).Where(e=>e.Castle>0&&HitSection(e)==section).Select(e=>(At:e.At,Order:0,Change:e.Castle));var repairs=works.Where(q=>q.Input.Kind==WorksKind.RepairCastle||q.Input.Kind==WorksKind.RepairSection&&q.Input.Section==section).Select(q=>(At:q.Due,Order:1,Change:-30));int damage=0;foreach(var e in events.Concat(repairs).OrderBy(e=>e.At).ThenBy(e=>e.Order))damage=Math.Clamp(damage+e.Change,0,100);return new(section,maximum,damage,Math.Max(0,maximum-damage));}
 internal static long StoneUsed(ContractWorld w,EntityId owner,EntityId site,string good,long at,long rev)=>good!="cut-stone"?0:5L*PublicWorks.Orders(w).Count(q=>q.Owner==owner&&q.Input.Site==site&&q.Input.Kind==WorksKind.StoneWall&&(q.At<at||q.At==at&&q.Revision<rev));
 internal static void Check(ContractWorld w,WorksInput i,long at,long rev){if(!IsFort(i.Kind)){if(i.Section!=null)throw new InvalidDataException("通常の開発に城の区画は指定しません");return;}if(!Enabled(w)||i.Section is not {} s||!Fits(s,i.Kind))throw new InvalidDataException("対象の城・区画・工法を確認してください");if(i.Kind==WorksKind.RepairSection&&Defense(w,i.Site,s,at,rev).Damage<=0)throw new InvalidDataException("この区画は損傷していません");if(i.Kind==WorksKind.StoneWall){var owner=RegionalDomains.Owner(w,i.Site)??throw new InvalidDataException("家の所有者が不明です");if(!CraftTrade.MasonsAvailable(w,owner,i.Site,at,rev)||CraftTrade.Stock(w,owner,i.Site,"cut-stone",at,rev)<5)throw new InvalidDataException("家が雇った現地の石工と、加工済み切石5個が必要です");}}
}
