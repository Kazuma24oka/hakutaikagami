namespace HakutaiKagami.Core.Contracts;

// 遠方の人物の休眠（設計 Docs/Proposals/NATIONAL_PLACEMENT_DESIGN_V1.md 第4節3、2026-11-12 v108）。
// 版を持たない世界（今までの開始と旧保存）は全員が日課の手番を持つまま。
// 版のある世界では、世界時計の1時間ごとの区切りの時刻に、操作する人物と本人の道の上の位置が30km以内の人物だけが、
// 次の区切りまで日課の手番を持つ（起きている）。遠方の人物は判断をせず、判断の記録も作らない。
// 区切りは世界時計で決まり、進める速さや呼び出しの細かさで起きる人は変わらない（同じ入力から同じ未来）。
public static class NpcDormancy
{
    public const string Policy="player-nearby-hourly-30km-v1";
    public const long Interval=3600;
    public const long RadiusMm=30_000_000;
    public static bool Enabled(ContractWorld w)=>Enabled(w.NewGame);
    public static bool Enabled(NewGameState? data)=>data?.NpcDormancyPolicy==Policy;
    public static void ValidatePolicy(NewGameState data)
    {
        if(data.NpcDormancyPolicy is not(null or Policy))throw new InvalidDataException("休眠の版が不正です");
        if(data.NpcDormancyPolicy!=null&&!NewGameNpcRoutine.ContinuingRules(data.NpcRoutinePolicy))throw new InvalidDataException("休眠は続けての日課の世界だけで使えます");
    }
    public static long Checkpoint(long at)=>at/Interval*Interval;
    public static bool Awake(ContractWorld w,EntityId actor,long at)
    {
        if(!Enabled(w))return true;
        long c=Checkpoint(at);return Near(RegionalCampaign.RoadActorPointAt(w,w.PlayerControl,c),RegionalCampaign.RoadActorPointAt(w,actor,c));
    }
    static bool Near(RoutePoint p,RoutePoint q){long dx=p.EastMm-q.EastMm,dy=p.NorthMm-q.NorthMm;return dx*dx+dy*dy<=RadiusMm*RadiusMm;}
    // 多くの人物をまとめて調べる（操作する人物の位置は1回だけ求める。結果は Awake と同じ）。
    public static Func<EntityId,bool> AwakeAt(ContractWorld w,long at)
    {
        if(!Enabled(w))return _=>true;
        long c=Checkpoint(at);var p=RegionalCampaign.RoadActorPointAt(w,w.PlayerControl,c);
        return actor=>Near(p,RegionalCampaign.RoadActorPointAt(w,actor,c));
    }
    // 次の区切りの時刻（起きる人が変わりうる時刻）。この時刻に必ず一度止まり、起きた人に手番を渡す。
    public static long? NextBoundary(ContractWorld w,long target)
    {
        if(!Enabled(w))return null;
        long next=checked(Checkpoint(w.ElapsedSeconds)+Interval);
        return next<=target?next:null;
    }
}
