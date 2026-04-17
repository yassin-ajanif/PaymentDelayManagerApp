using ClosedXML.Excel;
using PaymentDelayApp.BusinessLayer.Calculators;
using PaymentDelayApp.BusinessLayer.Models;
using PaymentDelayApp.Services;

namespace PaymentDelayApp.Tests;

[TestClass]
public sealed class InvoiceDashboardImportTryParseRowTests
{
    private static string Fr(DateOnly d) => d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private static (Dictionary<string, Supplier> map, HashSet<string> amb) Lookup(params Supplier[] suppliers)
    {
        var map = InvoiceDashboardImportService.BuildSupplierLookup(suppliers, out var amb);
        return (map, amb);
    }

    private static string? ParseRow(
        IXLWorksheet ws,
        int row,
        IReadOnlyDictionary<string, Supplier> map,
        HashSet<string> amb,
        out Invoice? invoice) =>
        InvoiceDashboardImportService.TryParseRow(ws, row, map, amb, out invoice);

    [TestMethod]
    public void TryParseRow_InvoiceDateEmpty_ReturnsMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2, ["", "", "", "", "", "", "", "", ""]);
        var err = ParseRow(ws, 2, map, amb, out var inv);
        Assert.AreEqual("Date de facture invalide ou vide.", err);
        Assert.IsNull(inv);
    }

    [TestMethod]
    public void TryParseRow_InvoiceDateWrongYear_ReturnsYearMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var y = DateTime.Today.Year;
        var wrong = new DateOnly(y - 2, 6, 10);
        var lim = EcheanceCalculator.DateEcheanceNormale(wrong, 10);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(wrong), "", "1", "S", "", "10", Fr(lim), "2 j", Fr(lim)]);
        var err = ParseRow(ws, 2, map, amb, out _);
        StringAssert.StartsWith(err, $"La date de facture doit être dans l'année {y}.");
    }

    [TestMethod]
    public void TryParseRow_DeliveryEmpty_ContinuesWithNullDelivery()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "S", "", "99,00", Fr(lim), "3 j", Fr(lim)]);
        var err = ParseRow(ws, 2, map, amb, out var inv);
        Assert.IsNull(err);
        Assert.IsNotNull(inv);
        Assert.IsNull(inv!.DeliveryOrServiceDate);
    }

    [TestMethod]
    public void TryParseRow_DeliveryDash_TreatedAsNull()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), DashboardInvoiceExcelLayout.EmptyCellDisplay, "N1", "S", "", "1", Fr(lim), "3 j", Fr(lim)]);
        var err = ParseRow(ws, 2, map, amb, out var inv);
        Assert.IsNull(err);
        Assert.IsNull(inv!.DeliveryOrServiceDate);
    }

    [TestMethod]
    public void TryParseRow_DeliveryInvalid_ReturnsMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "bad", "N1", "S", "", "1", Fr(lim), "3 j", Fr(lim)]);
        Assert.AreEqual("Date de livraison ou prestation invalide.", ParseRow(ws, 2, map, amb, out _));
    }

    [TestMethod]
    public void TryParseRow_DeliveryWrongYear_ReturnsYearMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        var del = new DateOnly(DateTime.Today.Year - 1, 1, 1);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), Fr(del), "N1", "S", "", "1", Fr(lim), "3 j", Fr(lim)]);
        var err = ParseRow(ws, 2, map, amb, out _);
        StringAssert.StartsWith(err, $"La date de livraison doit être dans l'année {DateTime.Today.Year}.");
    }

    [TestMethod]
    public void TryParseRow_NumberEmpty_ReturnsMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "", "S", "", "1", Fr(lim), "3 j", Fr(lim)]);
        Assert.AreEqual("N° de Facture obligatoire.", ParseRow(ws, 2, map, amb, out _));
    }

    [TestMethod]
    public void TryParseRow_SupplierEmpty_ReturnsMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "", "", "1", Fr(lim), "3 j", Fr(lim)]);
        Assert.AreEqual("Fournisseur obligatoire.", ParseRow(ws, 2, map, amb, out _));
    }

    [TestMethod]
    public void TryParseRow_SupplierAmbiguous_ReturnsMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(
            new Supplier { Id = 1, Name = "Dup" },
            new Supplier { Id = 2, Name = "dup" });
        Assert.AreEqual(0, map.Count);
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "Dup", "", "1", Fr(lim), "3 j", Fr(lim)]);
        var err = ParseRow(ws, 2, map, amb, out _);
        Assert.IsNotNull(err);
        Assert.IsTrue(err.Contains("ambigu", StringComparison.Ordinal));
    }

    [TestMethod]
    public void TryParseRow_SupplierUnknown_ReturnsMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "Known" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "Nobody", "", "1", Fr(lim), "3 j", Fr(lim)]);
        var err = ParseRow(ws, 2, map, amb, out _);
        StringAssert.StartsWith(err, "Fournisseur inconnu");
    }

    [TestMethod]
    public void TryParseRow_TtcEmpty_ReturnsMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "S", "", "", Fr(lim), "3 j", Fr(lim)]);
        Assert.AreEqual("TTC obligatoire.", ParseRow(ws, 2, map, amb, out _));
    }

    [TestMethod]
    public void TryParseRow_TtcInvalid_ReturnsMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "S", "", "x", Fr(lim), "3 j", Fr(lim)]);
        Assert.AreEqual("TTC invalide.", ParseRow(ws, 2, map, amb, out _));
    }

    [TestMethod]
    public void TryParseRow_EcheanceC7Empty_ReturnsMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "S", "", "1", "", "3 j", Fr(lim)]);
        Assert.AreEqual("Date d'échéance respectée obligatoire.", ParseRow(ws, 2, map, amb, out _));
    }

    [TestMethod]
    public void TryParseRow_EcheanceC9Dash_ReturnsMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "S", "", "1", Fr(lim), "3 j", DashboardInvoiceExcelLayout.EmptyCellDisplay]);
        Assert.AreEqual("Date d'échéance/facture obligatoire.", ParseRow(ws, 2, map, amb, out _));
    }

    [TestMethod]
    public void TryParseRow_EcheanceC7AndC9Mismatch_ReturnsMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        var lim2 = lim.AddDays(1);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "S", "", "1", Fr(lim), "3 j", Fr(lim2)]);
        Assert.AreEqual("Les dates d'échéance (colonnes 7 et 9) ne correspondent pas.", ParseRow(ws, 2, map, amb, out _));
    }

    [TestMethod]
    public void TryParseRow_DerivedEcheanceTooShort_ReturnsRangeMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = invD;
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "S", "", "1", Fr(lim), "3 j", Fr(lim)]);
        Assert.AreEqual("Délai d'échéance dérivé hors plage (1–120 jours).", ParseRow(ws, 2, map, amb, out _));
    }

    [TestMethod]
    public void TryParseRow_DerivedEcheanceTooLong_ReturnsRangeMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = invD.AddDays(121);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "S", "", "1", Fr(lim), "3 j", Fr(lim)]);
        Assert.AreEqual("Délai d'échéance dérivé hors plage (1–120 jours).", ParseRow(ws, 2, map, amb, out _));
    }

    [TestMethod]
    public void TryParseRow_ResteJoursEmpty_ReturnsMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "S", "", "1", Fr(lim), "", Fr(lim)]);
        Assert.AreEqual("Reste des jours obligatoire.", ParseRow(ws, 2, map, amb, out _));
    }

    [TestMethod]
    public void TryParseRow_ResteJoursMissingJ_ReturnsFormatMessage()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "N1", "S", "", "1", Fr(lim), "5", Fr(lim)]);
        Assert.AreEqual("Reste des jours invalide (format attendu : « 5 j »).", ParseRow(ws, 2, map, amb, out _));
    }

    [TestMethod]
    public void TryParseRow_ResteJoursVariants_Accepted()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        foreach (var c8 in new[] { "5 j", "5J", "  5 j  ", "-3 j" })
        {
            InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
                [Fr(invD), "", "N1", "S", "", "1", Fr(lim), c8, Fr(lim)]);
            var err = ParseRow(ws, 2, map, amb, out var inv);
            Assert.IsNull(err, c8);
            Assert.IsNotNull(inv);
            Assert.AreEqual(0, inv!.Id);
            Assert.IsFalse(inv.IsSettled);
            Assert.IsNull(inv.PaidAt);
        }
    }

    [TestMethod]
    public void TryParseRow_DesignationWhitespaceOnly_NullDesignation()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var (map, amb) = Lookup(new Supplier { Id = 1, Name = "S" });
        var invD = new DateOnly(DateTime.Today.Year, 4, 5);
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, 20);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 2,
            [Fr(invD), "", "  N77  ", "S", "   ", "1,5", Fr(lim), "1 j", Fr(lim)]);
        var err = ParseRow(ws, 2, map, amb, out var inv);
        Assert.IsNull(err);
        Assert.IsNull(inv!.Designation);
        Assert.AreEqual("N77", inv.InvoiceNumber);
        Assert.AreEqual(1.5m, inv.TtcAmount);
        Assert.AreEqual(20, inv.EcheanceFactureJours);
    }

    [TestMethod]
    public void TryParseRow_HappyPath_SetsInvoiceFields()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("S");
        var sup = new Supplier { Id = 42, Name = "Acme" };
        var (map, amb) = Lookup(sup);
        var invD = new DateOnly(DateTime.Today.Year, 8, 1);
        const int n = 45;
        var lim = EcheanceCalculator.DateEcheanceNormale(invD, n);
        InvoiceDashboardImportTestExcel.WriteDataCells(ws, 3,
            [Fr(invD), Fr(invD.AddDays(1)), "INV-1", "Acme", "Desc", "200,00", Fr(lim), "10 j", Fr(lim)]);
        var err = ParseRow(ws, 3, map, amb, out var inv);
        Assert.IsNull(err);
        Assert.IsNotNull(inv);
        Assert.AreEqual(42, inv!.SupplierId);
        Assert.IsNull(inv.Supplier);
        Assert.AreEqual(invD, inv.InvoiceDate);
        Assert.AreEqual(invD.AddDays(1), inv.DeliveryOrServiceDate);
        Assert.AreEqual("INV-1", inv.InvoiceNumber);
        Assert.AreEqual("Desc", inv.Designation);
        Assert.AreEqual(200m, inv.TtcAmount);
        Assert.AreEqual(n, inv.EcheanceFactureJours);
    }
}
