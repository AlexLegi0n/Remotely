using MediatR;

using Sirona.RemoteControl.Synchronizer.Services.Broadcasters;

namespace Sirona.RemoteControl.Synchronizer.Features.SetSessionPermissions;

internal sealed class SetSessionPermissionsCommandHandler(IPermissionBroadcaster broadcaster) : IRequestHandler<SetSessionPermissionsCommand>
{
    public Task Handle(SetSessionPermissionsCommand request, CancellationToken cancellationToken)
    {
        broadcaster.Broadcast(new SessionPermissions()
        {
            AllowConnection = request.Allowed
        });
        
        return Task.CompletedTask;
    }
}