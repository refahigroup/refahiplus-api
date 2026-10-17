using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application;
internal static class PoolPublicStates
{
 public static string Purchase(PoolPurchaseStatus value)=>value switch
 {
  PoolPurchaseStatus.AwaitingPayment=>"AwaitingPayment",PoolPurchaseStatus.Paid=>"Paid",PoolPurchaseStatus.CancellationPending=>"CancellationPending",
  PoolPurchaseStatus.Cancelled=>"Cancelled",PoolPurchaseStatus.Refunded=>"Refunded",_=>throw new InvalidOperationException("وضعیت خرید معتبر نیست")
 };
 public static string? Voucher(PoolVoucherStatus? value)=>value switch
 {
  null=>null,PoolVoucherStatus.Issued=>"Issued",PoolVoucherStatus.Redeemed=>"Redeemed",PoolVoucherStatus.Revoked=>"Revoked",PoolVoucherStatus.Expired=>"Expired",
  _=>throw new InvalidOperationException("وضعیت ووچر معتبر نیست")
 };
}
