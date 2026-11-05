using System.Security.Cryptography;
using System.Text;
namespace HakutaiKagami.Core.Contracts;

public enum TroopKind { Spear, Bow, Gun, Cavalry, Transport }
public enum BattleWeather { Clear, Rain, Fog, Snow }
public enum FormationKind { Line, Wedge, Wings, Circle }
public sealed record BattlefieldLoadout(EntityId Unit,TroopKind Kind,int Equipped);
public sealed record BattlefieldExpense(EntityId Unit,string Good,long Amount);
public sealed record BattlefieldCasualties(EntityId Unit,int Dead,int Wounded,int Deserted,int Recovered){public EntityId? Home{get;init;}public long? ReturnAt{get;init;}public JourneyLeg[] ReturnRoute{get;init;}=[];}
public static partial class BattlefieldRules
{
 public const string Policy="physical-gear-daily-weather-formation-wounded-rations-v1";
 public static bool Enabled(ContractWorld w)=>w.NewGame?.BattlefieldPolicy==Policy;
 public static BattleWeather Weather(ContractWorld w,EntityId site,long at){
  var bytes=SHA256.HashData(Encoding.UTF8.GetBytes(w.Seed+"/weather/"+site.Value+"/"+at/86400));
  var date=DateOnly.Parse(w.EpochDate).AddDays(checked((int)(at/86400)));
  return (bytes[0]%10) switch{0 or 1=>date.Month is 12 or 1 or 2?BattleWeather.Snow:BattleWeather.Rain,2=>BattleWeather.Fog,_=>BattleWeather.Clear};
 }
 public static long SightRange(ContractWorld w,FieldPoint position,long at){if(!Enabled(w))return 80000;var site=RegionalCampaign.Site(ContinuousWorld.Nearest(position));return Weather(w,site,at)==BattleWeather.Fog?20000:80000;}
 public static FieldPoint[] Formation(FieldPoint a,FieldPoint b,FormationKind kind){
  if(!Enum.IsDefined(kind)||RegionalGround.Distance(a,b)<10000)throw new InvalidDataException("陣形の軸は10m以上必要です");
  long dx=b.X-a.X,dy=b.Y-a.Y,length=RegionalGround.Distance(a,b);var center=new FieldPoint(a.X+dx/2,a.Y+dy/2);
  FieldPoint Offset(long across,long depth)=>new(center.X+dx*across/length-dy*depth/length,center.Y+dy*across/length+dx*depth/length);
  return kind switch{FormationKind.Wedge=>[Offset(-length/4,-5000),Offset(0,5000),Offset(length/4,-5000)],FormationKind.Wings=>[Offset(-length/2,5000),Offset(0,-5000),Offset(length/2,5000)],FormationKind.Circle=>[Offset(-5000,-3000),Offset(5000,-3000),Offset(0,6000)],_=>[a,center,b]};
 }
 public static int MovementPercent(TroopKind kind,BattleWeather weather,string ground){int n=100;if(weather is BattleWeather.Rain or BattleWeather.Snow)n=n*70/100;if(ground is "river" or "ford")n=n*50/100;if(kind==TroopKind.Cavalry&&ground=="forest")n=n*40/100;return Math.Max(10,n);}
 public static BattlefieldCasualties SplitLosses(EntityId unit,int loss,bool routed,int recovered=0){if(loss<0||recovered<0)throw new InvalidDataException("損害が不正です");int wounded=loss/2,deserted=routed?(loss-wounded)*2/3:(loss-wounded)/3,dead=loss-wounded-deserted;if(recovered>wounded)throw new InvalidDataException("負傷兵以上を回復できません");return new(unit,dead,wounded-recovered,deserted,recovered);}
 public static FieldLeg[] AdjustRoute(ContractWorld w,EntityId unit,FieldLeg[] path){if(!Enabled(w)||path.Length==0)return path;var kind=Loadout(w,unit,w.ElapsedSeconds,w.CommitSequence+1).Kind;var weather=Weather(w,RegionalCampaign.Site(ContinuousWorld.Nearest(path[0].From)),w.ElapsedSeconds);long at=path[0].StartAt;return path.Select(l=>{int factor=MovementPercent(kind,weather,l.Terrain);long speed=Math.Max(1,l.Speed*factor/100),length=RegionalGround.Distance(l.From,l.To),rise=Math.Abs(ContinuousWorld.Height(l.From)-ContinuousWorld.Height(l.To));long duration=Math.Max(1,((long)Math.Ceiling(Math.Sqrt((double)length*length+(double)rise*rise))+rise+speed-1)/speed);var result=l with{StartAt=at,EndAt=checked(at+duration),Speed=checked((int)speed)};at=result.EndAt;return result;}).ToArray();}
}
