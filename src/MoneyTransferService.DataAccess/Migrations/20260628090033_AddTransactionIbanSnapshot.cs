using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyTransferService.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionIbanSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AccountNumber",
                table: "Accounts",
                newName: "Iban");

            migrationBuilder.RenameIndex(
                name: "IX_Accounts_AccountNumber",
                table: "Accounts",
                newName: "IX_Accounts_Iban");

            migrationBuilder.AddColumn<string>(
                name: "ReceiverIban",
                table: "Transactions",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SenderIban",
                table: "Transactions",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiverIban",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SenderIban",
                table: "Transactions");

            migrationBuilder.RenameColumn(
                name: "Iban",
                table: "Accounts",
                newName: "AccountNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Accounts_Iban",
                table: "Accounts",
                newName: "IX_Accounts_AccountNumber");
        }
    }
}
