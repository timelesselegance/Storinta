using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HumanBodyWeb.Migrations
{
    public partial class CreatePostUserViewsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only create PostUserViews table, do not modify BlogPosts
            migrationBuilder.CreateTable(
                name: "PostUserViews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ViewedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostUserViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostUserViews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostUserViews_BlogPosts_PostId",
                        column: x => x.PostId,
                        principalTable: "BlogPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostUserViews_PostId",
                table: "PostUserViews",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_PostUserViews_UserId",
                table: "PostUserViews",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostUserViews");
        }
    }
}