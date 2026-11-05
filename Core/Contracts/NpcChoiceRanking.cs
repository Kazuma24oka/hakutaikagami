// 0b共通評価。呼出側は本人の知識と既存の行動契約で候補を構築する。
// 世界全体を受け取らず、本人に既知かつ実行可能な候補だけを比較する境界。
namespace HakutaiKagami.Core.Contracts;

public enum NpcAttention { Beginner, Standard, Equal }
public enum NpcCadence { Daily, Weekly, Monthly }
public enum NpcAspiration { Unspecified, PreserveHouse, UpholdOrder, ProsperDomain, EncourageTrade }
public sealed record NpcJudgmentSettings(string Policy,HistoryMode History,NpcAttention Attention);
public sealed record NpcPersonality(int Ambition,int Duty,int Caution,int Courage,int Affection,int GainSeeking);
public sealed record NpcChoiceCandidate(string Id,bool Known,bool Permitted,int Utility,
    NpcPersonality Weights,bool MatchesAspiration,bool MatchesHistoricalGoal,bool PlayerRecipient);
public sealed record NpcChoiceInput(EntityId Actor,ulong Seed,long At,NpcPersonality Personality,
    NpcAspiration Aspiration,HistoryMode History,NpcAttention Attention,NpcChoiceCandidate[] Candidates);

// 候補の許可は既存の行動契約が判定する。点数は許可・同意・在庫を作らない。
// 重みは史実の数値ではなく版付きのゲーム用調整値。公開人物カードには載せない。
public static class NpcChoiceRanking
{
    static void Check([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
    static int[] Values(NpcPersonality p)=>[p.Ambition,p.Duty,p.Caution,p.Courage,p.Affection,p.GainSeeking];
    public static int HistoricalWeight(HistoryMode mode)=>mode switch{
        HistoryMode.Historical=>40,HistoryMode.SemiHistorical=>12,HistoryMode.Fictional=>0,
        _=>throw new ArgumentOutOfRangeException(nameof(mode))};
    public static int RecipientWeight(NpcAttention mode)=>mode switch{
        NpcAttention.Beginner=>20,NpcAttention.Standard=>5,NpcAttention.Equal=>0,
        _=>throw new ArgumentOutOfRangeException(nameof(mode))};
    static long Score(NpcChoiceInput input,NpcChoiceCandidate candidate)
    {
        var p=input.Personality;var k=candidate.Weights;
        // 各性格0〜100、係数-10〜10を入口で検査する。
        long trait=((long)(p.Ambition-50)*k.Ambition+(p.Duty-50)*k.Duty+
            (p.Caution-50)*k.Caution+(p.Courage-50)*k.Courage+
            (p.Affection-50)*k.Affection+(p.GainSeeking-50)*k.GainSeeking)/50;
        return candidate.Utility+trait+(candidate.MatchesAspiration&&input.Aspiration!=NpcAspiration.Unspecified?15:0)+
            (candidate.MatchesHistoricalGoal?HistoricalWeight(input.History):0)+
            (candidate.PlayerRecipient?RecipientWeight(input.Attention):0);
    }
    public static string? Choose(NpcChoiceInput input)
    {
        Check(input!=null&&input.Actor.Value!=null&&input.Personality!=null&&input.At>=0&&Enum.IsDefined(input.Aspiration)&&Enum.IsDefined(input.History)&&Enum.IsDefined(input.Attention),"判断設定が不正です");
        Check(Values(input.Personality).All(v=>v>=0&&v<=100),"性格の範囲が不正です");
        Check(input.Candidates!=null&&input.Candidates.All(c=>c!=null&&!string.IsNullOrWhiteSpace(c.Id)&&c.Utility is >=-1000 and <=1000&&c.Weights!=null&&Values(c.Weights).All(v=>v is >=-10 and <=10)),"候補の範囲が不正です");
        Check(input.Candidates!.Select(c=>c.Id).Distinct(StringComparer.Ordinal).Count()==input.Candidates.Length,"候補が重複しています");
        string Tie(NpcChoiceCandidate c)=>ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(FormattableString.Invariant($"npc-choice-v1/{input.Seed}/{input.Actor.Value}/{input.At}/{c.Id}")));
        return input.Candidates.Where(c=>c.Known&&c.Permitted)
            .OrderByDescending(c=>Score(input,c)).ThenBy(Tie,StringComparer.Ordinal).ThenBy(c=>c.Id,StringComparer.Ordinal)
            .Select(c=>c.Id).FirstOrDefault();
    }
    public static long NextScheduledReview(string epoch,long at,NpcCadence cadence)
    {
        Check(at>=0&&Enum.IsDefined(cadence),"判断の間隔が不正です");
        if(cadence!=NpcCadence.Monthly){long interval=cadence==NpcCadence.Daily?86400:604800;return checked((at/interval+1)*interval);}
        var now=WorldCalendar.At(epoch,at);var next=new DateTime(now.Year,now.Month,1).AddMonths(1);
        return checked((next.Ticks-WorldCalendar.At(epoch,0).Ticks)/TimeSpan.TicksPerSecond);
    }
    // 同点はSeed+Actor+判断時刻+候補IDの固定ハッシュ。列挙順や描画頻度で変えない。
    // 既知の命令・実行中の仕事は、この裁量比較より前に扱う。
}
