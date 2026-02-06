using Gateway.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Shared.Data;

public class AnalyticsDbContext : DbContext
{
    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options) { }

    public DbSet<Request> Requests => Set<Request>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Request>(entity =>
        {
            entity.ToTable("requests");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Method).HasColumnName("method").HasMaxLength(10).IsRequired();
            entity.Property(e => e.Uri).HasColumnName("uri").IsRequired();
            entity.Property(e => e.Host).HasColumnName("host").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Ip).HasColumnName("ip").HasMaxLength(45);
            entity.Property(e => e.DeviceId).HasColumnName("device_id").HasMaxLength(255);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");
            entity.Property(e => e.RequestContentType).HasColumnName("request_content_type").HasMaxLength(255);
            entity.Property(e => e.RequestHeaders).HasColumnName("request_headers");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.StatusCode).HasColumnName("status_code");
            entity.Property(e => e.ResponseContentType).HasColumnName("response_content_type").HasMaxLength(255);
            entity.Property(e => e.ResponseHeaders).HasColumnName("response_headers");
            entity.Property(e => e.ResponseBody).HasColumnName("response_body");
            entity.Property(e => e.ReceivedAt).HasColumnName("received_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            entity.HasIndex(e => e.Host);
            entity.HasIndex(e => e.ReceivedAt);
            entity.HasIndex(e => e.DeviceId);
            entity.HasIndex(e => e.RequestContentType);
            entity.HasIndex(e => e.ResponseContentType);
        });
    }
}
