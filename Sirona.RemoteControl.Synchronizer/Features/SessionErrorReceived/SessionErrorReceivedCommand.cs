using MediatR;

namespace Sirona.RemoteControl.Synchronizer.Features.SessionErrorReceived;

public record SessionErrorReceivedCommand(string Message, string? Error) : IRequest;
