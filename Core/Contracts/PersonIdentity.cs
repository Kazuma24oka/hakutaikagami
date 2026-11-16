namespace HakutaiKagami.Core.Contracts;

// 人物の家系と信仰（2026-11-16、S01）。作成武将は登録で選んだもの、史実人物は百代鏡DBの本姓・宗派の候補（一覧の DbLineageFaith の世界だけ）。
// 家系・信仰は作成武将と同じ一覧（RegisteredOfficer.Lineages・Faiths）の値にする。分からないときは null（推測しない）。
public sealed record PersonIdentityValue(string Value,string Basis);

public static class PersonIdentity
{
    public const string ChosenBasis="登録で選んだもの";
    public const string DbBasis="百代鏡DBの記録の候補（記事による）";
    static readonly IReadOnlyDictionary<string,string> HonseiLineage=new Dictionary<string,string>(StringComparer.Ordinal){["源"]="源氏",["平"]="平氏",["藤原"]="藤原氏",["橘"]="橘氏"};
    static readonly IReadOnlyDictionary<string,string> ReligionFaith=new Dictionary<string,string>(StringComparer.Ordinal){["キリスト教"]="キリシタン",["浄土真宗"]="浄土真宗",["法華宗"]="日蓮宗",["禅宗"]="禅宗"};

    static HakutaiDbExtra? Db(ContractWorld w,EntityId person)=>
        NationalSystems.On(w,NationalSystems.DbLineageFaith)&&NationalStart.DbPerson(person.Value) is {} db?HakutaiDbSupplement.Person(db.Id):null;

    public static PersonIdentityValue? Lineage(ContractWorld w,EntityId person)
    {
        if(w.NewGame?.Registered.SingleOrDefault(r=>r.Id==person.Value) is {} r)return r.Lineage is {} l?new(l,ChosenBasis):null;
        return Db(w,person)?.Honsei is {} h?new(HonseiLineage.GetValueOrDefault(h.Name,"その他"),DbBasis+"："+h.AsWritten):null;
    }
    public static PersonIdentityValue? Faith(ContractWorld w,EntityId person)
    {
        if(w.NewGame?.Registered.SingleOrDefault(r=>r.Id==person.Value) is {} r)return r.Faith is {} f?new(f,ChosenBasis):null;
        // 候補が2つ以上（改宗など）なら時期が分からないので使わない。キリスト教は洗礼の年が分からず、1560年には洗礼前の人が多い
        // （大友宗麟は1578年、大村純忠は1563年）ので、1560年の開始の信仰にはしない。
        return Db(w,person)?.Religions is [var one]&&one!="キリスト教"&&ReligionFaith.TryGetValue(one,out var faith)?new(faith,DbBasis):null;
    }
}
