using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CreditFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReferenceId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditLedgerEntries_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "Id", "CreatedAt", "Email", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), new DateTimeOffset(new DateTime(2026, 1, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "billing@acmelabs.example", "Acme Labs" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), new DateTimeOffset(new DateTime(2026, 2, 4, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ops@northwind.example", "Northwind Analytics" },
                    { new Guid("33333333-3333-3333-3333-333333333333"), new DateTimeOffset(new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "finance@bluebird.example", "Bluebird Systems" }
                });

            migrationBuilder.InsertData(
                table: "CreditLedgerEntries",
                columns: new[] { "Id", "AccountId", "Amount", "CreatedAt", "Description", "IdempotencyKey", "ReferenceId", "Source", "Type" },
                values: new object[,]
                {
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000001"), new Guid("11111111-1111-1111-1111-111111111111"), 300, new DateTimeOffset(new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Initial monthly grant", "seed-acme-grant-1", "seed-acme-sub-1", 1, 1 },
                    { new Guid("aaaaaaaa-0000-0000-0000-000000000002"), new Guid("11111111-1111-1111-1111-111111111111"), -80, new DateTimeOffset(new DateTime(2026, 6, 5, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Model inference usage", "seed-acme-use-1", "seed-acme-event-1", 3, 2 },
                    { new Guid("bbbbbbbb-0000-0000-0000-000000000001"), new Guid("22222222-2222-2222-2222-222222222222"), 100, new DateTimeOffset(new DateTime(2026, 6, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Starter credit pack", "seed-nw-pack-1", "seed-nw-packref-1", 2, 1 },
                    { new Guid("bbbbbbbb-0000-0000-0000-000000000002"), new Guid("22222222-2222-2222-2222-222222222222"), -20, new DateTimeOffset(new DateTime(2026, 6, 6, 9, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "API usage", "seed-nw-use-1", "seed-nw-event-1", 3, 2 },
                    { new Guid("cccccccc-0000-0000-0000-000000000001"), new Guid("33333333-3333-3333-3333-333333333333"), 500, new DateTimeOffset(new DateTime(2026, 6, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Enterprise monthly allowance", "seed-bluebird-grant-1", "seed-bluebird-sub-1", 1, 1 },
                    { new Guid("cccccccc-0000-0000-0000-000000000002"), new Guid("33333333-3333-3333-3333-333333333333"), 40, new DateTimeOffset(new DateTime(2026, 6, 3, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Promotional adjustment", "seed-bluebird-adj-1", "seed-bluebird-adjref-1", 4, 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Email",
                table: "Accounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgerEntries_AccountId",
                table: "CreditLedgerEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedgerEntries_AccountId_IdempotencyKey",
                table: "CreditLedgerEntries",
                columns: new[] { "AccountId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditLedgerEntries");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
