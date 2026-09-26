using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence;

public sealed class MerchantConfiguration:IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> b){b.ToTable("merchants",t=>{t.HasCheckConstraint("ck_merchants_status","status IN (1,2,3,4)");t.HasCheckConstraint("ck_merchants_entity_type","entity_type IN (1,2)");});b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("merchant_id");b.Property(x=>x.EntityType).HasColumnName("entity_type").HasConversion<short>().IsRequired();b.Property(x=>x.NationalId).HasColumnName("national_id").HasMaxLength(11).IsRequired();b.Property(x=>x.LegalName).HasColumnName("legal_name").HasMaxLength(200).IsRequired();b.Property(x=>x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();b.Property(x=>x.SettlementWalletId).HasColumnName("settlement_wallet_id");b.Property(x=>x.Status).HasColumnName("status").HasConversion<short>();b.Property(x=>x.CreatedAt).HasColumnName("created_at");b.Property(x=>x.UpdatedAt).HasColumnName("updated_at");b.Property(x=>x.Version).HasColumnName("version").IsConcurrencyToken();b.HasIndex(x=>new{x.EntityType,x.NationalId}).IsUnique();b.HasMany(x=>x.Terminals).WithOne().HasForeignKey(x=>x.MerchantId).OnDelete(DeleteBehavior.Restrict);b.Navigation(x=>x.Terminals).UsePropertyAccessMode(PropertyAccessMode.Field);}
}
public sealed class TerminalConfiguration:IEntityTypeConfiguration<Terminal>
{
    public void Configure(EntityTypeBuilder<Terminal> b){b.ToTable("terminals",t=>{t.HasCheckConstraint("ck_terminals_status","status IN (1,2,3)");t.HasCheckConstraint("ck_terminals_amounts","min_payment_amount_minor > 0 AND max_payment_amount_minor > 0 AND min_payment_amount_minor <= max_payment_amount_minor");});b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("terminal_id");b.Property(x=>x.MerchantId).HasColumnName("merchant_id");b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();b.Property(x=>x.Status).HasColumnName("status").HasConversion<short>();b.Property(x=>x.ApprovedCategoryCode).HasColumnName("approved_category_code").HasMaxLength(100).IsRequired();b.Property(x=>x.MinPaymentAmountMinor).HasColumnName("min_payment_amount_minor");b.Property(x=>x.MaxPaymentAmountMinor).HasColumnName("max_payment_amount_minor");b.Property(x=>x.CreatedAt).HasColumnName("created_at");b.Property(x=>x.UpdatedAt).HasColumnName("updated_at");b.Property(x=>x.Version).HasColumnName("version").IsConcurrencyToken();b.HasIndex(x=>new{x.MerchantId,x.Name}).IsUnique();b.HasMany(x=>x.ReturnUrls).WithOne().HasForeignKey(x=>x.TerminalId).OnDelete(DeleteBehavior.Cascade);b.Navigation(x=>x.ReturnUrls).UsePropertyAccessMode(PropertyAccessMode.Field);b.HasMany(x=>x.Credentials).WithOne().HasForeignKey(x=>x.TerminalId).OnDelete(DeleteBehavior.Restrict);b.Navigation(x=>x.Credentials).UsePropertyAccessMode(PropertyAccessMode.Field);b.HasOne(x=>x.WebhookEndpoint).WithOne().HasForeignKey<WebhookEndpoint>(x=>x.TerminalId).OnDelete(DeleteBehavior.Restrict);}
}
public sealed class ReturnUrlConfiguration:IEntityTypeConfiguration<TerminalReturnUrl>
{
    public void Configure(EntityTypeBuilder<TerminalReturnUrl>b){b.ToTable("terminal_return_urls");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("return_url_id");b.Property(x=>x.TerminalId).HasColumnName("terminal_id");b.Property(x=>x.CanonicalUrl).HasColumnName("canonical_url").HasMaxLength(2048).IsRequired();b.Property(x=>x.CreatedAt).HasColumnName("created_at");b.HasIndex(x=>new{x.TerminalId,x.CanonicalUrl}).IsUnique();}
}
public sealed class CredentialConfiguration:IEntityTypeConfiguration<TerminalCredential>
{
    public void Configure(EntityTypeBuilder<TerminalCredential>b){b.ToTable("terminal_credentials",t=>{t.HasCheckConstraint("ck_credentials_status","status IN (1,2)");t.HasCheckConstraint("ck_credentials_hash_version","secret_hash_version > 0");t.HasCheckConstraint("ck_credentials_expiration","expires_at IS NULL OR expires_at > created_at");});b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("credential_id");b.Property(x=>x.TerminalId).HasColumnName("terminal_id");b.Property(x=>x.ClientId).HasColumnName("client_id").HasMaxLength(100).IsRequired();b.Property(x=>x.SecretHash).HasColumnName("secret_hash").HasMaxLength(500).IsRequired();b.Property(x=>x.SecretHashAlgorithm).HasColumnName("secret_hash_algorithm").HasMaxLength(30).IsRequired();b.Property(x=>x.SecretHashVersion).HasColumnName("secret_hash_version");b.Property(x=>x.Scopes).HasColumnName("scopes").HasMaxLength(500).IsRequired();b.Property(x=>x.Status).HasColumnName("status").HasConversion<short>();b.Property(x=>x.Label).HasColumnName("label").HasMaxLength(100).IsRequired();b.Property(x=>x.CreatedAt).HasColumnName("created_at");b.Property(x=>x.ExpiresAt).HasColumnName("expires_at");b.Property(x=>x.LastUsedAt).HasColumnName("last_used_at");b.Property(x=>x.RevokedAt).HasColumnName("revoked_at");b.HasIndex(x=>x.ClientId).IsUnique();}
}
public sealed class WebhookConfiguration:IEntityTypeConfiguration<WebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WebhookEndpoint>b){b.ToTable("webhook_endpoints",t=>{t.HasCheckConstraint("ck_webhooks_status","status IN (1,2)");t.HasCheckConstraint("ck_webhooks_secret_version","signing_secret_version > 0");t.HasCheckConstraint("ck_webhooks_https","url LIKE 'https://%'");});b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("webhook_endpoint_id");b.Property(x=>x.TerminalId).HasColumnName("terminal_id");b.Property(x=>x.Url).HasColumnName("url").HasMaxLength(2048).IsRequired();b.Property(x=>x.Status).HasColumnName("status").HasConversion<short>();b.Property(x=>x.SigningSecretCiphertext).HasColumnName("signing_secret_ciphertext").HasMaxLength(2000).IsRequired();b.Property(x=>x.SigningSecretVersion).HasColumnName("signing_secret_version");b.Property(x=>x.CreatedAt).HasColumnName("created_at");b.Property(x=>x.UpdatedAt).HasColumnName("updated_at");b.HasIndex(x=>x.TerminalId).IsUnique();}
}
public sealed class AuditConfiguration:IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord>b){b.ToTable("audit_records");b.HasKey(x=>x.Id);b.Property(x=>x.Id).HasColumnName("audit_record_id");b.Property(x=>x.Action).HasColumnName("action").HasMaxLength(100).IsRequired();b.Property(x=>x.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();b.Property(x=>x.EntityId).HasColumnName("entity_id");b.Property(x=>x.ActorId).HasColumnName("actor_id").HasMaxLength(100);b.Property(x=>x.MerchantId).HasColumnName("merchant_id");b.Property(x=>x.TerminalId).HasColumnName("terminal_id");b.Property(x=>x.MerchantOrderId).HasColumnName("merchant_order_id").HasMaxLength(100);b.Property(x=>x.OccurredAt).HasColumnName("occurred_at");b.HasIndex(x=>new{x.EntityType,x.EntityId,x.OccurredAt});}
}

