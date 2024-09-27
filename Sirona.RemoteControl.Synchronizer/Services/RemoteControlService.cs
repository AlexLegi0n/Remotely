using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Sirona.RemoteControl.Synchronizer.Features.BroadcastSessionCode;
using Sirona.RemoteControl.Synchronizer.Features.BroadcastSessionControlConnect;
using Sirona.RemoteControl.Synchronizer.Features.SessionErrorReceived;
using Sirona.RemoteControl.Synchronizer.Services.Broadcasters;

namespace Sirona.RemoteControl.Synchronizer.Services;

public class RemoteControlService(
    ILogger<RemoteControlService> logger,
    IMediator mediator,
    IPermissionBroadcaster broadcaster) : Synchronizer.RemoteControlService.RemoteControlServiceBase
{
    public override async Task<Empty> BroadcastSessionCode(SessionAuthentication request, ServerCallContext context)
    {
        BroadcastSessionCodeCommand command = new(request.Code);
        await mediator.Send(command, context.CancellationToken);

        return new Empty();
    }

    public override async Task<Empty> BroadcastSessionError(SessionControlError request, ServerCallContext context)
    {
        SessionErrorReceivedCommand command = new(request.Message, request.Error);
        await mediator.Send(command, context.CancellationToken);

        return new Empty();
    }

    public override async Task<Empty> BroadcastSessionControlRequested(SessionControlRequested request,
        ServerCallContext context)
    {
        BroadcastSessionControlConnectCommand command = new(request.UserName);
        await mediator.Send(command, context.CancellationToken);

        return new Empty();
    }

    public override async Task SubscribeToPermissionProvided(Empty request,
        IServerStreamWriter<SessionPermissions> responseStream,
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