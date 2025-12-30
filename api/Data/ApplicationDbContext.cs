using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using api.Models;

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
    public virtual DbSet<Gear> Gears { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("gear_condition", new[] { "NEW", "LIKE_NEW", "GOOD", "FAIR", "POOR" })
            .HasPostgresEnum("gear_status", new[] { "AVAILABLE", "RESERVED", "SOLD", "HIDDEN" })
            .HasPostgresEnum("transaction_preference", new[] { "DIRECT", "DELIVERY", "BOTH" })
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
        });

        modelBuilder.Entity<Gear>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("gear_pkey");
            entity.ToTable("gears");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Images).HasColumnName("images");
            entity.Property(e => e.Location).HasMaxLength(255).HasColumnName("location");
            entity.Property(e => e.Model).HasMaxLength(255).HasColumnName("model");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.SellerId).HasColumnName("seller_id");
            entity.Property(e => e.Title).HasMaxLength(255).HasColumnName("title");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").HasColumnName("updated_at");
            entity.Property(e => e.Views).HasColumnName("views");
            entity.Property(e => e.Year).HasColumnName("year");
            entity.Property(e => e.Condition).HasColumnName("condition");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.TransactionPreference).HasColumnName("transaction_preference");

            entity.HasOne(d => d.Category).WithMany(p => p.Gears)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_gear_category");

            entity.HasOne(d => d.Seller).WithMany(p => p.Gears)
                .HasForeignKey(d => d.SellerId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_gear_seller");
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
