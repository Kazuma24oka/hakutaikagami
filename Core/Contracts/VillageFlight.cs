namespace HakutaiKagami.Core.Contracts;

// 出発時の同じ働き手を道中から到着先へ移す。拠点間は既存の陸路の所要時間を使う。
public sealed partial record VillageFlight(string Petition,EntityId From,EntityId To,JourneyLeg[] Route,int People,long Revision)
{
    const long Wait=60*86400L;
    public sealed record Homecoming(EntityId Decree,EntityId Messenger,JourneyLeg[] Route,long Revision);
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Homecoming? Return {get;init;}

    public sealed record AllianceReply(EntityId Representative,long At,long Revision,bool Joined);
    public sealed record AllianceLetter(EntityId Id,EntityId Author,EntityId TaxDecree)
    {
        [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public AllianceReply? Reply {get;init;}
    }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public AllianceLetter? Letter {get;init;}

    static AllianceLetter WriteLetter(CommunityNotice petition,long at)=>new(
        HouseholdCourt.Id("village-alliance",petition.Message),VillageConsultation.Representative(petition.Site,at),petition.Petition!.TaxDecree!.Value);

    static AllianceReply Answer(ContractWorld w,VillageFlight flight,long at,long revision)
    {using var _ccRev=ControlContext.At(revision);
        bool pressured=LandSurvey.EffectiveDecree(w,flight.To,at)?.Input.Rate==LandTaxRate.Heavy;
        long hunger=CommunityEconomy.AccountAt(w,flight.To,at,revision).Hunger;
        return new(VillageConsultation.Representative(flight.To,at),at,revision,pressured||hunger>0);
    }

    static IEnumerable<(long At,int People)> Changes(VillageFlight[]? flights,EntityId site,long rev)
    {using var _ccRev=ControlContext.At(rev);
        foreach(var f in flights??[]){
            if(f.Revision>rev || site!=f.From && site!=f.To)continue;
            bool home=site==f.From;
            yield return (home?f.Route[0].StartAt:f.Route[^1].EndAt,home?-f.People:f.People);
            if(f.Return is {} r && r.Revision<=rev)
                yield return (home?r.Route[^1].EndAt:r.Route[0].StartAt,home?f.People:-f.People);
        }
    }
    internal static int Net(VillageFlight[]? flights,EntityId site,long at,long rev)=>
        Changes(flights,site,rev).Where(c=>c.At<=at).Sum(c=>c.People);
    internal static long LaborSeconds(VillageFlight[]? flights,EntityId site,long from,long until,long rev)=>
        Changes(flights,site,rev).Sum(c=>c.People*Math.Max(0,until-Math.Max(from,c.At)));

    internal static string LocalOutcome(ContractWorld w,EntityId site)
    {
        long now=w.ElapsedSeconds;
        var reports=new List<(long At,string Text)>();
        foreach(var f in w.Communities?.Flights??[]){
            if(f.From==site){
                reports.Add((f.Route[0].StartAt,$"重い年貢を逃れて働き手{f.People}人が村を離れました。"));
                if(f.Return is {} r && r.Route[^1].EndAt<=now)reports.Add((r.Route[^1].EndAt,$"減免を聞いた{f.People}人が村に帰ってきました。"));
            }else if(f.To==site && f.Route[^1].EndAt<=now){
                reports.Add((f.Route[^1].EndAt,$"{NewGameWorld.SiteName(f.From.Value)}から逃れてきた{f.People}人を受け入れました。"));
                if(f.Letter?.Reply is {} reply){
                    string other=NewGameWorld.SiteName(f.From.Value);
                    string answer=reply.Joined?"こちらも窮しています。ともに減免を求めると答えました。":"人々は受け入れましたが、連合への参加は見送りました。";
                    reports.Add((reply.At,$"{other}の代表から檄を受け取りました。{answer}"));
                }
                if(f.Return is {} r)reports.Add((r.Route[0].StartAt,$"避難していた{f.People}人が減免を聞き、帰路につきました。"));
            }
        }
        foreach(var f in w.Communities?.Flights??[]){
            if(f.Proclamation is not {} call)continue;
            if(site==f.To)reports.Add((call.Route[0].StartAt,"石山本願寺から門徒の村へ檄を送りました。"));
            if(site==f.From && call.Answer is {} answer)reports.Add((answer.At,answer.Joined?"本願寺の檄を受け取り、村は蜂起に加わると答えました。":"本願寺の檄を受け取りましたが、村は蜂起を見送りました。"));
        }
        foreach(var f in w.Communities?.Flights??[])
            if(f.From==site && f.Proclamation?.Dispersed is {} d)reports.Add((d.At,Suppression(w,f)!=null?"現地の兵に蜂起を鎮められました。領主への不信が残っています。":"村は蜂起を解き、参加した働き手が生業に戻りました。"));
        return reports.Count==0?"":reports.OrderBy(r=>r.At).Last().Text;
    }

