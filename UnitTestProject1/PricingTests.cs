using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Transportation_Management_System;

namespace UnitTestProject1
{
    [TestClass]
    public class PricingTests
    {
        private static Dictionary<long, Carrier> Carriers() => new Dictionary<long, Carrier>
        {
            [1] = TestData.PlanetExpress(),
            [3] = TestData.Tillman()
        };

        [TestMethod]
        public void Ftl_DryVan_UsesRatePerKmPlusOsht()
        {
            Trip trip = TestData.Trip(1, JobType.FTL, VanType.DryVan, 191);

            decimal cost = TripManager.CalculateTotalCost(new[] { trip }, Carriers(), TestData.OshtRates(), 0);

            // 5.21 $/km x 191 km x 1.08
            Assert.AreEqual(1074.72m, cost);
        }

        [TestMethod]
        public void Ftl_Reefer_AddsReeferPercentage()
        {
            Trip trip = TestData.Trip(1, JobType.FTL, VanType.Reefer, 191);

            decimal cost = TripManager.CalculateTotalCost(new[] { trip }, Carriers(), TestData.OshtRates(), 0);

            // 1074.7188 x 1.08 reefer charge
            Assert.AreEqual(1160.70m, cost);
        }

        [TestMethod]
        public void Ltl_ChargesPerPallet()
        {
            Trip trip = TestData.Trip(3, JobType.LTL, VanType.DryVan, 319);

            decimal cost = TripManager.CalculateTotalCost(new[] { trip }, Carriers(), TestData.OshtRates(), 10);

            // 0.3012 $/pallet/km x 319 km x 10 pallets x 1.05
            Assert.AreEqual(1008.87m, cost);
        }

        [TestMethod]
        public void Ltl_UsesLtlMarkupNotAHardCodedValue()
        {
            Trip trip = TestData.Trip(3, JobType.LTL, VanType.DryVan, 100);
            var rates = new Rate(RateType.LTL, 0.10);

            decimal cost = TripManager.CalculateTotalCost(new[] { trip }, Carriers(), rates, 1);

            Assert.AreEqual(Math.Round((decimal)(0.3012 * 100 * 1.10), 2), cost);
        }

        [TestMethod]
        public void MultipleTrips_AreAddedTogether()
        {
            // Regression: every trip used to overwrite the running total, so only the last one was billed.
            var trips = new[]
            {
                TestData.Trip(1, JobType.FTL, VanType.DryVan, 191),
                TestData.Trip(1, JobType.FTL, VanType.DryVan, 191)
            };

            decimal cost = TripManager.CalculateTotalCost(trips, Carriers(), TestData.OshtRates(), 0);

            Assert.AreEqual(2149.44m, cost);
        }

        [TestMethod]
        public void ReeferCharge_AppliesPerTripNotToRunningTotal()
        {
            var trips = new[]
            {
                TestData.Trip(1, JobType.FTL, VanType.DryVan, 100),
                TestData.Trip(1, JobType.FTL, VanType.Reefer, 100)
            };

            decimal cost = TripManager.CalculateTotalCost(trips, Carriers(), null, 0);

            Assert.AreEqual(Math.Round((decimal)(521 + 521 * 1.08), 2), cost);
        }

        [TestMethod]
        public void NoRates_MeansNoMarkup()
        {
            Trip trip = TestData.Trip(1, JobType.FTL, VanType.DryVan, 100);

            decimal cost = TripManager.CalculateTotalCost(new[] { trip }, Carriers(), new Rate(), 0);

            Assert.AreEqual(521.00m, cost);
        }

        [TestMethod]
        public void ZeroDistance_CostsNothing()
        {
            Trip trip = TestData.Trip(1, JobType.FTL, VanType.Reefer, 0);

            Assert.AreEqual(0m, TripManager.CalculateTotalCost(new[] { trip }, Carriers(), TestData.OshtRates(), 0));
        }

        [TestMethod]
        public void UnknownCarrier_Throws()
        {
            Trip trip = TestData.Trip(99, JobType.FTL, VanType.DryVan, 10);

            Assert.ThrowsException<KeyNotFoundException>(
                () => TripManager.CalculateTotalCost(new[] { trip }, Carriers(), TestData.OshtRates(), 0));
        }

        [TestMethod]
        public void Ltl_WithoutPallets_Throws()
        {
            Trip trip = TestData.Trip(3, JobType.LTL, VanType.DryVan, 10);

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => TripManager.CalculateTripCost(trip, TestData.Tillman(), TestData.OshtRates(), 0));
        }

        [TestMethod]
        public void NegativeDistance_Throws()
        {
            Trip trip = TestData.Trip(1, JobType.FTL, VanType.DryVan, -5);

            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => TripManager.CalculateTripCost(trip, TestData.PlanetExpress(), null, 0));
        }

        [TestMethod]
        public void InvalidJobType_Throws()
        {
            Trip trip = TestData.Trip(1, (JobType)7, VanType.DryVan, 5);

            Assert.ThrowsException<ArgumentException>(
                () => TripManager.CalculateTripCost(trip, TestData.PlanetExpress(), null, 0));
        }

        [TestMethod]
        public void EmptyTripList_CostsNothing()
        {
            Assert.AreEqual(0m, TripManager.CalculateTotalCost(new Trip[0], Carriers(), null, 0));
        }

        [TestMethod]
        public void Rate_ValueConstructor_DoesNotThrow()
        {
            // Regression: the dictionary was never created, so this constructor threw NullReferenceException.
            var rate = new Rate(RateType.FTL, 0.08);

            Assert.AreEqual(0.08, rate.RateValuePair[RateType.FTL]);
            Assert.AreEqual(0.08, TripManager.GetMarkup(rate, RateType.FTL));
            Assert.AreEqual(0.0, TripManager.GetMarkup(rate, RateType.LTL));
        }
    }
}
