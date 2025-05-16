using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanBodyWeb.Migrations
{
    public partial class AddAuthorToBlogPost : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) AuthorId sütununu ekle
            migrationBuilder.AddColumn<string>(
                name: "AuthorId",
                table: "BlogPosts",
                type: "text",
                nullable: false,
                defaultValue: "720b098a-234b-42df-bff0-d4495849ac00");

            // 2) Mevcut kayıtlar için geçerli bir AuthorId ata
            migrationBuilder.Sql(@"
                UPDATE ""BlogPosts""
                SET ""AuthorId"" = '720b098a-234b-42df-bff0-d4495849ac00'
                WHERE ""AuthorId"" IS NULL OR ""AuthorId"" = '';"
            );

            // 3) Index oluştur
            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_AuthorId",
                table: "BlogPosts",
                column: "AuthorId");

            // 4) Foreign key ekle
            migrationBuilder.AddForeignKey(
                name: "FK_BlogPosts_AspNetUsers_AuthorId",
                table: "BlogPosts",
                column: "AuthorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlogPosts_AspNetUsers_AuthorId",
                table: "BlogPosts");

            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_AuthorId",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "AuthorId",
                table: "BlogPosts");
        }
    }
}
