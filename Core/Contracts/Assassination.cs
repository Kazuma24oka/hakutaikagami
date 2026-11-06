namespace HakutaiKagami.Core.Contracts;

public sealed record PersonSighting(EntityId Original,EntityId Person,EntityId Site,long ObservedAt);

public static class Assassination
{
    // 同じ街道を通った履歴ではなく、指定時刻に移動中の一行程を調べる。
    public static EntityId? RoadMeeting(ContractWorld world, EntityId actor, EntityId other, long at)
    {
        if (actor == other || world.Branches == null) return null;
        JourneyLeg? ActiveLeg(EntityId person)
        {
            var trip = world.Branches.Journeys.LastOrDefault(j => j.Actor == person && j.Legs[0].StartAt <= at);
            if (trip == null) return null;
            var task = world.Tasks.Single(t => t.Id == trip.Task);
            long positionAt=at;
            if (task.FinishedAt is {} end && end<=at)
            {
                if (task.Execution!=ExecutionPhase.Stopped) return null;
                positionAt=end;
            }
            return trip.Legs.FirstOrDefault(l => l.StartAt <= positionAt && positionAt < l.EndAt && !NationalRoutes.Sea(l.Edge));
        }
        var leg = ActiveLeg(actor);
        if (leg == null || ActiveLeg(other)?.Edge != leg.Edge) return null;
        var a = RegionalCampaign.RoadActorPointAt(world, actor, at);
        var b = RegionalCampaign.RoadActorPointAt(world, other, at);
        decimal east = (decimal)a.EastMm - b.EastMm, north = (decimal)a.NorthMm - b.NorthMm;
        const int reachMm = 20_000;
        return east * east + north * north <= (decimal)reachMm * reachMm ? new EntityId("road:" + leg.Edge) : null;
    }

    public static PersonSighting[] Sightings(ContractWorld world,EntityId observer)
    {
        var rows=InformationSchemes.Events(world);
        var learned=rows.Where(m=>m.Input.Action==SchemeAction.SendScout && m.Actor==observer &&
            HakutaiKagami.Core.Messaging.MessageFlow.Knows(world.Messaging,InformationSchemes.ReportId(m.Input.Id),observer.Value))
            .Select(m=>m.Input.Id).ToHashSet();
        return rows.Where(e=>e.Input.Action==SchemeAction.ObservePerson && (e.Actor==observer ||
                e.Input.Reference is {} source && learned.Contains(source) && world.Evidence.Any(k=>k.Observer==observer && k.Message?.Value==InformationSchemes.ReportId(source) && k.Id==InformationSchemes.Id("received-evidence",InformationSchemes.ReportId(source)+"/"+InformationSchemes.Id("location-evidence",e.Input.Id.Value).Value))))
            .Select(e=>new PersonSighting(e.Input.Id,e.Input.Subject,e.Site,e.At)).ToArray();
    }

    internal static void ScoutPeople(ContractWorld world, SchemeEvent mission, EntityId site)
    {
        if(!CastleSiege.Enabled(world))return;
        foreach(var person in world.Characters.Where(c=>c.Id!=mission.Input.Person && c.Life!=LifeState.Dead &&
            TransportWork.At(world,c.Id,site,world.ElapsedSeconds,world.CommitSequence,true)))
        {
            var input=new SchemeInput(InformationSchemes.Id("observed-person",mission.Input.Id.Value+"/"+person.Id.Value),
                SchemeAction.ObservePerson,mission.Input.Person,person.Id,Reference:mission.Input.Id);
            InformationSchemes.Add(world,input,mission.Input.Person,site);
            var id=InformationSchemes.Id("location-evidence",input.Id.Value);
            world.Evidence=[..world.Evidence,new(id,mission.Input.Person,person.Id,"所在目撃:"+site.Value,world.ElapsedSeconds,world.ElapsedSeconds,null,null)];
            world.Knowledge=[..world.Knowledge,new(mission.Input.Person,id,world.ElapsedSeconds)];
        }
    }

