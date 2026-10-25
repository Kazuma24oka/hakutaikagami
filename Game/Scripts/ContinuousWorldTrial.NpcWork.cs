using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
public partial class ContinuousWorldTrial
{
    bool NpcWorkEnabled=>Name=="NpcWorkTrial"||PersonalStartEnabled;
    long NpcIssued=>S.DiagnosticSnapshot().Requests.First(r=>r.Intent.Action==ActionKind.Inspect).CreatedAt;
    string NpcPose()=>System.Text.Json.JsonSerializer.Serialize(new{World=WorkPose(),Decisions=S.DiagnosticSnapshot().NpcWork!.Decisions.Select(d=>new{d.Actor,d.At,d.InputHash,d.Reason,d.Request,d.Result})});
    void NpcOpenAndAnswer(){if(RegionalWork.Inbox(S).Any(c=>c.CanOpen))Click("work-open");if(RegionalWork.Inbox(S).Any(c=>c.CanDecide))Click(Case=="refuse"?"work-decline":"work-accept");}
    void NpcFailReport()
    {
        if(Case!="report-miss")return;var w=S.DiagnosticSnapshot();var task=w.Tasks.First(t=>t.Action==ActionKind.Inspect);string id="message:"+task.Id.Value.Replace(':','/')+"/report";
        var attempt=w.Messaging.Attempts.FirstOrDefault(a=>a.MessageId==id);if(attempt!=null&&attempt.Status==AttemptStatus.InTransit&&!attempt.FailOnArrival)S.DiagnosticTransport((m,t)=>MessageFlow.Fail(m,id));
    }
    void NpcContinue()
    {
        WorkArrive();if(!S.DiagnosticSnapshot().Tasks.Any(t=>t.Action==ActionKind.Inspect))WorkAdvance(S.Seconds+1);
        long at=NpcIssued;if(S.Seconds<at+1)WorkAdvance(at+1);NpcOpenAndAnswer();if(S.Seconds<at+5)WorkAdvance(at+5);NpcFailReport();if(S.Seconds<at+8)WorkAdvance(at+8);WorkAdvance(at+NpcWork.Interval+1);
    }
    async Task RunNpcWorkChecks()
    {
        try{
            System.IO.Directory.CreateDirectory(Output);
            if(Mode.EndsWith("restore")){
                for(int n=0;n<5;n++){Load(Case+"-s"+n);Check(NpcPose()==System.IO.File.ReadAllText(Output+"/"+Case+"-s"+n+"-pose.json"),"restored NPC memory, known input, travel and actual 3D "+n);await WorkCapture("restored-s"+n);NpcContinue();await WorkCapture("continued-s"+n);}
            }else{
                if(WorkNode!="castle"){
                    destination.Select(Array.FindIndex(NationalRoutes.Nodes,n=>n.Id==WorkNode));Click("depart");WorkAdvance(1);var leg=S.DiagnosticSnapshot().Branches!.Journeys.Single().Legs[0];WorkAdvance(Math.Max(1,(leg.StartAt+leg.EndAt)/2));
                    Check(!S.DiagnosticSnapshot().Tasks.Any(t=>t.Action==ActionKind.Inspect),"remote NPC does not dispatch from unseen personal travel");
                }else WorkAdvance(1);
                for(int n=0;n<5;n++){
                    if(n==1){WorkArrive();WorkAdvance(NpcIssued+1);Check(S.Paused&&RegionalWork.Inbox(S).Single().Task==null,"NPC automatically sends and sealed arrival hides body");}
                    if(n==2){NpcOpenAndAnswer();if(Case!="refuse")WorkAdvance(NpcIssued+2);}
                    if(n==3){WorkAdvance(NpcIssued+5);NpcFailReport();}
                    if(n==4)WorkAdvance(NpcIssued+8);
                    Save(Case+"-s"+n);System.IO.File.WriteAllText(Output+"/"+Case+"-s"+n+"-pose.json",NpcPose());await WorkCapture("s"+n);
                }
                NpcContinue();await WorkCapture("final");var w=S.DiagnosticSnapshot();var jobs=w.Tasks.Where(t=>t.Action==ActionKind.Inspect).ToArray();
                Check(jobs.Length==(Case=="report-miss"?1:2),"NPC waits for known return and cooldown before one repeat");
                Check(jobs[0].Execution==(Case=="refuse"?ExecutionPhase.Stopped:ExecutionPhase.Completed),"first voluntary decision respected");
                Check(jobs.All(t=>t.Issuer==RegionalWork.Roles.Single(r=>r.Node==WorkNode).Person),"actual local officer is the dispatcher");
                Check(w.NpcWork!.Decisions.Length<24,"NPC memory bounded in representative episode, no per-second churn");
                Check(!buttons["work-send"].Visible,"manual test dispatch is hidden in autonomous profile");
                if(Case!="report-miss")Check(RegionalWork.Inbox(S).Last().Task==null,"repeated job is also sealed, no autoaccept");
            }
            System.IO.File.WriteAllText(Output+"/"+Case+"-"+(Mode.EndsWith("restore")?"restore":"check")+"-checks.json",System.Text.Json.JsonSerializer.Serialize(new{Checks=checks}));GD.Print("NPC_WORK_PASSED "+WorkNode+" "+Case);GetTree().Quit();
        }catch(Exception e){GD.PrintErr(e);GetTree().Quit(1);}
    }
}
