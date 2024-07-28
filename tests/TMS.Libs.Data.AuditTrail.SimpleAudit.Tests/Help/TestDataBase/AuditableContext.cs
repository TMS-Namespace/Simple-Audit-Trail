using Microsoft.EntityFrameworkCore;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Tests.TestDataBase;

public partial class AuditableContext : SimpleAuditContext
{
    public DbSet<AuditTrailTableModel> AuditTrailTable { get; set; }

    public DbSet<AuditableTableModel> AuditableTable { get; set; }

    public DbSet<NotAuditableTableModel> NotAuditableTable { get; set; }

    public AuditableContext(DbContextOptions<SimpleAuditContext> options) : base(options) { }

    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //{
    //    optionsBuilder.UseSqlite("DataSource=:memory:");
    //}


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditableTableModel>(entity =>
        {
            entity
                .ToTable("auditable_table");

            entity
                .HasKey(e => e.Id);

            entity
                .Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            entity
                .Property(e => e.CompanyName)
                .HasColumnName("company_name")
                .HasColumnType("TEXT");

            entity
                .Property(e => e.Count)
                .HasColumnName("count")
                .HasColumnType("INTEGER")
                .IsRequired();

            entity
                .Property(e => e.CountDoubled)
                .HasColumnName("count_doubled")
                .HasColumnType("INTEGER")
                .HasComputedColumnSql("[count] * 2", stored: true);

            entity
                .Property(e => e.CreateAt)
                .HasColumnName("created_at")
                .HasColumnType("TEXT")
                .IsRequired();

            entity.Property(e => e.NotAuditableTableModelId)
                .HasColumnName("not_auditable_table_model_id")
                .HasColumnType("INTEGER")
                .IsRequired();

            entity
                .Ignore(e => e.CountTripled);

            entity
                .Ignore(e => e.CountQuadruplet);

            entity
                .HasOne(e => e.NotAuditableTableModel)
                .WithMany(e => e.AuditableTableModels)
                .HasForeignKey(e => e.NotAuditableTableModelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditTrailTableModel>(entity =>
        {
            entity
                .ToTable("audit_trail_table");

            entity
                .HasKey(e => e.Id);

            entity
                .Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            entity
                .Property(e => e.ReferenceId)
                .HasColumnName("reference_id")
                .HasColumnType("INTEGER")
                .IsRequired();

            entity
                .Property(e => e.TableName)
                .HasColumnName("table_name")
                .HasColumnType("TEXT")
                .IsRequired();

            entity
                .Property(e => e.UserName)
                .HasColumnName("user_name")
                .HasColumnType("TEXT")
                .IsRequired();

            entity
                .Property(e => e.IpAddress)
                .HasColumnName("ip_address")
                .HasColumnType("TEXT")
                .IsRequired();

            entity
                .Property(e => e.Action)
                .HasColumnName("action")
                .HasColumnType("TEXT")
                .IsRequired();

            entity
                .Property(e => e.Changes)
                .HasColumnName("changes")
                .HasColumnType("TEXT")
                .IsRequired()
                .HasMaxLength(2048);

            entity
                .Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("TEXT")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();
        });

        modelBuilder.Entity<NotAuditableTableModel>(entity =>
        {
            entity
                .ToTable("not_auditable_table");

            entity
                .HasKey(e => e.Id);

            entity
                .Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER");

            entity
                .Property(e => e.Name)
                .HasColumnName("name")
                .HasColumnType("TEXT")
                .IsRequired();

            entity
                .HasMany(e => e.AuditableTableModels)
                .WithOne(e => e.NotAuditableTableModel)
                .HasForeignKey(e => e.NotAuditableTableModelId);
        });
    }

}