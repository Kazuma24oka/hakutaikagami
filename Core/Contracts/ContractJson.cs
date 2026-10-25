using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace HakutaiKagami.Core.Contracts;

public sealed class EntityIdConverter : JsonConverter<EntityId>
{
    public override EntityId Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? new(reader.GetString()!) : throw new JsonException("ID must be a string.");
    public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options)
    {
        if (!EntityId.IsValid(value.Value)) throw new JsonException("Invalid ID.");
        writer.WriteStringValue(value.Value);
    }
}
public sealed class Int64StringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
    {
        if (r.TokenType != JsonTokenType.String || !long.TryParse(r.GetString(), NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var n) || n.ToString(CultureInfo.InvariantCulture) != r.GetString())
            throw new JsonException("Canonical Int64 string required.");
        return n;
    }
    public override void Write(Utf8JsonWriter w, long n, JsonSerializerOptions o) => w.WriteStringValue(n.ToString(CultureInfo.InvariantCulture));
}
public sealed class UInt64StringConverter : JsonConverter<ulong>
{
    public override ulong Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
    {
        if (r.TokenType != JsonTokenType.String || !ulong.TryParse(r.GetString(), NumberStyles.None,
            CultureInfo.InvariantCulture, out var n) || n.ToString(CultureInfo.InvariantCulture) != r.GetString())
            throw new JsonException("Canonical UInt64 string required.");
        return n;
    }
    public override void Write(Utf8JsonWriter w, ulong n, JsonSerializerOptions o) => w.WriteStringValue(n.ToString(CultureInfo.InvariantCulture));
}

