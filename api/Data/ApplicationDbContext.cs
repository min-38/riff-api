using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using api.Models;
using api.Models.Enums;

namespace api.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserOAuth> UserOAuths { get; set; }
    public virtual DbSet<BlockedUser> BlockedUsers { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }
    public virtual DbSet<Category> Categories { get; set; }
    public virtual DbSet<TradeGear> TradeGears { get; set; }
    public virtual DbSet<TradeGearView> TradeGearViews { get; set; }
    public virtual DbSet<TradeGearLike> TradeGearLikes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum<GearCategory>("gear_category")
            .HasPostgresEnum<GearSubCategory>("gear_sub_category")
            .HasPostgresEnum<GearDetailCategory>("gear_detail_category")
            .HasPostgresEnum<GearCondition>("gear_condition")
            .HasPostgresEnum<GearStatus>("gear_status")
            .HasPostgresEnum<TradeMethod>("trade_method")
            .HasPostgresEnum<Region>("region_type")
            .HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("categories_pkey");
            entity.ToTable("categories");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.Name).HasMaxLength(255).HasColumnName("name");
            entity.Property(e => e.Slug).HasMaxLength(255).HasColumnName("slug");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");

            entity.Ignore(e => e.Gears);
        });

        modelBuilder.Entity<TradeGear>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("trade_gears_pkey");
            entity.ToTable("trade_gears");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Title).HasMaxLength(100).HasColumnName("title");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.Category).HasColumnName("category")
                .HasColumnType("gear_category");
            entity.Property(e => e.SubCategory).HasColumnName("sub_category")
                .HasColumnType("gear_sub_category");
            entity.Property(e => e.DetailCategory).HasColumnName("detail_category")
                .HasColumnType("gear_detail_category");
            entity.Property(e => e.Condition).HasColumnName("condition")
                .HasColumnType("gear_condition");
            entity.Property(e => e.TradeMethod).HasColumnName("trade_method")
                .HasColumnType("trade_method");
            entity.Property(e => e.Region).HasColumnName("region")
                .HasColumnType("region_type");
            entity.Property(e => e.Status).HasColumnName("status")
                .HasColumnType("gear_status")
                .HasDefaultValue(Models.Enums.GearStatus.Selling);

            // ImageData를 JSON으로 변환
            var imageDataConverter = new ValueConverter<ImageData?, string?>(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<ImageData>(v, (JsonSerializerOptions?)null)
            );

            entity.Property(e => e.Images)
                .HasColumnName("images")
                .HasColumnType("jsonb")
                .HasConversion(imageDataConverter);
            entity.Property(e => e.ViewCount).HasColumnName("view_count")
                .HasDefaultValue(0);
            entity.Property(e => e.LikeCount).HasColumnName("like_count")
                .HasDefaultValue(0);
            entity.Property(e => e.ChatCount).HasColumnName("chat_count")
                .HasDefaultValue(0);
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            entity.HasOne(d => d.Author)
                .WithMany(p => p.Gears)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_trade_gears_author");
        });

        modelBuilder.Entity<TradeGearView>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("trade_gear_views_pkey");
            entity.ToTable("trade_gear_views");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GearId).HasColumnName("gear_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.ViewedAt).HasColumnName("viewed_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.TradeGear)
                .WithMany()
                .HasForeignKey(d => d.GearId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_views_gear");

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_views_user");
        });

        modelBuilder.Entity<TradeGearLike>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("trade_gear_likes_pkey");
            entity.ToTable("trade_gear_likes");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GearId).HasColumnName("gear_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.HasOne(d => d.TradeGear)
                .WithMany()
                .HasForeignKey(d => d.GearId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_likes_gear");

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_likes_user");

            // 한 사용자는 한 게시글에 한 번만 좋아요 가능
            entity.HasIndex(e => new { e.GearId, e.UserId })
                .IsUnique()
                .HasDatabaseName("uk_gear_user");

            // 인덱스
            entity.HasIndex(e => e.GearId).HasDatabaseName("idx_gear_likes_gear");
            entity.HasIndex(e => e.UserId).HasDatabaseName("idx_gear_likes_user");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");
            entity.ToTable("users");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Nickname).HasColumnName("nickname");
            entity.Property(e => e.Password).HasColumnName("password");
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.Verified).HasColumnName("verified");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.EmailVerificationToken).HasColumnName("email_verification_token");
            entity.Property(e => e.EmailVerificationTokenExpiredAt).HasColumnName("email_verification_token_expired_at");
            entity.Property(e => e.PasswordResetToken).HasColumnName("password_reset_token");
            entity.Property(e => e.PasswordResetTokenExpiredAt).HasColumnName("password_reset_token_expired_at");
            entity.Property(e => e.TermsOfServiceAgreed).HasColumnName("terms_of_service_agreed");
            entity.Property(e => e.PrivacyPolicyAgreed).HasColumnName("privacy_policy_agreed");
            entity.Property(e => e.MarketingAgreed).HasColumnName("marketing_agreed");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

            // 인덱스 추가 (토큰 검색 성능 향상)
            entity.HasIndex(e => e.EmailVerificationToken);
        });

        modelBuilder.Entity<UserOAuth>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_oauth_pkey");
            entity.ToTable("user_oauth");

            entity.Property(e => e.Id).ValueGeneratedOnAdd().HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Provider).HasMaxLength(50).HasColumnName("provider");
            entity.Property(e => e.ProviderId).HasMaxLength(255).HasColumnName("provider_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");

            entity.HasOne(d => d.User).WithMany(p => p.UserOAuths)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_user_oauth_user");

            // 한 유저는 각 Provider당 하나의 계정만
            entity.HasIndex(e => new { e.UserId, e.Provider }).IsUnique();
        });

        modelBuilder.Entity<BlockedUser>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("blocked_users_pkey");
            entity.ToTable("blocked_users");

            entity.Property(e => e.Id).ValueGeneratedOnAdd().HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Email).HasMaxLength(255).HasColumnName("email");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.BlockedAt).HasDefaultValueSql("now()").HasColumnName("blocked_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.BlockedBy).HasMaxLength(255).HasColumnName("blocked_by");

            // 외래키
            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_blocked_user_user");

            // 인덱스
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Email);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("refresh_tokens_pkey");
            entity.ToTable("refresh_tokens");

            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()").HasColumnName("id");
            entity.Property(e => e.Token).HasColumnName("token");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_refresh_token_user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
