using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentDelayApp.DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Ice = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FiscalId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Activite = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AlertSeuilJours = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DeliveryOrServiceDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Designation = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TtcAmount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    EcheanceFactureJours = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSettled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPaymentAlert = table.Column<bool>(type: "INTEGER", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceDate",
                table: "Invoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_IsPaymentAlert_IsSettled",
                table: "Invoices",
                columns: new[] { "IsPaymentAlert", "IsSettled" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_IsSettled",
                table: "Invoices",
                column: "IsSettled");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_SupplierId",
                table: "Invoices",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Suppliers");
        }
    }
}