public static class ContractJson
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(t =>
        {
            if (t.Kind == JsonTypeInfoKind.Object)
                foreach (var p in t.Properties.Where(p => p.Get != null)) p.IsRequired = !(t.Type==typeof(ContractWorld)&&p.Name==nameof(ContractWorld.FactionNotices)) && !(t.Type==typeof(ContractWorld)&&p.Name==nameof(ContractWorld.WorldClashes)) && !(t.Type==typeof(ContractWorld)&&p.Name==nameof(ContractWorld.NpcArmies)) && !((t.Type==typeof(NewGameState)||t.Type==typeof(NewCampaignProfile))&&p.Name=="WorldArmyPolicy") && !(t.Type==typeof(EngagementTaskTerms)&&p.Name==nameof(EngagementTaskTerms.Hostility)) && !(t.Type==typeof(ContractWorld)&&p.Name==nameof(ContractWorld.FactionStances)) && !(t.Type==typeof(NewMilitaryInput)&&(p.Name==nameof(NewMilitaryInput.Engagement)||p.Name==nameof(NewMilitaryInput.EngagementOriginal))) && !(t.Type==typeof(MilitaryNotice)&&p.Name==nameof(MilitaryNotice.Engagement)) && !(t.Type==typeof(NewMilitaryInput)&&p.Name==nameof(NewMilitaryInput.FromLedger)) && !(t.Type==typeof(ContractWorld)&&p.Name==nameof(ContractWorld.OtherMilitaryLedgers)) && !(t.Type==typeof(NewMilitaryState)&&p.Name==nameof(NewMilitaryState.Binding)) && !(t.Type==typeof(NewMilitaryInput)&&p.Name==nameof(NewMilitaryInput.LedgerPolicy)) && !(t.Type==typeof(WorkCycleState)&&p.Name==nameof(WorkCycleState.Duties)) && !(t.Type==typeof(Intent)&&p.Name==nameof(Intent.RoutePolicy)) && !(t.Type==typeof(NewGameState)&&p.Name==nameof(NewGameState.VoyageRules)) && !(t.Type==typeof(NewCampaignProfile)&&p.Name==nameof(NewCampaignProfile.VoyageRules)) && !(t.Type==typeof(SavePresentation)&&p.Name==nameof(SavePresentation.OperationPanel)) && !(t.Type==typeof(NewCampaignProfile)&&(p.Name==nameof(NewCampaignProfile.PersonalAuthorityLayout)||p.Name==nameof(NewCampaignProfile.HistoricalCatalog)||p.Name==nameof(NewCampaignProfile.NpcRoutinePolicy)||p.Name==nameof(NewCampaignProfile.InspectionPolicy)||p.Name==nameof(NewCampaignProfile.WorkPolicy)||p.Name==nameof(NewCampaignProfile.DemandPolicy))) && !(t.Type==typeof(NewGameState)&&(p.Name==nameof(NewGameState.PersonalAuthorityLayout)||p.Name==nameof(NewGameState.HistoricalCatalog)||p.Name==nameof(NewGameState.NpcRoutinePolicy)||p.Name==nameof(NewGameState.InspectionPolicy)||p.Name==nameof(NewGameState.WorkPolicy)||p.Name==nameof(NewGameState.DemandPolicy))) && !(t.Type==typeof(UnitDirectiveCancellationNote)&&p.Name==nameof(UnitDirectiveCancellationNote.HoldingPolicy)) && !(t.Type==typeof(UnitDirectiveCancellationState)&&p.Name==nameof(UnitDirectiveCancellationState.Holding)) && !(t.Type==typeof(UnitDirectiveNote)&&p.Name==nameof(UnitDirectiveNote.HoldingPolicy)) && !(t.Type==typeof(UnitDirectiveState)&&(p.Name==nameof(UnitDirectiveState.Holding)||p.Name==nameof(UnitDirectiveState.Cancellation))) && !(t.Type==typeof(UnitPostCancellation)&&(p.Name==nameof(UnitPostCancellation.ReplyPolicy)||p.Name==nameof(UnitPostCancellation.TransportPolicy))) && !(t.Type==typeof(TroopReceipt)&&p.Name==nameof(TroopReceipt.ArrivalStatus)) && !(t.Type==typeof(NewMilitaryInput)&&(p.Name==nameof(NewMilitaryInput.MotionContract)||p.Name==nameof(NewMilitaryInput.CycleContract)||p.Name==nameof(NewMilitaryInput.ExercisePolicy)||p.Name==nameof(NewMilitaryInput.ObservationPolicy)||p.Name==nameof(NewMilitaryInput.KnownTarget)||p.Name==nameof(NewMilitaryInput.InquiryTarget)||p.Name==nameof(NewMilitaryInput.Directive)||p.Name==nameof(NewMilitaryInput.DirectiveRetry)||p.Name==nameof(NewMilitaryInput.DirectiveHandoff)||p.Name==nameof(NewMilitaryInput.DirectiveCancellation)||p.Name==nameof(NewMilitaryInput.DirectiveCancelRetry)||p.Name==nameof(NewMilitaryInput.DirectiveCancelHandoff)||p.Name==nameof(NewMilitaryInput.AuthorityPolicy)||p.Name==nameof(NewMilitaryInput.TargetOriginal)||p.Name==nameof(NewMilitaryInput.RequestedEffectiveAt)||p.Name==nameof(NewMilitaryInput.TransportPolicy)||p.Name==nameof(NewMilitaryInput.DeliverBy)||p.Name==nameof(NewMilitaryInput.OriginalPolicy)||p.Name==nameof(NewMilitaryInput.PostPolicy))) && !(t.Type==typeof(UnitPostOrder)&&(p.Name==nameof(UnitPostOrder.TransportPolicy)||p.Name==nameof(UnitPostOrder.HoldingPolicy)||p.Name==nameof(UnitPostOrder.ReplyPolicy))) && !(t.Type==typeof(UnitPostInquiry)&&(p.Name==nameof(UnitPostInquiry.Target)||p.Name==nameof(UnitPostInquiry.TransportPolicy)||p.Name==nameof(UnitPostInquiry.ReplyPolicy))) && !(t.Type==typeof(NewMilitaryState)&&(p.Name==nameof(NewMilitaryState.Observation)||p.Name==nameof(NewMilitaryState.Authority)||p.Name==nameof(NewMilitaryState.Cancellation)||p.Name==nameof(NewMilitaryState.CommandHolding)||p.Name==nameof(NewMilitaryState.HeldDelivery))) && !(t.Type==typeof(NewMilitaryState)&&p.Name==nameof(NewMilitaryState.ReplyRecovery)) && !(t.Type==typeof(NewMilitaryState)&&p.Name==nameof(NewMilitaryState.UnitInquiry)) && !(t.Type==typeof(NewMilitaryState)&&p.Name==nameof(NewMilitaryState.UnitDirective)) && !(t.Type==typeof(NewMilitaryOrder)&&p.Name==nameof(NewMilitaryOrder.AffectedUnits)) && !(t.Type==typeof(NewMilitaryState)&&p.Name==nameof(NewMilitaryState.UnitRetry)) && !(t.Type==typeof(NewMilitaryState)&&p.Name==nameof(NewMilitaryState.Rout)) && !(t.Type==typeof(NewMilitaryState)&&p.Name==nameof(NewMilitaryState.Withdrawal)) && !(t.Type==typeof(MilitaryNotice)&&p.Name==nameof(MilitaryNotice.Combat)) && !(t.Type==typeof(RegisteredOfficer)&&p.Name==nameof(RegisteredOfficer.Look)) && !(t.Type==typeof(NpcWorkInputs)&&p.Name==nameof(NpcWorkInputs.DialoguePending)) && !(t.Type==typeof(DelegationQuestion)&&p.Name==nameof(DelegationQuestion.Resend)) && !(t.Type==typeof(DelegationAnswer)&&p.Name==nameof(DelegationAnswer.Retried)) && !(t.Type==typeof(SavePresentation)&&(p.Name==nameof(SavePresentation.ScenerySkin)||p.Name==nameof(SavePresentation.WorldCamera)||p.Name==nameof(SavePresentation.CouncilOpen)||p.Name==nameof(SavePresentation.MilitaryTarget)||p.Name==nameof(SavePresentation.MilitaryOriginal)||p.Name==nameof(SavePresentation.MilitaryCancellation))) && !(t.Type == typeof(ContractWorld) && (p.Name == nameof(ContractWorld.VoyageExpenses) || p.Name == nameof(ContractWorld.TransportWork) || p.Name == nameof(ContractWorld.WorkCycle) || p.Name == nameof(ContractWorld.NewMilitary) || p.Name == nameof(ContractWorld.Service) || p.Name == nameof(ContractWorld.NewGame) || p.Name == nameof(ContractWorld.Branches) || p.Name == nameof(ContractWorld.Couriers) || p.Name == nameof(ContractWorld.TravelDecisions) || p.Name == nameof(ContractWorld.TrafficStops) || p.Name == nameof(ContractWorld.Field) || p.Name == nameof(ContractWorld.Atlas) || p.Name == nameof(ContractWorld.Region) || p.Name == nameof(ContractWorld.NpcWork) || p.Name == nameof(ContractWorld.CommandMandates) || p.Name == nameof(ContractWorld.Engagement) || p.Name == nameof(ContractWorld.Supply) || p.Name == nameof(ContractWorld.Presence) || p.Name == nameof(ContractWorld.Delegation) || p.Name==nameof(ContractWorld.Career) || p.Name==nameof(ContractWorld.Military) || p.Name==nameof(ContractWorld.CourierRecoveries))) && !(t.Type==typeof(ResourceLot)&&p.Name==nameof(ResourceLot.MobileHolder)) && !(t.Type==typeof(CourierMission)&&(p.Name==nameof(CourierMission.DispatchPose)||p.Name==nameof(CourierMission.FirstMile)));
        });
        return new() { TypeInfoResolver = resolver, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 64, Converters = { new EntityIdConverter(), new Int64StringConverter(),
                new UInt64StringConverter(), new JsonStringEnumConverter(allowIntegerValues: false) } };
    }
    public static byte[] Write<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);
    public static T Read<T>(ReadOnlySpan<byte> bytes)
    {
        try
        {
            // Reject invalid UTF-8 and duplicate properties before materializing any DTO.
            var text = StrictUtf8.GetString(bytes);
            using var doc = JsonDocument.Parse(text, new() { MaxDepth = 64 });
            CheckKeys(doc.RootElement);
            return JsonSerializer.Deserialize<T>(text, Options) ?? throw new JsonException("Null document.");
        }
        catch (Exception e) when (e is JsonException or ArgumentException or DecoderFallbackException or NotSupportedException)
        { throw new InvalidDataException("Invalid common-contract JSON.", e); }
    }
    private static void CheckKeys(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in node.EnumerateObject())
            {
                if (!names.Add(p.Name)) throw new JsonException("Duplicate property.");
                CheckKeys(p.Value);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array) foreach (var child in node.EnumerateArray()) CheckKeys(child);
    }
    public static T Copy<T>(T value) => Read<T>(Write(value));
    public static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    public static string CanonicalHash<T>(T value)
    {
        // Arrays that are declared sets are canonicalized by identity; ordered histories remain ordered.
        object? normalized=value switch{
            DefinitionPack d=>d with{
                Characters=d.Characters.OrderBy(x=>x.Id.Value,StringComparer.Ordinal).ToArray(),
                Organizations=d.Organizations.OrderBy(x=>x.Id.Value,StringComparer.Ordinal).ToArray(),
                Sites=d.Sites.OrderBy(x=>x.Id.Value,StringComparer.Ordinal).ToArray(),
                AuthorityBases=d.AuthorityBases.OrderBy(x=>x.Id.Value,StringComparer.Ordinal).ToArray(),
                Armies=d.Armies.OrderBy(x=>x.Id.Value,StringComparer.Ordinal).ToArray()},
            PendingEvent e=>e with{DependsOn=e.DependsOn.OrderBy(x=>x.Value,StringComparer.Ordinal).ToArray()},
            _=>value};
        using var doc = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(normalized,normalized?.GetType()??typeof(T),Options));
        using var stream = new MemoryStream(); using var writer = new Utf8JsonWriter(stream);
        CanonicalWrite(writer, doc.RootElement); writer.Flush(); return Hash(stream.ToArray());
    }
    private static void CanonicalWrite(Utf8JsonWriter w, JsonElement n)
    {
        if (n.ValueKind == JsonValueKind.Object)
        {
            w.WriteStartObject(); foreach (var p in n.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { w.WritePropertyName(p.Name); CanonicalWrite(w, p.Value); } w.WriteEndObject();
        }
        else if (n.ValueKind == JsonValueKind.Array)
        { w.WriteStartArray(); foreach (var x in n.EnumerateArray()) CanonicalWrite(w, x); w.WriteEndArray(); }
        else n.WriteTo(w);
    }
}
