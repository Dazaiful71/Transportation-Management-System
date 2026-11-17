using System.Collections.Generic;
using Transportation_Management_System;

namespace UnitTestProject1
{
    /// <summary>
    /// In-memory copies of the sample data in database/02_seed.sql, so logic can be tested without MySQL.
    /// </summary>
    internal static class TestData
    {
        /// The corridor legs: each row is the leg from Destination to its East neighbour.
        public static List<Route> Routes()
        {
            var routes = new List<Route>
            {
                new Route(City.Windsor,    191, 2.50, City.Null,       City.London),
                new Route(City.London,     128, 1.75, City.Windsor,    City.Hamilton),
                new Route(City.Hamilton,    68, 1.25, City.London,     City.Toronto),
                new Route(City.Toronto,     60, 1.30, City.Hamilton,   City.Oshawa),
                new Route(City.Oshawa,     134, 1.65, City.Toronto,    City.Belleville),
                new Route(City.Belleville,  82, 1.20, City.Oshawa,     City.Kingston),
                new Route(City.Kingston,   196, 2.50, City.Belleville, City.Ottawa),
                new Route(City.Ottawa,       0, 0.00, City.Kingston,   City.Null),
            };

            TripManager.LinkRoutes(routes);
            return routes;
        }

        public static Rate OshtRates()
        {
            var rates = new Rate(RateType.FTL, 0.08);
            rates.RateValuePair[RateType.LTL] = 0.05;
            return rates;
        }

        public static Carrier PlanetExpress() => new Carrier("Planet Express", 5.21, 0.3621, 0.08) { CarrierID = 1 };

        public static Carrier Tillman() => new Carrier("Tillman Transport", 5.11, 0.3012, 0.09) { CarrierID = 3 };

        public static Trip Trip(long carrierId, JobType job, VanType van, int km) => new Trip
        {
            TripID = carrierId * 100,
            CarrierID = carrierId,
            JobType = job,
            VanType = van,
            TotalDistance = km
        };
    }
}
