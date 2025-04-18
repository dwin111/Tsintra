using FluentMigrator;

namespace Tsintra.Persistence.Migrations
{
    [Migration(20240320000000)]
    public class RefactorProductModels : Migration
    {
        public override void Up()
        {
            // Створюємо нові таблиці для ProductContent
            Create.Table("ProductContents")
                .WithColumn("ProductId").AsGuid().PrimaryKey()
                .WithColumn("Description").AsString().Nullable()
                .WithColumn("MainImage").AsString().Nullable()
                .WithColumn("Status").AsString().Nullable();

            Create.ForeignKey("FK_ProductContents_Products")
                .FromTable("ProductContents").ForeignColumn("ProductId")
                .ToTable("Products").PrimaryColumn("Id")
                .OnDelete(System.Data.Rule.Cascade);

            // Створюємо нові таблиці для ProductPricing
            Create.Table("ProductPricings")
                .WithColumn("ProductId").AsGuid().PrimaryKey()
                .WithColumn("Price").AsDecimal().NotNullable()
                .WithColumn("OldPrice").AsDecimal().Nullable()
                .WithColumn("Currency").AsString().Nullable();

            Create.ForeignKey("FK_ProductPricings_Products")
                .FromTable("ProductPricings").ForeignColumn("ProductId")
                .ToTable("Products").PrimaryColumn("Id")
                .OnDelete(System.Data.Rule.Cascade);

            // Створюємо нові таблиці для ProductInventory
            Create.Table("ProductInventories")
                .WithColumn("ProductId").AsGuid().PrimaryKey()
                .WithColumn("QuantityInStock").AsInt32().Nullable()
                .WithColumn("InStock").AsBoolean().NotNullable();

            Create.ForeignKey("FK_ProductInventories_Products")
                .FromTable("ProductInventories").ForeignColumn("ProductId")
                .ToTable("Products").PrimaryColumn("Id")
                .OnDelete(System.Data.Rule.Cascade);

            // Створюємо нові таблиці для ProductVariantContent
            Create.Table("ProductVariantContents")
                .WithColumn("VariantId").AsGuid().PrimaryKey()
                .WithColumn("MainImage").AsString().Nullable()
                .WithColumn("Status").AsString().Nullable();

            Create.ForeignKey("FK_ProductVariantContents_ProductVariants")
                .FromTable("ProductVariantContents").ForeignColumn("VariantId")
                .ToTable("ProductVariants").PrimaryColumn("Id")
                .OnDelete(System.Data.Rule.Cascade);

            // Створюємо нові таблиці для ProductVariantPricing
            Create.Table("ProductVariantPricings")
                .WithColumn("VariantId").AsGuid().PrimaryKey()
                .WithColumn("Price").AsDecimal().NotNullable()
                .WithColumn("OldPrice").AsDecimal().Nullable()
                .WithColumn("Currency").AsString().Nullable();

            Create.ForeignKey("FK_ProductVariantPricings_ProductVariants")
                .FromTable("ProductVariantPricings").ForeignColumn("VariantId")
                .ToTable("ProductVariants").PrimaryColumn("Id")
                .OnDelete(System.Data.Rule.Cascade);

            // Створюємо нові таблиці для ProductVariantInventory
            Create.Table("ProductVariantInventories")
                .WithColumn("VariantId").AsGuid().PrimaryKey()
                .WithColumn("QuantityInStock").AsInt32().Nullable()
                .WithColumn("InStock").AsBoolean().NotNullable();

            Create.ForeignKey("FK_ProductVariantInventories_ProductVariants")
                .FromTable("ProductVariantInventories").ForeignColumn("VariantId")
                .ToTable("ProductVariants").PrimaryColumn("Id")
                .OnDelete(System.Data.Rule.Cascade);

            // Переносимо дані зі старих таблиць в нові
            Execute.Sql(@"
                INSERT INTO ProductContents (ProductId, Description, MainImage, Status)
                SELECT Id, Description, MainImage, Status FROM Products;

                INSERT INTO ProductPricings (ProductId, Price, OldPrice, Currency)
                SELECT Id, Price, OldPrice, Currency FROM Products;

                INSERT INTO ProductInventories (ProductId, QuantityInStock, InStock)
                SELECT Id, QuantityInStock, InStock FROM Products;

                INSERT INTO ProductVariantContents (VariantId, MainImage, Status)
                SELECT Id, MainImage, Status FROM ProductVariants;

                INSERT INTO ProductVariantPricings (VariantId, Price, OldPrice, Currency)
                SELECT Id, Price, OldPrice, Currency FROM ProductVariants;

                INSERT INTO ProductVariantInventories (VariantId, QuantityInStock, InStock)
                SELECT Id, QuantityInStock, InStock FROM ProductVariants;
            ");

            // Видаляємо старі колонки з таблиць Products і ProductVariants
            Delete.Column("Description").FromTable("Products");
            Delete.Column("MainImage").FromTable("Products");
            Delete.Column("Status").FromTable("Products");
            Delete.Column("Price").FromTable("Products");
            Delete.Column("OldPrice").FromTable("Products");
            Delete.Column("Currency").FromTable("Products");
            Delete.Column("QuantityInStock").FromTable("Products");
            Delete.Column("InStock").FromTable("Products");

            Delete.Column("MainImage").FromTable("ProductVariants");
            Delete.Column("Status").FromTable("ProductVariants");
            Delete.Column("Price").FromTable("ProductVariants");
            Delete.Column("OldPrice").FromTable("ProductVariants");
            Delete.Column("Currency").FromTable("ProductVariants");
            Delete.Column("QuantityInStock").FromTable("ProductVariants");
            Delete.Column("InStock").FromTable("ProductVariants");
        }

        public override void Down()
        {
            // Додаємо назад старі колонки
            Alter.Table("Products")
                .AddColumn("Description").AsString().Nullable()
                .AddColumn("MainImage").AsString().Nullable()
                .AddColumn("Status").AsString().Nullable()
                .AddColumn("Price").AsDecimal().NotNullable()
                .AddColumn("OldPrice").AsDecimal().Nullable()
                .AddColumn("Currency").AsString().Nullable()
                .AddColumn("QuantityInStock").AsInt32().Nullable()
                .AddColumn("InStock").AsBoolean().NotNullable();

            Alter.Table("ProductVariants")
                .AddColumn("MainImage").AsString().Nullable()
                .AddColumn("Status").AsString().Nullable()
                .AddColumn("Price").AsDecimal().NotNullable()
                .AddColumn("OldPrice").AsDecimal().Nullable()
                .AddColumn("Currency").AsString().Nullable()
                .AddColumn("QuantityInStock").AsInt32().Nullable()
                .AddColumn("InStock").AsBoolean().NotNullable();

            // Переносимо дані назад
            Execute.Sql(@"
                UPDATE Products p
                SET Description = pc.Description,
                    MainImage = pc.MainImage,
                    Status = pc.Status,
                    Price = pp.Price,
                    OldPrice = pp.OldPrice,
                    Currency = pp.Currency,
                    QuantityInStock = pi.QuantityInStock,
                    InStock = pi.InStock
                FROM ProductContents pc
                JOIN ProductPricings pp ON p.Id = pp.ProductId
                JOIN ProductInventories pi ON p.Id = pi.ProductId
                WHERE p.Id = pc.ProductId;

                UPDATE ProductVariants pv
                SET MainImage = pvc.MainImage,
                    Status = pvc.Status,
                    Price = pvp.Price,
                    OldPrice = pvp.OldPrice,
                    Currency = pvp.Currency,
                    QuantityInStock = pvi.QuantityInStock,
                    InStock = pvi.InStock
                FROM ProductVariantContents pvc
                JOIN ProductVariantPricings pvp ON pv.Id = pvp.VariantId
                JOIN ProductVariantInventories pvi ON pv.Id = pvi.VariantId
                WHERE pv.Id = pvc.VariantId;
            ");

            // Видаляємо нові таблиці
            Delete.Table("ProductContents");
            Delete.Table("ProductPricings");
            Delete.Table("ProductInventories");
            Delete.Table("ProductVariantContents");
            Delete.Table("ProductVariantPricings");
            Delete.Table("ProductVariantInventories");
        }
    }
} 