using MediatR;

using Sirona.RemoteControl.Synchronizer.Services.Launchers;

namespace Sirona.RemoteControl.Synchronizer.Features.StartRemotely;

internal sealed class StartRemotelyCommandHandler(IAppLauncher launcher) : IRequestHandler<StartRemotelyCommand>
{
    public async Task Handle(StartRemotelyCommand request, CancellationToken cancellationToken)
    {
        await launcher.StartApp(cancellationToken);
    }
}