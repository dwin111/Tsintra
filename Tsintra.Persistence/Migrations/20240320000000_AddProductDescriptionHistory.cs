using FluentMigrator;

namespace Tsintra.Persistence.Migrations
{
    [Migration(20240320000000)]
    public class AddProductDescriptionHistory : Migration
    {
        public override void Up()
        {
            Create.Table("product_description_history")
                .WithColumn("id").AsInt32().PrimaryKey().Identity()
                .WithColumn("product_id").AsInt32().NotNullable()
                .WithColumn("description").AsString().NotNullable()
                .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);

            Create.Table("product_hashtags")
                .WithColumn("id").AsInt32().PrimaryKey().Identity()
                .WithColumn("product_id").AsInt32().NotNullable()
                .WithColumn("hashtag").AsString().NotNullable()
                .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);

            Create.Table("product_ctas")
                .WithColumn("id").AsInt32().PrimaryKey().Identity()
                .WithColumn("product_id").AsInt32().NotNullable()
                .WithColumn("cta").AsString().NotNullable()
                .WithColumn("created_at").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime);

            Create.ForeignKey("fk_product_description_history_product_id")
                .FromTable("product_description_history").ForeignColumn("product_id")
                .ToTable("products").PrimaryColumn("id");

            Create.ForeignKey("fk_product_hashtags_product_id")
                .FromTable("product_hashtags").ForeignColumn("product_id")
                .ToTable("products").PrimaryColumn("id");

            Create.ForeignKey("fk_product_ctas_product_id")
                .FromTable("product_ctas").ForeignColumn("product_id")
                .ToTable("products").PrimaryColumn("id");
        }

        public override void Down()
        {
            Delete.Table("product_description_history");
            Delete.Table("product_hashtags");
            Delete.Table("product_ctas");
        }
    }
} 