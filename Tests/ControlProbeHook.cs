using System.Runtime.CompilerServices;
using HakutaiKagami.Core.Contracts;

namespace HakutaiKagami.Tests;

// 調査用：環境変数 HAKUTAI_CONTROL_PROBE にファイル名があれば、検査の中で操作する人物を読んだ場所を書き出す。
static class ControlProbeHook
{
    [ModuleInitializer]internal static void Enable()
    {
        var handover=Environment.GetEnvironmentVariable("HAKUTAI_HANDOVER_PROBE");
        if(!string.IsNullOrEmpty(handover)){HandoverProbe.Enabled=true;AppDomain.CurrentDomain.ProcessExit+=(_,_)=>File.WriteAllLines(handover,HandoverProbe.Failures());}
        var path=Environment.GetEnvironmentVariable("HAKUTAI_CONTROL_PROBE");if(string.IsNullOrEmpty(path))return;
        ControlProbe.Enabled=true;
        AppDomain.CurrentDomain.ProcessExit+=(_,_)=>File.WriteAllLines(path,ControlProbe.Hits());
    }
}
