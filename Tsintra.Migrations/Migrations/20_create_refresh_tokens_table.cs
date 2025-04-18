using FluentMigrator;

namespace Tsintra.Migrations.Migrations
{
    [Migration(20)]
    public class CreateRefreshTokensTable : Migration
    {
        public override void Up()
        {
            Create.Table("RefreshTokens")
                .WithColumn("Id").AsGuid().PrimaryKey()
                .WithColumn("UserId").AsGuid().NotNullable()
                .WithColumn("Token").AsString(500).NotNullable()
                .WithColumn("ExpiryDate").AsDateTime().NotNullable()
                .WithColumn("CreatedAt").AsDateTime().NotNullable()
                .WithColumn("RevokedAt").AsDateTime().Nullable();

            Create.ForeignKey("FK_RefreshTokens_Users_UserId")
                .FromTable("RefreshTokens").ForeignColumn("UserId")
                .ToTable("Users").PrimaryColumn("Id")
                .OnDelete(System.Data.Rule.Cascade);

            Create.Index("IX_RefreshTokens_UserId")
                .OnTable("RefreshTokens")
                .OnColumn("UserId");

            Create.Index("IX_RefreshTokens_Token")
                .OnTable("RefreshTokens")
                .OnColumn("Token");
        }

        public override void Down()
        {
            Delete.Table("RefreshTokens");
        }
    }
} 