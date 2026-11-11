using System.Security.Cryptography;
using System.Text;
namespace HakutaiKagami.Core.Contracts;
public sealed record RegionalDisaster(string Id,EntityId Site,long At,string Kind,int Fields,int Town,int Castle);
public sealed record DisasterObservation(int Fields,int Town,int Castle);
public static class RegionalWeather
{
 public const string Policy="regional-seasonal-damage-repair-v1";
 public static bool Enabled(ContractWorld w)=>w.NewGame?.WeatherPolicy==Policy;
 static byte[] Hash(ContractWorld w,EntityId site,int year,string key)=>SHA256.HashData(Encoding.UTF8.GetBytes(w.Seed+"/"+Policy+"/"+site.Value+"/"+year+"/"+key));
 static long Time(ContractWorld w,int year,int month,int day)=>(new DateOnly(year,month,day).DayNumber-DateOnly.Parse(w.EpochDate).DayNumber)*86400L;
 public static bool North(EntityId site)=>WorldNetwork.Site(site).LatitudeE7>=370000000;
 public static bool West(EntityId site)=>WorldNetwork.Site(site).LongitudeE7<=1360000000;
 public static RegionalDisaster[] Events(ContractWorld w,EntityId site,long until){if(!Enabled(w))return [];var result=new List<RegionalDisaster>();int start=DateOnly.Parse(w.EpochDate).Year,end=DateOnly.Parse(w.EpochDate).AddDays((int)(until/86400)).Year;for(int year=start;year<=end;year++){var h=Hash(w,site,year,"events");void Add(int month,int day,string kind,int fields,int town,int castle){long at=Time(w,year,month,day);if(at>=0&&at<=until)result.Add(new(site.Value+"/"+year+"/"+kind,site,at,kind,fields,town,castle));}if(North(site)&&h[0]%3==0)Add(1,15,"大雪",10,10,0);if(h[1]%4==0)Add(6,15,"大雨",15,10,0);if(West(site)&&h[2]%5==0)Add(9,1,"台風",20,15,10);if(h[3]%20==0)Add(1+h[4]%12,10,"地震",10,25,25);}return result.OrderBy(e=>e.At).ThenBy(e=>e.Id,StringComparer.Ordinal).ToArray();}
 public static int Damage(ContractWorld w,EntityId site,WorksKind repair,long at,long rev){if(!Enabled(w))return 0;var events=Events(w,site,at).Select(e=>(At:e.At,Order:0,Amount:repair==WorksKind.RepairFields?e.Fields:repair==WorksKind.RepairTown?e.Town:e.Castle));var repairs=PublicWorks.Orders(w).Where(q=>q.Input.Site==site&&q.Input.Kind==repair&&q.Due<=at&&q.Revision<rev).Select(q=>(At:q.Due,Order:1,Amount:-30));int damage=0;foreach(var item in events.Concat(repairs).Concat(repair==WorksKind.RepairTown?DestructiveSchemes.Damage(w,site,SchemeTopic.Arson,at,rev):[]).OrderBy(e=>e.At).ThenBy(e=>e.Order))damage=Math.Clamp(damage+item.Amount,0,90);return damage;}
 public static int HarvestFactor(ContractWorld w,EntityId site,int year,long at,long rev){if(!Enabled(w))return 100;var h=Hash(w,site,year,"harvest");int crop=h[0]%10==0?120:h[0]%10==1?80:100;return crop*(100-Damage(w,site,WorksKind.RepairFields,at,rev))/100;}
 public static RegionalDisaster[] LocalEvents(ContractWorld w,EntityId actor)=>SocialWorld.Site(w,actor) is {} site?Events(w,site,w.ElapsedSeconds):[];
 public static DisasterObservation? Observe(ContractWorld w,EntityId site,long at,long rev)=>!Enabled(w)?null:new(Damage(w,site,WorksKind.RepairFields,at,rev),Damage(w,site,WorksKind.RepairTown,at,rev),Damage(w,site,WorksKind.RepairCastle,at,rev));
 internal static long? NextBoundary(ContractWorld w,long target)=>!Enabled(w)?null:MaterialMarkets.Sites(w).SelectMany(site=>Events(w,site,target)).Where(e=>e.At>w.ElapsedSeconds).Select(e=>(long?)e.At).Min();
 public static string LocalSummary(ContractWorld w,EntityId actor){if(!Enabled(w)||SocialWorld.Site(w,actor) is not {} site)return "現地の災害情報は未確認";return "現地の被害：田畑 "+Damage(w,site,WorksKind.RepairFields,w.ElapsedSeconds,w.CommitSequence+1)+"／町 "+Damage(w,site,WorksKind.RepairTown,w.ElapsedSeconds,w.CommitSequence+1)+"／城 "+Damage(w,site,WorksKind.RepairCastle,w.ElapsedSeconds,w.CommitSequence+1)+"（試作値）";}
}
