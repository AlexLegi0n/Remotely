using MediatR;

namespace Sirona.RemoteControl.Synchronizer.Features.SetSessionPermissions;

public record SetSessionPermissionsCommand(bool Allowed) : IRequest;