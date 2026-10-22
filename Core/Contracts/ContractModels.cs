using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

// New seconds-domain data. Never reinterpret the legacy prototype's Tick here.
[JsonConverter(typeof(EntityIdConverter))]
public readonly record struct EntityId
{
    public string Value { get; }
    public EntityId(string value)
    {
        if (!IsValid(value)) throw new ArgumentException("Invalid namespaced entity ID.", nameof(value));
        Value = value;
    }
    public static bool IsValid(string? value) => value is { Length: >= 3 and <= 128 } &&
        Regex.IsMatch(value, "^[a-z][a-z0-9-]*:[A-Za-z0-9._/-]+$", RegexOptions.CultureInvariant);
    public override string ToString() => Value ?? "";
}
public readonly record struct SimTime(long Seconds)
{
    public SimTime Add(long seconds) => new(checked(Seconds + seconds));
}
public enum LifeState { Adult, Minor, Dead }
public enum LocationKind { AtSite, Traveling, AtPosition }
public enum TaskKind { BindingOrder, VoluntaryRequest }
public enum ActionKind { Inspect, Transfer, Move, Cancel, Redirect }
public enum ExecutionPhase { NotStarted, Waiting, Running, Suspended, PartiallyCompleted, Completed, Stopped, Failed }
public enum AgreementPhase { Issued, AwaitingReceipt, AwaitingRecognition, AwaitingAssent, Active, Declined, Expired }
public enum ReportingPhase { NotCreated, Queued, InTransit, KnownByRecipient }
public enum AssessmentPhase { Pending, Confirmed, Disputed }
public enum ReservationPhase { Active, PartiallyConsumed, Consumed, Released }
public enum CompensationPhase { Pending, PartlySettled, Settled, Disputed }
public enum EventKind { Execute, Finish, Expire, Cancel }
public enum DeadlineCriterion { Dispatch, FormalReceipt, Opening, Completion }
public enum RelationKind { Membership, Service, Office, Control, Ownership, Occupation }
public enum ResultCode { Accepted, AlreadyAccepted, StateChanged, InvalidInput, IdPayloadConflict, NotAvailable,
    ConflictWait, MigrationRequired, UnsupportedVersion, CorruptSave, DependencyMismatch, IoFailure, SchedulerInvariant }

public sealed record DefinitionEntry([property: JsonRequired] EntityId Id, [property: JsonRequired] string Name);
public sealed record SiteDefinition([property: JsonRequired] EntityId Id, [property: JsonRequired] string Name,
    [property: JsonRequired] int LatitudeE7, [property: JsonRequired] int LongitudeE7, [property: JsonRequired] long HeightMm);
public sealed record ArmyEntry(EntityId Id,EntityId Organization,EntityId Commander);
public sealed record DefinitionPack([property: JsonRequired] EntityId Id, [property: JsonRequired] string Version,
    [property: JsonRequired] string Ruleset, [property: JsonRequired] DefinitionEntry[] Characters,
    [property: JsonRequired] DefinitionEntry[] Organizations, [property: JsonRequired] SiteDefinition[] Sites,
    [property: JsonRequired] int MaxAttempts, [property: JsonRequired] DefinitionEntry[] AuthorityBases,
    [property: JsonRequired] ArmyEntry[] Armies);
public sealed record CharacterState(EntityId Id, LifeState Life, EntityId AppearanceId, long Revision, bool WillObey = true);
public sealed record TimedRelation(EntityId Id, EntityId Subject, EntityId Target, string Kind,
    EntityId Basis, long From, long Until, long Revision);
public sealed record Grant(EntityId Id, EntityId Principal, EntityId Agent, EntityId Scope, ActionKind[] Actions,
    EntityId Basis, long From, long Until, long Limit, EntityId[] Assets, bool Redelegate, long Revision);
public sealed record LocationState(EntityId Actor, LocationKind Kind, EntityId? Site, EntityId? Route,
    int SegmentIndex, long ProgressMm, int? LatitudeE7, int? LongitudeE7, long? HeightMm, string? TerrainRevision, long Revision);
public sealed record UnitState(EntityId Id, EntityId Army, EntityId Commander, EntityId Organization, long Soldiers, long Revision);
public sealed record ResourceLot(EntityId Id, string Kind, EntityId Owner, EntityId Holder, EntityId Site,
    string Unit, long Quantity, long InitialQuantity, long Revision, [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] EntityId? MobileHolder=null);
public sealed record Reservation(EntityId Id, EntityId Lot, EntityId Task, long Amount, long Consumed, ReservationPhase Phase);
public sealed record TransferEntry(EntityId Id, EntityId Effect, EntityId FromLot, EntityId ToLot, long Amount, long At);
public sealed record DebtState(EntityId Id, EntityId Debtor, EntityId Creditor, string Unit, long Outstanding, EntityId Basis);
public sealed record DeadlinePolicy(DeadlineCriterion Criterion, long? At, bool FromOpening, long? DurationSeconds, long AbsoluteLatestAt);
public sealed record Intent(EntityId RequestId, EntityId Actor, EntityId Executor, EntityId Scope, ActionKind Action,
    EntityId? SourceLot, EntityId? DestinationLot, long Amount, EntityId? DestinationSite, DeadlinePolicy Deadline,
    long EffectiveAt, int DeliverySeconds, int WorkSeconds, int ReportSeconds, bool Important, TaskKind Kind,
    EntityId? TargetTask = null);
