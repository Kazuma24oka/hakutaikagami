namespace HakutaiKagami.Core.Contracts;

public enum StartTemplate { Ronin, Retainer, Subretainer, Castellan, LocalLord, Daimyo }
public enum HistoryMode { Historical, SemiHistorical, Fictional }
public enum PersonOrigin { Historical, Created }
// A new-game definition, never a live session or an inferred grant of authority.
public sealed record StartSelection(EntityId Person, StartTemplate Template, HistoryMode Mode, PersonOrigin Origin,
    EntityId Definition, string DefinitionVersion, string EpochDate, ulong Seed, string PolicyHash, string WorldHash);
public sealed record StartResourcePrice(string Kind, string Unit, long CostPerUnit);
// Developer-owned scenario policy. No product balance values are implied by a test fixture.
public sealed record StartBudgetPolicy(long Budget, long LandRightCost, long FollowerCost, long SoldierCost, StartResourcePrice[] Prices);
public sealed record StartPackage(StartSelection Selection, ContractWorld World, StartBudgetPolicy Policy);
public sealed record StartCheck(bool Eligible, string[] Reasons, long? CreationCost);

public static class StartConfiguration
{
    public static StartSelection Describe(ContractWorld world, EntityId person, StartTemplate template,
        PersonOrigin origin, StartBudgetPolicy policy, HistoryMode mode = HistoryMode.SemiHistorical) =>
        new(person, template, mode, origin, world.Definitions.Id, world.Definitions.Version,
            world.EpochDate, world.Seed, ContractJson.CanonicalHash(policy), ContractJson.CanonicalHash(world));

