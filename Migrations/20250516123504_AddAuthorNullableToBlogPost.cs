using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HumanBodyWeb.Migrations
{
    public partial class AddAuthorNullableToBlogPost : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Nullable olarak AuthorId sütununu ekliyoruz
            migrationBuilder.AddColumn<string>(
                name: "AuthorId",
                table: "BlogPosts",
                type: "text",
                nullable: true);

            // 2) Index oluşturuyoruz
            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_AuthorId",
                table: "BlogPosts",
                column: "AuthorId");

            // 3) Foreign key ekliyoruz (silinirse NULL olsun)
            migrationBuilder.AddForeignKey(
                name: "FK_BlogPosts_AspNetUsers_AuthorId",
                table: "BlogPosts",
                column: "AuthorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Tersini yapıyoruz: FK’i, index’i ve kolonu siliyoruz
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
