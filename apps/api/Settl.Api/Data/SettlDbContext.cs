using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Settl.Api.Domain;

namespace Settl.Api.Data;

/// <summary>
/// <see cref="IdentityUserContext{TUser,TKey}"/> (not the role-aware
/// <c>IdentityDbContext</c>) — ADR-0005 explicitly has no role concept.
/// </summary>
public class SettlDbContext(DbContextOptions<SettlDbContext> options) : IdentityUserContext<Member, Guid>(options)
{
    /// <summary>Alias for the base class's <see cref="IdentityUserContext{TUser,TKey}.Users"/> —
    /// every call site here predates Identity and says "Members".</summary>
    public DbSet<Member> Members => Users;

    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMembership> HouseholdMemberships => Set<HouseholdMembership>();
    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<EntryShare> EntryShares => Set<EntryShare>();
    public DbSet<RecurringTemplate> RecurringTemplates => Set<RecurringTemplate>();
    public DbSet<RecurringShare> RecurringShares => Set<RecurringShare>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<SettlementClosure> SettlementClosures => Set<SettlementClosure>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<EmittedNudge> EmittedNudges => Set<EmittedNudge>();
    public DbSet<LedgerEvent> LedgerEvents => Set<LedgerEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Member>(e =>
        {
            e.ToTable("Members");
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.AvatarColor).IsRequired();
            // Nullable emoji (contacts-phone-sms spec); cap the column so a single grapheme's worth of
            // code units (incl. ZWJ sequences) fits but nothing larger can be stored.
            e.Property(x => x.AvatarEmoji).HasMaxLength(32);
            // Stored as the enum name (like every other domain enum here); the default keeps
            // existing rows on "Direct" — the product default this setting now exposes.
            e.Property(x => x.NudgeTone).HasConversion<string>().IsRequired().HasDefaultValue(NudgeTone.Direct);
            // Nudge-digest emails default OFF — an explicit opt-in via the profile switch
            // (reminder-delivery spec). New rows fall to disabled via this default.
            e.Property(x => x.NudgeEmailsEnabled).IsRequired().HasDefaultValue(false);
            // Trust-notification read cursor (trust-notifications-v1 spec) — nullable; null means "never opened".
            e.Property(x => x.NotificationsSeenAt);
            e.Ignore(x => x.Initial);
        });

        b.Entity<Household>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Currency).IsRequired().HasDefaultValue("SEK");
            // Owner (household-ownership spec): a plain member reference, not a navigation — the owner is
            // always covered by a HouseholdMembership row, so no extra FK is needed.
            e.Property(x => x.OwnerMemberId).IsRequired();
        });

        b.Entity<HouseholdMembership>(e =>
        {
            e.HasKey(x => new { x.HouseholdId, x.MemberId });
            e.HasOne(x => x.Household).WithMany(h => h.Memberships)
                .HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Member).WithMany(m => m.Memberships)
                .HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Entry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().IsRequired();
            e.Property(x => x.SplitMode).HasConversion<string>().IsRequired();
            e.Property(x => x.Category).HasConversion<string>().IsRequired();
            e.Property(x => x.Title).IsRequired();
            e.HasOne(x => x.Household).WithMany(h => h.Entries)
                .HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.RecurringTemplate).WithMany(t => t.PostedEntries)
                .HasForeignKey(x => x.RecurringTemplateId).OnDelete(DeleteBehavior.SetNull);
            // Idempotency guard for recurring posts. NULL RecurringTemplateId rows are treated as
            // distinct by SQLite and Postgres, so non-recurring entries are unaffected.
            e.HasIndex(x => new { x.RecurringTemplateId, x.Date })
                .IsUnique()
                .HasDatabaseName("IX_Entry_RecurringTemplate_Date");
        });

        b.Entity<EntryShare>(e =>
        {
            e.HasKey(x => new { x.EntryId, x.MemberId });
            e.HasOne(x => x.Entry).WithMany(en => en.Shares)
                .HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Member).WithMany()
                .HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RecurringTemplate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Cadence).HasConversion<string>().IsRequired();
            e.Property(x => x.SplitMode).HasConversion<string>().IsRequired();
            e.Property(x => x.Category).HasConversion<string>().IsRequired();
            e.Property(x => x.Title).IsRequired();
            e.HasOne(x => x.Household).WithMany(h => h.RecurringTemplates)
                .HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RecurringShare>(e =>
        {
            e.HasKey(x => new { x.RecurringTemplateId, x.MemberId });
            e.HasOne(x => x.RecurringTemplate).WithMany(t => t.Shares)
                .HasForeignKey(x => x.RecurringTemplateId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Member).WithMany()
                .HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Settlement>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Household).WithMany(h => h.Settlements)
                .HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SettlementClosure>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Settlement).WithMany(s => s.Closures)
                .HasForeignKey(x => x.SettlementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Entry).WithMany()
                .HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Restrict);
            // A debt (entry + debtor/creditor) can be closed once.
            e.HasIndex(x => new { x.EntryId, x.DebtorMemberId, x.CreditorMemberId })
                .IsUnique()
                .HasDatabaseName("IX_SettlementClosure_Entry_Debtor_Creditor");
        });

        b.Entity<Invite>(e =>
        {
            e.HasKey(x => x.Id);
            // Email is null for SMS invites; PhoneNumber is null for email invites (contacts-phone-sms spec).
            e.Property(x => x.Channel).HasConversion<string>().IsRequired();
            e.Property(x => x.TokenHash).IsRequired();
            // HouseholdId is nullable (contact-only invites have none). Cascade so that
            // hard-deleting an empty household revokes its pending invites too (household-ownership spec);
            // contact-only invites have no household and are unaffected.
            e.HasOne(x => x.Household).WithMany()
                .HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.TokenHash).IsUnique();
        });

        b.Entity<Contact>(e =>
        {
            e.HasKey(x => new { x.OwnerMemberId, x.ContactMemberId });
            e.HasOne(x => x.OwnerMember).WithMany()
                .HasForeignKey(x => x.OwnerMemberId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ContactMember).WithMany()
                .HasForeignKey(x => x.ContactMemberId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EmittedNudge>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.NudgeKey).IsRequired();
            e.HasOne(x => x.Member).WithMany()
                .HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
            // Delivery-dedup guard: a given nudge identity is emailed to a member at most once
            // (reminder-delivery spec). The digest inserts a row per newly-sent key; this index
            // makes a re-send a no-op even under a racing/duplicate pass.
            e.HasIndex(x => new { x.MemberId, x.NudgeKey })
                .IsUnique()
                .HasDatabaseName("IX_EmittedNudge_Member_NudgeKey");
        });

        b.Entity<LedgerEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().IsRequired();
            e.Property(x => x.AffectedMemberIdsCsv).IsRequired();
            e.Property(x => x.PayloadJson).IsRequired();
            // Cascade with the household so archival/hard-delete of an empty household clears its
            // audit trail too. There is deliberately NO relationship to Entry/RecurringTemplate:
            // those may be hard-deleted, and the event must survive that (trust-notifications-v1 spec).
            e.HasOne(x => x.Household).WithMany()
                .HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            // The read projection loads a household's events newest-first.
            e.HasIndex(x => new { x.HouseholdId, x.OccurredAt })
                .HasDatabaseName("IX_LedgerEvent_Household_OccurredAt");
        });
    }
}
