using System.Globalization;

namespace HakutaiKagami.Core.Contracts;

// 暦の表示は共通時計から導出する。保存には経過秒だけを保持する。
// 和暦・旧暦の史料表現とは分離し、世界の計算には既存の西暦日付を使う。
public static class WorldCalendar
{
    public const long SecondsPerDay = 86400;
    public static readonly DateOnly HistoricalEnd = new(1650, 1, 1);

    public static DateTime At(string epochDate, long elapsedSeconds)
    {
        if (!DateOnly.TryParseExact(epochDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var epoch) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "開始日と経過秒が不正です。");
        var start = epoch.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        long maximum = (DateTime.MaxValue.Ticks - start.Ticks) / TimeSpan.TicksPerSecond;
        if (elapsedSeconds > maximum) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        return start.AddTicks(elapsedSeconds * TimeSpan.TicksPerSecond);
    }

    public static long HistoricalEndSeconds(string epochDate)
    {
        var start = At(epochDate, 0);
        return Math.Max(0, (HistoricalEnd.ToDateTime(TimeOnly.MinValue).Ticks - start.Ticks) / TimeSpan.TicksPerSecond);
    }

    public static string Display(string epochDate, long elapsedSeconds) =>
        At(epochDate, elapsedSeconds).ToString("yyyy年M月d日 HH:mm:ss", CultureInfo.InvariantCulture);
}
