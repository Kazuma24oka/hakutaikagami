using System.Reflection;
using HakutaiKagami.Core.Contracts;
using Xunit;
using Xunit.Abstractions;
namespace HakutaiKagami.Tests;
[Trait("Tier","Probe")]
public sealed class NationalCoverageProbe(ITestOutputHelper o)
{
    [Fact]
    public void ListEnabledSystems()
    {
        if(Environment.GetEnvironmentVariable("HAKUTAI_COVERAGE_PROBE")!="1")return;
        var w=NationalStart.Create(NationalStartTests.Nobunaga());
        var rows=new List<string>();
        foreach(var t in typeof(ContractWorld).Assembly.GetTypes().Where(t=>t.IsClass&&t.IsAbstract&&t.IsSealed))
        {
            var m=t.GetMethod("Enabled",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,[typeof(ContractWorld)],null);
            if(m==null||m.ReturnType!=typeof(bool))continue;
            bool on;try{on=(bool)m.Invoke(null,[w])!;}catch{continue;}
            rows.Add((on?"ON  ":"off ")+t.Name);
        }
        foreach(var r in rows.OrderBy(r=>r))o.WriteLine(r);
        File.WriteAllLines(Environment.GetEnvironmentVariable("HAKUTAI_COVERAGE_OUT")!,rows.OrderBy(r=>r));
    }
}