    static IEnumerable<CommunityNotice> Petitions(CommunityState ledger)=>ledger.Notices.Where(n=>n.Petition?.TaxDecree!=null)
        .GroupBy(n=>n.Petition!.TaxDecree).Select(g=>g.First());
    internal static IEnumerable<long> Boundaries(ContractWorld w)=>Petitions(w.Communities!).Select(n=>n.At+Wait)
        .Concat((w.Communities!.Flights??[]).SelectMany(f=>new[]{f.Route[^1].EndAt,f.Return?.Route[^1].EndAt??0,f.Proclamation?.Route[^1].EndAt??0}));

    internal static bool Depart(ContractWorld w)
    {
        var ledger=w.Communities!;
        var flights=(ledger.Flights??[]).ToList();
        foreach(var n in Petitions(ledger)){
            long at=w.ElapsedSeconds;
            if(n.At+Wait!=at || LandSurvey.EffectiveDecree(w,n.Site,at)?.Input.Id!=n.Petition!.TaxDecree || flights.Any(f=>f.Petition==n.Message) ||
                !VillageConsultation.WithholdsLaborAfterPetition(w,n.Site,at,w.CommitSequence+1))continue;
            var origin=NewGameWorld.Node(n.Site.Value);
            var account=ledger.Accounts.Single(a=>a.Site==n.Site);
            int people=Math.Min(10,account.Workforce-account.Reserved);
            if(people<=0)continue;
            var edges=BranchRoutes.GraphEdges(w);
            var blocked=edges.Where(e=>NationalRoutes.Sea(e.Id)||RegionalCampaign.BlockedAt(w,e.Id,at)).Select(e=>e.Id).ToArray();
            JourneyLeg[]? best=null;EntityId destination=default;
            foreach(var target in ledger.Accounts.Where(a=>a.Site!=n.Site).OrderBy(a=>a.Site.Value,StringComparer.Ordinal)){
                string[] path;
                try{path=BranchRoutes.Plan(origin,NewGameWorld.Node(target.Site.Value),blocked,w:w,departureAt:at);}
                catch(InvalidDataException){continue;}
                catch(InvalidOperationException){continue;}
                var route=BranchRoutes.Schedule(origin,path,at,w);
                if(route.Length==0||best!=null&&best[^1].EndAt<=route[^1].EndAt)continue;
                destination=target.Site;best=route;
            }
            if(best!=null)flights.Add(new(n.Message,n.Site,destination,best,people,w.CommitSequence){Letter=WriteLetter(n,at)});
        }
        bool changed=flights.Count!=(ledger.Flights?.Length??0);
        for(int i=0;i<flights.Count;i++){
            var f=flights[i];long at=w.ElapsedSeconds;
            if(f.Letter is {Reply:null} letter && at==f.Route[^1].EndAt){
                f=f with {Letter=letter with {Reply=Answer(w,f,at,w.CommitSequence)}};
                flights[i]=f;changed=true;
            }
            var called=AdvanceProclamation(w,f);
            if(!ReferenceEquals(f,called)){f=called;flights[i]=f;changed=true;}
            if(f.Return!=null || at<f.Route[^1].EndAt || LandSurvey.EffectiveDecree(w,f.From,at) is not {} decree ||
                decree.Input.Rate==LandTaxRate.Heavy || !TransportWork.At(w,decree.Actor,f.To,at,w.CommitSequence+1,true))continue;
            var hosts=ledger.Accounts.Single(a=>a.Site==f.To);
            if(hosts.Workforce-hosts.Reserved<f.People)continue;
            string[] path=f.Route.Reverse().Select(l=>l.Edge).ToArray();
            if(path.Any(id=>RegionalCampaign.BlockedAt(w,id,at)))continue;
            flights[i]=f with {Return=new(decree.Input.Id,decree.Actor,BranchRoutes.Schedule(NewGameWorld.Node(f.To.Value),path,at,w),w.CommitSequence)};
            changed=true;
        }
        if(!changed)return false;
        w.Communities=ledger with { Flights=flights.ToArray() };return true;
    }

