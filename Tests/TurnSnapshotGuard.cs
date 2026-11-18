using System.Runtime.CompilerServices;
using HakutaiKagami.Core.Contracts;

namespace HakutaiKagami.Tests;

// 検査では、進行の中で共有する手番の確認の写しが書き換えられていないことを毎回確かめる（2026-11-12、v107）。
static class TurnSnapshotGuard
{
    [ModuleInitializer]internal static void Enable()=>WorldSession.GuardTurnSnapshotsByDefault=true;
    // 全国の重い検査：その検査の流れの中だけ、遊ぶ画面と同じ設定にする（並べて流す別の検査には効かない）。
    // ・手番ごとの写しの照合を切る（照合そのものは小さな世界の検査で確かめる）
    // ・進めるたびの世界全体の検証を切る（遊ぶ画面と同じく、保存と読み込みでは検証する。全国は1分ごとに区切って進むので、区切りごとの検証が一番重い）
    // using var x=TurnSnapshotGuard.Off();
    internal static IDisposable Off(){bool before=WorldSession.GuardTurnSnapshots;WorldSession.GuardTurnSnapshots=false;WorldSession.ValidateTransitionsHere(false);return new Restore(before);}
    sealed class Restore(bool before):IDisposable{public void Dispose(){WorldSession.GuardTurnSnapshots=before;WorldSession.ValidateTransitionsHere(null);}}
}
