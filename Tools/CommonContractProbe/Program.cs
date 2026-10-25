using System.Text.Json;
using System.Text.Json.Nodes;
using HakutaiKagami.Core.Contracts;

// Headless integration/foreign-process probe. Only writes into the explicitly supplied test root.
if(args.Length!=2)throw new ArgumentException("Usage: CommonContractProbe exercise|restore|interrupt|peace|survey <test-root>");
var mode=args[0];var store=new GenerationSaveStore(args[1],ContractFixture.Create().Definitions);
WorldSession Load()
{
    var host=new WorldSessionHost(new(ContractFixture.Create()));var old=host.Current;
    if(host.CommitLoad(store.Prepare("probe"),old,old.Revision)!=ResultCode.Accepted)throw new InvalidOperationException("Load failed.");
    return host.Current;
}
string Digest(WorldSession session)
{
    var node=JsonNode.Parse(ContractJson.Write(session.DiagnosticSnapshot()))!;
    void Strip(JsonNode? n)
    {
        if(n is JsonObject o){o.Remove("Revision");o.Remove("CommitSequence");o.Remove("Paused");foreach(var x in o.ToArray())Strip(x.Value);}
        else if(n is JsonArray a)foreach(var x in a)Strip(x);
    }
    Strip(node);return ContractJson.CanonicalHash(node);
}
object Continue(WorldSession session)
{
    session.SetPaused(false);session.AdvanceTo(8);var next=session.Draw(new("decision:next"),"decisions");var s=session.DiagnosticSnapshot();
    return new{Seconds=s.ElapsedSeconds,Treasury=s.ResourceLots.Single(l=>l.Id==ContractFixture.Treasury).Quantity,
        Execution=s.Tasks.Single().Execution.ToString(),Assessment=s.Tasks.Single().Assessment.ToString(),Transfers=s.Transfers.Length,NextRandom=next,Digest=Digest(session)};
}
object result;
switch(mode)
{
    case "survey":
    {
        var candidate=SurveyRoadReview.Futamura();var decision=SurveyRoadReview.Evaluate(candidate,1560);
        var before=HistoricalRoads.Create();var graph=ContractJson.CanonicalHash(HistoricalRoads.Edges);
        bool refused=false;try{SurveyRoadReview.Replacement(candidate,1560);}catch(InvalidDataException){refused=true;}
        result=new{Mode=mode,Decision=decision,ReplacementCount=0,Refused=refused,GeometryUnchanged=graph==ContractJson.CanonicalHash(HistoricalRoads.Edges),DefinitionUnchanged=before.Definitions.Version==HistoricalRoads.Create().Definitions.Version,WorldDefinition=before.Definitions.Version};break;
    }

    case "exercise":
    {
        var s=new WorldSession(ContractFixture.Create());s.Draw(new("decision:first"),"decisions");
        s.Submit(ContractFixture.Order("request:cross-process-tax",ActionKind.Transfer,40),s.Revision);s.AdvanceTo(4);
        var saved=store.Save(s,"probe",s.DiagnosticSnapshot().WorldId,new(.375,60));
        result=new{Mode=mode,SavedSeconds=s.Seconds,SavedRevision=s.Revision,Generation=saved.GenerationId,SavedDigest=Digest(s),Expected=Continue(s)};break;
    }
    case "restore":
    {
        var s=Load();result=new{Mode=mode,RestoredSeconds=s.Seconds,InitiallyPaused=s.DiagnosticSnapshot().Paused,RestoredDigest=Digest(s),Actual=Continue(s)};break;
    }
    case "interrupt":
    {
        var s=Load();s.SetPaused(false);s.AdvanceTo(10);
        store.Save(s,"probe",s.DiagnosticSnapshot().WorldId,fault:stage=>{if(stage==SaveStage.GenerationVerified)Environment.Exit(73);});
        throw new InvalidOperationException("Interruption was not reached.");
    }
    case "peace":
    {
        var s=new WorldSession(ContractFixture.Create());var timeline=new List<object>();
        void Mark(string stage){var w=s.DiagnosticSnapshot();timeline.Add(new{Stage=stage,Seconds=w.ElapsedSeconds,
            Locations=w.Locations.Select(l=>new{Actor=l.Actor.Value,Kind=l.Kind.ToString(),Site=l.Site?.Value,ProgressMm=l.ProgressMm}),
            Tasks=w.Tasks.Select(t=>new{Id=t.Id.Value,Execution=t.Execution.ToString(),Report=t.Report.ToString(),Assessment=t.Assessment.ToString()})});}
        s.Submit(ContractFixture.Order("request:prepare",important:true),s.Revision);s.AdvanceTo(20);Mark("重要任務が到達・未開封で停止");
        var letter=s.GetActorView(ContractFixture.Officer).Messages.Single().Id;s.Open(ContractFixture.Officer,letter,s.Revision);s.SetPaused(false);s.AdvanceTo(8);Mark("本人が開封・準備作業・返報認識");
        s.Submit(ContractFixture.Order("request:delegate",ActionKind.Transfer,40,executor:ContractFixture.Clerk),s.Revision);
        s.Submit(ContractFixture.Order("request:move",ActionKind.Move),s.Revision);s.AdvanceTo(12);Mark("本人は移動中・別担当が並行履行");
        store.Save(s,"peace",s.DiagnosticSnapshot().WorldId,new(.5,10));
        var host=new WorldSessionHost(new(ContractFixture.Create()));var initial=host.Current;host.CommitLoad(store.Prepare("peace"),initial,initial.Revision);s=host.Current;Mark("途中保存から停止状態で復旧");
        s.SetPaused(false);s.AdvanceTo(16);Mark("移動・委任完了、主君への返報を確認");
        result=new{Mode=mode,Synthetic=true,Timeline=timeline,Treasury=s.DiagnosticSnapshot().ResourceLots.Single(l=>l.Id==ContractFixture.Treasury).Quantity,
            Completed=s.DiagnosticSnapshot().Tasks.Count(t=>t.Execution==ExecutionPhase.Completed),Assessed=s.DiagnosticSnapshot().Tasks.Count(t=>t.Assessment==AssessmentPhase.Confirmed),Digest=Digest(s)};break;
    }
    default:throw new ArgumentException("Unknown mode.");
}
Console.WriteLine(JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
