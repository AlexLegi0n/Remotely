using MediatR;

namespace Sirona.RemoteControl.Synchronizer.Features.BroadcastSessionCode;

public record BroadcastSessionCodeCommand(string Code) : IRequest;
