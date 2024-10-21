using MediatR;

using Sirona.RemoteControl.Synchronizer.Services.Launchers;

namespace Sirona.RemoteControl.Synchronizer.Features.StopRemotely;

internal sealed class StopRemotelyCommandHandler(IAppLauncher launcher) : IRequestHandler<StopRemotelyCommand>
{
    public async Task Handle(StopRemotelyCommand request, CancellationToken cancellationToken)
    {
        await launcher.StopApp(cancellationToken);
    }
}