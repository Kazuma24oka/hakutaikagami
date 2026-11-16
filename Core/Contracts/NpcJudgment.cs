namespace HakutaiKagami.Core.Contracts;

public sealed record NpcJudgmentInputs(NpcCadence Cadence,long ReadyAt,bool Due,NpcChoiceInput ChoiceInput,string? Choice,NpcChoiceInput? RecipientInput,EntityId? Recipient);
public static class NpcJudgment
{
    public const string Policy="own-known-personality-scheduled-choice-v4";
    public static bool Enabled(ContractWorld w)=>w.NewGame?.NpcRoutinePolicy==Policy;
    internal static void ValidateSettings(NewGameState data)
    {
        var s=data.Judgment;
        if(data.NpcRoutinePolicy!=Policy){if(s!=null)throw new InvalidDataException("旧NPC方針に判断設定を追加できません");return;}
        if(s==null||s.EventEdition is not (null or HistoricalEventRules.OwariEdition)||s.Policy!=Policy||!Enum.IsDefined(s.History)||!Enum.IsDefined(s.Attention)||s.PracticeForecastVersion is not (null or 2)||s.HealthEdition is not (null or PersonalHealth.Edition)||(s.PracticeForecastVersion != null && s.Challenge != NpcChallenge.Strong)||(s.Challenge is {} challenge && !Enum.IsDefined(challenge))||(s.Support is {} level && !Enum.IsDefined(level))||!CharacterDevelopment.IsPolicy(data.DevelopmentPolicy)||data.SocialPolicy!=CharacterRelations.Policy)
            throw new InvalidDataException("NPC判断には版付き設定・修行・性格の方針が必要です");
    }
    public static NpcPersonality Personality(ContractWorld w,EntityId actor)=>new(
        CharacterRelations.DiagnosticTrait(w,actor,SocialTrait.Ambition),CharacterRelations.DiagnosticTrait(w,actor,SocialTrait.Duty),
        CharacterRelations.DiagnosticTrait(w,actor,SocialTrait.Caution),CharacterRelations.DiagnosticTrait(w,actor,SocialTrait.Courage),
        CharacterRelations.DiagnosticTrait(w,actor,SocialTrait.Affection),CharacterRelations.DiagnosticTrait(w,actor,SocialTrait.GainSeeking));
    // 当主の一覧は、同じ世界・時点・版で多くの人物について続けて問うので直前の答えを覚える（手番と区切りの計算の中だけで使い、変更の途中では使わない）。
    [ThreadStatic] static (ContractWorld? W,long At,long Rev,long Commit,long? Ctx,HashSet<string>? Set) lastHeads;
    // 操作する人物の位置も、同じ世界・時点・版で多くの人物について続けて問うので直前の答えを覚える（手番と区切りの計算の中だけ）。
    [ThreadStatic] static (ContractWorld? W,long At,long Rev,long Commit,long? Ctx,RoutePoint? P) lastPlayer;
    static RoutePoint PlayerPoint(ContractWorld w,ContractWorld history,long at,long revision){var k=lastPlayer;if(k.P!=null&&ReferenceEquals(k.W,w)&&k.At==at&&k.Rev==revision&&k.Commit==w.CommitSequence&&k.Ctx==ControlContext.Revision)return k.P;var p=RegionalCampaign.RoadActorPointAt(history,w.PlayerControl,at);lastPlayer=(w,at,revision,w.CommitSequence,ControlContext.Revision,p);return p;}
    static HashSet<string> Heads(ContractWorld w,long at,long revision){var k=lastHeads;if(k.Set!=null&&ReferenceEquals(k.W,w)&&k.At==at&&k.Rev==revision&&k.Commit==w.CommitSequence&&k.Ctx==ControlContext.Revision)return k.Set;var set=HouseSuccession.Factions(w,at,revision).Select(f=>f.Head).ToHashSet(StringComparer.Ordinal);lastHeads=(w,at,revision,w.CommitSequence,ControlContext.Revision,set);return set;}
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NewGameState,HashSet<string>> localLords=new();
    static HashSet<string> LocalLords(NewGameState data)=>localLords.GetValue(data,d=>d.Start.People.Where(p=>p.InitialRole==StartTemplate.LocalLord).Select(p=>p.Id).ToHashSet(StringComparer.Ordinal));
    public static NpcCadence CadenceAt(ContractWorld w,EntityId actor,long at,long revision)
    {using var _ccRev=ControlContext.At(revision);
        // 近接は計算頻度だけに使う。人物の判断候補へ主人公の位置を渡さない。
        // 版より後に出した依頼の旅を除いた世界で位置を求める。今の版（以後の依頼がない）なら除くものはないので、写さずにそのまま使う。
        var history=revision>=w.CommitSequence?w:w.ForkTransportLocation();if(!ReferenceEquals(history,w))history.Branches=w.Branches! with{Journeys=w.Branches!.Journeys.Where(j=>w.Tasks.Any(t=>t.Id==j.Task&&w.Requests.Any(r=>r.Id==t.RequestId&&r.CommitSequence<=revision))).ToArray()};
        var site=RegionalCampaign.RoadActorPointAt(history,actor,at);
        var player=PlayerPoint(w,history,at,revision);
        if(site==player||Heads(w,at,revision).Contains(actor.Value)||LocalLords(w.NewGame!).Contains(actor.Value))return NpcCadence.Daily;
        return WorldIndex.RelationsOf(w.Memberships,actor).Any(m=>m.From<=at&&(at<m.Until||at==m.Until&&revision<m.Revision))?NpcCadence.Weekly:NpcCadence.Monthly;
    }
    static NpcPersonality SkillWeights(DevelopmentSkill s)=>s switch{
        DevelopmentSkill.Agriculture=>new(0,3,2,0,3,1),DevelopmentSkill.Commerce=>new(2,0,1,0,1,4),
        DevelopmentSkill.Construction=>new(1,3,3,0,1,0),DevelopmentSkill.Logistics=>new(0,4,3,0,2,1),
        DevelopmentSkill.Tactics=>new(3,1,0,4,0,0),DevelopmentSkill.IntelligenceGathering=>new(2,0,4,1,0,2),
        DevelopmentSkill.Etiquette=>new(1,3,1,0,4,0),
        // 32技能の版で加えた技能（性格のどれに好まれるか。試作の重み）。
        DevelopmentSkill.Siege=>new(3,1,2,3,0,0),DevelopmentSkill.Drill=>new(2,3,1,2,0,0),DevelopmentSkill.Navy=>new(2,1,1,3,0,1),
        DevelopmentSkill.Swordsmanship=>new(2,1,0,4,0,0),DevelopmentSkill.Spear=>new(1,2,0,4,0,0),DevelopmentSkill.Bow=>new(1,1,2,3,0,0),DevelopmentSkill.Cavalry=>new(2,1,0,4,0,0),DevelopmentSkill.Gun=>new(2,0,2,2,0,1),
        DevelopmentSkill.Finance=>new(1,2,3,0,0,3),DevelopmentSkill.Adjudication=>new(0,4,3,0,1,0),DevelopmentSkill.Irrigation=>new(0,3,3,0,2,1),
        DevelopmentSkill.Negotiation=>new(2,1,1,0,2,2),DevelopmentSkill.Mediation=>new(0,3,2,0,3,0),DevelopmentSkill.TalentAssessment=>new(2,2,2,0,1,0),
        DevelopmentSkill.Subversion=>new(4,0,1,1,0,2),DevelopmentSkill.Guard=>new(0,4,2,2,0,0),DevelopmentSkill.Stealth=>new(2,0,3,1,0,1),
        DevelopmentSkill.Mining=>new(1,1,1,0,0,4),DevelopmentSkill.Workshop=>new(1,2,1,0,0,3),DevelopmentSkill.Appraisal=>new(1,0,2,0,1,3),
        DevelopmentSkill.Tea=>new(2,1,1,0,3,1),DevelopmentSkill.Renga=>new(1,1,1,0,4,0),DevelopmentSkill.Teaching=>new(0,3,1,0,4,0),DevelopmentSkill.Writing=>new(1,3,2,0,1,0),DevelopmentSkill.Medicine=>new(0,3,2,0,4,0),
        _=>throw new ArgumentOutOfRangeException(nameof(s))};
    internal static NpcWorkInputs Apply(ContractWorld w,EntityId actor,long at,long revision,NpcWorkInputs basis)
    {using var _ccRev=ControlContext.At(revision);
        var cadence=CadenceAt(w,actor,at,revision);
        var own=w.Requests.Where(r=>r.CreatedAt<=at&&r.CommitSequence<=revision&&r.Intent.Actor==actor&&(r.Intent.Executor==actor||r.Intent.Action==ActionKind.Inspect)&&r.Intent.Action is ActionKind.Inspect or ActionKind.Train).OrderBy(r=>r.CreatedAt).ThenBy(r=>r.CommitSequence).LastOrDefault();
        long ready=own==null?0:NpcChoiceRanking.NextScheduledReview(w.EpochDate,own.CreatedAt,cadence);
        var aspiration=NpcAspirationProfiles.For(w,actor);
        var purpose=basis.Routine!.Purpose!;var candidates=new List<NpcChoiceCandidate>();
        bool due=at>=ready;
        if(due&&basis.CooldownElapsed&&basis.Routine.Need?.Needed==true)candidates.Add(new("inspect",true,basis.InspectionPermit&&PersonalHealth.Can(w,actor,HealthLimit.Travel,at),basis.Routine.Need.MissingEdges.Length>0?45:10,new(0,3,3,0,1,0),false,false,false));
        if(due&&purpose.PracticeDue&&basis.Routine.Need?.MissingEdges.Length is not >0)foreach(var skill in CharacterDevelopment.Skills(w))candidates.Add(new("practice:"+skill,true,basis.InspectionPermit&&PersonalHealth.Can(w,actor,HealthLimit.Practice,at),15,SkillWeights(skill),aspiration?.Skill==skill,aspiration?.Skill==skill && !w.NewGame!.Registered.Any(p => p.Id == actor.Value),false));
        var settings=w.NewGame!.Judgment!;var mode=w.TimeControl?.ContinuedAt is {} continued&&continued<at?HistoryMode.Fictional:settings.History;
        // 同じ裁量窓では時刻の刻みを同点処理へ混ぜない。
        var input=new NpcChoiceInput(actor,w.Seed,ready,Personality(w,actor),aspiration?.Aspiration??NpcAspiration.Unspecified,mode,settings.Attention,candidates.ToArray());
        if(candidates.Any(c=>c.Id=="inspect" && c.Permitted) && HistoricalEventRules.OwariScouting(w,actor,at,revision) is {} opportunity)
            input=HistoricalEventRules.Apply(input,[opportunity],actor==w.PlayerControl,mode==HistoryMode.Fictional);
        if (settings.Challenge is {} challenge)
        {
            int limit = challenge switch { NpcChallenge.Mild => 2, NpcChallenge.Standard => candidates.Count, NpcChallenge.Strong => candidates.Count, _ => throw new InvalidDataException("未知の手強さです") };
            var considered = input.Candidates.Where(c => c.Known && c.Permitted)
                .OrderByDescending(c => c.Utility)
                .ThenByDescending(c => c.MatchesAspiration)
                .ThenBy(c => c.Id, StringComparer.Ordinal)
                .Take(limit).Select(candidate =>
                {
                    if (challenge != NpcChallenge.Strong || !candidate.Id.StartsWith("practice:", StringComparison.Ordinal)) return candidate;
                    var skill = Enum.Parse<DevelopmentSkill>(candidate.Id[9..]);
                    int benefit = CharacterDevelopment.ForecastPractice(w, actor, skill, at, settings.PracticeForecastVersion == 2);
                    return candidate with { Utility = candidate.Utility + benefit };
                }).ToArray();
            input = input with { Candidates = considered };
        }
        var choice=NpcChoiceRanking.Choose(input);
        if(choice?.StartsWith("practice:",StringComparison.Ordinal)==true)purpose=purpose with{Skill=Enum.Parse<DevelopmentSkill>(choice[9..])};
        var recipients=choice=="inspect"?NpcLocalOffers.Recipients(w,actor,at,revision,ready):null;
        var selected=recipients==null?null:NpcChoiceRanking.Choose(recipients);
        return basis with{Routine=basis.Routine with{Policy=Policy,Purpose=purpose,Judgment=new(cadence,ready,due,input,choice,recipients,selected==null?null:new EntityId(selected))}};
    }
}
