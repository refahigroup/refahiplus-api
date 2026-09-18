using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Features.ExecuteOrderPayment;

namespace Refahi.Modules.Orders.Tests;

public sealed class ExecuteOrderPaymentCommandHandlerTests
{
    [Fact]
    public async Task Automatic_execution_uses_only_orders_suggested_allocations_and_stable_request_key()
    {
        var orderId=Guid.NewGuid();var userId=Guid.NewGuid();var destination=Guid.NewGuid();
        var firstWallet=Guid.NewGuid();var secondWallet=Guid.NewGuid();
        var mediator=new RecordingMediator(new OrderPaymentOptionsDto(orderId,"ORD",10_000,"IRR","Unpaid",[],
            [new(firstWallet,"OrgCredit",6_000,6_000),new(secondWallet,"REFAHI",4_000,4_000)],
            10_000,true,0));
        var handler=new ExecuteOrderPaymentCommandHandler(mediator);
        var result=await handler.Handle(new ExecuteOrderPaymentCommand(orderId,userId,"cpg-pay-stable",destination),default);
        Assert.Equal("Succeeded",result.ExecutionStatus);
        Assert.NotNull(mediator.PayCommand);
        Assert.Equal("cpg-pay-stable",mediator.PayCommand!.IdempotencyKey);
        Assert.Equal(destination,mediator.PayCommand.DestinationWalletId);
        Assert.Equal([(firstWallet,6_000L),(secondWallet,4_000L)],
            mediator.PayCommand.Allocations.Select(x=>(x.WalletId,x.AmountMinor)).ToArray());
    }

    [Fact]
    public async Task Insufficient_projection_never_dispatches_pay_order()
    {
        var orderId=Guid.NewGuid();var userId=Guid.NewGuid();
        var mediator=new RecordingMediator(new OrderPaymentOptionsDto(orderId,"ORD",10_000,"IRR","Unpaid",[],[],8_000,false,2_000));
        var result=await new ExecuteOrderPaymentCommandHandler(mediator).Handle(
            new ExecuteOrderPaymentCommand(orderId,userId,"cpg-pay-stable",Guid.NewGuid()),default);
        Assert.Equal("InsufficientFunds",result.ExecutionStatus);
        Assert.Null(mediator.PayCommand);
    }

    private sealed class RecordingMediator(OrderPaymentOptionsDto options):IMediator
    {
        public PayOrderCommand? PayCommand{get;private set;}
        private readonly Guid paymentId=Guid.NewGuid();
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request,CancellationToken ct=default)
        {
            object response=request switch
            {
                GetOrderPaymentOptionsQuery=>options,
                PayOrderCommand pay=>Record(pay),
                GetOrderPaymentExecutionStatusQuery query=>new OrderPaymentExecutionStatusDto(query.OrderId,query.UserId,
                    options.FinalAmountMinor,"IRR","CreditPayments",Guid.NewGuid(),"CreditPayments","store","Succeeded",Guid.NewGuid(),paymentId,DateTimeOffset.UtcNow),
                _=>throw new NotSupportedException(request.GetType().Name)
            };
            return Task.FromResult((TResponse)response);
        }
        private PayOrderResponse Record(PayOrderCommand command){PayCommand=command;return new(command.OrderId,paymentId,"Paid");}
        public Task<object?> Send(object request,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task Publish(object notification,CancellationToken ct=default)=>Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification,CancellationToken ct=default) where TNotification:INotification=>Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,CancellationToken ct=default)=>throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request,CancellationToken ct=default)=>throw new NotSupportedException();
    }
}