    internal static bool KnownAtDispatch(ContractWorld w,SchemeInput input,EntityId principal,long at,long revision)
    {
        var rows=InformationSchemes.Events(w);
        var sight=rows.SingleOrDefault(e=>e.Input.Id==input.Reference && e.Input.Action==SchemeAction.ObservePerson &&
            e.Input.Subject==input.Subject && e.Site==input.Destination && e.At<=at && e.Revision<revision);
        if(sight==null)return false;
        if(sight.Actor==principal)return true;
        if(sight.Input.Reference is not {} missionId)return false;
        var message=InformationSchemes.ReportId(missionId);
        var source=InformationSchemes.Id("location-evidence",sight.Input.Id.Value);
        return w.Evidence.Any(e=>e.Observer==principal && e.RecordedAt<=at && e.Message?.Value==message &&
            e.Id==InformationSchemes.Id("received-evidence",message+"/"+source.Value));
    }

    internal static SchemeClaim? Witness(ContractWorld world,SchemeEvent outcome,EntityId observer)
    {
        if(!IsConclusion(outcome.Input.Action) || observer==outcome.Actor)return null;
        if(outcome.Input.Subject==observer)
        {
            if(outcome.Input.Action==SchemeAction.FatalEncounter)return null;
        }
        else if(!LocalSecurity.Reserved(world,observer,outcome.At,outcome.Revision) ||
            !TransportWork.At(world,observer,outcome.Site,outcome.At,outcome.Revision,true))return null;
        return new(outcome.Input.Id,observer,outcome.Actor,observer,"現地で襲撃者を目撃した",outcome.At,outcome.Site,"本人の目撃");
    }
    internal static EntityId ExecuteMission(ContractWorld world,SchemeEvent mission,EntityId site,bool arrived)
    {
        var agent=mission.Input.Person;
        var subject=mission.Input.Subject;
        var claim="現地で対象を確認できず、任務を断念した";
        if(arrived && !DeadAt(world,subject,world.ElapsedSeconds,world.CommitSequence) &&
            TransportWork.At(world,subject,site,world.ElapsedSeconds,world.CommitSequence,true))
        {
            SchemeInput Input(string key,SchemeAction action,EntityId? original=null)=>new(
                InformationSchemes.Id(key,mission.Input.Id.Value),action,agent,subject,Reference:original);
            var sight=Input("mission-sighting",SchemeAction.ObservePerson);
            var contact=Input("mission-contact",SchemeAction.SeekContact,sight.Id);
            var attempt=Input("mission-attempt",SchemeAction.AttemptAssassination,contact.Id);
            foreach(var step in new[]{sight,contact,attempt})
            {
                world.CommitSequence=checked(world.CommitSequence+1);
                InformationSchemes.Execute(world,step,agent);
            }
            claim=ResultText(world.Schemes!.Events[^1].Input.Action);
        }
        var id=InformationSchemes.Id("operation-observation",mission.Input.Id.Value);
        world.Evidence=[..world.Evidence,new(id,agent,subject,claim,world.ElapsedSeconds,world.ElapsedSeconds,null,null)];
        world.Knowledge=[..world.Knowledge,new(agent,id,world.ElapsedSeconds)];
        return id;
    }

    internal static string ResultText(SchemeAction result)=>result switch{
        SchemeAction.FatalEncounter=>"対象の死亡を確認した",SchemeAction.CaughtInEncounter=>"護衛に捕縛された",
        SchemeAction.EscapedEncounter=>"接触戦を離脱した",_=>throw new InvalidDataException("接触戦の結果がありません")};

