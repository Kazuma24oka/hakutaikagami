namespace HakutaiKagami.Core.Contracts;
public static partial class RegionalBattle
{
    // 地方用の架空三勢力を作らず、全国の既存の軍務・物理命令・交戦履歴を使用する。
    internal static NewMilitaryInput NationalInput(ContractWorld w,RegionalRequest request)
    {
        if(request.Actor!=w.PlayerControl||request.Recipient!=request.Actor||request.Supersedes!=null)
            throw new InvalidDataException("本人の自隊以外は既存の遠隔軍務命令を使用してください");
        return request.Action switch
        {
            RegionalAction.Move=>new(request.Id,NewMilitaryAction.Form,0,request.A,request.B),
            RegionalAction.Hold=>new(request.Id,NewMilitaryAction.Hold),
            RegionalAction.Retreat=>new(request.Id,NewMilitaryAction.Recall),
            _=>throw new InvalidDataException("交戦は敵対認識と軍務許可を確認してください")
        };
    }
    static RegionalView[] NationalView(ContractWorld w,EntityId observer)
    {
        // 本人が持つ既存の時点付き観測だけを投影。他軍は既存のNpcArmies.Sightsで表示する。
        if(observer!=w.PlayerControl)return [];
        return NewGameMilitary.KnownMilitary(w).Select(v=>new RegionalView(v.Unit,v.Position,v.Heading,
            WorldGround.Height(w,v.Position),v.Soldiers,v.Morale??0,v.Phase,v.ObservedAt,v.Live)).ToArray();
    }
}
