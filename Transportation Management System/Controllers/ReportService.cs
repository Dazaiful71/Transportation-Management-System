/* -- FILEHEADER COMMENT --
    FILE        :   ReportService.cs
    PROJECT     :   Transportation Management System
    DESCRIPTION :   Builds invoice summary figures and CSV exports for the Planner's report.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Transportation_Management_System
{
    ///
    /// \class InvoiceSummary
    ///
    /// \brief Headline figures for a set of invoices.
    ///
    public sealed class InvoiceSummary
    {
        public int InvoiceCount { get; set; }

        public decimal TotalRevenue { get; set; }

        public decimal AverageInvoice { get; set; }

        public int TotalDistance { get; set; }

        public double AverageDays { get; set; }

        /// Client with the highest invoiced total, or null when there are no invoices.
        public string TopClient { get; set; }

        public decimal TopClientRevenue { get; set; }

        ///
        /// \brief One-line description for the report screen.
        ///
        public override string ToString()
        {
            if (InvoiceCount == 0)
            {
                return "No invoices in this period.";
            }

            CultureInfo ca = CultureInfo.GetCultureInfo("en-CA");
            string text = string.Format(ca, "{0} invoice{1} | {2:C} total | {3:C} avg | {4:N0} km",
                InvoiceCount, InvoiceCount == 1 ? string.Empty : "s", TotalRevenue, AverageInvoice, TotalDistance);

            if (!string.IsNullOrEmpty(TopClient))
            {
                text += string.Format(ca, " | top: {0}", TopClient);
            }

            return text;
        }
    }

    ///
    /// \class ReportService
    ///
    /// \brief Pure helpers for invoice reporting. No database or UI access, so they are unit tested directly.
    ///
    public static class ReportService
    {
        /// Column headers written to the CSV export, in order.
        public static readonly string[] CsvHeaders =
        {
            "OrderID", "ClientName", "Origin", "Destination", "PalletQuantity",
            "DistanceKm", "Days", "TotalAmount", "CompletedDate"
        };

        ///
        /// \brief Work out totals and averages for a set of invoices.
        ///
        public static InvoiceSummary Summarize(IEnumerable<Invoice> invoices)
        {
            List<Invoice> list = (invoices ?? Enumerable.Empty<Invoice>()).Where(i => i != null).ToList();
            var summary = new InvoiceSummary { InvoiceCount = list.Count };

            if (list.Count == 0)
            {
                return summary;
            }

            summary.TotalRevenue = list.Sum(i => i.TotalAmount);
            summary.AverageInvoice = Math.Round(summary.TotalRevenue / list.Count, 2, MidpointRounding.AwayFromZero);
            summary.TotalDistance = list.Sum(i => i.TotalKM);
            summary.AverageDays = Math.Round(list.Average(i => i.Days), 1);

            var top = list
                .GroupBy(i => i.ClientName ?? string.Empty)
                .Select(g => new { Client = g.Key, Revenue = g.Sum(i => i.TotalAmount) })
                .OrderByDescending(g => g.Revenue)
                .ThenBy(g => g.Client, StringComparer.OrdinalIgnoreCase)
                .First();

            summary.TopClient = top.Client;
            summary.TopClientRevenue = top.Revenue;
            return summary;
        }

        ///
        /// \brief Keep only invoices completed within the given number of days before `now`.
        ///
        public static List<Invoice> FilterByPeriod(IEnumerable<Invoice> invoices, DateTime now, int days)
        {
            if (days < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(days), "The period cannot be negative.");
            }

            DateTime from = now.AddDays(-days);
            return (invoices ?? Enumerable.Empty<Invoice>())
                .Where(i => i != null && i.CompletedDate >= from && i.CompletedDate <= now)
                .ToList();
        }

        ///
        /// \brief Build a CSV document (RFC 4180 style) for a set of invoices.
        ///
        /// Numbers and dates use the invariant culture so the file opens the same everywhere.
        ///
        public static string BuildInvoiceCsv(IEnumerable<Invoice> invoices)
        {
            var csv = new StringBuilder();
            csv.Append(string.Join(",", CsvHeaders)).Append("\r\n");

            foreach (Invoice invoice in invoices ?? Enumerable.Empty<Invoice>())
            {
                if (invoice == null)
                {
                    continue;
                }

                string[] fields =
                {
                    invoice.OrderID.ToString(CultureInfo.InvariantCulture),
                    invoice.ClientName,
                    invoice.Origin.ToString(),
                    invoice.Destination.ToString(),
                    invoice.PalletQuantity.ToString(CultureInfo.InvariantCulture),
                    invoice.TotalKM.ToString(CultureInfo.InvariantCulture),
                    invoice.Days.ToString("0.0", CultureInfo.InvariantCulture),
                    invoice.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture),
                    invoice.CompletedDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                };

                csv.Append(string.Join(",", fields.Select(EscapeCsv))).Append("\r\n");
            }

            return csv.ToString();
        }

        ///
        /// \brief Quote a CSV field when needed, and neutralise spreadsheet formulas.
        ///
        /// A value starting with = + - @ could run as a formula in Excel, so it is prefixed with a quote.
        ///
        public static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if ("=+-@".IndexOf(value[0]) >= 0 && !IsPlainNumber(value))
            {
                value = "'" + value;
            }

            bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 || value != value.Trim();
            if (!needsQuotes)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        ///
        /// \brief Suggested file name for an export, e.g. TMS-Invoices-2026-11-18.csv
        ///
        public static string SuggestFileName(DateTime now, bool onlyPastTwoWeeks)
        {
            string scope = onlyPastTwoWeeks ? "Last2Weeks" : "All";
            return $"TMS-Invoices-{scope}-{now:yyyy-MM-dd}.csv";
        }

        private static bool IsPlainNumber(string value)
        {
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
        }
    }
}
