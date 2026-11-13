using System.Text;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

// 捕虜の身代金（BD-44・C7、2026-11-14）。捕らえた家の当主が、捕虜の家の当主へ書状で身代金を求める。
// 相手の当主は書状が届いて開封した時点で、本人の私財と書状の額だけで払うか断るかを決め、返書を出す（捕虜の本当の状態は知らない）。
// 払う場合は返書と一緒に銭が運ばれる：出した時点で相手の私財から減り、本人が返書を開封した時点で本人の私財に入る。
// 受け取っても捕虜は自動で解放しない。解放は当主の明示の操作（ManageCaptive）。
public enum RansomAction { Demand,Pay,Refuse }
public sealed record RansomPaper(EntityId Id,RansomAction Action,EntityId Person,long Amount,EntityId? Source,EntityId Author,EntityId Recipient,EntityId Address,long At,long Revision,string Message);
public sealed record RansomOpening(string Message,EntityId Actor,long At,long Revision);
public sealed record RansomCard(EntityId Id,EntityId Person,long Amount,string Stage);

public static partial class BattleCaptivity
{
    public const string RansomKind="captive-ransom-paper";
    public const long RansomMin=100,RansomMax=3000,NpcRansomLimit=1500;
    static RansomPaper[] Papers(ContractWorld w)=>w.Captives?.Papers??[];
    static RansomOpening[] Openings(ContractWorld w)=>w.Captives?.Openings??[];
    static string Hash(string s)=>ContractJson.Hash(Encoding.UTF8.GetBytes(s));
    static string PaperMessage(EntityId id)=>"message:captive-ransom/"+Hash(id.Value);
    static EntityId ReplyId(RansomPaper demand)=>new("request:captive-ransom-reply/"+Hash(demand.Id.Value));
    static bool Opened(ContractWorld w,RansomPaper p,long at,long rev)=>Openings(w).Any(o=>o.Message==p.Message&&o.Actor==p.Recipient&&Before(o.At,o.Revision,at,rev));
    static EntityId Home(ContractWorld w,EntityId owner,long at,long rev)=>new(HouseSuccession.Factions(w,at,rev).Single(f=>f.Id==owner.Value).Home);

