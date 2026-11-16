using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

// 表示用の推定。初期支配原本・戦略原本・個人知識を変更しない。
public static class NationalControlProjection
{
    static string Key(string province,string district)=>NationalDistrictControl.ProvinceKey(province)+"/"+district.Replace("郡","");
    static readonly Lazy<Dictionary<string,EntityId[]>> districtSites=new(()=>
    {
        var nodes=CastleRoutes.Catalog.Nodes.Where(n=>n.Castle!=null).ToDictionary(n=>n.Castle,n=>new EntityId("site:"+n.Id),StringComparer.Ordinal);
        return nodes.Keys.Select(HakutaiDb.Castle).OfType<HakutaiDbCastle>().Where(c=>c.Province!=null&&c.District!=null&&nodes.ContainsKey(c.Id))
            .GroupBy(c=>Key(c.Province!,c.District!)).ToDictionary(g=>g.Key,g=>g.Select(c=>nodes[c.Id]).Distinct().ToArray(),StringComparer.Ordinal);
    });
    static Dictionary<EntityId,EntityId?> KnownChanges(ContractWorld world,EntityId observer)
    {
        if(world.Strategy is not {} strategy)return new();
        var known=world.Messaging.Messages.Where(m=>m.Kind==NationalStrategy.LetterKind&&(m.SenderId==observer.Value||MessageFlow.Knows(world.Messaging,m.Id,observer.Value)))
            .Select(m=>m.CorrelationId).ToHashSet(StringComparer.Ordinal);
        if(known.Count==0)return new();
        var campaigns=strategy.Campaigns.ToDictionary(c=>c.Id);
        var changed=new Dictionary<EntityId,EntityId?>();
        foreach(var outcome in strategy.Outcomes.Where(o=>o.Captured&&known.Contains(o.Campaign.Value)&&o.At<=world.ElapsedSeconds&&o.Revision<=world.CommitSequence).OrderBy(o=>o.At).ThenBy(o=>o.Revision))
            if(campaigns.TryGetValue(outcome.Campaign,out var campaign))
            {
                var owner=NationalStrategy.Owner(world,campaign.Target,outcome.At,outcome.Revision);
                // 同じ刻・同じ版に未認識の別の落城があっても、その持ち主を漏らさない。
                changed[campaign.Target]=owner==campaign.Attacker?owner:campaign.Attacker;
            }
        return changed;
    }
    public static Dictionary<string,string?> CastleOwnersFor(ContractWorld world,EntityId observer)
    {
        var owners=world.NewGame?.Start.Castles.ToDictionary(c=>c.Id,c=>c.Faction,StringComparer.Ordinal)??new();
        foreach(var change in KnownChanges(world,observer))if(owners.ContainsKey(change.Key.Value))owners[change.Key.Value]=change.Value?.Value;
        return owners;
    }
    public static NationalDistrictControlState? For(ContractWorld world,EntityId observer)
    {
        if(world.DistrictControl is not {} initial)return null;
        var changed=KnownChanges(world,observer);if(changed.Count==0)return initial;
        var starts=world.NewGame!.Start.Castles.ToDictionary(c=>new EntityId(c.Id),c=>c.Faction);
        var districts=HakutaiDb.Districts.ToDictionary(d=>d.Id,StringComparer.Ordinal);
        return initial with{Districts=initial.Districts.Select(record=>
        {
            var district=districts[record.Id];
            if(!districtSites.Value.TryGetValue(Key(district.Province,district.Name),out var sites)||!sites.Any(changed.ContainsKey))return record;
            var owners=sites.Select(site=>changed.TryGetValue(site,out var owner)?owner?.Value:starts.GetValueOrDefault(site))
                .Where(f=>f!=null).Select(f=>NationalStart.DbHouse(f!)).ToArray();
            if(owners.Length==0||owners.Any(h=>h==null))return record;
            var groups=owners.GroupBy(h=>h!,StringComparer.Ordinal).OrderBy(g=>g.Key,StringComparer.Ordinal).ToArray();
            long total=record.Shares.Sum(s=>s.Kokudaka),remaining=total;
            var shares=groups.Select((g,i)=>{long amount=i==groups.Length-1?remaining:total*g.Count()/owners.Length;remaining-=amount;return new NationalDistrictShare(g.Key,amount);}).ToArray();
            return record with{Owner=groups.Length==1?groups[0].Key:null,OwnerBasis="推定：認識済みの城支配",Shares=shares};
        }).ToArray()};
    }
}
