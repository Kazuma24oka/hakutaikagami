using System.Text;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

// 捕虜の身代金（BD-44・C7、2026-11-14）。捕らえた家の当主が、捕虜の家の当主へ書状で身代金を求める。
// 相手の当主は書状が届いて開封した時点で、本人の私財と書状の額だけで払うか断るかを決め、返書を出す（捕虜の本当の状態は知らない）。
// 払う場合は返書と一緒に銭が運ばれる：出した時点で相手の私財から減り、本人が返書を開封した時点で本人の私財に入る。
// 受け取っても捕虜は自動で解放しない。解放は当主の明示の操作（ManageCaptive）。
public enum RansomAction { Demand,Pay,Refuse,Exchange,AcceptExchange }
public sealed record RansomPaper(EntityId Id,RansomAction Action,EntityId Person,long Amount,EntityId? Source,EntityId Author,EntityId Recipient,EntityId Address,long At,long Revision,string Message)
{
    // 捕虜交換の申し入れ（Exchange）で、相手が捕らえている自分の家の者。
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public EntityId? Other {get;init;}
}
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
        var last=Last(w,person,at,rev);
        Need(EntityId.IsValid(id.Value)&&(actor==w.PlayerControl||NpcCaptors(w)&&last!=null&&id==NpcDemandId(person,last.Value.Capture.At)&&amount==NpcRansom)&&amount is >=RansomMin and <=RansomMax,"身代金は100〜3000文で求めてください");
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
        string other=p.Other is {} o?FamilyBirth.People(w).SingleOrDefault(x=>x.Id==o.Value)?.Name??o.Value:"";
        string body=p.Action switch{RansomAction.Demand=>$"{name}殿の身代金として{p.Amount}文を求めます。",RansomAction.Pay=>$"{name}殿の身代金{p.Amount}文を送ります。",
            RansomAction.Exchange=>$"{name}殿と{other}殿を引き換えに放ちたく存じます。",RansomAction.AcceptExchange=>$"{name}殿と{other}殿の引き換えに応じます。",_=>$"{name}殿の身代金には応じません。"};
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
    // NPC の当主が捕らえた者の処遇（全国の開始の一覧の NpcCaptors）。捕らえた家の当主は、捕縛の1日後に捕虜の家の当主へ身代金500文を書状で求める。
    // 返書が届けばすぐ開封し、払われていれば、その城にいるときに放つ。断られたら30日後に、その城にいるときに放つ。登用・処断は NPC はしない。
    public const long NpcRansom=500,NpcWaitSeconds=86400,NpcMercySeconds=30*86400;
    internal static bool NpcCaptors(ContractWorld w)=>NationalSystems.On(w,"NpcCaptors");
    static EntityId NpcDemandId(EntityId person,long capturedAt)=>new("request:npc-captive-ransom/"+Hash(person.Value+"/"+capturedAt));
    static EntityId NpcReleaseId(EntityId person,long capturedAt)=>new("request:npc-captive-release/"+Hash(person.Value+"/"+capturedAt));
    static void NpcCaptorTurns(ContractWorld w)
    {
        if(!NpcCaptors(w))return;long at=w.ElapsedSeconds,rev=w.CommitSequence;
        foreach(var (c,capRev) in Captures(w).ToArray())
        {
            if(c.Holder==w.PlayerControl||HouseSuccession.Head(w,c.CaptorOwner,at,rev)==w.PlayerControl)continue;
            var last=Last(w,c.Person,at,rev+1);if(last==null||last.Value.Revision!=capRev||EndOf(w,c.Person,capRev,at,rev+1)!=null)continue;
            var head=HouseSuccession.Head(w,c.CaptorOwner,at,rev);
            // 捕虜の交換：互いに相手の家の者を捕らえていれば、家の ID の小さい側の当主が書状で引き換えを申し入れる（身代金より先）。
            var proposed=Papers(w).FirstOrDefault(p=>p.Action==RansomAction.Exchange&&p.Person==c.Person&&p.Author==head);
            if(proposed!=null||Mutual(w,c,at,rev) is not null)
            {
                if(proposed==null&&Mutual(w,c,at,rev) is {} theirs&&string.CompareOrdinal(c.CaptorOwner.Value,c.FromOwner.Value)<0&&at>=c.At+NpcWaitSeconds)
                    Post(w,PrepareExchange(w,NpcExchangeId(c.Person,theirs.Person),c,theirs,head,at,rev));
                // 相手の受諾を開封したら、その城にいるときに放つ。
                var accept=Papers(w).SingleOrDefault(p=>p.Action==RansomAction.AcceptExchange&&p.Recipient==head&&p.Person==c.Person);
                if(accept!=null&&!Openings(w).Any(o=>o.Message==accept.Message)&&Delivered(w,accept))
                {if(!MessageFlow.Knows(w.Messaging,accept.Message,head.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(accept.Message,head.Value,at,accept.Message)];RecordOpening(w,head,accept.Message);}
                bool agreed=Papers(w).Any(p=>p.Action==RansomAction.AcceptExchange&&p.Person==c.Person&&(p.Author==head||Opened(w,p,at,rev+1)));
                if(agreed&&!OwnIdUsed(w,NpcReleaseId(c.Person,c.At))&&CanAct(w,c,head,at,rev))Apply(w,new(NpcReleaseId(c.Person,c.At),CaptiveAction.Release,c.Person),head);
                continue;
            }
            // 身代金を求める（捕虜の家の当主が別人で、権限を持つとき）。
            var demandId=NpcDemandId(c.Person,c.At);var demand=Papers(w).SingleOrDefault(p=>p.Id==demandId);
            if(demand==null)
            {
                if(at<c.At+NpcWaitSeconds)continue;
                try{Post(w,PrepareDemand(w,demandId,c.Person,NpcRansom,head,at,rev));}catch(InvalidDataException){}
                continue;
            }
            // 返書を開封する。
            var reply=Papers(w).SingleOrDefault(p=>p.Source==demand.Id);
            if(reply!=null&&!Openings(w).Any(o=>o.Message==reply.Message)&&Delivered(w,reply)&&reply.Recipient==head)
            {if(!MessageFlow.Knows(w.Messaging,reply.Message,head.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(reply.Message,head.Value,at,reply.Message)];RecordOpening(w,head,reply.Message);}
            bool paid=reply!=null&&reply.Action==RansomAction.Pay&&Opened(w,reply,at,rev+1);
            bool refusedLong=reply!=null&&reply.Action==RansomAction.Refuse&&Opened(w,reply,at,rev+1)&&at>=reply.At+NpcMercySeconds;
            if((paid||refusedLong)&&!OwnIdUsed(w,NpcReleaseId(c.Person,c.At))&&CanAct(w,c,head,at,rev))
                Apply(w,new(NpcReleaseId(c.Person,c.At),CaptiveAction.Release,c.Person),head);
        }
    }
    static EntityId NpcExchangeId(EntityId mine,EntityId theirs)=>new("request:npc-captive-exchange/"+Hash(mine.Value+"/"+theirs.Value));
    // c（自分の家が捕らえた相手の家の者）に対して、相手の家が捕らえている自分の家の者（まだ拘束中）。
    static BattleCapture? Mutual(ContractWorld w,BattleCapture c,long at,long rev)=>Captures(w).Select(x=>x.Capture).Where(x=>x.CaptorOwner==c.FromOwner&&x.FromOwner==c.CaptorOwner&&x.Holder!=w.PlayerControl&&HeldAt(w,x.Person,at,rev+1)).OrderBy(x=>x.At).ThenBy(x=>x.Person.Value,StringComparer.Ordinal).Select(x=>(BattleCapture?)x).FirstOrDefault();
    static RansomPaper PrepareExchange(ContractWorld w,EntityId id,BattleCapture mine,BattleCapture theirs,EntityId actor,long at,long rev)
    {using var _ccRev=ControlContext.At(rev);
        Need(NpcCaptors(w)&&id==NpcExchangeId(mine.Person,theirs.Person)&&HeldAt(w,mine.Person,at,rev+1)&&HeldAt(w,theirs.Person,at,rev+1)&&FactionStances.ResolveAuthority(w,actor,mine.CaptorOwner,at,Math.Min(rev,w.CommitSequence))!=null,"引き換えの申し入れが不正です");
        var head=HouseSuccession.Head(w,mine.FromOwner,at,rev);
        return new(id,RansomAction.Exchange,mine.Person,0,null,actor,head,Home(w,mine.FromOwner,at,rev),at,rev,PaperMessage(id)){Other=theirs.Person};
    }
    // 引き換えの申し入れを受けた当主：自分の家の者が戻るので応じ、受諾の返書を出す。相手の家の者は、その城にいるときに放つ。
    static void AnswerExchanges(ContractWorld w)
    {
        if(!NpcCaptors(w))return;long at=w.ElapsedSeconds,rev=w.CommitSequence;
        foreach(var p in Papers(w).Where(p=>p.Action==RansomAction.Exchange&&p.Recipient!=w.PlayerControl).ToArray())
        {
            if(!Openings(w).Any(o=>o.Message==p.Message)&&Delivered(w,p))
            {
                if(!MessageFlow.Knows(w.Messaging,p.Message,p.Recipient.Value))w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(p.Message,p.Recipient.Value,at,p.Message)];
                RecordOpening(w,p.Recipient,p.Message);
                if(HeldAt(w,p.Person,at,rev+1)&&p.Other is {} q&&HeldAt(w,q,at,rev+1))
                {var id=new EntityId("request:npc-captive-exchange-reply/"+Hash(p.Id.Value));Post(w,new RansomPaper(id,RansomAction.AcceptExchange,p.Person,0,p.Id,p.Recipient,p.Author,p.Address,at,rev,PaperMessage(id)){Other=q});}
            }
            // 受諾したら、自分が捕らえている相手の家の者を放つ。
            if(Papers(w).Any(r=>r.Action==RansomAction.AcceptExchange&&r.Source==p.Id)&&p.Other is {} other&&Last(w,other,at,rev+1) is {} held&&EndOf(w,other,held.Revision,at,rev+1)==null)
            {var c=held.Capture;var id=NpcReleaseId(other,c.At);if(!OwnIdUsed(w,id)&&CanAct(w,c,p.Recipient,at,rev))Apply(w,new(id,CaptiveAction.Release,other),p.Recipient);}
        }
    }
    // 相手の当主は届いた書状をすぐ開封し、その場で返書を出す。
    internal static void Update(ContractWorld w)
    {
        NpcCaptorTurns(w);AnswerExchanges(w);
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
            else if(p.Action==RansomAction.Exchange)Need(NpcCaptors(w)&&p.Other is {} q&&Last(w,p.Person,p.At,p.Revision+1) is {} mine&&Last(w,q,p.At,p.Revision+1) is {} theirs&&p==PrepareExchange(w,p.Id,mine.Capture,theirs.Capture,p.Author,p.At,p.Revision),"引き換えの申し入れが一致しません");
            else if(p.Action==RansomAction.AcceptExchange)Need(papers.SingleOrDefault(d=>d.Id==p.Source&&d.Action==RansomAction.Exchange) is {} ex&&openings.Any(o=>o.Message==ex.Message&&o.Actor==ex.Recipient&&o.At==p.At&&o.Revision==p.Revision)&&p.Author==ex.Recipient&&p.Recipient==ex.Author&&p.Person==ex.Person&&p.Other==ex.Other,"引き換えの受諾が一致しません");
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
