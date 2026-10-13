using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CategoryManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class ManualIdsAndCategoryCodeSeq : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "category_code_seq");

            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "products",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "categories",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    max_code bigint;
                BEGIN
                    SELECT COALESCE(MAX(NULLIF(regexp_replace(category_code, '\D', '', 'g'), '')::bigint), 0)
                    INTO max_code
                    FROM categories;

                    IF max_code > 0 THEN
                        PERFORM setval('category_code_seq', max_code);
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "category_code_seq");

            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "products",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "categories",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);
        }
    }
}
