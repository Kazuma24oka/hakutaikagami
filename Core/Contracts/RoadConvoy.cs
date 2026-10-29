namespace HakutaiKagami.Core.Contracts;
public static class RoadConvoy
{
    public const string Policy="common-land-retainer-partial-rice-supply-fixed-assembly-v17",LandRoute="house-rice-road-supply-trip-v1";
    const long Day=86400;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or PersonalLife.Policy or CastleHandover.Policy;
    internal static bool IsRoad(CampConvoyJob j)=>j.Terms.RoadDestination!=null;
    static CampConvoyJob[] Jobs(ContractWorld w,long at,long rev)=>FieldConvoy.Jobs(w,at,rev).Where(IsRoad).ToArray();
    static bool Free(ContractWorld w,EntityId actor,long at,long rev)=>!w.Requests.Any(r=>r.Intent.Executor==actor&&r.CreatedAt<=at&&r.CommitSequence<rev&&w.Tasks.Any(t=>t.RequestId==r.Id&&(t.FinishedAt==null||t.FinishedAt>at)));
    static bool At(ContractWorld w,EntityId actor,EntityId site,long at,long rev)=>TransportWork.At(w,actor,site,at,rev,true);
    internal static FieldPoint Point(ContractWorld w,CampConvoyJob j,long at)=>FieldWorld.FromWorld(RegionalCampaign.RoadActorPointAt(w,j.Terms.Carrier,at));
    static EntityId Goal(CampConvoyJob j)=>j.ReturnStarted==null?j.Terms.RoadDestination!.Value:j.Terms.Home;
    static EntityId Effect(ContractWorld w,CampConvoyJob j,string action){var root="request:road-supply/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(j.Terms.Id.Value))+"/"+action.Replace(':','/');var id=new EntityId(root);int n=0;while(FactionStances.OtherIdUsed(w,id)||FactionStances.OwnIdUsed(w,id))id=new(root+"/n"+(++n));return id;}
    internal static CampConvoyEntry Dispatch(ContractWorld w,CampConvoyInput i,long at,long rev)
    {
        Need(Enabled(w)&&i.Action==CampConvoyAction.Dispatch&&i.Job==null&&i.RelatedTask==null&&i.Carrier!=null&&i.Camp==null&&i.DestinationSite!=null&&i.RiceKg is >0 and <=100&&w.NewMilitary!=null,"街道補給の担当家臣・行先・100kg以内の米を指定してください");
        var c=NewGameMilitary.WorldEngagementBefore(w,w.NewMilitary!.Terms.Army,at,rev-1);var s=c.NewMilitary;
        Need(s!=null&&s.Terms.Principal==w.PlayerControl&&NewGameMilitary.RoadMayLead(c)&&!NewGameMilitary.Deployed(c),"当主が現地で同じ部隊を引き受けてから補給を合意します");
        var home=s.Terms.Site;var carrier=i.Carrier!.Value;var destination=i.DestinationSite!.Value;
        Need(destination!=home&&w.Definitions.Sites.Any(n=>n.Id==destination)&&RegionalDomains.Owner(w,home)==s.Terms.Owner&&RegionalDomains.Head(w,home)==w.PlayerControl&&RegionalDomains.Authority(w,w.PlayerControl,home,at,rev),"元の家の処分権と、別の集合拠点を確認してください");
        Need(At(w,w.PlayerControl,home,at,rev)&&At(w,carrier,home,at,rev)&&!FieldConvoy.Busy(w,carrier,at,rev)&&!NpcFreight.Busy(w,carrier,at,rev),"家臣と当主が元の拠点で合流し、他の運搬を終えてください");
        NpcFreight.CheckCarrier(w,new(i.Id,HouseFreightAction.Dispatch){Carrier=carrier},carrier,w.PlayerControl,home,at,rev);
        try{BranchRoutes.Plan(RegionalCampaign.Destination(w,home),RegionalCampaign.Destination(w,destination),BranchRoutes.GraphEdges(w).Where(e=>NationalRoutes.Sea(e.Id)).Select(e=>e.Id),w:w,departureAt:at);}catch(InvalidDataException){throw new InvalidDataException("この集合拠点へは陸路で運べません");}
        Need(CommunityEconomy.AccountAt(w,home,at,rev).HouseRice-HouseSalary.Reserved(w,home,at,rev)>=i.RiceKg,"家の未引当米が不足しています");
        var camp=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==destination).Position);
        return new(i,at,rev,new(i.Id,w.PlayerControl,carrier,s.Terms.Owner,home,s.Terms.Army,NewGameMilitary.CarryFor(c),NewGameMilitary.ConvoyCase(c),camp,i.RiceKg,at,checked(at+30*Day),rev,[]){RoadDestination=destination},null);
    }
    static long Receivable(ContractWorld w,CampConvoyJob j,long at,long rev)
    {
        if(!Enabled(w)||!IsRoad(j)||j.Returned!=null||j.ReturnStarted!=null||j.CargoKg<=0||at>=j.Terms.Until||FreightOperations.Stopped(w,j.Terms.Id,at,rev)||!Free(w,j.Terms.Carrier,at,rev)||!At(w,j.Terms.Carrier,j.Terms.RoadDestination!.Value,at,rev))return 0;
        if(!NewGameMilitary.LedgerStates(w).Any(s=>s.Terms.Army==j.Terms.Army))return 0;
        var c=NewGameMilitary.WorldEngagementBefore(w,j.Terms.Army,at,rev-1);
        if(c.NewMilitary==null||NewGameMilitary.TotalStrength(c,at)<=0||NewGameMilitary.ConvoyCase(c)!=j.Terms.Case||c.NewMilitary.Terms.Owner!=j.Terms.Owner||!NewGameMilitary.CustodyAt(c,at,rev)||NewGameMilitary.Deployed(c)||NewGameMilitary.Detached(c)||!ArmyRoadMarch.AtAssembly(c)||ArmyRoadMarch.AssemblySite(c,at)!=j.Terms.RoadDestination||!At(c,c.PlayerControl,j.Terms.RoadDestination.Value,at,rev))return 0;
        return Math.Min(j.CargoKg,Math.Max(0,NewGameMilitary.Capacity-NewGameMilitary.CarryAt(c,at,rev))/MilitaryEconomy.PersonSecondsPerKg);
    }
    internal static bool CanDeliver(ContractWorld w,CampConvoyJob j,long at,long rev)=>Receivable(w,j,at,rev)>0;
    static bool Meeting(ContractWorld w,CampConvoyJob j,long at,long rev)=>Free(w,j.Terms.Carrier,at,rev)&&BranchRoutes.GraphNodes(w).Any(n=>At(w,w.PlayerControl,RegionalCampaign.Site(n.Id),at,rev)&&At(w,j.Terms.Carrier,RegionalCampaign.Site(n.Id),at,rev));
    internal static CampConvoyEntry Step(ContractWorld w,CampConvoyInput i,CampConvoyJob j,long at,long rev)
    {
        Need(Enabled(w)&&i.Job==j.Terms.Id&&i.Carrier==null&&i.Camp==null&&i.RiceKg==0&&i.DestinationSite==null,"合意した街道補給原本を指定してください");
        Need(i.Action==CampConvoyAction.ReportStop?i.RelatedTask!=null:i.RelatedTask==null,"停止報告だけに停止した移動原本を指定します");
        Need(i.Action==CampConvoyAction.CancelAtMeeting||at%60==0,"家臣の判断は共通時計の毎分境界で行います");
        if(i.Action==CampConvoyAction.Deliver){long kg=Receivable(w,j,at,rev);Need(kg>0,"集合拠点の本人・同じ部隊・空き容量が揃っていません");return new(i,at,rev,null,null){TransferredKg=kg};}
        if(i.Action is CampConvoyAction.StartReturn or CampConvoyAction.CancelAtMeeting){Need(j.ReturnStarted==null&&Free(w,j.Terms.Carrier,at,rev),"現在の街道行程を終えてから帰還します");Need(i.Action==CampConvoyAction.CancelAtMeeting?Meeting(w,j,at,rev):j.CargoKg==0||at>=j.Terms.Until||FreightOperations.Stopped(w,j.Terms.Id,at,rev),"現地での取消受領・期限・受渡し済みを確認してください");return new(i,at,rev,null,[]);}
        if(i.Action==CampConvoyAction.ReportStop){var t=w.Tasks.SingleOrDefault(t=>t.Id==i.RelatedTask&&t.Executor==j.Terms.Carrier&&t.Execution==ExecutionPhase.Stopped&&t.FinishedAt<=at);Need(t!=null&&w.Requests.Any(r=>r.Id==t.RequestId&&r.Intent.RoutePolicy==LandRoute&&r.CreatedAt>=j.Terms.At)&&!w.CampConvoys!.Entries.Any(e=>e.Input.Job==j.Terms.Id&&e.Input.RelatedTask==i.RelatedTask&&e.Revision<rev),"報告する未報告の街道停止原本がありません");return new(i,at,rev,null,null);}
        Need(i.Action==CampConvoyAction.Return&&j.ReturnStarted!=null&&Free(w,j.Terms.Carrier,at,rev)&&At(w,j.Terms.Carrier,j.Terms.Home,at,rev),"元の家へ実帰還してから残米を返納してください");return new(i,at,rev,null,null){ReturnedKg=j.CargoKg};
    }
    internal static bool IsTrip(ContractWorld w,Intent i)=>Enabled(w)&&i.RoutePolicy==LandRoute&&w.Requests.Any(r=>r.Intent==i&&Jobs(w,r.CreatedAt,r.CommitSequence).Any(j=>j.Terms.Carrier==i.Actor&&j.Returned==null));
    internal static void ValidatePending(ContractWorld w,Intent i)
    {Need(Enabled(w)&&i.Actor==i.Executor&&i.Actor!=w.PlayerControl,"家臣本人の街道移動ではありません");var j=Jobs(w,w.ElapsedSeconds,w.CommitSequence+1).SingleOrDefault(j=>j.Terms.Carrier==i.Actor&&j.Returned==null);Need(j!=null&&i==TravelWorld.ActorMove(w,i.RequestId.Value,i.Actor,RegionalCampaign.Destination(w,Goal(j)),true,LandRoute),"合意した集合拠点または元の家への陸路ではありません");}
    static string[] LocalOpenings(ContractWorld w,CampConvoyJob j)
    {var node=BranchRoutes.GraphNodes(w).FirstOrDefault(n=>At(w,j.Terms.Carrier,RegionalCampaign.Site(n.Id),w.ElapsedSeconds,w.CommitSequence+1));if(node==null)return [];var known=BranchRoutes.KnownBlocked(w,j.Terms.Carrier);return BranchRoutes.GraphEdges(w).Where(e=>(e.From==node.Id||e.To==node.Id)&&known.Contains(e.Id)&&!w.Branches!.BlockedEdges.Contains(e.Id)).Select(e=>e.Id).ToArray();}
    static bool PathKnown(ContractWorld w,CampConvoyJob j)
    {try{BranchRoutes.Plan(TravelWorld.Origin(w,j.Terms.Carrier),RegionalCampaign.Destination(w,Goal(j)),VoyageExpense.PlanningExclusions(w,j.Terms.Carrier,true),w:w);return true;}catch(InvalidDataException){return false;}}
    internal static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;var times=new List<long>();long minute=checked((w.ElapsedSeconds/60+1)*60);
        foreach(var j in Jobs(w,w.ElapsedSeconds,w.CommitSequence+1).Where(j=>j.Returned==null)){
            if(j.ReturnStarted==null&&j.Terms.Until>w.ElapsedSeconds)times.Add(checked((j.Terms.Until+59)/60*60));
            if(!Free(w,j.Terms.Carrier,w.ElapsedSeconds,w.CommitSequence+1))continue;
            bool atGoal=At(w,j.Terms.Carrier,Goal(j),w.ElapsedSeconds,w.CommitSequence+1);
            bool report=w.Tasks.Any(t=>t.Executor==j.Terms.Carrier&&t.Execution==ExecutionPhase.Stopped&&t.FinishedAt>=j.Terms.At&&w.Requests.Any(r=>r.Id==t.RequestId&&r.CreatedAt>=j.Terms.At&&r.Intent.RoutePolicy==LandRoute)&&!w.CampConvoys!.Entries.Any(e=>e.Input.Job==j.Terms.Id&&e.Input.RelatedTask==t.Id));
            bool turn=report||j.ReturnStarted!=null&&atGoal||j.ReturnStarted==null&&(j.CargoKg==0||w.ElapsedSeconds>=j.Terms.Until||FreightOperations.Stopped(w,j.Terms.Id,w.ElapsedSeconds,w.CommitSequence+1))||!atGoal&&(PathKnown(w,j)||LocalOpenings(w,j).Length>0)||atGoal&&j.ReturnStarted==null&&At(w,w.PlayerControl,Goal(j),w.ElapsedSeconds,w.CommitSequence+1)&&w.NewMilitary?.Terms.Army==j.Terms.Army&&NewGameMilitary.ConvoyCase(w)==j.Terms.Case&&NewGameMilitary.CustodyAt(w,w.ElapsedSeconds,w.CommitSequence+1)&&!NewGameMilitary.Deployed(w)&&!NewGameMilitary.Detached(w)&&ArmyRoadMarch.AtAssembly(w);
            if(turn)times.Add(minute);
        }return times.Where(t=>t>w.ElapsedSeconds&&t<=target).Order().Select(t=>(long?)t).FirstOrDefault();
    }
    internal static void TakeTurns(WorldSession session)
    {
        var w=session.DiagnosticSnapshot();if(!Enabled(w)||w.Paused||w.ElapsedSeconds%60!=0)return;
        foreach(var old in Jobs(w,w.ElapsedSeconds,w.CommitSequence+1).Where(j=>j.Returned==null)){
            w=session.DiagnosticSnapshot();var j=Jobs(w,w.ElapsedSeconds,w.CommitSequence+1).Single(j=>j.Terms.Id==old.Terms.Id);var t=j.Terms;if(!Free(w,t.Carrier,w.ElapsedSeconds,w.CommitSequence+1))continue;
            if(j.ReturnStarted!=null&&At(w,t.Carrier,t.Home,w.ElapsedSeconds,w.CommitSequence+1)){session.RunCampConvoy(new(Effect(w,j,"return"),CampConvoyAction.Return,t.Id));continue;}
            if(j.ReturnStarted==null&&(j.CargoKg==0||w.ElapsedSeconds>=t.Until||FreightOperations.Stopped(w,t.Id,w.ElapsedSeconds,w.CommitSequence+1))){session.RunCampConvoy(new(Effect(w,j,"start-return"),CampConvoyAction.StartReturn,t.Id));continue;}
            if(CanDeliver(w,j,w.ElapsedSeconds,w.CommitSequence+1)){session.RunCampConvoy(new(Effect(w,j,"deliver/"+w.ElapsedSeconds),CampConvoyAction.Deliver,t.Id));continue;}
            var stopped=w.Tasks.LastOrDefault(q=>q.Executor==t.Carrier&&q.Execution==ExecutionPhase.Stopped&&q.FinishedAt>=t.At&&w.Requests.Any(r=>r.Id==q.RequestId&&r.CreatedAt>=t.At&&r.Intent.RoutePolicy==LandRoute));if(stopped!=null&&!w.CampConvoys!.Entries.Any(e=>e.Input.Job==t.Id&&e.Input.RelatedTask==stopped.Id)){session.RunCampConvoy(new(Effect(w,j,"stop/"+stopped.Id.Value),CampConvoyAction.ReportStop,t.Id){RelatedTask=stopped.Id});continue;}
            if(At(w,t.Carrier,Goal(j),w.ElapsedSeconds,w.CommitSequence+1))continue;
            foreach(var edge in LocalOpenings(w,j)){var result=session.InspectRoad(t.Carrier,edge,session.Revision);Need(result.Code==ResultCode.Accepted,"現地の道路再確認に失敗しました");}w=session.DiagnosticSnapshot();
            try{session.Submit(TravelWorld.ActorMove(w,Effect(w,j,"move/"+w.ElapsedSeconds).Value,t.Carrier,RegionalCampaign.Destination(w,Goal(j)),true,LandRoute),w.CommitSequence);}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or ArgumentException){/* Keep cargo at the real node until a legal road or return is available. */}
        }
    }
    internal static CampConvoyCard Card(ContractWorld w,CampConvoyJob j)
    {
        var observed=FreightOperations.KnownObservation(w,j.Terms.Id);bool returned=j.Returned!=null&&(At(w,w.PlayerControl,j.Terms.Home,j.Returned.At,j.Returned.Revision+1)||At(w,w.PlayerControl,j.Terms.Home,w.ElapsedSeconds,w.CommitSequence+1)||w.Branches!.Journeys.Any(q=>q.Actor==w.PlayerControl&&RegionalCampaign.Site(q.Legs[^1].To)==j.Terms.Home&&w.Tasks.Any(t=>t.Id==q.Task&&t.Execution==ExecutionPhase.Completed&&t.FinishedAt>=j.Returned.At&&t.FinishedAt<=w.ElapsedSeconds)));
        string status=returned?"元の家で未使用米を返納済み":FreightOperations.KnownStatus(w,j.Terms.Id,"合意した集合拠点への街道補給を依頼中（現況は未確認）");if(j.DeliveredKg>0)status+=" / 現地で累計"+j.DeliveredKg+"kgを受領";
        return new(j.Terms.Id,j.Terms.Carrier,j.Terms.Camp,j.Terms.RiceKg,status,j.Returned==null&&j.ReturnStarted==null&&Meeting(w,j,w.ElapsedSeconds,w.CommitSequence+1)){RoadDestination=j.Terms.RoadDestination,Report=observed};
    }
    public static double WalkedDistance(ContractWorld w,CampConvoyJob j,double at)
    {
        double amount=0;foreach(var q in w.Branches!.Journeys.Where(q=>q.Actor==j.Terms.Carrier)){
            var task=w.Tasks.Single(t=>t.Id==q.Task);var r=w.Requests.Single(r=>r.Id==task.RequestId);if(r.Intent.RoutePolicy!=LandRoute||!Jobs(w,r.CreatedAt,r.CommitSequence).Any(old=>old.Terms.Id==j.Terms.Id&&old.Returned==null))continue;
            double time=Math.Min(at,task.FinishedAt??at);foreach(var l in q.Legs){var e=BranchRoutes.GraphEdges(w).Single(e=>e.Id==l.Edge);var a=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>n.Id==l.From).Position);var b=FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>n.Id==l.To).Position);amount+=RegionalGround.Distance(a,b)*Math.Clamp((time-TravelWorld.Departure(w,l.Edge,l.StartAt))*e.FootSpeed/e.LengthMm,0,1);}
        }return amount;
    }
    internal static void Validate(ContractWorld w)
    {
        if(!Enabled(w)){Need(w.CampConvoys?.Entries.All(e=>e.Input.DestinationSite==null&&e.Input.RelatedTask==null&&e.Terms?.RoadDestination==null&&e.TransferredKg==null&&e.ReturnedKg==null)!=false,"旧版へ街道補給を追加できません");return;}
        foreach(var j in Jobs(w,w.ElapsedSeconds,w.CommitSequence+1)){Need(j.Terms.Outbound.Length==0&&j.Terms.Camp==FieldWorld.FromWorld(BranchRoutes.GraphNodes(w).Single(n=>RegionalCampaign.Site(n.Id)==j.Terms.RoadDestination).Position),"街道補給の集合拠点が違います");if(j.Returned!=null)Need(j.DeliveredKg+j.Returned.ReturnedKg==j.Terms.RiceKg,"受領と未使用米の返納が原資と違います");}
        foreach(var r in w.Requests.Where(r=>r.Intent.RoutePolicy==LandRoute)){var j=Jobs(w,r.CreatedAt,r.CommitSequence).SingleOrDefault(j=>j.Terms.Carrier==r.Intent.Actor&&j.Returned==null);Need(j!=null&&r.Intent.Actor==r.Intent.Executor&&r.Intent.DestinationSite==Goal(j),"街道補給の移動が当時の合意と違います");}
    }
}
