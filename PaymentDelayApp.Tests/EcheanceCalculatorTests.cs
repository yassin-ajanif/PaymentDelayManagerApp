using PaymentDelayApp.BusinessLayer.Calculators;

namespace PaymentDelayApp.Tests;

[TestClass]
public sealed class EcheanceCalculatorTests
{
    [TestMethod]
    public void ResteDesJours_IsDeadlineMinusToday()
    {
        var invoiceDate = new DateOnly(2026, 1, 1);
        var today = new DateOnly(2026, 1, 1);
        var n = 10;
        // deadline 2026-01-11 → 10 days left
        Assert.AreEqual(10, EcheanceCalculator.ResteDesJours(invoiceDate, today, n));
    }

    [TestMethod]
    public void ResteDesJours_NegativeWhenPastDeadline()
    {
        var invoiceDate = new DateOnly(2026, 1, 1);
        var today = new DateOnly(2026, 1, 20);
        var n = 10;
        // deadline 2026-01-11, today 2026-01-20 → -9
        Assert.AreEqual(-9, EcheanceCalculator.ResteDesJours(invoiceDate, today, n));
    }
}
