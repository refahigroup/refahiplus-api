using Microsoft.EntityFrameworkCore;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence;

public sealed class CreditPaymentsDbContext(DbContextOptions<CreditPaymentsDbContext> options)
    : DbContext(options)
{
    public const string DefaultSchema = "credit_payments";
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<TerminalCredential> TerminalCredentials => Set<TerminalCredential>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<GatewayPayment> GatewayPayments => Set<GatewayPayment>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<CheckoutSession> CheckoutSessions => Set<CheckoutSession>();
    public DbSet<CheckoutOtpChallenge> CheckoutOtpChallenges => Set<CheckoutOtpChallenge>();
    public DbSet<FundingContinuation> FundingContinuations => Set<FundingContinuation>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<PaymentTermination> PaymentTerminations => Set<PaymentTermination>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<RiskVelocityCounter> RiskVelocityCounters => Set<RiskVelocityCounter>();
    public DbSet<RiskReservation> RiskReservations => Set<RiskReservation>();
    public DbSet<RiskDecisionRecord> RiskDecisions => Set<RiskDecisionRecord>();
    public DbSet<ReconciliationRun> ReconciliationRuns => Set<ReconciliationRun>();
    public DbSet<ReconciliationFinding> ReconciliationFindings => Set<ReconciliationFinding>();
    public DbSet<CreditPaymentIntegrationEvent> IntegrationEvents => Set<CreditPaymentIntegrationEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CreditPaymentsDbContext).Assembly);
    }
}
