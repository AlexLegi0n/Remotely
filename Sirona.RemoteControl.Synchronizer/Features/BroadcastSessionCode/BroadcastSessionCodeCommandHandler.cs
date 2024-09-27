using MediatR;

using Sirona.RemoteControl.Synchronizer.Services.Broadcasters;

namespace Sirona.RemoteControl.Synchronizer.Features.BroadcastSessionCode;

internal sealed class BroadcastSessionCodeCommandHandler(IStateBroadcaster codesBroadcaster)
    : IRequestHandler<BroadcastSessionCodeCommand>
{
    public Task Handle(BroadcastSessionCodeCommand request, CancellationToken cancellationToken)
    {
        SynchronizerState state = new()
        {
            State = State.AuthenticationRequested,
            SessionId = request.Code
        };
        codesBroadcaster.Broadcast(state);

        return Task.CompletedTask;
    }
}