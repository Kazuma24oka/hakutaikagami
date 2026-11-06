using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

public sealed record FamilyBirthRecord(EntityId Child,EntityId FirstParent,EntityId SecondParent,
    EntityId Marriage,EntityId Site,long At,long Revision);

public static class FamilyBirth
{
    const long Day=86400;
    static EntityId ChildId(EntityId marriage,long at)=>new("person:born/"+ContractJson.Hash(
        Encoding.UTF8.GetBytes(FormattableString.Invariant($"{marriage.Value}/{at}")))[..24]);
    // 仮想出生の試作値。実施済み婚姻ごとに一年に一回候補を求める。
    static IEnumerable<(MarriageTerms Terms,long At)> Candidates(ContractWorld w,long from,long until)
    {
        if(w.NewGame is not {AdultAge:FamilyKin.ComingOfAge} data || w.Marriage==null)return [];
        if(data.Judgment?.History==HistoryMode.Historical && !(w.TimeControl?.ContinuedAt<until))return [];
        var result=new List<(MarriageTerms,long)>();
        foreach(var marriage in w.Marriage.Entries.Where(e=>e.Input.Action==MarriageAction.Implement))
        {
            var terms=w.Marriage.Entries.Single(e=>e.Input.Id==marriage.Input.Original).Terms!;
            for(long cycle=Math.Max(0,(from-marriage.At)/Day/365-1); cycle<=(until-marriage.At)/Day/365;cycle++)
            {
                var key=FormattableString.Invariant($"family-birth-v1/{data.Seed}/{terms.Id.Value}/{cycle}");
                var digest=SHA256.HashData(Encoding.UTF8.GetBytes(key));
                if(digest[0]>=96)continue;
                long due=marriage.At+(cycle*365+280+BinaryPrimitives.ReadUInt16LittleEndian(digest.AsSpan(1))%60)*Day;
                if(from<due && due<=until && !Assassination.DeadAt(w,terms.First,due,w.CommitSequence) &&
                    !Assassination.DeadAt(w,terms.Second,due,w.CommitSequence))result.Add((terms,due));
            }
        }
        return result;
    }

    internal static long? Next(ContractWorld world,long target)=>
        Candidates(world,world.ElapsedSeconds,target).Select(p=>(long?)p.At).Min();

    internal static void Apply(ContractWorld w)
    {
        foreach(var (terms,at) in Candidates(w,w.ElapsedSeconds-1,w.ElapsedSeconds))
        {
            var site=SocialWorld.Site(w,terms.First);
            if(site==null || SocialWorld.Site(w,terms.Second)!=site)continue;
            var child=ChildId(terms.Id,at);
            if(w.Births?.Any(b=>b.Child==child)==true)continue;
            w.Births=[..w.Births??[],new(child,terms.First,terms.Second,terms.Id,site.Value,at,w.CommitSequence)];
        }
    }

    internal static void Validate(ContractWorld world,ContractWorld? prior)
    {
        var births=world.Births??[];
        if(prior?.Births is {} previous && !births.Take(previous.Length).SequenceEqual(previous))
            throw new InvalidDataException("既存の出生記録が変更されています");
        if(births.Select(b=>b.Child).Distinct().Count()!=births.Length)throw new InvalidDataException("出生記録が重複しています");
        foreach(var b in births)
        {
            if(b.At<=0 || b.At>world.ElapsedSeconds || b.Revision<=0 || b.Revision>world.CommitSequence ||
                b.Child!=ChildId(b.Marriage,b.At) ||
                !Candidates(world,b.At-1,b.At).Any(c=>c.Terms.Id==b.Marriage && c.Terms.First==b.FirstParent && c.Terms.Second==b.SecondParent) ||
                !TransportWork.At(world,b.FirstParent,b.Site,b.At,b.Revision,true) ||
                !TransportWork.At(world,b.SecondParent,b.Site,b.At,b.Revision,true))
                throw new InvalidDataException("出生時の親子・所在地・婚姻原本が一致しません");
        }
    }
}
