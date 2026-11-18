namespace HakutaiKagami.Core.Contracts;

// 近くだけ細かく（2026-11-18、ユーザー判断「その案で直して」。Kenshi と同じ考え方）。一覧に NearPlay のある新規の全国開始だけ。
// ・日課を細かく回すのは、操作する人物から10km以内の人だけ（それより前の世界は30km）。遠くの家と人は、月ごとの家の戦略と書状で動き続ける。
// ・運搬・補給・NPCの軍・知らせへの反応・NPCの日課の判断は、ゲーム内の1分ごとの区切りに回す（何もすることがなくても全国を確かめていたため）。
// ・1分の区切りでは必ず止まる（進める速さや呼び出しの細かさで結果が変わらない）。
public static class NearPlay
{
    public const string Name=NationalSystems.NearPlay;
    public const long Minute=60,RadiusMm=10_000_000;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name);
    internal static bool Skip(ContractWorld w)=>On(w)&&w.ElapsedSeconds%Minute!=0;
    internal static long? NextBoundary(ContractWorld w,long target){if(!On(w))return null;long next=checked((w.ElapsedSeconds/Minute+1)*Minute);return next<=target?next:null;}
}
