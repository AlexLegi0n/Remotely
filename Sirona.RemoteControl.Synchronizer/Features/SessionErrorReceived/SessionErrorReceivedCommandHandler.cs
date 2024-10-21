using MediatR;

using Sirona.RemoteControl.Synchronizer.Services.Broadcasters;

namespace Sirona.RemoteControl.Synchronizer.Features.SessionErrorReceived;

internal sealed class SessionErrorReceivedCommandHandler(IStateBroadcaster broadcaster)
    : IRequestHandler<SessionErrorReceivedCommand>
{
    public Task Handle(SessionErrorReceivedCommand request, CancellationToken cancellationToken)
    {
        SynchronizerState state = new()
        {
            Message = $"{request.Message}. Error: {request.Error ?? "Unhandled error"}",
            State = State.ConnectionError
        };

        broadcaster.Broadcast(state);

        return Task.CompletedTask;
    }
}