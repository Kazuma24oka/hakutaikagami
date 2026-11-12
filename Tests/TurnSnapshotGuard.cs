using System.Runtime.CompilerServices;
using HakutaiKagami.Core.Contracts;

namespace HakutaiKagami.Tests;

// 検査では、進行の中で共有する手番の確認の写しが書き換えられていないことを毎回確かめる（2026-11-12、v107）。
static class TurnSnapshotGuard
{
    [ModuleInitializer]internal static void Enable()=>WorldSession.GuardTurnSnapshots=true;
}
