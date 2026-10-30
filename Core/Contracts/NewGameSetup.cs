namespace HakutaiKagami.Core.Contracts;

// Authoring data and a scenario projection, not runtime permissions or a ContractWorld.
public sealed record OfficerAttributes(int Leadership, int Valor, int Intelligence, int Politics)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public int? Negotiation {get;init;}
}
public sealed record RegisteredOfficer(string Id, string? CopiedFrom, string Name, int BirthYear,
    OfficerAttributes Attributes, StartTemplate Role, string Site, string? Lord, string? FactionName, [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] HakutaiKagami.Core.Appearance.OfficerAppearance? Look = null);
public sealed record OfficerGroup(string Id, string Name, RegisteredOfficer[] Officers);
public sealed record OfficerLibrary(int Schema, RegisteredOfficer[] Ungrouped, OfficerGroup[] Groups);
public sealed record SetupPerson(string Id, string Name, string Site, string? Faction, string? Lord, bool Created, bool PersonalRetainer, int? BirthYear = null, OfficerAttributes? Attributes = null, StartTemplate? InitialRole = null);
public sealed record SetupCastle(string Id, string? Faction);
public sealed record SetupFaction(string Id, string Name, string Head, string Home);
public sealed record SetupScenario(int Schema, string EpochDate, SetupPerson[] People, SetupCastle[] Castles, SetupFaction[] Factions);
public sealed record DaimyoPlacement(string Officer, string Faction, string[] Castles, bool IncorporateOldHead);
public sealed record WorldSetupRequest(RegisteredOfficer[] Officers, DaimyoPlacement[] Daimyo);
public sealed record SetupChange(string Person, string Kind, string Detail);
public sealed record WorldSetupPreview(string ScenarioHash, string RequestHash, SetupScenario? World, string[] Errors, SetupChange[] Changes)
{
    public bool IsEligible() => World != null && Errors is {Length:0};
}

