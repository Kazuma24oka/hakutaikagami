namespace HakutaiKagami.Core.Contracts;

public sealed partial class WorldSession
{
    [ThreadStatic]internal static bool SlowWorldChangeCopy;
    // 開始設定はセッションの外へ渡さず、ゲーム中は差し替えない。
    // 変更する世界の本体だけを複写し、私的な前後の世代で開始設定を共有する。
    // 読込み・構築とDiagnosticSnapshotは従来どおり全体を深く複写し、外部との共有を作らない。
    private static ContractWorld CopyForChange(ContractWorld source)
    {
        if(SlowWorldChangeCopy||!NationalSystems.On(source,"NpcArmies"))return ContractJson.Copy(source);
        var body=source.ForkTransportLocation();body.NewGame=null;
        var result=ContractJson.Copy(body);result.NewGame=source.NewGame;return result;
    }
}
