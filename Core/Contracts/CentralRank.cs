using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

public static class CentralRank
{
    public const string ReplyKind="central-rank-written-decision";
    public const long Offering=60;
    static long ReviewDue(ContractWorld w,long at) => CentralCourtCatalog.HasShogunate(w.NewGame?.HistoricalCatalog)
        ? at+(long)(WorldCalendar.At(w.EpochDate,at).AddMonths(2)-WorldCalendar.At(w.EpochDate,at)).TotalSeconds : at+86400;
    public const string RankName="従五位下";
    internal static bool Handles(CourtAction action)=>action is CourtAction.FileRankPetition or CourtAction.IssueCourtRank or CourtAction.RejectCourtRank;
    static bool Decision(CourtEvent e)=>e.Input.Action is CourtAction.IssueCourtRank or CourtAction.RejectCourtRank;
    static string Reply(CourtEvent e)=>HouseholdCourt.Id("rank-reply",e.Input.Id.Value).Value;
    public static string ReplyId(CourtEvent e)=>Reply(e);
    // 本人が返書を開封して叙任を知った時刻。出世の目標に使う。
    public static IEnumerable<long> LearnedBy(ContractWorld w,EntityId person)=>
        HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.IssueCourtRank&&e.Input.Person==person).Select(e=>MessageFlow.LearnedAt(w.Messaging,Reply(e),person.Value)).Where(t=>t!=null).Select(t=>t!.Value);
    // 取次への申し入れ（ask）に、朝廷の決裁がすでに出ているか。
    public static bool Decided(CourtEvent[] rows,CourtEvent ask)=>rows.Any(d=>Decision(d)&&RequestedRoot(rows,d)==ask.Input.Id);
    static EntityId? RequestedRoot(CourtEvent[] rows,CourtEvent e)
    {
        var cur=e;
        for(int n=0;n<4&&cur!=null;n++){if(cur.Input.Action==CourtAction.AskCourtIntroduction)return cur.Input.Id;cur=rows.SingleOrDefault(r=>r.Input.Id==cur.Input.Reference);}
        return null;
    }
    // 奏請の元（取次への申し入れ）で望んだ位階・官職。決裁・提出の行から参照をたどる。
    public static string? RequestedTitle(CourtEvent[] rows,CourtEvent e)
    {
        var cur=e;
        for(int n=0;n<4&&cur!=null;n++){if(cur.Input.Action==CourtAction.AskCourtIntroduction)return cur.Input.Title;cur=rows.SingleOrDefault(r=>r.Input.Id==cur.Input.Reference);}
        return null;
    }
    static string Body(ContractWorld w,CourtEvent e)
    {
        if(RequestedTitle(HouseholdCourt.Events(w),e) is not {} t)
            return e.Input.Action==CourtAction.IssueCourtRank?"朝廷の返書：従五位下の叙位を認める。":"朝廷の返書：今回の叙位の奏請は認められなかった。";
        var name=CentralTitles.Title(t)!.Name;
        return e.Input.Action==CourtAction.IssueCourtRank?$"朝廷の返書：{name}を認める。":$"朝廷の返書：今回の{name}の奏請は認められなかった。";
    }
    static CourtEvent Record(CourtEvent source,CourtAction action,long at,long revision)
    {using var _ccRev=ControlContext.At(revision);
        var input=new CourtInput(HouseholdCourt.Id(action.ToString(),source.Input.Id.Value),action,
            source.Input.Person,source.Input.Owner,CentralCourtCatalog.Kyoto,Reference:source.Input.Id);
        return new(input,CentralCourtCatalog.CourtBroker,CentralCourtCatalog.Kyoto,at,revision,false);
    }
    static CourtAction Resolve(ContractWorld w,CourtEvent petition)
    {
        // 望む位階・官職があれば表の決まりで審議する（CentralTitles）。
        if(RequestedTitle(HouseholdCourt.Events(w),petition) is {} title)
            return CentralTitles.Eligible(w,petition.Input.Person,petition.Input.Owner,title,petition.At,petition.Revision)?CourtAction.IssueCourtRank:CourtAction.RejectCourtRank;
        // 代表経路の仮資格。申請時点の当主としての身分を審議し、軍権や所領は移転しない。
        return (CentralCourtCatalog.HasShogunate(w.NewGame?.HistoricalCatalog)
            ? RankLeave.Eligible(w,petition.Input.Person,petition.Input.Owner,petition.At,petition.Revision)
            : HouseSuccession.Head(w,petition.Input.Owner,petition.At,petition.Revision-1)==petition.Input.Person)?
            CourtAction.IssueCourtRank:CourtAction.RejectCourtRank;
    }
    static bool Linked(CourtEvent[] rows,CourtEvent source)=>rows.Any(e=>Handles(e.Input.Action)&&e.Input.Reference==source.Input.Id);
    internal static IEnumerable<long> Due(ContractWorld w)
    {
        var rows=HouseholdCourt.Events(w);
        return rows.Where(e=>e.Input.Action==CourtAction.FileRankPetition && !Linked(rows,e)).Select(e=>ReviewDue(w,e.At));
    }
    internal static IEnumerable<long> FilingDates(ContractWorld w) => HouseholdCourt.Events(w)
        .Where(e=>e.Input.Action==CourtAction.AcceptCourtIntroduction).Select(e=>e.At+1);
    internal static long Offerings(ContractWorld w,EntityId wallet,long at,long rev) =>
        !CentralCourtCatalog.HasShogunate(w.NewGame?.HistoricalCatalog)?0:
        HouseholdCourt.Events(w).LongCount(e=>e.Input.Action==CourtAction.AskCourtIntroduction &&
            NewGameWork.Wallet(e.Actor)==wallet && (e.At<at || e.At==at && e.Revision<rev))*Offering;
    internal static void Process(ContractWorld w)
    {
        if(!CentralCourtCatalog.HasCourt(w.NewGame?.HistoricalCatalog) || w.Court==null)return;
        var rows=w.Court.Events;
        foreach(var e in rows.Where(e=>e.Input.Action is CourtAction.AcceptCourtIntroduction or CourtAction.FileRankPetition)){
            if(Linked(rows,e))continue;
            CourtAction action;
            if(e.Input.Action==CourtAction.AcceptCourtIntroduction){
                if(w.ElapsedSeconds<=e.At || Assassination.DeadAt(w,e.Actor,w.ElapsedSeconds,w.CommitSequence) ||
                    !TransportWork.At(w,e.Actor,e.Place,w.ElapsedSeconds,w.CommitSequence,true))continue;
                action=CourtAction.FileRankPetition;
            }else{
                if(w.ElapsedSeconds<ReviewDue(w,e.At))continue;
                action=Resolve(w,e);
            }
            var row=Record(e,action,w.ElapsedSeconds,w.CommitSequence);
            w.Court=w.Court with{Events=[..w.Court.Events,row]};
            if(!Decision(row))continue;
            var address=row.Place.Value;
            var letter=new MessageEnvelope(Reply(row),row.Input.Id.Value,ReplyKind,row.Actor.Value,row.Input.Person.Value,
                Body(w,row),address,row.At){DeliverySite=address};
            MessageFlow.Send(w.Messaging,letter,row.At);
            w.SealedMessages=[..w.SealedMessages,new(letter.Id)];
            RegionalCampaign.Bind(w);
        }
    }
    public static string? KnownRank(ContractWorld w,EntityId person)
    {
        var rows=HouseholdCourt.Events(w);
        var known=rows.Where(e=>e.Input.Action==CourtAction.IssueCourtRank && e.Input.Person==person && MessageFlow.Knows(w.Messaging,Reply(e),person.Value)).ToArray();
        return known.Length==0?null:string.Join("・",known.Select(e=>RequestedTitle(rows,e) is {} t?CentralTitles.Title(t)!.Name:RankName));
    }

    internal static void Validate(ContractWorld w)
    {
        var rows=HouseholdCourt.Events(w);var seen=new HashSet<EntityId>();var letters=new HashSet<string>();
        if(rows.Any(e=>Handles(e.Input.Action)) && !CentralCourtCatalog.HasCourt(w.NewGame?.HistoricalCatalog))
            throw new InvalidDataException("中央制度の開始版が違います");
        foreach(var e in rows.Where(e=>Handles(e.Input.Action))){
            var source=rows.SingleOrDefault(s=>s.Input.Id==e.Input.Reference && s.Revision<e.Revision);
            if(source==null || !seen.Add(source.Input.Id) || Record(source,e.Input.Action,e.At,e.Revision)!=e)
                throw new InvalidDataException("奏請と発給原本の対応が不正です");
            if(!Decision(e)){
                if(source.Input.Action!=CourtAction.AcceptCourtIntroduction || source.At>=e.At ||
                    !TransportWork.At(w,e.Actor,e.Place,e.At,e.Revision,true)||Assassination.DeadAt(w,e.Actor,e.At,e.Revision))
                    throw new InvalidDataException("取次が引受後に京で提出した原本が必要です");
                continue;
            }
            if(source.Input.Action!=CourtAction.FileRankPetition || e.At!=ReviewDue(w,source.At) || e.Input.Action!=Resolve(w,source))
                throw new InvalidDataException("朝廷の審議日または申請資格が一致しません");
            var id=Reply(e);letters.Add(id);
            var m=w.Messaging.Messages.SingleOrDefault(m=>m.Id==id);
            if(m==null || m.Body!=Body(w,e) || m.Kind!=ReplyKind || m.CorrelationId!=e.Input.Id.Value || m.CreatedAt!=e.At ||
               m.SenderId!=e.Actor.Value || m.RecipientId!=e.Input.Person.Value || m.DeliverySite!=e.Place.Value ||
               !w.SealedMessages.Any(s=>s.Value==id))throw new InvalidDataException("朝廷の返書が原本と一致しません");
        }
        if(w.Messaging.Messages.Any(m=>m.Kind==ReplyKind&&!letters.Contains(m.Id)) || Due(w).Any(t=>t<=w.ElapsedSeconds))
            throw new InvalidDataException("奏請の決裁または返書の原本が欠落しています");
    }
}