    internal static void ValidateMissionObservation(ContractWorld world,SchemeEvent mission,SchemeEvent capture)
    {
        var id=InformationSchemes.Id("operation-observation",mission.Input.Id.Value);
        var attempt=InformationSchemes.Id("mission-attempt",mission.Input.Id.Value);
        var result=InformationSchemes.Events(world).SingleOrDefault(e=>IsConclusion(e.Input.Action)&&e.Input.Reference==attempt);
        var claim=result==null?"現地で対象を確認できず、任務を断念した":ResultText(result.Input.Action);
        if(!capture.Sources.SequenceEqual(new[]{id}) || !world.Evidence.Any(e=>e.Id==id&&e.Observer==mission.Input.Person&&e.Subject==mission.Input.Subject&&
            e.RecordedAt==capture.At&&e.ObservedAt==capture.At&&e.Message==null&&e.Claim==claim))
            throw new InvalidDataException("暗殺の返報が現地の事実と一致しません");
    }
    public static bool DeadAt(ContractWorld w,EntityId person,long at,long rev) =>
        (w.NaturalDeaths?.Any(d=>d.Person==person && (d.At<at || d.At==at && d.Revision<=rev))??false) ||
        InformationSchemes.Events(w).Any(e=>e.Input.Action==SchemeAction.FatalEncounter && e.Input.Subject==person &&
            (e.At<at || e.At==at && e.Revision<=rev));

    public static SchemeEvent? CaptivityAt(ContractWorld world, EntityId person, long at, long revision)
    {
        var history=InformationSchemes.Events(world).Where(e=>e.At<at || e.At==at && e.Revision<revision);
        var capture=history.LastOrDefault(e=>e.Input.Action==SchemeAction.CaughtInEncounter && e.Actor==person);
        return capture!=null && !history.Any(e=>e.Input.Action==SchemeAction.ReleaseCaptive && e.Input.Reference==capture.Input.Id)
            ? capture : null;
    }

    static bool Together(ContractWorld world, EntityId first, EntityId second, EntityId place, long at, long revision) =>
        place.Value.StartsWith("road:",StringComparison.Ordinal) ? RoadMeeting(world,first,second,at)==place :
        TransportWork.At(world,first,place,at,revision,true) && TransportWork.At(world,second,place,at,revision,true);

    internal static void CheckRelease(ContractWorld world,SchemeInput input,EntityId actor,EntityId site,long at,long rev)
    {
        var capture=CaptivityAt(world,input.Subject,at,rev);
        if(capture==null || capture.Input.Id!=input.Reference || capture.Input.Subject!=actor || capture.Site!=site ||
            input.Person!=actor || input.Destination!=null || input.Topic!=SchemeTopic.ForeignContact ||
            DeadAt(world,actor,at,rev-1) || !Together(world,actor,input.Subject,site,at,rev))
            throw new InvalidDataException("捕縛原本と現地にいる身柄引受人が必要です");
    }
    internal static SchemeAction Clash(ContractWorld world,SchemeInput input,EntityId actor,EntityId site,long at,long rev)
    {
        var contact=InformationSchemes.Events(world).SingleOrDefault(e=>e.Input.Id==input.Reference &&
            e.Input.Action==SchemeAction.SeekContact && e.Actor==actor && e.Input.Subject==input.Subject && e.Site==site && e.Revision<rev);
        if(contact==null || input.Person!=actor || input.Destination!=null || input.Topic!=SchemeTopic.ForeignContact || DeadAt(world,actor,at,rev-1) ||
            DeadAt(world,input.Subject,at,rev-1) || !Together(world,actor,input.Subject,site,at,rev))
            throw new InvalidDataException("接触した対象を現地で再確認してください");
        if(InformationSchemes.Events(world).Any(e=>IsConclusion(e.Input.Action)&&e.Actor==actor&&e.Input.Subject==input.Subject&&e.At/86400==at/86400&&e.Revision<rev))
            throw new InvalidDataException("今日は同じ対象との接触戦を終えています");
        var start=world.NewGame!.Start;
        var attacker=start.People.Single(p=>p.Id==actor.Value);
        var target=start.People.Single(p=>p.Id==input.Subject.Value);
        int guards=site.Value.StartsWith("road:",StringComparison.Ordinal)?0:LocalSecurity.GuardsAt(world,site,at,rev);
        bool headquarters=HouseSuccession.Factions(world,at,rev-1).Any(f=>f.Head==target.Id && f.Home==site.Value);
        int odds=Math.Clamp(42+((attacker.Attributes?.Valor??50)-(target.Attributes?.Valor??50))/2-10*guards-(headquarters?40:0),2,85);
        var key=FormattableString.Invariant($"{world.NewGame.Seed}/{actor.Value}/{target.Id}/{site.Value}/{at/86400}/encounter");
        var draw=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return draw[0]*100/256<odds?SchemeAction.FatalEncounter:draw[1]*100/256<15+Math.Min(50,guards*10)?SchemeAction.CaughtInEncounter:SchemeAction.EscapedEncounter;
    }

