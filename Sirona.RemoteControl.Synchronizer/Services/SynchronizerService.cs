using Google.Protobuf.WellKnownTypes;

using Grpc.Core;

using MediatR;

using Sirona.RemoteControl.Synchronizer.Features.SetSessionPermissions;
using Sirona.RemoteControl.Synchronizer.Features.StartRemotely;
using Sirona.RemoteControl.Synchronizer.Features.StopRemotely;
using Sirona.RemoteControl.Synchronizer.Services.Broadcasters;

namespace Sirona.RemoteControl.Synchronizer.Services;

public class SynchronizerService(
    IMediator mediator,
    IStateBroadcaster broadcaster,
    ILogger<SynchronizerService> logger) : SyncService.SyncServiceBase
{
    public override async Task<OperationResult> StartRemoteSession(StartRemoteSessionRequest request,
        ServerCallContext context)
    {
        await mediator.Send(new StartRemotelyCommand(), context.CancellationToken);

        return new OperationResult
        {
            Message = "Remotely client app has been started",
            StatusCode = StatusCode.NoError
        };
    }

    public override async Task<OperationResult> StopRemoteSession(StopRemoteSessionRequest request,
        ServerCallContext context)
    {
        await mediator.Send(new StopRemotelyCommand(), context.CancellationToken);

        return new OperationResult
        {
            StatusCode = StatusCode.NoError,
            Message = "Remotely client app has been stopped"
        };
    }

    public override async Task<OperationResult> SetSessionPermissions(SetSessionPermissionsRequest request,
        ServerCallContext context)
    {
        await mediator.Send(new SetSessionPermissionsCommand(request.AllowConnection), context.CancellationToken);

        var message = request.AllowConnection ? "Approval was provided" : "Remote session was declined";

        return new OperationResult
        {
            StatusCode = StatusCode.NoError,
            Message = message
        };
    }

    public override async Task SubscribeSynchronizerSateUpdate(Empty request,
        IServerStreamWriter<SynchronizerState> responseStream,
        ServerCallContext context)
    {
        string peer = context.Peer;

        logger.LogInformation("{Peer} subscribes to synchronizer state", peer);

        context.CancellationToken.Register(() => OnCancelCallback(peer));

        try
        {
            await broadcaster.Subscribe()
                .ToAsyncEnumerable()
                .ForEachAwaitAsync(async x => await responseStream.WriteAsync(x, context.CancellationToken));
        }
        catch (TaskCanceledException)
        {
            logger.LogInformation("{Peer} unsubscribed from synchronizer state", peer);
        }
    }

    private void OnCancelCallback(string peer)
    {
        logger.LogInformation("{Peer} cancels subscription to synchronizer state", peer);
    }
}