public sealed record RequestReceipt(EntityId Id, string BodyHash, Intent Intent, long CreatedAt, long CommitSequence);
public sealed record TaskState(EntityId Id, EntityId RequestId, EntityId Issuer, EntityId Executor, EntityId Scope,
    ActionKind Action, TaskKind Kind, AgreementPhase Agreement, ExecutionPhase Execution, ReportingPhase Report,
    AssessmentPhase Assessment, long Progress, long? StartedAt, long? FinishedAt, long? DeadlineAt,
    long AbsoluteLatestAt, long EffectiveAt, string InternalReason, long Revision);
public sealed record ActionState(EntityId Id, EntityId Task, EntityId Actor, string Channel, ExecutionPhase Phase,
    long StartedAt, long Until, long Progress);
public sealed record Evidence(EntityId Id, EntityId Observer, EntityId Subject, string Claim, long? ObservedAt,
    long RecordedAt, EntityId? Message, EntityId? Supersedes);
public sealed record Knowledge(EntityId Character, EntityId Evidence, long LearnedAt);
public sealed record PendingEvent(EntityId Id, EntityId Task, EventKind Kind, long At, EntityId[] DependsOn);
public sealed record AppliedEffect(EntityId Id, string BusinessKey, string BodyHash, EntityId Task, long At, string Result);
public sealed record OutgoingMessage(EntityId Id, EntityId Task, EntityId Sender, EntityId Recipient,
    string Kind, string Body, long At, int Delay, bool Sent);
public sealed record Compensation(EntityId Id, EntityId OriginalEffect, long Outstanding, CompensationPhase Phase);
public sealed record RandomStream(string Purpose, ulong State, long DrawCount);
public sealed record RandomDraw(EntityId Id, string Purpose, ulong Before, uint Result, ulong After, long At);
public sealed record ConflictDecision(EntityId Id, EntityId[] Inputs, string Rule, string Result, long At);
public sealed record ChronicleEntry(EntityId Id, EntityId Actor, string Text, long At);

// All sections are required even when empty. Mutable DTO never escapes a live session.
public sealed class ContractWorld
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TerrainAtlasState? Atlas { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CourierState? Couriers { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TravelDecision[]? TravelDecisions { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TrafficStop[]? TrafficStops { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FieldState? Field { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RegionalState? Region { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BranchState? Branches { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NpcWorkState? NpcWork { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CommandMandate[]? CommandMandates { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EngagementState? Engagement { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SupplyState? Supply { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FieldPresenceState? Presence { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DelegationState? Delegation { get; set; }
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public CareerState? Career {get;set;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public MilitaryState? Military {get;set;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public RecoveredCourier[]? CourierRecoveries {get;set;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
    public NewGameState? NewGame {get;set;}
    public required int Schema { get; set; }
    public required EntityId WorldId { get; set; }
    public required string TimeDomain { get; set; }
    public required string EpochDate { get; set; }
    public required long ElapsedSeconds { get; set; }
    public required long CommitSequence { get; set; }
    public required ulong Seed { get; set; }
    public required bool Paused { get; set; }
    public required DefinitionPack Definitions { get; set; }
    public required CharacterState[] Characters { get; set; }
    public required TimedRelation[] Memberships { get; set; }
    public required TimedRelation[] ServiceBonds { get; set; }
    public required TimedRelation[] Offices { get; set; }
    public required TimedRelation[] LandRights { get; set; }
    public required Grant[] Grants { get; set; }
    public required LocationState[] Locations { get; set; }
    public required UnitState[] Units { get; set; }
    public required TaskState[] Tasks { get; set; }
    public required ActionState[] Actions { get; set; }
    public required ResourceLot[] ResourceLots { get; set; }
    public required Reservation[] Reservations { get; set; }
    public required TransferEntry[] Transfers { get; set; }
    public required DebtState[] Debts { get; set; }
    public required MessageState Messaging { get; set; }
    public required EntityId[] SealedMessages { get; set; }
    public required Evidence[] Evidence { get; set; }
    public required Knowledge[] Knowledge { get; set; }
    public required PendingEvent[] PendingEvents { get; set; }
    public required AppliedEffect[] EffectLedger { get; set; }
    public required OutgoingMessage[] Outbox { get; set; }
    public required Compensation[] Compensations { get; set; }
    public required RandomStream[] RandomStreams { get; set; }
    public required RandomDraw[] RandomDraws { get; set; }
    public required Dictionary<string, long> IdCounters { get; set; }
    public required ConflictDecision[] ConflictDecisions { get; set; }
    public required EntityId PlayerControl { get; set; }
    public required ChronicleEntry[] Chronicle { get; set; }
    public required RequestReceipt[] Requests { get; set; }
}
public sealed record CommandResult(ResultCode Code, EntityId RequestId, long CommitSequence, string VisibleReason);
public sealed record VisibleTask(EntityId Id, EntityId Issuer, EntityId Executor, string KnownStatus, long? DeadlineAt);
public sealed record VisibleMessage(EntityId Id, string Sender, bool Sealed, string? KnownBody);
public sealed record ActorView(EntityId Observer, long Revision, long ElapsedSeconds, LocationState Location,
    VisibleTask[] Tasks, Evidence[] Evidence, VisibleMessage[] Messages);
public sealed record AdvanceResult(long ReachedAt, int Processed, string StopReason);
