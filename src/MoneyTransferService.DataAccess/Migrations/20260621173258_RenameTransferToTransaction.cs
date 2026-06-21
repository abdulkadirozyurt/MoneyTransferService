using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyTransferService.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RenameTransferToTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_Accounts_ReceiverAccountId",
                table: "Transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_Accounts_SenderAccountId",
                table: "Transfers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Transfers",
                table: "Transfers");

            migrationBuilder.RenameTable(
                name: "Transfers",
                newName: "Transactions");

            migrationBuilder.RenameIndex(
                name: "IX_Transfers_IdempotencyKey",
                table: "Transactions",
                newName: "IX_Transactions_IdempotencyKey");

            migrationBuilder.RenameIndex(
                name: "IX_Transfers_ReceiverAccountId",
                table: "Transactions",
                newName: "IX_Transactions_ReceiverAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_Transfers_SenderAccountId",
                table: "Transactions",
                newName: "IX_Transactions_SenderAccountId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Transactions",
                table: "Transactions",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_ReceiverAccountId",
                table: "Transactions",
                column: "ReceiverAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Accounts_SenderAccountId",
                table: "Transactions",
                column: "SenderAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_ReceiverAccountId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Accounts_SenderAccountId",
                table: "Transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Transactions",
                table: "Transactions");

            migrationBuilder.RenameTable(
                name: "Transactions",
                newName: "Transfers");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_IdempotencyKey",
                table: "Transfers",
                newName: "IX_Transfers_IdempotencyKey");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_ReceiverAccountId",
                table: "Transfers",
                newName: "IX_Transfers_ReceiverAccountId");

            migrationBuilder.RenameIndex(
                name: "IX_Transactions_SenderAccountId",
                table: "Transfers",
                newName: "IX_Transfers_SenderAccountId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Transfers",
                table: "Transfers",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_Accounts_ReceiverAccountId",
                table: "Transfers",
                column: "ReceiverAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_Accounts_SenderAccountId",
                table: "Transfers",
                column: "SenderAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
