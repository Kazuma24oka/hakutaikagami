namespace HakutaiKagami.Core.Contracts;

public sealed partial record VillageFlight
{
    public sealed record TempleCall(EntityId Issuer,EntityId Original,JourneyLeg[] Route,long Revision)
    {
        [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public AllianceReply? Answer {get;init;}
        [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? Mustered {get;init;}
        [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public AllianceReply? Dispersed {get;init;}
    }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TempleCall? Proclamation {get;init;}

    static readonly EntityId Honganji=new("org:ishiyama-honganji");
    // 参加判断の試作用設定。各村の史実の宗派人口を示す値ではない。
    static bool HasFollowers(EntityId village)=>village.Value is "site:sakai" or "site:kyoto";

    static AllianceReply HearTemple(ContractWorld w,VillageFlight f,long at,long rev)
    {using var _ccRev=ControlContext.At(rev);
        bool unresolved=VillageConsultation.WithholdsLaborAfterPetition(w,f.From,at,rev+1);
        bool hungry=CommunityEconomy.AccountAt(w,f.From,at,rev).Hunger>0;
        return new(VillageConsultation.Representative(f.From,at),at,rev,HasFollowers(f.From)&&(unresolved||hungry));
    }

    internal static int ArmedAt(VillageFlight[]? flights,EntityId site,long at,long rev)=>
        (flights??[]).Where(f=>f.From==site && f.Proclamation?.Answer is {} a && a.At<=at && a.Revision<=rev &&
            !(f.Proclamation.Dispersed is {} d && d.At<=at && d.Revision<=rev)).Sum(f=>f.Proclamation!.Mustered??0);

    internal static long ArmedLaborSeconds(VillageFlight[]? flights,EntityId site,long from,long until,long rev)=>
        (flights??[]).Where(f=>f.From==site && f.Proclamation?.Answer is {} a && a.Revision<=rev).Sum(f=>{
            var c=f.Proclamation!;long end=c.Dispersed is {} d && d.Revision<=rev?Math.Min(until,d.At):until;
            return (c.Mustered??0)*Math.Max(0,end-Math.Max(from,c.Answer!.At));
        });

    static VillageFlight AdvanceProclamation(ContractWorld w,VillageFlight f)
    {
        long at=w.ElapsedSeconds;
        if(f.Proclamation is {Answer:null} call && call.Route[^1].EndAt==at)
        {
            var answer=HearTemple(w,f,at,w.CommitSequence);
            var account=CommunityEconomy.AccountAt(w,f.From,at,w.CommitSequence);
            int members=answer.Joined?Math.Clamp(account.Workforce-account.Reserved,0,10):0;
            return f with {Proclamation=call with {Answer=answer,Mustered=members}};
        }
        if(f.Proclamation is {Mustered:>0,Dispersed:null} rising && Suppression(w,f) is {} order)
            return f with {Proclamation=rising with {Dispersed=new(VillageConsultation.Representative(f.From,order.At),order.At,order.Revision,false)}};
        if(f.Proclamation is {Mustered:>0,Dispersed:null,Answer:{} signed} active && at>signed.At){
            var present=HearTemple(w,f,at,w.CommitSequence);
            if(!present.Joined)return f with {Proclamation=active with {Dispersed=present}};
        }
        if(f.Proclamation!=null || f.To.Value!="site:ishiyama" || f.Letter?.Reply is not {Joined:true} consent ||
            consent.At!=at || !HasFollowers(f.From))return f;
        string[] path=f.Route.Reverse().Select(l=>l.Edge).ToArray();
        if(path.Any(id=>RegionalCampaign.BlockedAt(w,id,at)))return f;
        return f with {Proclamation=new(Honganji,f.Letter.Id,BranchRoutes.Schedule(NewGameWorld.Node(f.To.Value),path,at,w),w.CommitSequence)};
    }

    internal static VillageEntry? Suppression(ContractWorld w,VillageFlight f)=>w.Villages?.Entries.SingleOrDefault(e=>
        e.Input.Action==VillageAction.SuppressUprising && e.Input.Site==f.From && f.Letter!=null && e.Input.Proposal==f.Letter.Id);

    static void ValidateProclamation(ContractWorld w,VillageFlight f)
    {
        if(f.Proclamation is not {} call)return;
        if(call.Route is not {Length:>0} || call.Issuer!=Honganji || f.To.Value!="site:ishiyama" || !HasFollowers(f.From) ||
            f.Letter?.Reply is not {Joined:true} consent || call.Original!=f.Letter.Id || call.Revision!=consent.Revision ||
            !BranchRoutes.Schedule(NewGameWorld.Node(f.To.Value),f.Route.Reverse().Select(l=>l.Edge).ToArray(),consent.At,w).SequenceEqual(call.Route) ||
            (call.Answer!=null)!=(call.Route[^1].EndAt<=w.ElapsedSeconds))
            throw new InvalidDataException("本願寺の檄の発信者・村の原書・配送経路が一致しません");
        if(call.Answer is {} answer && (answer.At!=call.Route[^1].EndAt || answer.Revision<call.Revision || answer.Revision>w.CommitSequence ||
            answer!=HearTemple(w,f,answer.At,answer.Revision)))
            throw new InvalidDataException("檄の到着・村代表の応答が一致しません");
        if(call.Mustered is {} count){
            if(call.Answer is not {} answerAt)throw new InvalidDataException("未到着の檄に動員があります");
            var account=CommunityEconomy.AccountAt(w,f.From,answerAt.At,answerAt.Revision);
            if(count!=(answerAt.Joined?Math.Clamp(account.Workforce-account.Reserved,0,10):0))
                throw new InvalidDataException("蜂起人数が村の動ける人手と一致しません");
        }
        if(Suppression(w,f) is {} action){
            if(call.Dispersed!=new AllianceReply(VillageConsultation.Representative(f.From,action.At),action.At,action.Revision,false))
                throw new InvalidDataException("鎮圧の原本と解散時点が一致しません");
        }else if(call.Dispersed is {} d && (call.Mustered is not >0 || d.At<=call.Answer!.At ||
            d.Revision<=call.Answer.Revision || d.Revision>w.CommitSequence || d.At>w.ElapsedSeconds ||
            d.Joined || d!=HearTemple(w,f,d.At,d.Revision)))
            throw new InvalidDataException("村が蜂起を解いた時点と事情が一致しません");
    }
}