    public static bool IsConclusion(SchemeAction action)=>action is SchemeAction.FatalEncounter or SchemeAction.CaughtInEncounter or SchemeAction.EscapedEncounter;

    internal static void Execute(ContractWorld world,SchemeInput input,EntityId actor,EntityId site)
    {
        var outcome=Clash(world,input,actor,site,world.ElapsedSeconds,world.CommitSequence);
        InformationSchemes.Add(world,input,actor,site);
        var result=input with {Id=InformationSchemes.Id("encounter",input.Id.Value),Action=outcome,Reference=input.Id};
        InformationSchemes.Add(world,result,actor,site);
        if(site.Value.StartsWith("road:",StringComparison.Ordinal))
        {
            foreach(var journey in world.Branches!.Journeys.Where(j=>j.Actor==input.Subject || j.Actor==actor))
            {
                var task=world.Tasks.Single(t=>t.Id==journey.Task);
                if(task.Execution==ExecutionPhase.Running && (outcome==SchemeAction.CaughtInEncounter ||
                    outcome==SchemeAction.FatalEncounter && journey.Actor==input.Subject))
                    WorldSession.Stop(world,task,"街道での接触戦により行程終了");
            }
        }
        if(outcome==SchemeAction.FatalEncounter)
            world.Characters=world.Characters.Select(c=>c.Id==input.Subject?c with{Life=LifeState.Dead,Revision=world.CommitSequence}:c).ToArray();
    }    internal static void CheckContact(ContractWorld world,SchemeInput input,EntityId actor,EntityId site,long at,long revision)
    {
        if (!CastleSiege.Enabled(world) || DeadAt(world,actor,at,revision-1) || DeadAt(world,input.Subject,at,revision-1) || input.Subject==actor || input.Person!=actor || input.Destination!=null ||
            input.Topic!=SchemeTopic.ForeignContact || !world.Characters.Any(c=>c.Id==input.Subject))
            throw new InvalidDataException("現地で確かめる相手を指定してください");
        bool road = site.Value.StartsWith("road:",StringComparison.Ordinal);
        if (road ? RoadMeeting(world,actor,input.Subject,at)!=site : !TransportWork.At(world,actor,site,at,revision,true))
            throw new InvalidDataException("自分が現地に到着してから行動してください");
        if (input.Action==SchemeAction.SeekContact)
        {
            var sight=InformationSchemes.Events(world).SingleOrDefault(e=>e.Input.Id==input.Reference && e.Input.Action==SchemeAction.ObservePerson &&
                e.Actor==actor && e.Input.Subject==input.Subject && e.Site==site && e.Revision<revision && e.At<=at);
            if (sight==null) throw new InvalidDataException("この場所で対象を確認した情報が必要です");
        }
        else if(input.Reference is {} original){
            var mission=InformationSchemes.Events(world).SingleOrDefault(e=>e.Input.Id==original && e.Input.Action==SchemeAction.SendScout && e.Input.Person==actor && e.Input.Destination==site && e.Revision<revision);
            if(mission==null || input.Id!=InformationSchemes.Id("observed-person",original.Value+"/"+input.Subject.Value) ||
                !world.Tasks.Any(t=>t.RequestId==mission.Movement!.RequestId && t.Execution==ExecutionPhase.Completed && t.FinishedAt==at))
                throw new InvalidDataException("所在を目撃した派遣と到着時刻が違います");
        }
        if (!road && !TransportWork.At(world,input.Subject,site,at,revision,true))
            throw new InvalidDataException("現地で対象を見つけられませんでした");
    }
}