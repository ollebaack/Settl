using Microsoft.EntityFrameworkCore;
using Settl.Api.Domain;

namespace Settl.Api.Data;

public class SettlDbContext(DbContextOptions<SettlDbContext> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMembership> HouseholdMemberships => Set<HouseholdMembership>();
    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<EntryShare> EntryShares => Set<EntryShare>();
    public DbSet<RecurringTemplate> RecurringTemplates => Set<RecurringTemplate>();
    public DbSet<RecurringShare> RecurringShares => Set<RecurringShare>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<SettlementClosure> SettlementClosures => Set<SettlementClosure>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Member>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.AvatarColor).IsRequired();
            e.Ignore(x => x.Initial);
        });

        b.Entity<Household>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.Currency).IsRequired().HasDefaultValue("SEK");
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
    }
}