public sealed class GatewayPaymentConfiguration : IEntityTypeConfiguration<GatewayPayment>
{
    public void Configure(EntityTypeBuilder<GatewayPayment> b)
    {
        b.ToTable("gateway_payments", t =>
        {
            t.HasCheckConstraint("ck_gateway_payments_amount", "amount_minor > 0");
            t.HasCheckConstraint("ck_gateway_payments_currency", "currency = 'IRR'");
            t.HasCheckConstraint("ck_gateway_payments_status", "status IN (1,2,3,4,5,6,7,8,9,10)");
            t.HasCheckConstraint("ck_gateway_payments_order_length", "char_length(merchant_order_id) BETWEEN 1 AND 100");
            t.HasCheckConstraint("ck_gateway_payments_description_length", "description IS NULL OR char_length(description) <= 250");
            t.HasCheckConstraint("ck_gateway_payments_return_url_length", "char_length(return_url) BETWEEN 1 AND 2048");
            t.HasCheckConstraint("ck_gateway_payments_metadata_length", "octet_length(metadata::text) <= 4096");
            t.HasCheckConstraint("ck_gateway_payments_expiry", "expires_at > created_at");
            t.HasCheckConstraint("ck_gateway_payments_version", "version > 0");
            t.HasCheckConstraint(
                "ck_gateway_payments_order_binding",
                "(order_id IS NULL AND order_bound_at IS NULL) OR (order_id IS NOT NULL AND order_bound_at IS NOT NULL)");
            t.HasCheckConstraint("ck_gateway_payments_captured_amount",
                "captured_amount_minor >= 0 AND captured_amount_minor <= amount_minor");
            t.HasCheckConstraint("ck_gateway_payments_refunded_amount",
                "refunded_amount_minor >= 0 AND refunded_amount_minor <= captured_amount_minor");
            t.HasCheckConstraint("ck_gateway_payments_success",
                "status <> 5 OR (captured_amount_minor = amount_minor AND paid_at IS NOT NULL AND wallet_payment_id IS NOT NULL)");
            t.HasCheckConstraint("ck_gateway_payments_refund_state",
                "(status <> 9 OR (refunded_amount_minor > 0 AND refunded_amount_minor < captured_amount_minor)) AND (status <> 10 OR (captured_amount_minor > 0 AND refunded_amount_minor = captured_amount_minor))");
            t.HasCheckConstraint("ck_gateway_payments_policy",
                "(policy_id IS NULL AND policy_version IS NULL AND policy_hash IS NULL) OR (policy_id IS NOT NULL AND policy_version IS NOT NULL AND policy_hash IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("gateway_payment_id");
        b.Property(x => x.MerchantId).HasColumnName("merchant_id");
        b.Property(x => x.TerminalId).HasColumnName("terminal_id");
        b.Property(x => x.MerchantOrderId).HasColumnName("merchant_order_id").HasMaxLength(100).IsRequired();
        b.Property(x => x.AmountMinor).HasColumnName("amount_minor");
        b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(250);
        b.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        b.Property(x => x.ReturnUrl).HasColumnName("return_url").HasMaxLength(2048).IsRequired();
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.MerchantDisplayNameSnapshot).HasColumnName("merchant_display_name_snapshot").HasMaxLength(200).IsRequired();
        b.Property(x => x.TerminalNameSnapshot).HasColumnName("terminal_name_snapshot").HasMaxLength(150).IsRequired();
        b.Property(x => x.ApprovedCategoryCodeSnapshot).HasColumnName("approved_category_code_snapshot").HasMaxLength(100).IsRequired();
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.OrderId).HasColumnName("order_id");
        b.Property(x => x.OrderBoundAt).HasColumnName("order_bound_at");
        b.Property(x => x.CapturedAmountMinor).HasColumnName("captured_amount_minor");
        b.Property(x => x.RefundedAmountMinor).HasColumnName("refunded_amount_minor");
        b.Property(x => x.WalletPaymentId).HasColumnName("wallet_payment_id");
        b.Property(x => x.PaidAt).HasColumnName("paid_at");
        b.Property(x => x.PolicyId).HasColumnName("policy_id").HasMaxLength(100);
        b.Property(x => x.PolicyVersion).HasColumnName("policy_version").HasMaxLength(100);
        b.Property(x => x.PolicyHash).HasColumnName("policy_hash").HasMaxLength(64);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => new { x.TerminalId, x.MerchantOrderId }).IsUnique().HasDatabaseName("ux_gateway_payments_terminal_order");
        b.HasIndex(x => x.OrderId).IsUnique().HasFilter("\"order_id\" IS NOT NULL")
            .HasDatabaseName("ux_gateway_payments_order_id");
        b.HasIndex(x => x.WalletPaymentId).IsUnique().HasFilter("\"wallet_payment_id\" IS NOT NULL")
            .HasDatabaseName("ux_gateway_payments_wallet_payment_id");
        b.HasIndex(x => new { x.Status, x.ExpiresAt })
            .HasDatabaseName("ix_gateway_payments_expiration");
        b.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Terminal>().WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> b)
    {
        b.ToTable("payment_attempts", t =>
        {
            t.HasCheckConstraint("ck_payment_attempts_sequence", "attempt_sequence > 0");
            t.HasCheckConstraint("ck_payment_attempts_trigger", "trigger IN (1,2,3)");
            t.HasCheckConstraint("ck_payment_attempts_status", "status IN (1,2,3,4,5)");
            t.HasCheckConstraint("ck_payment_attempts_retry", "retry_count >= 0");
            t.HasCheckConstraint("ck_payment_attempts_version", "version > 0");
            t.HasCheckConstraint("ck_payment_attempts_lease",
                "(lease_owner IS NULL AND lease_until IS NULL) OR (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
            t.HasCheckConstraint("ck_payment_attempts_completion",
                "(status IN (1,2,3) AND completed_at IS NULL) OR (status IN (4,5) AND completed_at IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("payment_attempt_id");
        b.Property(x => x.GatewayPaymentId).HasColumnName("gateway_payment_id");
        b.Property(x => x.AttemptSequence).HasColumnName("attempt_sequence");
        b.Property(x => x.Trigger).HasColumnName("trigger").HasConversion<short>();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        b.Property(x => x.NormalizedOutcomeCode).HasColumnName("normalized_outcome_code").HasMaxLength(100);
        b.Property(x => x.OrderId).HasColumnName("order_id");
        b.Property(x => x.PolicyId).HasColumnName("policy_id").HasMaxLength(100).IsRequired();
        b.Property(x => x.PolicyVersion).HasColumnName("policy_version").HasMaxLength(100).IsRequired();
        b.Property(x => x.PolicyHash).HasColumnName("policy_hash").HasMaxLength(64).IsRequired();
        b.Property(x => x.DestinationWalletId).HasColumnName("destination_wallet_id");
        b.Property(x => x.WalletIntentId).HasColumnName("wallet_intent_id");
        b.Property(x => x.WalletPaymentId).HasColumnName("wallet_payment_id");
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.LastCheckedAt).HasColumnName("last_checked_at");
        b.Property(x => x.NextRetryAt).HasColumnName("next_retry_at");
        b.Property(x => x.RetryCount).HasColumnName("retry_count");
        b.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(100);
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => new { x.GatewayPaymentId, x.AttemptSequence }).IsUnique()
            .HasDatabaseName("ux_payment_attempts_payment_sequence");
        b.HasIndex(x => x.GatewayPaymentId).IsUnique()
            .HasFilter("\"status\" IN (1,2,3)")
            .HasDatabaseName("ux_payment_attempts_active_payment");
        b.HasIndex(x => x.WalletPaymentId).IsUnique()
            .HasFilter("\"wallet_payment_id\" IS NOT NULL")
            .HasDatabaseName("ux_payment_attempts_wallet_payment_id");
        b.HasIndex(x => new { x.Status, x.NextRetryAt, x.LeaseUntil })
            .HasDatabaseName("ix_payment_attempts_recovery");
        b.HasOne<GatewayPayment>().WithMany().HasForeignKey(x => x.GatewayPaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentTerminationConfiguration : IEntityTypeConfiguration<PaymentTermination>
{
    public void Configure(EntityTypeBuilder<PaymentTermination> b)
    {
        b.ToTable("payment_terminations",t=>
        {
            t.HasCheckConstraint("ck_payment_terminations_kind","kind IN (1,2)");
            t.HasCheckConstraint("ck_payment_terminations_source","source IN (1,2,3)");
            t.HasCheckConstraint("ck_payment_terminations_status","status IN (1,2,3,4)");
            t.HasCheckConstraint("ck_payment_terminations_reason","reason IS NULL OR char_length(reason) <= 250");
            t.HasCheckConstraint("ck_payment_terminations_retry","retry_count >= 0");
            t.HasCheckConstraint("ck_payment_terminations_version","version > 0");
            t.HasCheckConstraint("ck_payment_terminations_resolution",
                "(status IN (1,2) AND resolved_at IS NULL) OR (status IN (3,4) AND resolved_at IS NOT NULL)");
        });
        b.HasKey(x=>x.Id);
        b.Property(x=>x.Id).HasColumnName("payment_termination_id");
        b.Property(x=>x.GatewayPaymentId).HasColumnName("gateway_payment_id");
        b.Property(x=>x.Kind).HasColumnName("kind").HasConversion<short>();
        b.Property(x=>x.Source).HasColumnName("source").HasConversion<short>();
        b.Property(x=>x.Status).HasColumnName("status").HasConversion<short>();
        b.Property(x=>x.Reason).HasColumnName("reason").HasMaxLength(250);
        b.Property(x=>x.ReleaseReference).HasColumnName("release_reference").HasMaxLength(128).IsRequired();
        b.Property(x=>x.RequestedAt).HasColumnName("requested_at");
        b.Property(x=>x.ResolvedAt).HasColumnName("resolved_at");
        b.Property(x=>x.LastCheckedAt).HasColumnName("last_checked_at");
        b.Property(x=>x.NextRetryAt).HasColumnName("next_retry_at");
        b.Property(x=>x.RetryCount).HasColumnName("retry_count");
        b.Property(x=>x.CreatedAt).HasColumnName("created_at");
        b.Property(x=>x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x=>x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x=>x.GatewayPaymentId).IsUnique().HasDatabaseName("ux_payment_terminations_payment");
        b.HasIndex(x=>new{x.Status,x.NextRetryAt}).HasDatabaseName("ix_payment_terminations_recovery");
        b.HasOne<GatewayPayment>().WithMany().HasForeignKey(x=>x.GatewayPaymentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CheckoutSessionConfiguration : IEntityTypeConfiguration<CheckoutSession>
{
    public void Configure(EntityTypeBuilder<CheckoutSession> b)
    {
        b.ToTable("checkout_sessions", t =>
        {
            t.HasCheckConstraint("ck_checkout_sessions_status", "status IN (1,2,3,4,5,6,7)");
            t.HasCheckConstraint("ck_checkout_sessions_nonce", "octet_length(token_nonce) = 32");
            t.HasCheckConstraint("ck_checkout_sessions_hash", "octet_length(token_hash) = 32");
            t.HasCheckConstraint("ck_checkout_sessions_key_version", "token_key_version > 0");
            t.HasCheckConstraint("ck_checkout_sessions_expiry", "expires_at > created_at");
            t.HasCheckConstraint("ck_checkout_sessions_version", "version > 0");
            t.HasCheckConstraint("ck_checkout_sessions_auth_consistency",
                "(status = 1 AND customer_id IS NULL AND authenticated_at IS NULL AND authorization_secret_hash IS NULL AND authorization_expires_at IS NULL) OR " +
                "(status IN (2,3,4) AND customer_id IS NOT NULL AND authenticated_at IS NOT NULL AND octet_length(authorization_secret_hash) = 32 AND authorization_expires_at IS NOT NULL AND authorization_expires_at <= expires_at) OR status IN (5,6,7)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("checkout_session_id");
        b.Property(x => x.GatewayPaymentId).HasColumnName("gateway_payment_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        b.Property(x => x.TokenNonce).HasColumnName("token_nonce").HasColumnType("bytea").IsRequired();
        b.Property(x => x.TokenHash).HasColumnName("token_hash").HasColumnType("bytea").IsRequired();
        b.Property(x => x.TokenKeyVersion).HasColumnName("token_key_version");
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.AuthenticatedAt).HasColumnName("authenticated_at");
        b.Property(x => x.AuthorizationSecretHash).HasColumnName("authorization_secret_hash").HasColumnType("bytea");
        b.Property(x => x.AuthorizationExpiresAt).HasColumnName("authorization_expires_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_checkout_sessions_token_hash");
        b.HasIndex(x => x.GatewayPaymentId).IsUnique()
            .HasFilter("\"status\" IN (1,2,3,4)")
            .HasDatabaseName("ux_checkout_sessions_active_payment");
        b.HasOne<GatewayPayment>().WithMany().HasForeignKey(x => x.GatewayPaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CheckoutOtpChallengeConfiguration : IEntityTypeConfiguration<CheckoutOtpChallenge>
{
    public void Configure(EntityTypeBuilder<CheckoutOtpChallenge> b)
    {
        b.ToTable("checkout_otp_challenges", t =>
        {
            t.HasCheckConstraint("ck_checkout_otp_status", "status IN (1,2,3,4,5,6,7)");
            t.HasCheckConstraint("ck_checkout_otp_expiry", "expires_at > sent_at");
            t.HasCheckConstraint("ck_checkout_otp_attempts", "verify_attempt_count >= 0");
            t.HasCheckConstraint("ck_checkout_otp_sequence", "send_sequence > 0");
            t.HasCheckConstraint("ck_checkout_otp_reference_consistency",
                "(status = 2 AND resolved_customer_id IS NOT NULL AND protected_reference_code IS NOT NULL) OR " +
                "(status = 3 AND resolved_customer_id IS NULL AND protected_reference_code IS NULL) OR status IN (1,4,5,6,7)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("checkout_otp_challenge_id");
        b.Property(x => x.CheckoutSessionId).HasColumnName("checkout_session_id");
        b.Property(x => x.ResolvedCustomerId).HasColumnName("resolved_customer_id");
        b.Property(x => x.ProtectedReferenceCode).HasColumnName("protected_reference_code").HasMaxLength(4096);
        b.Property(x => x.OtpType).HasColumnName("otp_type");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        b.Property(x => x.SentAt).HasColumnName("sent_at");
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.VerifyAttemptCount).HasColumnName("verify_attempt_count");
        b.Property(x => x.SendSequence).HasColumnName("send_sequence");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.CheckoutSessionId, x.SendSequence }).IsUnique()
            .HasDatabaseName("ux_checkout_otp_session_sequence");
        b.HasIndex(x => x.CheckoutSessionId).IsUnique()
            .HasFilter("\"status\" IN (2,3)")
            .HasDatabaseName("ux_checkout_otp_active_session");
        b.HasOne<CheckoutSession>().WithMany().HasForeignKey(x => x.CheckoutSessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("idempotency_records", t =>
        {
            t.HasCheckConstraint("ck_idempotency_hash", "octet_length(request_hash) = 32");
            t.HasCheckConstraint("ck_idempotency_hash_version", "request_hash_version = 1");
            t.HasCheckConstraint("ck_idempotency_state", "state IN (1,2)");
            t.HasCheckConstraint("ck_idempotency_resource_state", "(state = 1 AND resource_id IS NULL AND completed_at IS NULL) OR (state = 2 AND resource_id IS NOT NULL AND completed_at IS NOT NULL)");
            t.HasCheckConstraint("ck_idempotency_key_length", "char_length(idempotency_key) BETWEEN 8 AND 128");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("idempotency_record_id");
        b.Property(x => x.TerminalId).HasColumnName("terminal_id");
        b.Property(x => x.OperationType).HasColumnName("operation_type").HasMaxLength(50).IsRequired();
        b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(128).IsRequired();
        b.Property(x => x.RequestHash).HasColumnName("request_hash").HasColumnType("bytea").IsRequired();
        b.Property(x => x.RequestHashVersion).HasColumnName("request_hash_version");
        b.Property(x => x.ResourceId).HasColumnName("resource_id");
        b.Property(x => x.State).HasColumnName("state").HasConversion<short>();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.HasIndex(x => new { x.TerminalId, x.OperationType, x.IdempotencyKey }).IsUnique().HasDatabaseName("ux_idempotency_terminal_operation_key");
        b.HasOne<Terminal>().WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
        // ResourceId is a polymorphic logical reference (payment/refund); no cross-type FK.
    }
}

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> b)
    {
        b.ToTable("refunds", t =>
        {
            t.HasCheckConstraint("ck_refunds_amount", "amount_minor > 0");
            t.HasCheckConstraint("ck_refunds_currency", "currency = 'IRR'");
            t.HasCheckConstraint("ck_refunds_status", "status IN (1,2,3,4,5)");
            t.HasCheckConstraint("ck_refunds_retry", "retry_count >= 0 AND order_projection_retry_count >= 0");
            t.HasCheckConstraint("ck_refunds_version", "version > 0");
            t.HasCheckConstraint("ck_refunds_lease", "(lease_owner IS NULL AND lease_until IS NULL) OR (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
            t.HasCheckConstraint("ck_refunds_completion", "(status IN (1,2,3) AND completed_at IS NULL) OR (status IN (4,5) AND completed_at IS NOT NULL)");
            t.HasCheckConstraint("ck_refunds_projection_status", "order_projection_status IN (1,2,3)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("refund_id");
        b.Property(x => x.GatewayPaymentId).HasColumnName("gateway_payment_id");
        b.Property(x => x.TerminalId).HasColumnName("terminal_id");
        b.Property(x => x.AmountMinor).HasColumnName("amount_minor");
        b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3);
        b.Property(x => x.MerchantRefundReference).HasColumnName("merchant_refund_reference").HasMaxLength(100);
        b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(250);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        b.Property(x => x.NormalizedFailureCode).HasColumnName("normalized_failure_code").HasMaxLength(100);
        b.Property(x => x.WalletRefundId).HasColumnName("wallet_refund_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.ProcessingStartedAt).HasColumnName("processing_started_at");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.LastCheckedAt).HasColumnName("last_checked_at");
        b.Property(x => x.NextRetryAt).HasColumnName("next_retry_at");
        b.Property(x => x.RetryCount).HasColumnName("retry_count");
        b.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(100);
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until");
        b.Property(x => x.OrderProjectionStatus).HasColumnName("order_projection_status").HasConversion<short>();
        b.Property(x => x.OrderProjectionRetryCount).HasColumnName("order_projection_retry_count");
        b.Property(x => x.OrderProjectionNextRetryAt).HasColumnName("order_projection_next_retry_at");
        b.Property(x => x.OrderRefundNotifiedAt).HasColumnName("order_refund_notified_at");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => x.GatewayPaymentId).HasDatabaseName("ix_refunds_gateway_payment");
        b.HasIndex(x => new { x.Status, x.NextRetryAt, x.LeaseUntil }).HasDatabaseName("ix_refunds_recovery");
        b.HasIndex(x => x.WalletRefundId).IsUnique().HasFilter("\"wallet_refund_id\" IS NOT NULL").HasDatabaseName("ux_refunds_wallet_refund");
        b.HasIndex(x => new { x.TerminalId, x.MerchantRefundReference }).IsUnique()
            .HasFilter("\"merchant_refund_reference\" IS NOT NULL").HasDatabaseName("ux_refunds_terminal_merchant_reference");
        b.HasOne<GatewayPayment>().WithMany().HasForeignKey(x => x.GatewayPaymentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Terminal>().WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FundingContinuationConfiguration : IEntityTypeConfiguration<FundingContinuation>
{
    public void Configure(EntityTypeBuilder<FundingContinuation> b)
    {
        b.ToTable("funding_continuations", t =>
        {
            t.HasCheckConstraint("ck_funding_continuations_amount", "required_deficit_minor > 0");
            t.HasCheckConstraint("ck_funding_continuations_currency", "currency = 'IRR'");
            t.HasCheckConstraint("ck_funding_continuations_status", "status IN (1,2,3,4,5,6)");
            t.HasCheckConstraint("ck_funding_continuations_version", "version > 0");
            t.HasCheckConstraint("ck_funding_continuations_retry", "retry_count >= 0");
            t.HasCheckConstraint("ck_funding_continuations_consent", "auto_resume_consent_at IS NOT NULL");
            t.HasCheckConstraint("ck_funding_continuations_completion",
                "(status IN (1,2,3) AND completed_at IS NULL) OR (status IN (4,5,6) AND completed_at IS NOT NULL)");
            t.HasCheckConstraint("ck_funding_continuations_lease",
                "(lease_owner IS NULL AND lease_until IS NULL) OR (lease_owner IS NOT NULL AND lease_until IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("funding_continuation_id");
        b.Property(x => x.GatewayPaymentId).HasColumnName("gateway_payment_id");
        b.Property(x => x.CheckoutSessionId).HasColumnName("checkout_session_id");
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.PersonalWalletId).HasColumnName("personal_wallet_id");
        b.Property(x => x.RequiredDeficitMinor).HasColumnName("required_deficit_minor");
        b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        b.Property(x => x.ClientReference).HasColumnName("client_reference").HasMaxLength(128).IsRequired();
        b.Property(x => x.WalletFundingSessionId).HasColumnName("wallet_funding_session_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<short>();
        b.Property(x => x.AutoResumeConsentAt).HasColumnName("auto_resume_consent_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.FundingStartedAt).HasColumnName("funding_started_at");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.LastCheckedAt).HasColumnName("last_checked_at");
        b.Property(x => x.NextRetryAt).HasColumnName("next_retry_at");
        b.Property(x => x.RetryCount).HasColumnName("retry_count");
        b.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(100);
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => x.ClientReference).IsUnique()
            .HasDatabaseName("ux_funding_continuations_client_reference");
        b.HasIndex(x => x.WalletFundingSessionId).IsUnique()
            .HasFilter("\"wallet_funding_session_id\" IS NOT NULL")
            .HasDatabaseName("ux_funding_continuations_wallet_funding_session");
        b.HasIndex(x => x.GatewayPaymentId).IsUnique()
            .HasFilter("\"status\" IN (1,2,3)")
            .HasDatabaseName("ux_funding_continuations_active_payment");
        b.HasIndex(x => new { x.Status, x.NextRetryAt, x.LeaseUntil })
            .HasDatabaseName("ix_funding_continuations_recovery");
        b.HasOne<GatewayPayment>().WithMany().HasForeignKey(x => x.GatewayPaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CheckoutSession>().WithMany().HasForeignKey(x => x.CheckoutSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
