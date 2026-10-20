namespace HakutaiKagami.Core.Contracts;

public enum RegionalAction { Move, Engage, Retreat, Hold }
public sealed record RegionalRequest(EntityId Id, EntityId Actor, EntityId Recipient, RegionalAction Action, FieldPoint A, FieldPoint B, EntityId? Supersedes = null);
public sealed record RegionalUnit(EntityId Id, EntityId Commander, int Side, FieldPoint Home, FieldPoint Position, FieldPoint Heading, FieldPoint Goal, FieldPoint RouteOrigin, FieldLeg[] Route, int InitialSoldiers, int Soldiers, int InitialMorale, int Morale, string Phase);
public sealed record RegionalCommand(RegionalRequest Request, long IssuedAt, string Message, FieldPoint Address, long? RecognizedAt, long? AppliedAt, string Result);
public sealed record RegionalDelivery(string Attempt, FieldPoint Origin, FieldPoint Address, FieldLeg[] Path, long DueAt);
public sealed record RegionalLoss(long At, EntityId Unit, int Soldiers, int Morale);
public sealed record RegionalSight(EntityId Observer, EntityId Unit, FieldPoint Position, FieldPoint Heading, int Soldiers, int Morale, string Phase, long At);
public sealed record RegionalState(int PerForce, bool Refuses, bool LowMorale, bool Active, long Since, long Tick, bool Hostile, RegionalUnit[] Units, RegionalCommand[] Commands, RegionalDelivery[] Deliveries, RegionalLoss[] Losses, RegionalSight[] Sights);
public sealed record RegionalView(EntityId Unit, FieldPoint Position, FieldPoint Heading, long Height, int Soldiers, int Morale, string Phase, long ObservedAt, bool Live);
