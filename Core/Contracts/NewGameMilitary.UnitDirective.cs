using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public enum UnitDirectiveKind { Move, Hold, Recall }
public enum UnitDirectiveOutcome { Applied, Superseded, AuthorityEnded, RoutActive, PathBlocked }
public sealed record UnitDirectiveInput(string Contract,KnownMilitaryTarget Known,UnitDirectiveKind Kind,FieldPoint? Goal);
public sealed record UnitDirectiveNote(EntityId Request,UnitDirectiveInput Directive,string? ReplyPolicy,string? TransportPolicy);
public sealed record UnitDirectiveAnswer(string Original,EntityId Unit,UnitDirectiveKind Kind,UnitDirectiveOutcome Outcome,long At,FieldPoint Position);
public sealed record UnitDirectiveEffect(EntityId Id,long At,long Revision,UnitDirectiveOutcome Outcome,EntityId? Order);
public sealed record UnitDirectiveState(string Contract,TroopDispatch[] Dispatches,TroopReceipt[] Receipts,TroopReply[] Replies,UnitDirectiveEffect[] Effects);
public static partial class NewGameMilitary
{
    public const string UnitDirectiveContract="known-own-recipient-fixed-point-local-scope-directive-v1";
    public const string UnitDirectiveRole="standing-unit-reception/scoped-directive-v1";
    static bool DirectiveAction(NewMilitaryAction a)=>a==NewMilitaryAction.SendUnitDirective;
    public static bool UnitDirectiveAllowed(ContractWorld w)=>RecognitionEnabled(w)&&Detached(w)&&Deployed(w)&&CustodyAt(w,w.ElapsedSeconds,w.CommitSequence);
    static void ValidateUnitDirectiveInput(ContractWorld w,NewMilitaryInput i)
    {
        Need(DirectiveAction(i.Action)==(i.Directive!=null),"分隊指示の操作と本文が一致しません");if(i.Directive is not {} x)return;
        Need(x.Contract==UnitDirectiveContract&&x.Known!=null&&Enum.IsDefined(x.Kind)&&UnitIds.Contains(x.Known.Unit)&&SelectKnownTarget(w,x.Known.Unit)==x.Known,"分隊指示の既知宛先・版が違います");
        Need(x.Kind==UnitDirectiveKind.Move?x.Goal!=null&&InExerciseRange(w,x.Goal):x.Goal==null,"移動以外に行先は指定しません");
        Need(i.Amount==0&&i.A==null&&i.B==null&&i.InquiryTarget==null&&i.KnownTarget==null&&i.OriginalPolicy==null&&i.TargetOriginal==null&&i.RequestedEffectiveAt==null,"分隊指示に別操作の値が混入しています");
    }
    static void ApplyUnitDirective(ContractWorld w,NewMilitaryInput i)
    {
        if(!DirectiveAction(i.Action))return;var x=i.Directive!;var from=PersonPosition(w,w.ElapsedSeconds);var to=x.Known.Position;
        var route=RegionalGround.Plan(from,to,w.ElapsedSeconds,courier:true,atlas:true);long due=Math.Max(w.ElapsedSeconds+1,route.LastOrDefault()?.EndAt??0);string message=UnitMessage(i.Id,"directive");
        MessageFlow.Send(w.Messaging,new(message,i.Id.Value,"unit-post-directive",w.PlayerControl.Value,x.Known.Unit.Value,Text(new UnitDirectiveNote(i.Id,x,i.PostPolicy,i.TransportPolicy)),w.NewMilitary!.Terms.Site.Value,w.ElapsedSeconds,i.DeliverBy),w.ElapsedSeconds,checked((int)(due-w.ElapsedSeconds)));
        var state=w.NewMilitary.UnitDirective??new(UnitDirectiveContract,[],[],[],[]);
        w.NewMilitary=w.NewMilitary with{UnitDirective=state with{Dispatches=[..state.Dispatches,new(i.Id,w.ElapsedSeconds,w.CommitSequence,x.Known.Unit,UnitDirectiveRole,new("evidence:unit-directive/"+Hash(Text(x.Known))),from,to,route,due,message,MessageFlow.Latest(w.Messaging,message).Id)]}};
    }
    static UnitDirectiveState? DirectiveBefore(UnitDirectiveState? s,long at,long rev)
    {
        if(s==null)return null;var ds=s.Dispatches.Where(d=>Before(d.At,d.Revision,at,rev)).ToArray();return ds.Length==0?null:s with{Dispatches=ds,Receipts=s.Receipts.Where(d=>Before(d.At,d.Revision,at,rev)).ToArray(),Replies=s.Replies.Where(d=>Before(d.At,d.Revision,at,rev)).ToArray(),Effects=s.Effects.Where(d=>Before(d.At,d.Revision,at,rev)).ToArray()};
    }
    static bool Affects(NewMilitaryOrder o,int unit)=>o.AffectedUnits==null||o.AffectedUnits.Contains(unit);
    static NewMilitaryOrder? EffectiveOrder(ContractWorld w,int unit,long at)=>w.NewMilitary!.Orders.LastOrDefault(o=>o.At<=at&&Affects(o,unit));
    static bool TryScopedOrder(ContractWorld w,EntityId id,int index,FieldPoint goal,UnitDirectiveKind kind)
    {
        var origins=Enumerable.Range(0,3).Select(n=>Position(w,n,w.ElapsedSeconds)).ToArray();
        var routes=Enumerable.Range(0,3).Select(n=>EffectiveOrder(w,n,w.ElapsedSeconds)?.Routes[n].ToArray()??[]).ToArray();
        try{
            var obstacles=origins.Where((p,k)=>k!=index&&RegionalGround.Distance(p,origins[index])>=2250).Distinct().ToArray();
            FieldLeg[] path=kind==UnitDirectiveKind.Hold?[]:RegionalGround.Plan(origins[index],goal,w.ElapsedSeconds,obstacles,atlas:true);bool found=false;
            for(int wait=0;wait<=600;wait++){
                var candidate=path.Select(l=>l with{StartAt=l.StartAt+wait,EndAt=l.EndAt+wait}).ToArray();long until=candidate.LastOrDefault()?.EndAt??w.ElapsedSeconds;
                if(Enumerable.Range(0,3).Where(k=>k!=index).Any(k=>RegionalGround.Conflict(candidate,origins[index],routes[k],origins[k],w.ElapsedSeconds,Math.Max(until,routes[k].LastOrDefault()?.EndAt??0)+1)))continue;
                routes[index]=candidate;found=true;break;
            }
            if(!found)return false;
        }catch(InvalidDataException){return false;}
        w.NewMilitary=w.NewMilitary! with{Orders=[..w.NewMilitary.Orders,new(id,NewMilitaryAction.SendUnitDirective,w.ElapsedSeconds,w.CommitSequence,origins,routes){AffectedUnits=[index]}]};return true;
    }
    static long OrderIssuedRevision(ContractWorld w,NewMilitaryOrder order)
    {
        if(w.NewMilitary!.UnitDirective is not {} state)return order.Revision;
        var effect=state.Effects.SingleOrDefault(e=>e.Order==order.Id);
        if(effect!=null)return state.Dispatches.Single(d=>d.Id==effect.Id).Revision;
        var recall=w.NewMilitary.Withdrawal?.Dispatches.SingleOrDefault(d=>UnitOrder(d.Id)==order.Id);
        return recall?.Revision??order.Revision;
    }
    static bool ApplyDirectedArrival(ContractWorld w,TroopDispatch d)
    {
        int index=ReceivingUnitIndex(d);Need(index>=0,"分隊指示の対象がありません");var note=ContractJson.Read<UnitDirectiveNote>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==d.Message).Body));
        UnitDirectiveOutcome outcome;EntityId? order=null;
        if(!SameMilitaryCase(w,d)||!CustodyAt(w,w.ElapsedSeconds,w.CommitSequence))outcome=UnitDirectiveOutcome.AuthorityEnded;
        else if(w.NewMilitary!.UnitDirective!.Effects.Any(e=>e.Id==d.Id))return false;
        else if(w.NewMilitary.Orders.Any(o=>Affects(o,index)&&OrderIssuedRevision(w,o)>d.Revision))outcome=UnitDirectiveOutcome.Superseded;
        else if(RoutActive(w)&&RoutPhase(w,index) is not ("standing" or "regrouped"))outcome=UnitDirectiveOutcome.RoutActive;
        else{
            var goal=note.Directive.Kind switch{UnitDirectiveKind.Move=>note.Directive.Goal!,UnitDirectiveKind.Recall=>Home(w,index),_=>Position(w,index,w.ElapsedSeconds)};
            order=UnitOrder(d.Id);if(TryScopedOrder(w,order.Value,index,goal,note.Directive.Kind))outcome=UnitDirectiveOutcome.Applied;else{outcome=UnitDirectiveOutcome.PathBlocked;order=null;}
        }
        var s=w.NewMilitary!.UnitDirective!;w.NewMilitary=w.NewMilitary with{UnitDirective=s with{Effects=[..s.Effects,new(d.Id,w.ElapsedSeconds,w.CommitSequence,outcome,order)]}};return outcome==UnitDirectiveOutcome.Applied;
    }
    static string DirectiveReplyBody(ContractWorld w,TroopDispatch d)
    {
        var note=ContractJson.Read<UnitDirectiveNote>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==d.Message).Body));var effect=w.NewMilitary!.UnitDirective!.Effects.Single(e=>e.Id==d.Id);
        return Text(new UnitDirectiveAnswer(d.Message,d.Recipient,note.Directive.Kind,effect.Outcome,w.ElapsedSeconds,Position(w,ReceivingUnitIndex(d),w.ElapsedSeconds)));
    }
    static bool ApplyStandingRecall(ContractWorld w,TroopDispatch original)
    {
        if(w.NewMilitary!.UnitDirective==null){Order(w,new(UnitOrder(original.Id),NewMilitaryAction.Recall));return true;}
        var eligible=Enumerable.Range(0,3).Where(i=>!w.NewMilitary.Orders.Any(o=>Affects(o,i)&&OrderIssuedRevision(w,o)>original.Revision)).ToArray();if(eligible.Length==0)return false;
        var origins=Enumerable.Range(0,3).Select(i=>Position(w,i,w.ElapsedSeconds)).ToArray();var routes=Enumerable.Range(0,3).Select(i=>EffectiveOrder(w,i,w.ElapsedSeconds)?.Routes[i].ToArray()??[]).ToArray();
        var planned=new HashSet<int>();
        try{foreach(int i in eligible){
            var obstacles=origins.Where((p,k)=>k!=i&&RegionalGround.Distance(p,origins[i])>=2250).Concat(planned.Select(k=>Home(w,k))).Distinct().ToArray();
            var path=RegionalGround.Plan(origins[i],Home(w,i),w.ElapsedSeconds,obstacles,atlas:true);bool found=false;
            for(int wait=0;wait<=600;wait++){
                var candidate=path.Select(l=>l with{StartAt=l.StartAt+wait,EndAt=l.EndAt+wait}).ToArray();long until=candidate.LastOrDefault()?.EndAt??w.ElapsedSeconds;
                if(Enumerable.Range(0,3).Where(k=>k!=i&&(!eligible.Contains(k)||planned.Contains(k))).Any(k=>RegionalGround.Conflict(candidate,origins[i],routes[k],origins[k],w.ElapsedSeconds,Math.Max(until,routes[k].LastOrDefault()?.EndAt??0)+1)))continue;
                routes[i]=candidate;found=true;break;
            }
            if(!found)return false;planned.Add(i);
        }}catch(InvalidDataException){return false;}
        w.NewMilitary=w.NewMilitary with{Orders=[..w.NewMilitary.Orders,new(UnitOrder(original.Id),NewMilitaryAction.Recall,w.ElapsedSeconds,w.CommitSequence,origins,routes){AffectedUnits=eligible}]};return true;
    }
    static (UnitCancelDisposition,EntityId?) StopScopedRecall(ContractWorld w,NewMilitaryOrder original,string originalMessage)
    {
        var current=Enumerable.Range(0,3).Where(i=>EffectiveOrder(w,i,w.ElapsedSeconds)?.Id==original.Id).ToArray();
        if(current.Length==0)return(UnitCancelDisposition.Superseded,null);
        var moving=current.Where(i=>(original.Routes[i].LastOrDefault()?.EndAt??0)>w.ElapsedSeconds).ToArray();if(moving.Length==0)return(UnitCancelDisposition.TooLate,null);
        var id=new EntityId("order:unit-cancel/"+Hash(originalMessage+"/"+w.ElapsedSeconds));
        if(!w.NewMilitary!.Orders.Any(o=>o.Id==id)){
            var origins=Enumerable.Range(0,3).Select(i=>Position(w,i,w.ElapsedSeconds)).ToArray();var routes=Enumerable.Range(0,3).Select(i=>moving.Contains(i)?[]:EffectiveOrder(w,i,w.ElapsedSeconds)?.Routes[i].ToArray()??[]).ToArray();
            w.NewMilitary=w.NewMilitary with{Orders=[..w.NewMilitary.Orders,new(id,NewMilitaryAction.Hold,w.ElapsedSeconds,w.CommitSequence,origins,routes){AffectedUnits=moving}]};
        }
        return(UnitCancelDisposition.Stopped,id);
    }
    static void ValidateUnitDirectiveShape(ContractWorld w)
    {
        var s=w.NewMilitary!;var x=s.UnitDirective;Need((x!=null)==s.Requests.Any(q=>DirectiveAction(q.Input.Action)),"分隊指示と原判断の台帳が一致しません");
        foreach(var o in s.Orders)Need(o.AffectedUnits==null||x!=null&&o.AffectedUnits.Length>0&&o.AffectedUnits.Distinct().Count()==o.AffectedUnits.Length&&o.AffectedUnits.All(i=>i is >=0 and <3),"命令の対象隊が不正です");
        if(x==null)return;
        Need(x.Contract==UnitDirectiveContract&&x.Dispatches!=null&&x.Receipts!=null&&x.Replies!=null&&x.Effects!=null,"分隊指示の台帳が不正です");
        Need(x.Dispatches.Length==s.Requests.Count(q=>DirectiveAction(q.Input.Action))&&x.Dispatches.Select(d=>d.Id).Distinct().Count()==x.Dispatches.Length,"分隊指示の原本が欠落・重複しています");
        Need(x.Receipts.Length==x.Dispatches.Count(d=>d.DueAt<=w.ElapsedSeconds)&&x.Replies.Length==x.Receipts.Count(r=>r.ReplyDueAt<=w.ElapsedSeconds)&&x.Effects.Length==x.Receipts.Count(r=>r.Delivered),"分隊指示の受領・効力・返書が欠落しています");
        Need(x.Receipts.Select(r=>r.Id).Distinct().Count()==x.Receipts.Length&&x.Replies.Select(r=>r.Id).Distinct().Count()==x.Replies.Length&&x.Effects.Select(e=>e.Id).Distinct().Count()==x.Effects.Length,"分隊指示の受領・効力が重複しています");
        foreach(var e in x.Effects){var r=x.Receipts.Single(t=>t.Id==e.Id);Need(Enum.IsDefined(e.Outcome)&&r.Delivered&&r.At==e.At&&r.Revision==e.Revision&&r.Applied==(e.Outcome==UnitDirectiveOutcome.Applied)&&(e.Order!=null)==r.Applied,"分隊指示の効力と受令が違います");}
    }
    public static string UnitDirectiveStatus(ContractWorld w)
    {
        var x=w.NewMilitary?.UnitDirective;var d=x?.Dispatches.LastOrDefault(d=>SameMilitaryCase(w,d));if(d==null)return "";var r=x!.Receipts.SingleOrDefault(r=>r.Id==d.Id);string name=UnitRecipientName(d.Recipient);
        if(r==null||!MessageFlow.Knows(w.Messaging,r.ReplyMessage,w.PlayerControl.Value))return name+"への指示：返書未受領";
        if(!r.Delivered)return name+"への指示：伝令が指定地点で正式受領できず";
        var a=ContractJson.Read<UnitDirectiveAnswer>(System.Text.Encoding.UTF8.GetBytes(w.Messaging.Messages.Single(m=>m.Id==r.ReplyMessage).Body));string text=a.Outcome switch{UnitDirectiveOutcome.Applied=>"受令・指示を実行",UnitDirectiveOutcome.Superseded=>"新しい命令を維持",UnitDirectiveOutcome.AuthorityEnded=>"指揮権終了で実行せず",UnitDirectiveOutcome.RoutActive=>"敗走中のため実行せず",_=>"経路を確保できず指示を実行せず"};return name+"の返書（開始"+a.At+"秒）："+text;
    }
}
