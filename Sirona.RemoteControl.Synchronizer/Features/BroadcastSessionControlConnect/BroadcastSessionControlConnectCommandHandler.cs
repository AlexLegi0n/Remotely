using MediatR;

using Sirona.RemoteControl.Synchronizer.Services.Broadcasters;

namespace Sirona.RemoteControl.Synchronizer.Features.BroadcastSessionControlConnect;

internal sealed class BroadcastSessionControlConnectCommandHandler(IStateBroadcaster broadcaster)
    : IRequestHandler<BroadcastSessionControlConnectCommand>
{
    public Task Handle(BroadcastSessionControlConnectCommand request, CancellationToken cancellationToken)
    {
        SynchronizerState state = new()
        {
            UserName = request.UserName,
            State = State.PermissionRequested,
            Message = "A remote control session has been requested"
        };

        broadcaster.Broadcast(state);

        return Task.CompletedTask;
    }
}