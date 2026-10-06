using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookClub.Migrations
{
    /// <inheritdoc />
    public partial class AddShelfAndPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Pages",
                table: "Books",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserBooks_Rating",
                table: "UserBooks",
                sql: "\"Rating\" IS NULL OR \"Rating\" BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserBooks_Status",
                table: "UserBooks",
                sql: "\"Status\" IN ('WantToRead', 'Reading', 'Finished')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserBooks_Rating",
                table: "UserBooks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserBooks_Status",
                table: "UserBooks");

            migrationBuilder.DropColumn(
                name: "Pages",
                table: "Books");
        }
    }
}
