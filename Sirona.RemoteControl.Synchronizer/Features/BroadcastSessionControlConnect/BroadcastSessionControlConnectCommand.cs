using MediatR;

namespace Sirona.RemoteControl.Synchronizer.Features.BroadcastSessionControlConnect;

public record BroadcastSessionControlConnectCommand(string UserName) : IRequest;
