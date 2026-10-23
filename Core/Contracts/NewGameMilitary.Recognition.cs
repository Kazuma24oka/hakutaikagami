using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public sealed record MilitarySeenUnit(EntityId Unit,long At,long Revision,FieldPoint Position,FieldPoint Heading,int Soldiers,int Morale,string Phase,string Kind);
public sealed record MilitaryKnownUnit(EntityId Unit,long ObservedAt,FieldPoint Position,FieldPoint Next,FieldPoint Heading,int Soldiers,int? Morale,string Phase,string Kind,bool Live,string Source);
public sealed record MilitarySeenCourier(string Token,FieldPoint Position,FieldPoint Next);
public static partial class NewGameMilitary
{
    public const long RecognitionRangeMm=80000; // Existing regional trial coefficient, not final terrain visibility.
    static FieldPoint RecognitionOrigin(ContractWorld w)=>FieldWorld.FromWorld(RegionalCampaign.ActorPointAt(w,w.PlayerControl,w.ElapsedSeconds));
    static bool InRecognitionRange(FieldPoint observer,FieldPoint p)=>RegionalGround.Distance(observer,p)<=RecognitionRangeMm;
    static MilitarySeenUnit[] DirectMilitarySamples(ContractWorld w)
    {
        if(w.NewMilitary==null)return [];
        var observer=RecognitionOrigin(w);var rows=new List<MilitarySeenUnit>();var strength=OwnCombatReport(w,w.ElapsedSeconds);
        for(int i=0;i<3;i++){
            var p=Position(w,i,w.ElapsedSeconds);if(!InRecognitionRange(observer,p))continue;
            var next=Position(w,i,w.ElapsedSeconds+1);
            rows.Add(new(UnitIds[i],w.ElapsedSeconds,w.CommitSequence,p,new(next.X-p.X,next.Y-p.Y),strength[i].Soldiers,strength[i].Morale,strength[i].Soldiers==0?"destroyed":RoutPhase(w,i),"own"));
        }
        if(Deployed(w)&&ExerciseVisible(w)){
            var opponent=ExerciseState(w,w.ElapsedSeconds).Opponents;
            for(int i=0;i<3;i++){
                var p=ExercisePosition(w,i);if(!InRecognitionRange(observer,p))continue;
                rows.Add(new(opponent[i].Unit,w.ElapsedSeconds,w.CommitSequence,p,new(-1,0),opponent[i].Soldiers,opponent[i].Morale,"standing","exercise"));
            }
        }
        return rows.OrderBy(r=>r.Unit.Value,StringComparer.Ordinal).ToArray();
    }
    // The caller MUST supply validated, saved, observer-owned samples from the explicitly enabled observation epoch.
    // No retroactive auto-memory reconstructed from an unversioned old save.
    static MilitaryKnownUnit[] ProjectKnownMilitary(ContractWorld w,IEnumerable<MilitarySeenUnit> recorded)
    {
        var current=DirectMilitarySamples(w).ToDictionary(r=>r.Unit);var list=new Dictionary<EntityId,MilitaryKnownUnit>();
        foreach(var row in recorded){
            bool live=current.TryGetValue(row.Unit,out var now)&&now with{Revision=row.Revision}==row;
            var next=live?new FieldPoint(row.Position.X+row.Heading.X,row.Position.Y+row.Heading.Y):row.Position;
            list[row.Unit]=new(row.Unit,row.At,row.Position,next,row.Heading,row.Soldiers,row.Morale,row.Phase,row.Kind,live,"本人の目撃");
        }
        foreach(var receipt in w.NewMilitary?.UnitInquiry?.Receipts??[]){
            if(!receipt.Delivered||!MessageFlow.Knows(w.Messaging,receipt.ReplyMessage,w.PlayerControl.Value))continue;
            var message=w.Messaging.Messages.Single(m=>m.Id==receipt.ReplyMessage);
            var report=ContractJson.Read<UnitPostInquiryAnswer>(System.Text.Encoding.UTF8.GetBytes(message.Body));
            var id=new EntityId(report.Unit);
            if(id!=UnitIds[0])continue;
            if(list.TryGetValue(id,out var known)&&known.ObservedAt>=report.At)continue;
            list[id]=new(id,report.At,report.Position,report.Position,new(0,0),report.Soldiers,null,report.RoutPhase,"own",false,"受領した本隊の返書");
        }
        return list.Values.OrderBy(r=>r.Unit.Value,StringComparer.Ordinal).ToArray();
    }
    public static bool RecognitionEnabled(ContractWorld w)=>w.NewMilitary?.Observation!=null;
    public static MilitaryKnownUnit[] KnownMilitary(ContractWorld w)=>w.NewMilitary?.Observation is {} s?ProjectKnownMilitary(w,s.Frames.SelectMany(f=>f.Units)):[];
    public static bool AllOwnCurrentlySeen(ContractWorld w)=>UnitIds.All(id=>KnownMilitary(w).Any(r=>r.Unit==id&&r.Live));
    public static MilitarySeenCourier[] ProjectNearbyMilitaryCouriers(ContractWorld w)
    {
        var observer=RecognitionOrigin(w);
        return CourierFrames(w,w.ElapsedSeconds).Where(f=>InRecognitionRange(observer,f.Position))
            .Select(f=>new MilitarySeenCourier("courier:observed/"+Hash(f.Carrier.Value),f.Position,f.Next)).ToArray();
    }
}