    static RansomPaper PrepareDemand(ContractWorld w,EntityId id,EntityId person,long amount,EntityId actor,long at,long rev)
    {using var _ccRev=ControlContext.At(rev);
        Need(Enabled(w)&&NewGameWork.Enabled(w),"この世界では銭のやり取りができません");
        Need(EntityId.IsValid(id.Value)&&actor==w.PlayerControl&&amount is >=RansomMin and <=RansomMax,"身代金は100〜3000文で求めてください");
        var last=Last(w,person,at,rev);
        Need(last!=null&&EndOf(w,person,last.Value.Revision,at,rev)==null&&!Assassination.DeadAt(w,person,at,rev-1),"その人物は捕らえていません");
        var c=last.Value.Capture;
        Need(FactionStances.ResolveAuthority(w,actor,c.CaptorOwner,at,Math.Min(rev,w.CommitSequence))!=null,"捕らえた家の当主権限が必要です");
        Need(!Papers(w).Any(p=>p.Action==RansomAction.Demand&&p.Person==person&&p.Revision>last.Value.Revision&&Before(p.At,p.Revision,at,rev)),"この捕虜の身代金はすでに求めています");
        var head=HouseSuccession.Head(w,c.FromOwner,at,rev);
        Need(head!=person&&FactionStances.ResolveAuthority(w,head,c.FromOwner,at,Math.Min(rev,w.CommitSequence))!=null,"相手の家に返事のできる当主がいません");
        return new(id,RansomAction.Demand,person,amount,null,actor,head,Home(w,c.FromOwner,at,rev),at,rev,PaperMessage(id));
    }
    // 相手の当主の判断：書状の額が上限以内で、本人の私財で払えれば払う。
    static RansomPaper Reply(ContractWorld w,RansomPaper demand,long at,long rev)
    {
        bool pay=demand.Amount<=NpcRansomLimit&&TransportWork.WalletBefore(w,demand.Recipient,at,rev)>=demand.Amount;
        var id=ReplyId(demand);var owner=Last(w,demand.Person,demand.At,demand.Revision)!.Value.Capture.CaptorOwner;
        return new(id,pay?RansomAction.Pay:RansomAction.Refuse,demand.Person,pay?demand.Amount:0,demand.Id,demand.Recipient,demand.Author,Home(w,owner,at,rev),at,rev,PaperMessage(id));
    }
    static MessageEnvelope Envelope(ContractWorld w,RansomPaper p)
    {
        string name=FamilyBirth.People(w).SingleOrDefault(x=>x.Id==p.Person.Value)?.Name??p.Person.Value;
        string body=p.Action switch{RansomAction.Demand=>$"{name}殿の身代金として{p.Amount}文を求めます。",RansomAction.Pay=>$"{name}殿の身代金{p.Amount}文を送ります。",_=>$"{name}殿の身代金には応じません。"};
        return new(p.Message,p.Id.Value,RansomKind,p.Author.Value,p.Recipient.Value,body,p.Address.Value,p.At);
    }
    static void Post(ContractWorld w,RansomPaper p)
    {
        w.Captives=w.Captives! with{Papers=[..Papers(w),p]};
        MessageFlow.Send(w.Messaging,Envelope(w,p),p.At,1);w.SealedMessages=[..w.SealedMessages,new(p.Message)];
    }
    internal static void Demand(ContractWorld w,EntityId id,EntityId person,long amount,EntityId actor)
    {
        Need(!OwnIdUsed(w,id)&&!Papers(w).Any(p=>p.Id==id),"別の操作に使ったIDです");
        Post(w,PrepareDemand(w,id,person,amount,actor,w.ElapsedSeconds,w.CommitSequence));
    }
    public static string? Address(ContractWorld w,MessageEnvelope m)=>m.Kind==RansomKind&&Papers(w).SingleOrDefault(p=>p.Message==m.Id) is {} p?NewGameWorld.Node(p.Address.Value):null;
    // 本人が開封したとき（WorldSession.Open）。
    internal static void RecordOpening(ContractWorld w,EntityId actor,string message)
    {
        if(!Enabled(w)||Openings(w).Any(o=>o.Message==message)||!Papers(w).Any(p=>p.Message==message&&p.Recipient==actor))return;
        w.Captives=w.Captives! with{Openings=[..Openings(w),new(message,actor,w.ElapsedSeconds,w.CommitSequence)]};
    }
    static bool Delivered(ContractWorld w,RansomPaper p)=>w.Messaging.Attempts.Any(a=>a.MessageId==p.Message&&a.ReceiverId==p.Recipient.Value&&a.Status==AttemptStatus.Delivered);
    // 相手の当主は届いた書状をすぐ開封し、その場で返書を出す。
    internal static void Update(ContractWorld w)
    {
        if(!Enabled(w)||Papers(w).Length==0)return;
        foreach(var p in Papers(w).Where(p=>p.Action==RansomAction.Demand&&p.Recipient!=w.PlayerControl).ToArray())
        {
            if(Openings(w).Any(o=>o.Message==p.Message)||!Delivered(w,p))continue;
            if(!MessageFlow.Knows(w.Messaging,p.Message,p.Recipient.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(p.Message,p.Recipient.Value,w.ElapsedSeconds,p.Message)];
            RecordOpening(w,p.Recipient,p.Message);
            if(FactionStances.ResolveAuthority(w,p.Recipient,Last(w,p.Person,p.At,p.Revision)!.Value.Capture.FromOwner,w.ElapsedSeconds,w.CommitSequence)!=null)
                Post(w,Reply(w,p,w.ElapsedSeconds,w.CommitSequence));
        }
    }
    // 銭の動き：支払いの返書を出した時点で相手の私財から減り、本人が開封した時点で本人の私財に入る。
    internal static long Net(ContractWorld w,EntityId wallet,long at=long.MaxValue,long rev=long.MaxValue)
    {
        if(w.Captives is null)return 0;long n=PleaNet(w,wallet,at,rev);
        foreach(var p in Papers(w).Where(p=>p.Action==RansomAction.Pay))
        {
            if(NewGameWork.Wallet(p.Author)==wallet&&Before(p.At,p.Revision,at,rev))n=checked(n-p.Amount);
            if(NewGameWork.Wallet(p.Recipient)==wallet&&Opened(w,p,at,rev))n=checked(n+p.Amount);
        }
        return n;
    }
    static void ValidateRansom(ContractWorld w)
    {
        var papers=Papers(w);var openings=Openings(w);
        Need(papers.Select(p=>p.Id).Distinct().Count()==papers.Length&&!papers.Any(p=>OwnIdUsed(w,p.Id))&&openings.Select(o=>o.Message).Distinct().Count()==openings.Length,"身代金の書状が重複しています");
        Need(papers.Zip(papers.Skip(1)).All(x=>x.First.At<=x.Second.At&&x.First.Revision<=x.Second.Revision),"身代金の書状の順序が不正です");
        foreach(var p in papers)
        {
            Need(p.At>=0&&p.At<=w.ElapsedSeconds&&p.Revision>0&&p.Revision<=w.CommitSequence&&p.Message==PaperMessage(p.Id),"身代金の書状の時点が不正です");
            if(p.Action==RansomAction.Demand)Need(p==PrepareDemand(w,p.Id,p.Person,p.Amount,p.Author,p.At,p.Revision),"身代金の要求が一致しません");
            else
            {
                var demand=papers.SingleOrDefault(d=>d.Id==p.Source&&d.Action==RansomAction.Demand);
                Need(demand!=null&&openings.Any(o=>o.Message==demand.Message&&o.Actor==demand.Recipient&&o.At==p.At&&o.Revision==p.Revision)&&p==Reply(w,demand,p.At,p.Revision),"身代金の返書が一致しません");
            }
            var m=w.Messaging.Messages.SingleOrDefault(x=>x.Id==p.Message);
            Need(m!=null&&m==Envelope(w,p)&&w.SealedMessages.Contains(new(p.Message)),"身代金の書状の本文が一致しません");
        }
        Need(w.Messaging.Messages.Count(m=>m.Kind==RansomKind)==papers.Length,"身代金の書状の数が一致しません");
        foreach(var o in openings)
        {
            var p=papers.Single(p=>p.Message==o.Message);
            Need(p.Recipient==o.Actor&&o.At>=p.At&&o.At<=w.ElapsedSeconds&&o.Revision>p.Revision&&o.Revision<=w.CommitSequence&&MessageFlow.LearnedAt(w.Messaging,o.Message,o.Actor.Value)==o.At&&
                w.Messaging.Attempts.Any(a=>a.MessageId==o.Message&&a.ReceiverId==o.Actor.Value&&a.Status==AttemptStatus.Delivered&&a.FinishedAt<=o.At),"身代金の書状の到着・開封が一致しません");
        }
    }
    // 本人が知る身代金の段階。
    public static RansomCard[] Ransoms(ContractWorld w,EntityId observer)
    {
        if(!Enabled(w))return [];long at=w.ElapsedSeconds,rev=w.CommitSequence+1;
        return Papers(w).Where(p=>p.Action==RansomAction.Demand&&p.Author==observer).Select(d=>
        {
            var reply=Papers(w).SingleOrDefault(p=>p.Source==d.Id);
            string stage=reply==null||!Opened(w,reply,at,rev)?"返事待ち":reply.Action==RansomAction.Pay?"受け取り":"断り";
            return new RansomCard(d.Id,d.Person,d.Amount,stage);
        }).ToArray();
    }
    internal static IEnumerable<(long At,long Revision)> ExternalInputs(ContractWorld w)=>
        Entries(w).Select(e=>(e.At,e.Revision)).Concat(Papers(w).Where(p=>p.Author==w.PlayerControl).Select(p=>(p.At,p.Revision))).Concat(Openings(w).Where(o=>o.Actor==w.PlayerControl).Select(o=>(o.At,o.Revision)));
    internal static void Rewind(ContractWorld w,long at,long rev)
    {
        if(w.Captives is not {} s)return;
        bool Keep(long a,long r)=>a<at||a==at&&r<=rev;
        w.Captives=s with{Entries=s.Entries.Where(e=>Keep(e.At,e.Revision)).ToArray(),Papers=s.Papers.Where(p=>Keep(p.At,p.Revision)).ToArray(),Openings=s.Openings.Where(o=>Keep(o.At,o.Revision)).ToArray()};
    }
}

public sealed partial class WorldSession
{
    public RansomCard[] Ransoms(){lock(gate)return BattleCaptivity.Ransoms(state,state.PlayerControl);}
    public CommandResult DemandRansom(EntityId id,EntityId person,long amount,long revision){using var _ccRev=ControlContext.At(revision);lock(gate){
        if(!BattleCaptivity.Enabled(state))return new(ResultCode.NotAvailable,id,state.CommitSequence,"この保存では捕縛を使えません");
        var old=state.Captives!.Papers.SingleOrDefault(p=>p.Id==id);if(old!=null)return new(old.Person==person&&old.Amount==amount&&old.Author==state.PlayerControl?ResultCode.AlreadyAccepted:ResultCode.IdPayloadConflict,id,state.CommitSequence,"処理済みの操作です");
        if(BattleCaptivity.OwnIdUsed(state,id)||FactionStances.OtherIdUsed(state,id)||FactionStances.OwnIdUsed(state,id))return new(ResultCode.IdPayloadConflict,id,state.CommitSequence,"別操作のIDです");
        if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
        try{Change(w=>BattleCaptivity.Demand(w,id,person,amount,w.PlayerControl));}catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException){return new(ResultCode.NotAvailable,id,state.CommitSequence,e.Message);}
        return new(ResultCode.Accepted,id,state.CommitSequence,"書状を送りました");
    }}
}
