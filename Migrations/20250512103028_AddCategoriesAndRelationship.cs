using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HumanBodyWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriesAndRelationship : Migration
    {
        /// <inheritdoc />
       protected override void Up(MigrationBuilder migrationBuilder)
{
    // 1) Categories tablosu + ilk kayıt
    migrationBuilder.CreateTable(
        name: "Categories",
        columns: table => new
        {
            Id   = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                                     NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            Name = table.Column<string>(maxLength: 60, nullable: false),
            Slug = table.Column<string>(nullable: false)
        },
        constraints: table => table.PrimaryKey("PK_Categories", x => x.Id));

    // Varsayılan kategori (Id = 1)
    migrationBuilder.InsertData(
        table: "Categories",
        columns: new[] { "Id", "Name", "Slug" },
        values : new object[] { 1, "Genel", "genel" });

    // 2) BlogPosts → CategoryId  (ÖNCE nullable ekle)
    migrationBuilder.AddColumn<int>(
        name: "CategoryId",
        table: "BlogPosts",
        type: "integer",
        nullable: true);                         // ← nullable!

    // 3) Eski satırları varsayılan kategoriye ata
    migrationBuilder.Sql(
        @"UPDATE ""BlogPosts"" SET ""CategoryId"" = 1 WHERE ""CategoryId"" IS NULL;");

    // 4) Sütunu NOT NULL’a çevir
    migrationBuilder.AlterColumn<int>(
        name: "CategoryId",
        table: "BlogPosts",
        type: "integer",
        nullable: false,
        oldNullable: true);

    // 5) İndeks + Yabancı anahtar
    migrationBuilder.CreateIndex(
        name: "IX_BlogPosts_CategoryId",
        table: "BlogPosts",
        column: "CategoryId");

    migrationBuilder.AddForeignKey(
        name: "FK_BlogPosts_Categories_CategoryId",
        table: "BlogPosts",
        column: "CategoryId",
        principalTable: "Categories",
        principalColumn: "Id",
        onDelete: ReferentialAction.Cascade);
}


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlogPosts_Categories_CategoryId",
                table: "BlogPosts");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_CategoryId",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "BlogPosts");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "BlogPosts",
                type: "text",
                nullable: true);
        }
    }
}
