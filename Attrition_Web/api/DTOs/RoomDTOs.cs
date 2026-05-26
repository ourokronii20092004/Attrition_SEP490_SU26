namespace Attrition.API.DTOs;

public record CreateRoomReq(string? RoomName, bool IsPrivate);
public record JoinRoomReq(Guid CharacterId);
public record DispatchCommandReq(string Command, string Payload);
