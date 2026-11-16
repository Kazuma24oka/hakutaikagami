using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record ClinicalReportInput(EntityId Id,EntityId Patient,EntityId Recipient);
public sealed record ClinicalReport(ClinicalReportInput Input,EntityId Observer,EntityId Site,long ObservedAt,long Revision,HealthCard[] Cards);
public static partial class ClinicalHealth
{
    public const string ReportKind="dated-clinical-report-v2";
    internal static string LetterId(ClinicalReport r)=>"message:clinical/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(r.Input.Id.Value));
    static MessageEnvelope Envelope(ContractWorld w,ClinicalReport r)=>new(LetterId(r),r.Input.Id.Value,ReportKind,r.Observer.Value,r.Input.Recipient.Value,System.Text.Encoding.UTF8.GetString(ContractJson.Write(r)),FamilyBirth.People(w).Single(p=>p.Id==r.Input.Recipient.Value).Site,r.ObservedAt);
    static bool RecipientKnown(ContractWorld w,ClinicalReport r)=>r.Observer!=r.Input.Recipient&&(
        HouseholdService.BondsAt(w,r.ObservedAt,r.Revision).Any(b=>b.Subject==r.Observer&&b.Target==r.Input.Recipient&&b.From<=r.ObservedAt&&r.ObservedAt<b.Until)||
        FamilyBirth.People(w).Single(p=>p.Id==r.Observer.Value).Lord==r.Input.Recipient.Value||FamilyBirth.People(w).Single(p=>p.Id==r.Input.Recipient.Value).Lord==r.Observer.Value);
    public static EntityId[] ReportRecipients(ContractWorld w,EntityId observer)=>!Enabled(w)?[]:FamilyBirth.People(w).Where(p=>p.Id!=observer.Value&&(p.Lord==observer.Value||FamilyBirth.People(w).Single(x=>x.Id==observer.Value).Lord==p.Id)).Select(p=>new EntityId(p.Id)).ToArray();
    internal static void SendReport(ContractWorld w,ClinicalReportInput input,EntityId observer)
    {
        var site=SocialWorld.Site(w,observer);Need(site!=null,"拠点にいません");
        var r=new ClinicalReport(input,observer,site.Value,w.ElapsedSeconds,w.CommitSequence,PersonalHealth.ObservedCards(w,input.Patient,w.ElapsedSeconds));
        Need(RecipientKnown(w,r),"報告先ではありません");
        Need(TransportWork.At(w,input.Patient,r.Site,r.ObservedAt,r.Revision,true),"病状を観測していません");
        w.Health=w.Health! with{Clinic=w.Health.Clinic! with{Reports=[..w.Health.Clinic.Reports,r]}};
        var letter=Envelope(w,r);MessageFlow.Send(w.Messaging,letter,r.ObservedAt,1);w.SealedMessages=[..w.SealedMessages,new(letter.Id)];RegionalCampaign.Bind(w);
    }
    public static ClinicalReport[] KnownReports(ContractWorld w,EntityId observer)=>!Enabled(w)?[]:w.Health!.Clinic!.Reports.Where(r=>r.Input.Recipient==observer&&MessageFlow.Knows(w.Messaging,LetterId(r),observer.Value)).ToArray();
    internal static HealthView? ReportView(ContractWorld w,EntityId observer,EntityId person)
    {
        var r=KnownReports(w,observer).Where(r=>r.Input.Patient==person).OrderBy(r=>r.ObservedAt).ThenBy(r=>r.Revision).LastOrDefault();
        return r==null?null:new(person,false,true,r.Cards,[]);
    }
    static void ValidateReports(ContractWorld w,ContractWorld? prior)
    {
        var rows=w.Health!.Clinic!.Reports;Need(rows!=null,"病状報告の原本がありません");
        if(prior?.Health?.Clinic is {} old)Need(ContractJson.CanonicalHash(rows.Take(old.Reports.Length).ToArray())==ContractJson.CanonicalHash(old.Reports),"過去の病状報告を変更できません");
        long rev=-1;var ids=new HashSet<EntityId>();
        foreach(var r in rows)
        {
            Need(EntityId.IsValid(r.Input.Id.Value)&&ids.Add(r.Input.Id)&&r.ObservedAt>=0&&r.ObservedAt<=w.ElapsedSeconds&&r.Revision>rev&&r.Revision<=w.CommitSequence,"病状報告の時点が不正です");
            Need(!FactionStances.OtherIdUsed(w,r.Input.Id,includeClinical:false)&&!FactionStances.OwnIdUsed(w,r.Input.Id)&&!Plans(w).Any(p=>p.Input.Id==r.Input.Id),"別操作の原本です");
            Need(RecipientKnown(w,r)&&TransportWork.At(w,r.Observer,r.Site,r.ObservedAt,r.Revision,true)&&TransportWork.At(w,r.Input.Patient,r.Site,r.ObservedAt,r.Revision,true),"観測者・患者・報告先が不正です");
            Need(ContractJson.CanonicalHash(r.Cards)==ContractJson.CanonicalHash(PersonalHealth.ObservedCards(w,r.Input.Patient,r.ObservedAt)),"病状の観測時点と内容が違います");
            Need(w.Messaging.Messages.SingleOrDefault(m=>m.Id==LetterId(r))==Envelope(w,r)&&w.SealedMessages.Contains(new(LetterId(r))),"病状報告の原文・封書が違います");rev=r.Revision;
        }
        Need(w.Messaging.Messages.Count(m=>m.Kind==ReportKind)==rows.Length,"病状報告が原本と対応していません");
    }
}
public sealed partial class WorldSession
{
    public EntityId[] HealthReportRecipients(){lock(gate)return ClinicalHealth.ReportRecipients(state,state.PlayerControl);}
    public ClinicalReport[] HealthReports(){lock(gate)return ClinicalHealth.KnownReports(state,state.PlayerControl);}
    public CommandResult ReportHealth(ClinicalReportInput input,EntityId actor,long revision){using var _ccRev=ControlContext.At(revision);lock(gate){
        if(actor!=state.PlayerControl||!ClinicalHealth.Enabled(state))return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,"病状報告");
        var old=state.Health!.Clinic!.Reports.SingleOrDefault(r=>r.Input.Id==input.Id);
        if(old!=null)return new(old.Input==input?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"病状報告");
        if(FactionStances.OtherIdUsed(state,input.Id)||FactionStances.OwnIdUsed(state,input.Id)||PersonalHealth.OwnIdUsed(state,input.Id))return new(ResultCode.IdPayloadConflict,input.Id,state.CommitSequence,"原本");
        if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,input.Id,state.CommitSequence,"時点");
        try{Change(w=>ClinicalHealth.SendReport(w,input,actor));return new(ResultCode.Accepted,input.Id,state.CommitSequence,"病状報告");}
        catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,input.Id,state.CommitSequence,e.Message);}
    }}
}
