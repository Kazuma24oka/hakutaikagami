namespace HakutaiKagami.Core.Contracts;

public sealed record NpcAspirationProfile(string Actor,NpcAspiration Aspiration,DevelopmentSkill Skill,string Source,string Interpretation);
public static class NpcAspirationProfiles
{
    // 人物の行動傾向のゲーム上の解釈。史料の文面・数値や未来の事件を本人の知識へ入れない。
    static readonly NpcAspirationProfile[] rows=[
        new("person:ujiyasu",NpcAspiration.ProsperDomain,DevelopmentSkill.Agriculture,"https://odawara-digital-museum.jp/selection/hojo/24/","1550年の税制改革の解説を根拠とする民政志向。農業修行への重みはゲーム用の解釈であり、史実の技能値ではない。"),
        new("person:nobunaga",NpcAspiration.EncourageTrade,DevelopmentSkill.Commerce,"https://www.pref.gifu.lg.jp/page/349236.html","1568年の加納市場宛定の解説から商業振興の志向を解釈。未来の市場成立・支配・命令を強制しない。"),
        new("person:shingen",NpcAspiration.ProsperDomain,DevelopmentSkill.Construction,"https://www.city.kai.yamanashi.jp/page/3868.html","信玄堤の治水構想を根拠とする領国基盤への志向。成立年代は伝承を含む。工事や築堤を自動で発生させない。"),
        new("person:yoshishige",NpcAspiration.EncourageTrade,DevelopmentSkill.Commerce,"https://www.city.oita.oita.jp/o157/bunkasports/citypromotion/1369370791117.html","海外交易を通じた経済力の解説を根拠とする交易志向。外交・改宗・収益はこの好みから自動で成立しない。")
    ];
    public static NpcAspirationProfile? For(ContractWorld w, EntityId actor)
    {
        var person = w.NewGame?.Start.People.SingleOrDefault(p => p.Id == actor.Value);
        if (person == null) return null;
        if (!person.Created) return rows.SingleOrDefault(r => r.Actor == actor.Value);
        var choice = w.NewGame!.Registered.SingleOrDefault(p => p.Id == actor.Value)?.Aspiration;
        if (choice is null or NpcAspiration.Unspecified) return null;
        var skill = choice.Value switch
        {
            NpcAspiration.PreserveHouse => DevelopmentSkill.Logistics,
            NpcAspiration.UpholdOrder => DevelopmentSkill.Etiquette,
            NpcAspiration.ProsperDomain => DevelopmentSkill.Agriculture,
            NpcAspiration.EncourageTrade => DevelopmentSkill.Commerce,
            _ => throw new InvalidDataException("登録武将の志が不正です")
        };
        return new(actor.Value, choice.Value, skill, "登録武将の設定", "本人が目指す方向");
    }
}
