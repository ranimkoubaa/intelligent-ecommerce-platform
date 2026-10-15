using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BotResponse = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachedImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetectedIntent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MatchedProductId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_Products_MatchedProductId",
                        column: x => x.MatchedProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_MatchedProductId",
                table: "ChatMessages",
                column: "MatchedProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");
        }
    }
}
