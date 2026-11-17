using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Transportation_Management_System;

namespace UnitTestProject1
{
    [TestClass]
    public class RouteCalculationTests
    {
        private const double Tolerance = 0.001;

        [TestMethod]
        public void GetLegs_NeighbouringCities_ReturnsSingleLeg()
        {
            List<RouteLeg> legs = TripManager.GetLegs(City.Windsor, City.London, TestData.Routes());

            Assert.AreEqual(1, legs.Count);
            Assert.AreEqual(City.Windsor, legs[0].From);
            Assert.AreEqual(City.London, legs[0].To);
            Assert.AreEqual(191, legs[0].Distance);
        }

        [TestMethod]
        public void GetLegs_Westbound_ReturnsLegsInTravelOrder()
        {
            List<RouteLeg> legs = TripManager.GetLegs(City.Hamilton, City.Windsor, TestData.Routes());

            CollectionAssert.AreEqual(
                new[] { City.Hamilton, City.London },
                legs.Select(l => l.From).ToArray());
            CollectionAssert.AreEqual(
                new[] { City.London, City.Windsor },
                legs.Select(l => l.To).ToArray());
            CollectionAssert.AreEqual(new[] { 128, 191 }, legs.Select(l => l.Distance).ToArray());
        }

        [TestMethod]
        public void GetLegs_SameCity_ReturnsNoLegs()
        {
            Assert.AreEqual(0, TripManager.GetLegs(City.Toronto, City.Toronto, TestData.Routes()).Count);
        }

        [TestMethod]
        public void GetLegs_RowOrderDoesNotMatter()
        {
            List<Route> shuffled = TestData.Routes();
            shuffled.Reverse();
            TripManager.LinkRoutes(shuffled);

            RouteEstimate estimate = TripManager.CalculateRoute(City.Windsor, City.Ottawa, JobType.FTL, shuffled);

            Assert.AreEqual(859, estimate.TotalDistance);
        }

        [TestMethod]
        public void GetLegs_MissingRoute_Throws()
        {
            List<Route> routes = TestData.Routes().Where(r => r.Destination != City.Toronto).ToList();

            Assert.ThrowsException<InvalidOperationException>(
                () => TripManager.GetLegs(City.Hamilton, City.Oshawa, routes));
        }

        [TestMethod]
        public void GetLegs_NullCity_Throws()
        {
            Assert.ThrowsException<ArgumentException>(
                () => TripManager.GetLegs(City.Null, City.Ottawa, TestData.Routes()));
        }

        [TestMethod]
        public void CalculateRoute_FtlOneLeg_AddsLoadAndUnload()
        {
            RouteEstimate estimate = TripManager.CalculateRoute(City.Windsor, City.London, JobType.FTL, TestData.Routes());

            Assert.AreEqual(191, estimate.TotalDistance);
            Assert.AreEqual(2.0 + 2.5 + 2.0, estimate.TotalHours, Tolerance);
            Assert.AreEqual(0, estimate.RestDays);
        }

        [TestMethod]
        public void CalculateRoute_LtlStopsAtEveryCityInBetween()
        {
            RouteEstimate estimate = TripManager.CalculateRoute(City.Windsor, City.Hamilton, JobType.LTL, TestData.Routes());

            // load + Windsor->London + stop in London + London->Hamilton + unload
            Assert.AreEqual(319, estimate.TotalDistance);
            Assert.AreEqual(2.0 + 2.5 + 2.0 + 1.75 + 2.0, estimate.TotalHours, Tolerance);
        }

        [TestMethod]
        public void CalculateRoute_FtlDoesNotStopInBetween()
        {
            RouteEstimate estimate = TripManager.CalculateRoute(City.Windsor, City.Hamilton, JobType.FTL, TestData.Routes());

            Assert.AreEqual(2.0 + 2.5 + 1.75 + 2.0, estimate.TotalHours, Tolerance);
        }

        [TestMethod]
        public void CalculateRoute_DirectionDoesNotChangeDistance()
        {
            List<Route> routes = TestData.Routes();

            RouteEstimate east = TripManager.CalculateRoute(City.London, City.Kingston, JobType.FTL, routes);
            RouteEstimate west = TripManager.CalculateRoute(City.Kingston, City.London, JobType.FTL, routes);

            Assert.AreEqual(east.TotalDistance, west.TotalDistance);
            Assert.AreEqual(east.DrivingHours, west.DrivingHours, Tolerance);
        }

        [TestMethod]
        public void CalculateRoute_FullCorridor_AddsOvernightRest()
        {
            RouteEstimate estimate = TripManager.CalculateRoute(City.Windsor, City.Ottawa, JobType.FTL, TestData.Routes());

            // 12.15 h of driving cannot fit in one 8 h driving day, so the driver rests once.
            // Day 1: 2 h load + 8 h driving = 10 h on duty, then rests 14 h.
            // Day 2: 4.15 h driving + 2 h unload.
            Assert.AreEqual(859, estimate.TotalDistance);
            Assert.AreEqual(12.15, estimate.DrivingHours, Tolerance);
            Assert.AreEqual(1, estimate.RestDays);
            Assert.AreEqual(10 + 14 + 4.15 + 2, estimate.TotalHours, Tolerance);
            Assert.AreEqual(1.3, estimate.TotalDays, Tolerance);
        }

        [TestMethod]
        public void CalculateRoute_LtlLongRun_RespectsTwelveHourDutyLimit()
        {
            RouteEstimate estimate = TripManager.CalculateRoute(City.Windsor, City.Ottawa, JobType.LTL, TestData.Routes());

            // 2 load + 12.15 driving + 6 stops x 2 h + 2 unload = 28.15 h on duty, which needs rest days.
            Assert.IsTrue(estimate.RestDays >= 2, $"Expected at least two rests, got {estimate.RestDays}.");
            Assert.IsTrue(estimate.TotalHours > 28.15);
        }

        [TestMethod]
        public void CalculateDistanceAndTime_FillsTrip()
        {
            var trip = new Trip { OriginCity = City.Toronto, DestinationCity = City.Oshawa, JobType = JobType.FTL };

            TripManager.CalculateDistanceAndTime(trip, TestData.Routes());

            Assert.AreEqual(60, trip.TotalDistance);
            Assert.AreEqual(2.0 + 1.3 + 2.0, trip.TotalTime, Tolerance);
        }

        [TestMethod]
        public void LinkRoutes_SetsNeighbourPointers()
        {
            List<Route> routes = TestData.Routes();
            Route toronto = routes.Single(r => r.Destination == City.Toronto);

            Assert.AreEqual(City.Hamilton, toronto.WestPtr.Destination);
            Assert.AreEqual(City.Oshawa, toronto.EastPtr.Destination);
            Assert.IsNull(routes.Single(r => r.Destination == City.Windsor).WestPtr);
            Assert.IsNull(routes.Single(r => r.Destination == City.Ottawa).EastPtr);
        }
    }
}