    // Callers retain their input. Failed checks neither change the world nor create any rights.
    public static StartCheck Check(ContractWorld world, StartSelection choice, StartBudgetPolicy policy)
    {
        if (world == null || choice == null || policy == null || policy.Prices == null || policy.Prices.Any(p => p == null))
            return new(false, ["開始設定に必要な項目がありません"], null);
        var errors = new List<string>();
        void Need(bool ok, string reason) { if (!ok) errors.Add(reason); }
        try { ContractValidation.Validate(world); }
        catch (InvalidDataException) { return new(false, ["開始世界の整合検査に失敗しました"], null); }
        Need(Enum.IsDefined(choice.Template) && Enum.IsDefined(choice.Mode) && Enum.IsDefined(choice.Origin), "不明な開始設定です");
        Need(world.ElapsedSeconds == 0 && world.CommitSequence == 0 && world.Tasks.Length == 0 && world.Requests.Length == 0,
            "開始後の世界を新規開始として扱えません");
        Need(choice.Definition == world.Definitions.Id && choice.DefinitionVersion == world.Definitions.Version &&
            choice.EpochDate == world.EpochDate && choice.Seed == world.Seed && choice.EpochDate == "1560-01-01", "開始定義・版・年代・Seedが一致しません");
        Need(choice.WorldHash == ContractJson.CanonicalHash(world), "開始世界の内容が選択時から変わっています");
        Need(choice.PolicyHash == ContractJson.CanonicalHash(policy), "開始予算の定義が一致しません");
        Need(policy.Budget >= 0 && policy.LandRightCost > 0 && policy.FollowerCost > 0 && policy.SoldierCost > 0 &&
            policy.Prices.All(p => p.CostPerUnit > 0 && !string.IsNullOrWhiteSpace(p.Kind) && !string.IsNullOrWhiteSpace(p.Unit)) &&
            policy.Prices.Select(p => (p.Kind, p.Unit)).Distinct().Count() == policy.Prices.Length, "開始予算の定義が不正です");
        var person = world.Characters.SingleOrDefault(c => c.Id == choice.Person);
        Need(person?.Life == LifeState.Adult && world.PlayerControl == choice.Person, "操作人物は開始世界の成人本人である必要があります");
        Need(world.Locations.Any(l => l.Actor == choice.Person && l.Kind == LocationKind.AtSite), "開始人物の拠点が必要です");
        bool Active(TimedRelation relation) => relation.From <= 0 && 0 < relation.Until;
        var memberships = world.Memberships.Where(b => b.Subject == choice.Person && Active(b)).ToArray();
        var bonds = world.ServiceBonds.Where(b => b.Subject == choice.Person && Active(b)).ToArray();
        var offices = world.Offices.Where(b => b.Subject == choice.Person && Active(b)).ToArray();
        var lands = world.LandRights.Where(b => b.Subject == choice.Person && Active(b)).ToArray();
        var followers = world.ServiceBonds.Where(b => b.Target == choice.Person && Active(b)).ToArray();
        bool HasHouse = memberships.Length > 0, HasLord = bonds.Length > 0;
        bool HoldsLand = lands.Any(b => b.Kind is "ownership" or "control");
        bool IsCastellan = offices.Any(b => b.Kind == "castellan");
        bool IsHead = offices.Any(b => b.Kind == "house-head");
        bool LordHasLord = bonds.Any(b => world.ServiceBonds.Any(x => x.Subject == b.Target && Active(x)));
        switch (choice.Template)
        {
            case StartTemplate.Ronin:
                Need(!HasHouse && !HasLord && offices.Length == 0 && lands.Length == 0, "浪人の所属・主従・役職・土地権が矛盾します");
                break;
            case StartTemplate.Retainer:
                Need(HasHouse && HasLord && !IsCastellan && !IsHead, "家臣には所属と直属主君が必要です");
                break;
            case StartTemplate.Subretainer:
                Need(HasHouse && HasLord && LordHasLord && !IsCastellan && !IsHead, "陪臣には直属主君とその主従関係が必要です");
                break;
            case StartTemplate.Castellan:
                Need(HasHouse && IsCastellan, "城主の役職と所属が必要です");
                break;
            case StartTemplate.LocalLord:
                Need(HasHouse && IsHead && HoldsLand, "国人領主の家代表の役職と土地の明示権利が必要です");
                break;
            case StartTemplate.Daimyo:
                Need(HasHouse && IsHead && HoldsLand && followers.Length > 0, "大名の家代表・土地権・直属関係が必要です");
                break;
        }
        // Command remains an explicit relation between a person, a unit and its organization.
        foreach (var unit in world.Units.Where(u => u.Commander == choice.Person))
            Need(memberships.Any(m => m.Target == unit.Organization), "未所属の他家部隊を開始時に指揮できません");
        if (choice.Template == StartTemplate.Ronin)
            Need(!world.Grants.Any(g => g.Agent == choice.Person && g.From <= 0 && 0 < g.Until &&
                (g.Actions.Contains(ActionKind.Transfer) || g.Redelegate)), "浪人に未収録の徴収・再委任権を与えられません");
        long? cost = null;
        if (choice.Origin == PersonOrigin.Historical)
        {
            Need(false, "史実人物の開始時の身分・権限は詳細採録前のため選択できません");
            Need(NationalPersonnel.Catalog.People.Any(p => p.Id == choice.Person), "出典付き人物台帳にない人物を史実人物として開始できません");
        }
        else if (choice.Origin == PersonOrigin.Created)
        {
            Need(choice.Person.Value?.StartsWith("person:created/", StringComparison.Ordinal) == true &&
                !NationalPersonnel.Catalog.People.Any(p => p.Id == choice.Person), "作成人物は独立IDで史実人物を上書きしません");
            try
            {
                var chargedOwners = memberships.Where(_ => IsHead).Select(m => m.Target).Append(choice.Person).ToHashSet();
                long rights = world.LandRights.LongCount(b => chargedOwners.Contains(b.Subject) && Active(b));
                long total = checked(rights * policy.LandRightCost + followers.Length * policy.FollowerCost);
                foreach (var unit in world.Units.Where(u => u.Commander == choice.Person))
                    total = checked(total + checked(unit.Soldiers * policy.SoldierCost));
                foreach (var lot in world.ResourceLots.Where(l => chargedOwners.Contains(l.Owner)))
                {
                    var rate = policy.Prices.SingleOrDefault(p => p.Kind == lot.Kind && p.Unit == lot.Unit);
                    Need(rate != null, "私有資源に対応する開始価格がありません");
                    if (rate != null) total = checked(total + checked(lot.Quantity * rate.CostPerUnit));
                }
                cost = total; Need(total <= policy.Budget, "作成人物の開始予算を超えています");
            }
            catch (Exception e) when (e is OverflowException or InvalidOperationException)
            { errors.Add("開始予算を安全に計算できません"); }
        }
        return new(errors.Count == 0, errors.Distinct(StringComparer.Ordinal).ToArray(), cost);
    }
}
