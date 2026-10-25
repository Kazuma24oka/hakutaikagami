namespace HakutaiKagami.Core.Contracts;
public sealed record WorkTrustFact(EntityId Observer,EntityId Subject,string Basis,string Kind,long At);
public sealed record WorkContactProof(string Basis,string Description,long EventAt,long LearnedAt);
public sealed record WorkContact(EntityId Other,string Summary,WorkContactProof[] Proofs,bool Local,bool CanDiscussService,bool CanDiscussMilitary);
// Directed knowledge-based work trust. No reciprocal score, global reputation or authority.
public static class WorkRelations
{
    public static WorkTrustFact[] Facts(ContractWorld w,EntityId observer,EntityId subject)
    {
        if(!WorkFollowup.Enabled(w)||observer==subject)return [];
        var facts=new List<WorkTrustFact>();
        foreach(var g in w.Messaging.Messages.Where(m=>m.Kind==AssessmentNotice.Kind&&m.SenderId==observer.Value&&m.RecipientId==subject.Value).Select(m=>(Message:m,Note:AssessmentNotice.Read(m.Body))).GroupBy(x=>x.Note.Task)){
            var last=g.MaxBy(x=>x.Note.Version);
            if(WorkProgression.Completed(last.Note.Review.Achievement))facts.Add(new(observer,subject,last.Message.Id,"点検の履行を認めた",last.Message.CreatedAt));
        }
        foreach(var j in TransportWork.Jobs(w)){
            var t=j.Handover.Terms;
            foreach(var e in j.Handover.Entries){
                if(e.Input.Action==HandoverAction.Report&&t.Issuer==observer&&t.Carrier==subject)facts.Add(new(observer,subject,e.Input.Id.Value,"運搬の履行を現地報告で確認",e.Input.At));
                if(e.Input.Action==HandoverAction.Pay&&t.Carrier==observer&&t.Issuer==subject)facts.Add(new(observer,subject,e.Input.Id.Value,"約束の運賃を実際に受領",e.Input.At));
            }
        }
        foreach(var p in w.WorkCycle!.Payments){var task=w.Tasks.Single(t=>t.Id==p.Task);if(task.Executor==observer&&task.Issuer==subject)facts.Add(new(observer,subject,p.Id.Value,"約束の点検報酬を実際に受領",p.At));}
        foreach(var d in NewGameMilitary.Debriefs(w,false))if(d.Principal==observer&&w.PlayerControl==subject)facts.Add(new(observer,subject,d.Notice.Value,"軍務の帰還・返納・返報を確認",d.At));
        return facts.OrderBy(f=>f.At).ThenBy(f=>f.Basis,StringComparer.Ordinal).ToArray();
    }
    // The player's view is testimony and mutual handovers, never a projection of NPC Facts.
    public static WorkContact[] Known(ContractWorld w)
    {
        if(!WorkFollowup.Enabled(w))return [];
        var proofs=new List<(EntityId Other,WorkContactProof Proof)>();
        foreach(var o in w.WorkCycle!.Openings){var m=w.Messaging.Messages.Single(m=>m.Id==o.Message);if(m.Kind!=AssessmentNotice.Kind||m.RecipientId!=w.PlayerControl.Value)continue;var n=AssessmentNotice.Read(m.Body);if(WorkProgression.Completed(n.Review.Achievement))proofs.Add((n.Assessor,new(m.Id,"点検の働きを認める返答を読んだ",m.CreatedAt,o.At)));}
        foreach(var j in TransportWork.Jobs(w))foreach(var e in j.Handover.Entries){var t=j.Handover.Terms;if(t.Carrier!=w.PlayerControl)continue;string? description=e.Input.Action switch{HandoverAction.Report=>"届けを済ませた旨を荷主へ現地報告した",HandoverAction.Pay=>"荷主から約束の運賃を受け取った",_=>null};if(description!=null)proofs.Add((t.Issuer,new(e.Input.Id.Value,description,e.Input.At,e.Input.At)));}
        foreach(var p in w.WorkCycle.Payments){var t=w.Tasks.Single(t=>t.Id==p.Task);if(t.Executor==w.PlayerControl)proofs.Add((t.Issuer,new(p.Id.Value,"相手から約束の点検報酬を受け取った",p.At,p.At)));}
        foreach(var d in NewGameMilitary.Debriefs(w))proofs.Add((d.Principal,new(d.Notice.Value,"軍務の帰還と返納を確認した返答を読んだ",d.At,w.NewMilitary!.Openings.Single(o=>o.Message==d.Notice.Value).At)));
        var service=NewGameService.View(w);var own=w.Locations.Single(l=>l.Actor==w.PlayerControl);
        return proofs.GroupBy(x=>x.Other).Select(g=>{
            var records=g.Select(x=>x.Proof).DistinctBy(x=>x.Basis).OrderByDescending(x=>x.LearnedAt).ThenBy(x=>x.Basis,StringComparer.Ordinal).ToArray();
            bool local=own.Kind==LocationKind.AtSite&&w.Locations.Any(l=>l.Actor==g.Key&&l.Kind==LocationKind.AtSite&&l.Site==own.Site)&&RegionalCampaign.Nearby(w,w.PlayerControl).Any(p=>p.Actor==g.Key);
            bool apply=local&&service.CanApply&&NewGameService.LocalPrincipals(w).Contains(g.Key);
            bool military=local&&service.Principal==g.Key&&NewGameService.BoundAt(w,g.Key,w.ElapsedSeconds,w.CommitSequence);
            return new WorkContact(g.Key,records[0].Description,records,local,apply,military);
        }).OrderBy(c=>c.Other.Value,StringComparer.Ordinal).ToArray();
    }
}