public static class NewGameSetup
{
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool yes, string error) { if (!yes) throw new InvalidDataException(error); }
    static bool Text(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 128 && !s.Any(char.IsControl);
    static bool Key(string? s) => Text(s) && s!.All(c => char.IsAsciiLetterOrDigit(c) || c is ':' or '/' or '-' or '_');
    static void Unique(IEnumerable<string> ids) { var a=ids.ToArray(); Need(a.All(Key) && a.Distinct(StringComparer.Ordinal).Count()==a.Length,"IDが重複または不正です"); }
    static void Officer(RegisteredOfficer p)
    {
        Need(p != null && Key(p.Id) && p.Id.StartsWith("created:",StringComparison.Ordinal),"登録武将のIDが不正です");
        Need(Text(p.Name) && Key(p.Site) && Enum.IsDefined(p.Role),"登録武将の名前・立場・場所が不正です");
        Need(p.BirthYear is >= 1 and <= 9999 && p.Attributes != null,"生年・能力が不正です");
        Need(new[]{p.Attributes.Leadership,p.Attributes.Valor,p.Attributes.Intelligence,p.Attributes.Politics,p.Attributes.Negotiation??0}.All(n=>n>=0),"能力は負数にできません");
        Need(p.CopiedFrom == null || (Key(p.CopiedFrom) && p.CopiedFrom != p.Id),"複製元が不正です");
        Need(p.Lord == null || (Key(p.Lord) && p.Lord != p.Id),"直属主君が不正です");
        Need(p.Role != StartTemplate.Daimyo || (Text(p.FactionName) && p.Lord == null),"大名の勢力名・主従が不正です");
        Need(p.Role != StartTemplate.Ronin || p.Lord == null,"浪人本人に主君を設定できません");
        if(p.Look!=null){try{p.Look.Validate(HakutaiKagami.Core.Appearance.AppearanceCatalog.Slots);}catch(Exception e){throw new InvalidDataException("外見の部品・色が不正です",e);}}
    }
    public static void Validate(OfficerLibrary library)
    {
        Need(library != null && library.Schema==1 && library.Ungrouped!=null && library.Groups!=null,"武将ライブラリの版・項目が不正です");
        Need(library.Ungrouped.All(p=>p!=null) && library.Groups.All(g=>g!=null && g.Officers!=null),"ライブラリの項目が欠落しています");
        Unique(library.Groups.Select(g=>g.Id)); Need(library.Groups.All(g=>Text(g.Name)),"グループ名が不正です");
        var all=library.Ungrouped.Concat(library.Groups.SelectMany(g=>g.Officers)).ToArray();
        Need(all.All(p=>p!=null),"武将が欠落しています"); Unique(all.Select(p=>p.Id)); foreach(var p in all)Officer(p);
        // CopiedFrom is provenance, not a live link; deleting or editing the source does not edit a copy.
    }
    public static OfficerLibrary CopyToGroup(OfficerLibrary source, string officer, string group, string copyId)
    {
        Validate(source); var p=source.Ungrouped.SingleOrDefault(p=>p.Id==officer);
        Need(p!=null && source.Groups.Any(g=>g.Id==group),"複製元またはグループがありません");
        var copy=ContractJson.Copy(p!) with{Id=copyId,CopiedFrom=p!.Id};
        var result=ContractJson.Copy(source);result=result with{Groups=result.Groups.Select(g=>g.Id==group?g with{Officers=g.Officers.Append(copy).ToArray()}:g).ToArray()};
        Validate(result);return result;
    }
    public static RegisteredOfficer[] Select(OfficerLibrary library, string[] individuals, string? group)
    {
        Validate(library);Need(individuals!=null && (group==null || individuals.Length==0),"個別選択とグループ選択は同時に使えません");
        Unique(individuals);
        var selected=group!=null?library.Groups.SingleOrDefault(g=>g.Id==group)?.Officers:individuals.Select(id=>library.Ungrouped.SingleOrDefault(p=>p.Id==id)).ToArray();
        Need(selected!=null && selected.All(p=>p!=null),"選択した登録武将がありません");return ContractJson.Copy(selected!.Select(p=>p!).ToArray());
    }
    public static void Validate(SetupScenario s)
    {
        Need(s!=null && s.Schema==1 && s.People!=null && s.Castles!=null && s.Factions!=null && DateOnly.TryParseExact(s.EpochDate,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _),"開始世界の定義が不正です");
        Need(s.People.All(p=>p!=null) && s.Castles.All(c=>c!=null) && s.Factions.All(f=>f!=null),"開始世界の項目が欠落しています");
        Unique(s.People.Select(p=>p.Id));Unique(s.Castles.Select(c=>c.Id));Unique(s.Factions.Select(f=>f.Id));
        Need(s.People.All(p=>Text(p.Name) && Key(p.Site) && (p.Faction==null || s.Factions.Any(f=>f.Id==p.Faction)) && (p.Lord==null || s.People.Any(q=>q.Id==p.Lord))),"人物の所属・主従・場所が不正です");
        Need(s.People.Where(p=>p.Created).All(p=>p.BirthYear is >=1 and <=9999 && p.Attributes!=null && p.InitialRole!=null && Enum.IsDefined(p.InitialRole.Value) && new[]{p.Attributes.Leadership,p.Attributes.Valor,p.Attributes.Intelligence,p.Attributes.Politics,p.Attributes.Negotiation??0}.All(n=>n>=0)),"登録人物の生年・能力・開始立場がありません");
        Need(s.Castles.All(c=>c.Faction==null || s.Factions.Any(f=>f.Id==c.Faction)),"城の所有勢力がありません");
        Need(s.Factions.All(f=>Text(f.Name) && s.People.Any(p=>p.Id==f.Head && p.Faction==f.Id && p.Lord==null) && s.Castles.Any(c=>c.Id==f.Home && c.Faction==f.Id)),"大名または本拠が不正です");
        foreach(var p in s.People){var seen=new HashSet<string>();var q=p;while(q.Lord!=null){Need(seen.Add(q.Id),"主従が循環しています");var lord=s.People.Single(x=>x.Id==q.Lord);Need(q.Faction==lord.Faction,"主従の所属が矛盾します");q=lord;}}
    }
    public static WorldSetupPreview Preview(SetupScenario original, WorldSetupRequest request)
    {
        // Hashes bind confirmation to exactly the scenario and choices that were reviewed.
        string sh=ContractJson.CanonicalHash(original),rh=ContractJson.CanonicalHash(request);
        try{return Build(original,request,sh,rh);}catch(InvalidDataException e){return new(sh,rh,null,[e.Message],[]);}
    }
    static WorldSetupPreview Build(SetupScenario original, WorldSetupRequest request,string sh,string rh)
    {
        Validate(original);Need(request!=null && request.Officers!=null && request.Daimyo!=null && request.Officers.All(p=>p!=null) && request.Daimyo.All(p=>p!=null && p.Castles!=null),"配置設定が欠落しています");
        Unique(request.Officers.Select(p=>p.Id));foreach(var p in request.Officers)Officer(p);
        int year=DateOnly.ParseExact(original.EpochDate,"yyyy-MM-dd").Year;
        Need(request.Officers.All(p=>p.BirthYear<=year),"生まれる前の登録武将は登場できません");
        Need(request.Officers.All(p=>!original.People.Any(q=>q.Id==p.Id)),"既存人物を登録武将で上書きできません");
        Need(request.Officers.All(p=>original.Castles.Any(c=>c.Id==p.Site)),"登場する城がシナリオにありません");
        Unique(request.Daimyo.Select(d=>d.Officer));Unique(request.Daimyo.Select(d=>d.Faction));
        Need(request.Daimyo.All(d=>Key(d.Faction) && !original.Factions.Any(f=>f.Id==d.Faction) && d.Castles.Length>0 && d.Castles.Distinct().Count()==d.Castles.Length && d.Castles.All(id=>original.Castles.Any(c=>c.Id==id))),"勢力ID・領土指定が不正です");
        Need(request.Daimyo.SelectMany(d=>d.Castles).GroupBy(id=>id).All(g=>g.Count()==1),"複数の大名が同じ城を指定しています");
        Need(request.Daimyo.All(d=>request.Officers.Any(p=>p.Id==d.Officer && p.Role==StartTemplate.Daimyo && d.Castles.Contains(p.Site))) && request.Officers.Where(p=>p.Role==StartTemplate.Daimyo).All(p=>request.Daimyo.Any(d=>d.Officer==p.Id)),"大名の本拠・領土設定がありません");
        if(request.Officers.Length==0)return new(sh,rh,ContractJson.Copy(original),[],[]);
        // All territorial changes are resolved simultaneously, never by selection/list order.
        var claims=request.Daimyo.SelectMany(d=>d.Castles.Select(c=>(Castle:c,Placement:d))).ToDictionary(x=>x.Castle,x=>x.Placement,StringComparer.Ordinal);
        var castles=original.Castles.Select(c=>claims.TryGetValue(c.Id,out var d)?c with{Faction=d.Faction}:c).ToArray();
        var people=original.People.ToDictionary(p=>p.Id,p=>p,StringComparer.Ordinal);var factions=new List<SetupFaction>();var changes=new List<SetupChange>();
        foreach(var old in original.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            var remaining=castles.Where(c=>c.Faction==old.Id).OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray();
            var oldHead=people[old.Head];
            var taken=original.Castles.Where(c=>c.Faction==old.Id && claims.ContainsKey(c.Id)).Select(c=>c.Id).ToHashSet();
            if(taken.Count==0){factions.Add(old);continue;}
            // A takeover of the original home determines the defeated head's optional new allegiance.
            var receiver=claims.GetValueOrDefault(old.Home);bool absorbed=remaining.Length==0 && receiver?.IncorporateOldHead==true;
            var followers=new HashSet<string>{old.Head};bool more=true;
            while(more){more=false;foreach(var p in original.People.Where(p=>p.Faction==old.Id && p.PersonalRetainer && p.Lord!=null && followers.Contains(p.Lord)))more|=followers.Add(p.Id);}
            string? home=remaining.Length==0?null:remaining.Any(c=>c.Id==old.Home)?old.Home:remaining[0].Id;
            if(home!=null)factions.Add(old with{Home=home});
            foreach(var p in original.People.Where(p=>p.Faction==old.Id))
            {
                bool loyal=followers.Contains(p.Id);
                if(loyal && remaining.Length==0){people[p.Id]=p with{Faction=absorbed?receiver!.Faction:null,Lord=p.Id==old.Head?(absorbed?receiver!.Officer:null):p.Lord};}
                else if(loyal && taken.Contains(p.Site)){people[p.Id]=p with{Site=home!};}
                else if(!loyal && taken.Contains(p.Site)){var to=claims[p.Site];people[p.Id]=p with{Faction=to.Faction,Lord=to.Officer};}
                else if(remaining.Length==0){Need(false,"領土消滅時の城外家臣の処遇が未指定です");}
            }
            changes.Add(new(old.Head,home!=null?"relocated":absorbed?"incorporated":"ronin-with-followers",home??oldHead.Site));
        }
        foreach(var d in request.Daimyo){var p=request.Officers.Single(p=>p.Id==d.Officer);factions.Add(new(d.Faction,p.FactionName!,p.Id,p.Site));}
        var resolving=new HashSet<string>();
        SetupPerson Resolve(RegisteredOfficer p)
        {
            if(people.TryGetValue(p.Id,out var known))return known;
            Need(resolving.Add(p.Id),"登録武将の主従が循環しています");
            string? faction=p.Role==StartTemplate.Ronin?null:p.Role==StartTemplate.Daimyo?request.Daimyo.Single(d=>d.Officer==p.Id).Faction:castles.Single(c=>c.Id==p.Site).Faction;
            string? lord=null;
            if(p.Role is not(StartTemplate.Ronin or StartTemplate.Daimyo)){
                if(p.Lord!=null){var other=request.Officers.SingleOrDefault(q=>q.Id==p.Lord);var principal=other==null?people.GetValueOrDefault(p.Lord):Resolve(other);Need(principal!=null,"直属主君がこの世界に登場していません");faction=principal.Faction;lord=principal.Id;}
                else{Need(faction!=null,"この場所の所属が未設定です。直属相手を指定するか浪人/大名を選んでください");lord=factions.Single(f=>f.Id==faction).Head;}
            }
            var value=new SetupPerson(p.Id,p.Name,p.Site,faction,lord,true,p.Lord!=null,p.BirthYear,ContractJson.Copy(p.Attributes),p.Role);people.Add(p.Id,value);resolving.Remove(p.Id);return value;
        }
        foreach(var p in request.Officers)Resolve(p);
        // Preserve foreign guests/prisoners: only allegiance members above are reassigned.
        var result=new SetupScenario(1,original.EpochDate,people.Values.OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray(),castles.OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray(),factions.OrderBy(f=>f.Id,StringComparer.Ordinal).ToArray());
        Validate(result);return new(sh,rh,ContractJson.Copy(result),[],changes.ToArray());
    }
    public static SetupScenario Confirm(SetupScenario scenario,WorldSetupRequest request,WorldSetupPreview reviewed)
    {
        Need(reviewed!=null && reviewed.IsEligible() && reviewed.ScenarioHash==ContractJson.CanonicalHash(scenario) && reviewed.RequestHash==ContractJson.CanonicalHash(request),"確認後に開始設定が変更されています");
        var fresh=Preview(scenario,request);Need(fresh.IsEligible() && ContractJson.CanonicalHash(fresh.World)==ContractJson.CanonicalHash(reviewed.World),"配置結果を再確認してください");
        return ContractJson.Copy(fresh.World!);
    }
}
