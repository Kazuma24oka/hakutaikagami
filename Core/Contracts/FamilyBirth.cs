using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

public sealed record FamilyBirthRecord(EntityId Child,EntityId FirstParent,EntityId? SecondParent,
    EntityId? Marriage,EntityId Site,long At,long Revision);

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

    static readonly EntityId WinterPrincess=new("person:fuyuhime");
    static FamilyBirthRecord? Historical(ContractWorld w,long from,long until,long revision)
    {using var _ccRev=ControlContext.At(revision);
        if(w.NewGame is not {AdultAge:FamilyKin.ComingOfAge} data ||
            data.Judgment?.History is not (HistoryMode.Historical or HistoryMode.SemiHistorical))return null;
        var father=data.Start.People.SingleOrDefault(p=>p.Id=="person:nobunaga");
        if(father==null || data.Start.People.Any(p=>p.Id==WinterPrincess.Value))return null;
        // 生年には異説がある。月日と配置は開始シナリオの規約。
        long at=(long)(new DateOnly(1561,7,1).DayNumber-DateOnly.ParseExact(w.EpochDate,"yyyy-MM-dd").DayNumber)*Day;
        if(at<=0 || at<=from || at>until || Assassination.DeadAt(w,new(father.Id),at,revision))return null;
        return new(WinterPrincess,new(father.Id),null,null,new(father.Site),at,revision);
    }

    internal static long? Next(ContractWorld world,long target)=>
        Candidates(world,world.ElapsedSeconds,target).Select(p=>(long?)p.At)
        .Append(Historical(world,world.ElapsedSeconds,target,world.CommitSequence)?.At).Min();

    internal static void Apply(ContractWorld w)
    {
        if(Historical(w,w.ElapsedSeconds-1,w.ElapsedSeconds,w.CommitSequence) is {} historical)Add(w,historical);
        foreach(var (terms,at) in Candidates(w,w.ElapsedSeconds-1,w.ElapsedSeconds))
        {
            var site=SocialWorld.Site(w,terms.First);
            if(site!=null && SocialWorld.Site(w,terms.Second)==site)
                Add(w,new(ChildId(terms.Id,at),terms.First,terms.Second,terms.Id,site.Value,at,w.CommitSequence));
        }
    }

    static void Add(ContractWorld w,FamilyBirthRecord birth)
    {
        var child=birth.Child;
        if(w.Births?.Any(b=>b.Child==child)==true)return;
        w.Births=[..w.Births??[],birth];
        w.Grants=[..w.Grants,..NewGameWorld.BirthGrants(w,birth)];
        w.Characters=[..w.Characters,new(child,LifeState.Minor,Appearance(w,birth),w.CommitSequence)];
        w.Locations=[..w.Locations,new(child,LocationKind.AtSite,birth.Site,null,0,0,null,null,null,null,w.CommitSequence)];
        w.Definitions=w.Definitions with{Characters=[..w.Definitions.Characters,new(child,Person(w,birth).Name)]};
        FamilyNotice.AnnounceBirth(w,birth);
    }

    public static SetupPerson Person(ContractWorld world,FamilyBirthRecord birth)
    {
        var index=Array.FindIndex(world.Births!,b=>b.Child==birth.Child);
        return new(birth.Child.Value,birth.Child==WinterPrincess?"冬姫":"若君"+(index+1),birth.Site.Value,null,null,true,false,
            WorldCalendar.At(world.EpochDate,birth.At).Year,new OfficerAttributes(50,50,50,50),StartTemplate.Ronin)
            {BirthParents=birth.SecondParent is {} second?[birth.FirstParent.Value,second.Value]:[birth.FirstParent.Value]};
    }

    public static IEnumerable<SetupPerson> People(ContractWorld world)=>world.NewGame!.Start.People
        .Concat((world.Births??[]).Select(b=>Person(world,b)));

    static EntityId Appearance(ContractWorld w,FamilyBirthRecord birth)=>
        new("appearance:"+ContractJson.Hash(ContractJson.Write(NewGameWorld.Look(w.NewGame!,birth.FirstParent.Value))));

    internal static void Validate(ContractWorld world,ContractWorld? prior)
    {
        var births=world.Births??[];
        if(prior?.Births is {} previous && !births.Take(previous.Length).SequenceEqual(previous))
            throw new InvalidDataException("既存の出生記録が変更されています");
        if(births.Select(b=>b.Child).Distinct().Count()!=births.Length)throw new InvalidDataException("出生記録が重複しています");
        foreach(var b in births)
        {
            var state=world.Characters.SingleOrDefault(c=>c.Id==b.Child);
            if(state==null || state.AppearanceId!=Appearance(world,b) ||
                state.Life!=(Assassination.DeadAt(world,b.Child,world.ElapsedSeconds,world.CommitSequence)?LifeState.Dead:
                    FamilyKin.LifeAt(world.NewGame!,Person(world,b),world.ElapsedSeconds)) ||
                !world.Definitions.Characters.Contains(new DefinitionEntry(b.Child,Person(world,b).Name)))
                throw new InvalidDataException("出生児の人物名簿・外見・成長が原本と一致しません");
            if(b.At<=0 || b.At>world.ElapsedSeconds || b.Revision<=0 || b.Revision>world.CommitSequence ||
                (b.Marriage is not {} marriage ? Historical(world,b.At-1,b.At,b.Revision)!=b :
                b.SecondParent is not {} other || b.Child!=ChildId(marriage,b.At) ||
                !Candidates(world,b.At-1,b.At).Any(c=>c.Terms.Id==b.Marriage && c.Terms.First==b.FirstParent && c.Terms.Second==b.SecondParent) ||
                !TransportWork.At(world,b.FirstParent,b.Site,b.At,b.Revision,true) ||
                !TransportWork.At(world,other,b.Site,b.At,b.Revision,true)))
                throw new InvalidDataException("出生時の親子・所在地・婚姻原本が一致しません");
        }
    }
}