    internal static void Validate(ContractWorld w)
    {
        var ledger=w.Communities;
        if(ledger?.Flights==null)return;
        var seen=new HashSet<string>();
        foreach(var f in ledger.Flights){
            var n=ledger.Notices.SingleOrDefault(n=>n.Message==f.Petition);
            if(n?.Petition?.TaxDecree==null || f.From!=n.Site || f.To==f.From || !CommunityEconomy.SitesFor(w).Contains(f.To) ||
                !seen.Add(f.Petition) || f.People is <1 or >10 || f.Route is not {Length:>0} ||
                f.Revision<n.Revision || f.Revision>w.CommitSequence || f.Route[0].StartAt!=n.At+Wait || f.Route[0].StartAt>w.ElapsedSeconds ||
                f.Route.Any(l=>NationalRoutes.Sea(l.Edge)) || RegionalCampaign.Site(f.Route[0].From)!=f.From || RegionalCampaign.Site(f.Route[^1].To)!=f.To ||
                !BranchRoutes.Schedule(f.Route[0].From,f.Route.Select(l=>l.Edge).ToArray(),f.Route[0].StartAt,w).SequenceEqual(f.Route))
                throw new InvalidDataException("逃散の嘆願・人数・陸路・時刻が一致しません");
            var before=CommunityEconomy.AccountAt(w,f.From,f.Route[0].StartAt,f.Revision);
            if(f.People!=Math.Min(10,before.Workforce-before.Reserved) ||
                LandSurvey.EffectiveDecree(w,f.From,f.Route[0].StartAt)?.Input.Id!=n.Petition.TaxDecree ||
                !VillageConsultation.WithholdsLaborAfterPetition(w,f.From,f.Route[0].StartAt,f.Revision+1))
                throw new InvalidDataException("逃散時点の減免嘆願・労働可能人数が一致しません");
            if(f.Letter is {} letter){
                if((letter with {Reply=null})!=WriteLetter(n,f.Route[0].StartAt) ||
                    (letter.Reply!=null)!=(w.ElapsedSeconds>=f.Route[^1].EndAt))
                    throw new InvalidDataException("連合の檄と到着時点が一致しません");
                if(letter.Reply is {} answer && (answer.At!=f.Route[^1].EndAt || answer.Revision<f.Revision ||
                    answer.Revision>w.CommitSequence || answer!=Answer(w,f,answer.At,answer.Revision)))
                    throw new InvalidDataException("受取村の代表と現地事情に基づく返答が一致しません");
            }
            ValidateProclamation(w,f);
            if(f.Return is {} r){
                if(r.Route is not {Length:>0} || r.Revision<f.Revision || r.Revision>w.CommitSequence)
                    throw new InvalidDataException("帰村の経路・版が不正です");
                long start=r.Route[0].StartAt;
                if(start<f.Route[^1].EndAt || start>w.ElapsedSeconds ||
                    LandSurvey.EffectiveDecree(w,f.From,start) is not {} decree || decree.Input.Id!=r.Decree ||
                    decree.Input.Rate==LandTaxRate.Heavy || decree.Actor!=r.Messenger ||
                    !TransportWork.At(w,r.Messenger,f.To,start,r.Revision+1,true) ||
                    !BranchRoutes.Schedule(NewGameWorld.Node(f.To.Value),f.Route.Reverse().Select(l=>l.Edge).ToArray(),start,w).SequenceEqual(r.Route))
                    throw new InvalidDataException("減免の現地伝達と帰村経路が一致しません");
            }
        }
    }
}
