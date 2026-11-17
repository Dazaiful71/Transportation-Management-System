using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Transportation_Management_System;

namespace UnitTestProject1
{
    [TestClass]
    public class ReportServiceTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 18, 12, 0, 0);

        private static Invoice Invoice(long id, string client, decimal amount, int km, double days, DateTime completed) => new Invoice
        {
            OrderID = id,
            ClientName = client,
            Origin = City.Windsor,
            Destination = City.Toronto,
            TotalAmount = amount,
            TotalKM = km,
            Days = days,
            PalletQuantity = 0,
            CompletedDate = completed
        };

        [TestMethod]
        public void Summarize_Empty_ReportsNoInvoices()
        {
            InvoiceSummary summary = ReportService.Summarize(new List<Invoice>());

            Assert.AreEqual(0, summary.InvoiceCount);
            Assert.AreEqual(0m, summary.TotalRevenue);
            Assert.IsNull(summary.TopClient);
            Assert.AreEqual("No invoices in this period.", summary.ToString());
        }

        [TestMethod]
        public void Summarize_Null_IsTreatedAsEmpty()
        {
            Assert.AreEqual(0, ReportService.Summarize(null).InvoiceCount);
        }

        [TestMethod]
        public void Summarize_ComputesTotalsAveragesAndTopClient()
        {
            var invoices = new[]
            {
                Invoice(1, "Acme", 100.00m, 100, 1.0, Now),
                Invoice(2, "Bolt", 250.50m, 300, 2.0, Now),
                Invoice(3, "Acme", 200.00m, 200, 3.0, Now)
            };

            InvoiceSummary summary = ReportService.Summarize(invoices);

            Assert.AreEqual(3, summary.InvoiceCount);
            Assert.AreEqual(550.50m, summary.TotalRevenue);
            Assert.AreEqual(183.50m, summary.AverageInvoice);
            Assert.AreEqual(600, summary.TotalDistance);
            Assert.AreEqual(2.0, summary.AverageDays, 0.001);
            Assert.AreEqual("Acme", summary.TopClient);
            Assert.AreEqual(300.00m, summary.TopClientRevenue);
            StringAssert.StartsWith(summary.ToString(), "3 invoices");
        }

        [TestMethod]
        public void FilterByPeriod_KeepsOnlyRecentInvoices()
        {
            var invoices = new[]
            {
                Invoice(1, "A", 1m, 1, 1, Now.AddDays(-1)),
                Invoice(2, "B", 1m, 1, 1, Now.AddDays(-14)),
                Invoice(3, "C", 1m, 1, 1, Now.AddDays(-15)),
                Invoice(4, "D", 1m, 1, 1, Now.AddDays(1))
            };

            List<Invoice> recent = ReportService.FilterByPeriod(invoices, Now, 14);

            CollectionAssert.AreEquivalent(new long[] { 1, 2 }, recent.ConvertAll(i => i.OrderID));
        }

        [TestMethod]
        public void FilterByPeriod_NegativeDays_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ReportService.FilterByPeriod(new Invoice[0], Now, -1));
        }

        [TestMethod]
        public void BuildInvoiceCsv_WritesHeaderAndInvariantNumbers()
        {
            string csv = ReportService.BuildInvoiceCsv(new[] { Invoice(7, "Acme", 1234.5m, 859, 1.25, Now) });

            string[] lines = csv.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual(string.Join(",", ReportService.CsvHeaders), lines[0]);
            Assert.AreEqual("7,Acme,Windsor,Toronto,0,859,1.3,1234.50,2026-11-18 12:00:00", lines[1]);
        }

        [TestMethod]
        public void BuildInvoiceCsv_NoInvoices_WritesOnlyHeader()
        {
            Assert.AreEqual(string.Join(",", ReportService.CsvHeaders) + "\r\n", ReportService.BuildInvoiceCsv(null));
        }

        [TestMethod]
        public void EscapeCsv_QuotesCommasQuotesAndNewlines()
        {
            Assert.AreEqual("\"Smith, Jones & Co\"", ReportService.EscapeCsv("Smith, Jones & Co"));
            Assert.AreEqual("\"The \"\"Best\"\" Freight\"", ReportService.EscapeCsv("The \"Best\" Freight"));
            Assert.AreEqual("\"line1\nline2\"", ReportService.EscapeCsv("line1\nline2"));
            Assert.AreEqual("plain", ReportService.EscapeCsv("plain"));
            Assert.AreEqual(string.Empty, ReportService.EscapeCsv(null));
        }

        [TestMethod]
        public void EscapeCsv_NeutralisesSpreadsheetFormulas()
        {
            Assert.AreEqual("'=HYPERLINK(\"x\")", ReportService.EscapeCsv("=HYPERLINK(\"x\")").Trim('"').Replace("\"\"", "\""));
            Assert.AreEqual("'@SUM(A1)", ReportService.EscapeCsv("@SUM(A1)"));
            Assert.AreEqual("'+cmd", ReportService.EscapeCsv("+cmd"));
        }

        [TestMethod]
        public void EscapeCsv_LeavesNegativeNumbersAlone()
        {
            Assert.AreEqual("-12.50", ReportService.EscapeCsv("-12.50"));
        }

        [TestMethod]
        public void SuggestFileName_IncludesScopeAndDate()
        {
            Assert.AreEqual("TMS-Invoices-All-2026-11-18.csv", ReportService.SuggestFileName(Now, false));
            Assert.AreEqual("TMS-Invoices-Last2Weeks-2026-11-18.csv", ReportService.SuggestFileName(Now, true));
        }
    }
}
