using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace InventoryManagement.Data
{
    //dotnet ef migrations add [Migration name] --project InventoryManagement.csproj
    //dotnet ef database update
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new database context instance that supports audit metadata collection from the active HTTP user.
        /// </summary>
        /// <param name="options">Entity Framework context options used to configure the database provider and behavior.</param>
        /// <param name="httpContextAccessor">Accessor used to resolve the current request user for audit records.</param>
        /// <remarks>
        /// Expected output: a fully configured context instance ready for dependency-injection use.
        /// Possible errors: throws dependency resolution exceptions when required services are not available.
        /// </remarks>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
        public DbSet<PurchaseOrderProduct> PurchaseOrderProducts => Set<PurchaseOrderProduct>();
        public DbSet<Audit> Audits => Set<Audit>();
        public DbSet<GeminiSettings> GeminiSettings => Set<GeminiSettings>();
        public DbSet<GeminiAvailableModel> GeminiAvailableModels => Set<GeminiAvailableModel>();

        /// <summary>
        /// Persists tracked changes and generates audit entries for supported entity state transitions.
        /// </summary>
        /// <param name="acceptAllChangesOnSuccess">Indicates whether <see cref="ChangeTracker.AcceptAllChanges"/> is called after a successful save.</param>
        /// <returns>The number of state entries written to the database.</returns>
        /// <remarks>
        /// Expected output: data changes are committed and corresponding audit records are queued.
        /// Possible errors: propagates database provider and concurrency exceptions from Entity Framework.
        /// </remarks>
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            AddAuditEntries();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        /// <summary>
        /// Asynchronously persists tracked changes and generates audit entries for supported entity state transitions.
        /// </summary>
        /// <param name="acceptAllChangesOnSuccess">Indicates whether <see cref="ChangeTracker.AcceptAllChanges"/> is called after a successful save.</param>
        /// <param name="cancellationToken">Cancellation token used to cancel the asynchronous save operation.</param>
        /// <returns>A task that resolves to the number of state entries written to the database.</returns>
        /// <remarks>
        /// Expected output: data changes are committed and corresponding audit records are queued.
        /// Possible errors: propagates cancellation, database provider, and concurrency exceptions from Entity Framework.
        /// </remarks>
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            AddAuditEntries();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>
        /// Configures entity relationships, indexes, and delete behaviors for the domain model.
        /// </summary>
        /// <param name="builder">Model builder used to configure entities and relational mappings.</param>
        /// <remarks>
        /// Expected output: model metadata required by Entity Framework migrations and runtime mapping.
        /// Possible errors: may throw configuration exceptions if model definitions are invalid.
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<PurchaseOrderProduct>()
                .HasKey(pop => new { pop.PurchaseOrderId, pop.ProductId });
            builder.Entity<PurchaseOrderProduct>()
                .HasOne(pop => pop.PurchaseOrder)
                .WithMany(po => po.PurchaseOrderProducts)
                .HasForeignKey(pop => pop.PurchaseOrderId);
            builder.Entity<PurchaseOrderProduct>()
                .HasOne(pop => pop.Product)
                .WithMany(p => p.PurchaseOrderProducts)
                .HasForeignKey(pop => pop.ProductId);

            builder.Entity<PurchaseOrder>()
                .HasOne(po => po.Supplier)
                .WithMany(s => s.PurchaseOrders)
                .HasForeignKey(po => po.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Product>()
                .HasMany(p => p.Categories)
                .WithMany(c => c.Products)
                .UsingEntity(j => j.ToTable("ProductCategories"));

            builder.Entity<Category>()
                .HasIndex(c => c.Name)
                .IsUnique();

            builder.Entity<GeminiSettings>()
                .HasIndex(gs => gs.Provider)
                .IsUnique();

            builder.Entity<GeminiAvailableModel>()
                .HasOne(gm => gm.GeminiSettings)
                .WithMany(gs => gs.AvailableModels)
                .HasForeignKey(gm => gm.GeminiSettingsId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<GeminiAvailableModel>()
                .HasIndex(gm => new { gm.GeminiSettingsId, gm.ModelName })
                .IsUnique();

            // No explicit configuration needed for Product-Provider relationship
            builder.Entity<Product>();
            builder.Entity<Supplier>();
            builder.Entity<Category>();
        }

        /// <summary>
        /// Creates audit entities from tracked changes that qualify for auditing and stages them for persistence.
        /// </summary>
        /// <remarks>
        /// Expected output: audit records are added to <see cref="Audits"/> for applicable tracked entities.
        /// Possible errors: no custom exceptions are thrown, but metadata access failures can propagate from change tracking.
        /// </remarks>
        private void AddAuditEntries()
        {
            ChangeTracker.DetectChanges();

            var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var audits = new List<Audit>();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (!ShouldAudit(entry))
                {
                    continue;
                }

                audits.Add(new Audit
                {
                    UserId = userId,
                    Action = entry.State.ToString(),
                    TableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
                    RecordId = TryGetRecordId(entry),
                    Timestamp = DateTime.UtcNow,
                    Details = BuildDetails(entry)
                });
            }

            if (audits.Count > 0)
            {
                Audits.AddRange(audits);
            }
        }

        /// <summary>
        /// Determines whether a tracked entity entry should produce an audit record.
        /// </summary>
        /// <param name="entry">Tracked entity entry under evaluation.</param>
        /// <returns><see langword="true"/> when the entry should be audited; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// Expected output: a boolean decision based on entity type and state.
        /// Possible errors: no custom exceptions are thrown by this method.
        /// </remarks>
        private static bool ShouldAudit(EntityEntry entry)
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified and not EntityState.Deleted)
            {
                return false;
            }

            if (entry.Entity is Audit)
            {
                return false;
            }

            return entry.Metadata.ClrType.Namespace == typeof(ApplicationDbContext).Namespace;
        }

        /// <summary>
        /// Attempts to extract a numeric primary key identifier from a tracked entity entry.
        /// </summary>
        /// <param name="entry">Tracked entity entry that may contain a scalar primary key value.</param>
        /// <returns>The converted integer identifier when available; otherwise <see langword="null"/>.</returns>
        /// <remarks>
        /// Expected output: a normalized integer key for audit record linking.
        /// Possible errors: can throw overflow exceptions when converting large key values to <see cref="int"/>.
        /// </remarks>
        private static int? TryGetRecordId(EntityEntry entry)
        {
            var keyProperty = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            var keyValue = entry.State == EntityState.Deleted ? keyProperty?.OriginalValue : keyProperty?.CurrentValue;

            return keyValue switch
            {
                int id => id,
                long id => checked((int)id),
                short id => id,
                byte id => id,
                _ => null
            };
        }

        /// <summary>
        /// Builds a human-readable change summary for entity properties based on the current entity state.
        /// </summary>
        /// <param name="entry">Tracked entity entry whose property values are formatted for auditing.</param>
        /// <returns>A semicolon-separated details string, or <see langword="null"/> when no detail items exist.</returns>
        /// <remarks>
        /// Expected output: property-level change details suitable for audit inspection.
        /// Possible errors: no custom exceptions are thrown by this method.
        /// </remarks>
        private static string? BuildDetails(EntityEntry entry)
        {
            var details = new List<string>();

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.IsPrimaryKey())
                {
                    continue;
                }

                var name = property.Metadata.Name;

                if (entry.State == EntityState.Added)
                {
                    details.Add($"{name}: {property.CurrentValue}");
                    continue;
                }

                if (entry.State == EntityState.Deleted)
                {
                    details.Add($"{name}: {property.OriginalValue}");
                    continue;
                }

                if (!property.IsModified)
                {
                    continue;
                }

                details.Add($"{name}: {property.OriginalValue} -> {property.CurrentValue}");
            }

            return details.Count > 0 ? string.Join("; ", details) : null;
        }
    }
}
