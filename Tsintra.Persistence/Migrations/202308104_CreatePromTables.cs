using FluentMigrator;

namespace Tsintra.Persistence.Migrations
{
    [Migration(202308104)]
    public class CreatePromTables : Migration
    {
        public override void Up()
        {
            // Create PromGroups table
            Create.Table("PromGroups")
                .WithColumn("Id").AsInt64().PrimaryKey().Identity()
                .WithColumn("ExternalId").AsInt64().Nullable()
                .WithColumn("Name").AsString(255).NotNullable()
                .WithColumn("Description").AsString(2000).Nullable()
                .WithColumn("Image").AsString(500).Nullable()
                .WithColumn("ParentGroupId").AsInt64().Nullable()
                .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefaultValue(SystemMethods.CurrentDateTime)
                .WithColumn("UpdatedAt").AsDateTime().NotNullable().WithDefaultValue(SystemMethods.CurrentDateTime);

            // Create PromGroupMultilang table for name_multilang and description_multilang
            Create.Table("PromGroupMultilang")
                .WithColumn("Id").AsInt64().PrimaryKey().Identity()
                .WithColumn("GroupId").AsInt64().NotNullable().ForeignKey("FK_PromGroupMultilang_GroupId", "PromGroups", "Id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("LanguageCode").AsString(10).NotNullable()
                .WithColumn("Name").AsString(255).Nullable()
                .WithColumn("Description").AsString(2000).Nullable();
            
            Create.Index("IX_PromGroupMultilang_GroupId_LangCode")
                .OnTable("PromGroupMultilang")
                .OnColumn("GroupId")
                .Ascending()
                .OnColumn("LanguageCode")
                .Ascending();

            // Create PromCategories table
            Create.Table("PromCategories")
                .WithColumn("Id").AsInt64().PrimaryKey().Identity()
                .WithColumn("ExternalId").AsInt64().Nullable()
                .WithColumn("Caption").AsString(255).NotNullable()
                .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefaultValue(SystemMethods.CurrentDateTime)
                .WithColumn("UpdatedAt").AsDateTime().NotNullable().WithDefaultValue(SystemMethods.CurrentDateTime);
            
            // Create PromProducts table
            Create.Table("PromProducts")
                .WithColumn("Id").AsInt64().PrimaryKey().Identity()
                .WithColumn("ExternalId").AsString(255).Nullable()
                .WithColumn("Name").AsString(255).NotNullable()
                .WithColumn("Sku").AsString(100).Nullable()
                .WithColumn("Keywords").AsString(500).Nullable()
                .WithColumn("Presence").AsString(50).Nullable()
                .WithColumn("Price").AsDecimal(19, 4).NotNullable()
                .WithColumn("Currency").AsString(10).NotNullable().WithDefaultValue("UAH")
                .WithColumn("Description").AsString(4000).Nullable()
                .WithColumn("GroupId").AsInt64().Nullable().ForeignKey("FK_PromProducts_GroupId", "PromGroups", "Id").OnDelete(System.Data.Rule.SetNull)
                .WithColumn("CategoryId").AsInt64().Nullable().ForeignKey("FK_PromProducts_CategoryId", "PromCategories", "Id").OnDelete(System.Data.Rule.SetNull)
                .WithColumn("MainImage").AsString(500).Nullable()
                .WithColumn("SellingType").AsString(50).Nullable()
                .WithColumn("Status").AsString(50).Nullable()
                .WithColumn("QuantityInStock").AsInt32().Nullable()
                .WithColumn("MeasureUnit").AsString(50).Nullable()
                .WithColumn("IsVariation").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("VariationBaseId").AsInt64().Nullable()
                .WithColumn("VariationGroupId").AsInt64().Nullable()
                .WithColumn("DateModified").AsDateTime().Nullable()
                .WithColumn("InStock").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefaultValue(SystemMethods.CurrentDateTime)
                .WithColumn("UpdatedAt").AsDateTime().NotNullable().WithDefaultValue(SystemMethods.CurrentDateTime);
            
            Create.Index("IX_PromProducts_GroupId")
                .OnTable("PromProducts")
                .OnColumn("GroupId");
            
            Create.Index("IX_PromProducts_CategoryId")
                .OnTable("PromProducts")
                .OnColumn("CategoryId");
            
            Create.Index("IX_PromProducts_ExternalId")
                .OnTable("PromProducts")
                .OnColumn("ExternalId");
            
            // Create PromProductMultilang table for name_multilang and description_multilang
            Create.Table("PromProductMultilang")
                .WithColumn("Id").AsInt64().PrimaryKey().Identity()
                .WithColumn("ProductId").AsInt64().NotNullable().ForeignKey("FK_PromProductMultilang_ProductId", "PromProducts", "Id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("LanguageCode").AsString(10).NotNullable()
                .WithColumn("Name").AsString(255).Nullable()
                .WithColumn("Description").AsString(4000).Nullable();
            
            Create.Index("IX_PromProductMultilang_ProductId_LangCode")
                .OnTable("PromProductMultilang")
                .OnColumn("ProductId")
                .Ascending()
                .OnColumn("LanguageCode")
                .Ascending();
            
            // Create PromImages table
            Create.Table("PromImages")
                .WithColumn("Id").AsInt64().PrimaryKey().Identity()
                .WithColumn("ExternalId").AsInt64().Nullable()
                .WithColumn("ProductId").AsInt64().NotNullable().ForeignKey("FK_PromImages_ProductId", "PromProducts", "Id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("ThumbnailUrl").AsString(500).Nullable()
                .WithColumn("Url").AsString(500).NotNullable()
                .WithColumn("IsMain").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("SortOrder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefaultValue(SystemMethods.CurrentDateTime)
                .WithColumn("UpdatedAt").AsDateTime().NotNullable().WithDefaultValue(SystemMethods.CurrentDateTime);
            
            Create.Index("IX_PromImages_ProductId")
                .OnTable("PromImages")
                .OnColumn("ProductId");
        }

        public override void Down()
        {
            Delete.Table("PromImages");
            Delete.Table("PromProductMultilang");
            Delete.Table("PromProducts");
            Delete.Table("PromCategories");
            Delete.Table("PromGroupMultilang");
            Delete.Table("PromGroups");
        }
    }
} 