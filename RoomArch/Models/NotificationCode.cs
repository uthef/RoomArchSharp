namespace RoomArch.Models;

public enum NotificationCode
{
    AuthorizationSuccess,
    RoomLeft,
    NoRoomToLeave,
    RoomJoined,
    RoomCreated,
    LeaveBeforeCreating,
    LeaveBeforeJoining,
    UsernameTaken,
    RoomNameTaken,
    KickedOut,
    KickedOutByHost,
    RoomDoesNotExist,
    InvalidUsername,
    InvalidPassword,
    InvalidRoomName,
    ClientLimitReached,
    RoomConfigurationNotSpecified,
    RoomModificationNotSpecified,
    UnallowedRequest,
    RoomLocked
}
