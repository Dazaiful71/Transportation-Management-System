/* -- FILEHEADER COMMENT --
    FILE		:	TripManager.cs
    PROJECT		:	Transportation Management System
    PROGRAMMER	:  * Ana De Oliveira
                   * Icaro Ryan Oliveira Souza
                   * Lazir Pascual
                   * Rohullah Noory
    DATE		:	2021-12-07
    DESCRIPTION	:	This file contains the source for the TripManager class.
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace Transportation_Management_System
{
    ///
    /// \class TripManager
    ///
    /// \brief Performs the cost, distance and time calculations for trips.
    ///
    /// The calculations are split in two layers:
    ///   - pure static methods (CalculateTripCost, CalculateRoute, ...) that take every input as a
    ///     parameter, so they can be unit tested without a database;
    ///   - thin wrappers (CalculateTotalCostTrips, CalculateDistanceAndTime) that load carriers,
    ///     rates and routes through the DAL and then call the pure methods.
    ///
    public class TripManager
    {
        /// Hours needed to load the truck at the origin, or unload it at the destination.
        public const double LoadUnloadHours = 2.0;

        /// Hours spent at each intermediate city on an LTL run (pickups and drop-offs).
        public const double LtlStopHours = 2.0;

        /// Maximum hours a driver may drive in one day.
        public const double MaxDrivingHoursPerDay = 8.0;

        /// Maximum on-duty hours (driving plus loading/stops) in one day.
        public const double MaxOnDutyHoursPerDay = 12.0;

        /// Hours in a day, used to work out the overnight rest.
        public const double HoursPerDay = 24.0;

        ///
        /// \brief Calculate the total cost of every trip of an order, reading carriers and rates from the database.
        ///
        /// \param trips  - <b>List<Trip></b> - Trips that make up the order.
        /// \param palletQuantity  - <b>int</b> - Pallets on the order (used for LTL pricing).
        ///
        /// \return Total cost for the order.
        ///
        public static decimal CalculateTotalCostTrips(List<Trip> trips, int palletQuantity)
        {
            if (trips == null || trips.Count == 0)
            {
                return 0m;
            }

            DAL db = new DAL();
            Rate oshtRates = db.GetOSHTRates();
            var carriers = new Dictionary<long, Carrier>();

            foreach (Trip trip in trips)
            {
                if (!carriers.ContainsKey(trip.CarrierID))
                {
                    carriers[trip.CarrierID] = db.FilterCarriersByID(trip.CarrierID);
                }
            }

            return CalculateTotalCost(trips, carriers, oshtRates, palletQuantity);
        }

        ///
        /// \brief Older overload kept for callers that do not know the pallet count (treated as one pallet).
        ///
        public static decimal CalculateTotalCostTrips(List<Trip> trips)
        {
            return CalculateTotalCostTrips(trips, 1);
        }

        ///
        /// \brief Sum the cost of every trip using already-loaded carriers and rates. Has no database access.
        ///
        /// \param trips  - <b>IEnumerable<Trip></b> - Trips to price.
        /// \param carriersById  - <b>IDictionary<long, Carrier></b> - Carrier for each trip's CarrierID.
        /// \param oshtRates  - <b>Rate</b> - OSHT mark-up rates.
        /// \param palletQuantity  - <b>int</b> - Pallets on the order (used for LTL pricing).
        ///
        /// \return Total cost rounded to cents.
        ///
        public static decimal CalculateTotalCost(IEnumerable<Trip> trips, IDictionary<long, Carrier> carriersById, Rate oshtRates, int palletQuantity)
        {
            if (trips == null)
            {
                throw new ArgumentNullException(nameof(trips));
            }

            if (carriersById == null)
            {
                throw new ArgumentNullException(nameof(carriersById));
            }

            decimal total = 0m;
            foreach (Trip trip in trips)
            {
                if (!carriersById.TryGetValue(trip.CarrierID, out Carrier carrier) || carrier == null)
                {
                    throw new KeyNotFoundException($"Carrier #{trip.CarrierID} for trip #{trip.TripID} was not found.");
                }

                total += CalculateTripCost(trip, carrier, oshtRates, palletQuantity);
            }

            return Math.Round(total, 2, MidpointRounding.AwayFromZero);
        }

        ///
        /// \brief Calculate the cost of a single trip.
        ///
        /// FTL: carrier FTL rate x km x (1 + OSHT FTL mark-up).
        /// LTL: carrier LTL rate x km x pallets x (1 + OSHT LTL mark-up).
        /// Reefer vans add the carrier's reefer charge on top, e.g. 0.08 adds 8%.
        ///
        /// \return Unrounded cost of the trip.
        ///
        public static decimal CalculateTripCost(Trip trip, Carrier carrier, Rate oshtRates, int palletQuantity)
        {
            if (trip == null)
            {
                throw new ArgumentNullException(nameof(trip));
            }

            if (carrier == null)
            {
                throw new ArgumentNullException(nameof(carrier));
            }

            if (trip.TotalDistance < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(trip), "Trip distance cannot be negative.");
            }

            double cost;
            switch (trip.JobType)
            {
                case JobType.FTL:
                    cost = carrier.FTLRate * trip.TotalDistance * (1 + GetMarkup(oshtRates, RateType.FTL));
                    break;

                case JobType.LTL:
                    if (palletQuantity <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(palletQuantity), "An LTL trip needs at least one pallet.");
                    }

                    cost = carrier.LTLRate * trip.TotalDistance * palletQuantity * (1 + GetMarkup(oshtRates, RateType.LTL));
                    break;

                default:
                    throw new ArgumentException("Trip must have a valid job type (FTL or LTL).", nameof(trip));
            }

            switch (trip.VanType)
            {
                case VanType.DryVan:
                    break;

                case VanType.Reefer:
                    cost *= 1 + carrier.ReeferCharge;
                    break;

                default:
                    throw new ArgumentException("Trip must have a valid van type (Dry Van or Reefer).", nameof(trip));
            }

            return (decimal)cost;
        }

        ///
        /// \brief Get the OSHT mark-up for a rate type, or 0 when none is configured.
        ///
        public static double GetMarkup(Rate rates, RateType type)
        {
            if (rates?.RateValuePair == null)
            {
                return 0.0;
            }

            return rates.RateValuePair.TryGetValue(type, out double value) ? value : 0.0;
        }

        ///
        /// \brief Fill in the trip's total distance and time, reading the route table from the database.
        ///
        public void CalculateDistanceAndTime(Trip trip)
        {
            if (trip == null)
            {
                throw new ArgumentNullException(nameof(trip));
            }

            if (trip.OriginCity == trip.DestinationCity)
            {
                trip.TotalDistance = 0;
                trip.TotalTime = 0.0;
                return;
            }

            DAL db = new DAL();
            CalculateDistanceAndTime(trip, db.GetRoutes());
        }

        ///
        /// \brief Fill in the trip's total distance and time from a given route table. Has no database access.
        ///
        public static void CalculateDistanceAndTime(Trip trip, IList<Route> routes)
        {
            if (trip == null)
            {
                throw new ArgumentNullException(nameof(trip));
            }

            RouteEstimate estimate = CalculateRoute(trip.OriginCity, trip.DestinationCity, trip.JobType, routes);
            trip.TotalDistance = estimate.TotalDistance;
            trip.TotalTime = estimate.TotalHours;
        }

        ///
        /// \brief Work out the legs between two cities on the corridor.
        ///
        /// Each route row describes the leg from its Destination city to the city on its East.
        /// Travelling west uses the same legs in reverse order.
        ///
        /// \return The legs in travel order. Empty when origin and destination are the same.
        ///
        public static List<RouteLeg> GetLegs(City origin, City destination, IList<Route> routes)
        {
            if (routes == null)
            {
                throw new ArgumentNullException(nameof(routes));
            }

            if (origin == City.Null || destination == City.Null)
            {
                throw new ArgumentException("Origin and destination must be real cities.");
            }

            var legs = new List<RouteLeg>();
            if (origin == destination)
            {
                return legs;
            }

            Dictionary<City, Route> byCity = IndexRoutes(routes);
            bool eastbound = destination > origin;
            City west = eastbound ? origin : destination;
            City east = eastbound ? destination : origin;

            City current = west;
            var visited = new HashSet<City>();
            while (current != east)
            {
                if (!byCity.TryGetValue(current, out Route route))
                {
                    throw new InvalidOperationException($"The route table has no entry for {current}.");
                }

                if (route.East == City.Null || !visited.Add(current))
                {
                    throw new InvalidOperationException($"The route table does not connect {west} to {east}.");
                }

                legs.Add(new RouteLeg(current, route.East, route.Distance, route.Time));
                current = route.East;
            }

            if (!eastbound)
            {
                legs.Reverse();
                legs = legs.Select(l => new RouteLeg(l.To, l.From, l.Distance, l.DrivingHours)).ToList();
            }

            return legs;
        }

        ///
        /// \brief Calculate total distance and elapsed time for a trip, including stops and rest.
        ///
        /// Rules:
        ///   - 2 hours to load at the origin and 2 hours to unload at the destination;
        ///   - LTL trips also stop for 2 hours at every city in between;
        ///   - a driver may drive at most 8 hours and work at most 12 hours a day,
        ///     after which they rest until the next day starts.
        ///
        public static RouteEstimate CalculateRoute(City origin, City destination, JobType jobType, IList<Route> routes)
        {
            List<RouteLeg> legs = GetLegs(origin, destination, routes);
            if (legs.Count == 0)
            {
                return new RouteEstimate(0, 0.0, 0.0, 0);
            }

            var clock = new DriverClock();
            clock.Work(LoadUnloadHours, driving: false);

            for (int i = 0; i < legs.Count; i++)
            {
                clock.Work(legs[i].DrivingHours, driving: true);

                bool isLastLeg = i == legs.Count - 1;
                if (!isLastLeg && jobType == JobType.LTL)
                {
                    clock.Work(LtlStopHours, driving: false);
                }
            }

            clock.Work(LoadUnloadHours, driving: false);

            return new RouteEstimate(
                legs.Sum(l => l.Distance),
                Math.Round(clock.ElapsedHours, 2),
                Math.Round(legs.Sum(l => l.DrivingHours), 2),
                clock.RestDays);
        }

        ///
        /// \brief Link each route to its east and west neighbours by city, regardless of row order.
        ///
        public static void LinkRoutes(IList<Route> routes)
        {
            Dictionary<City, Route> byCity = IndexRoutes(routes);
            foreach (Route route in routes)
            {
                route.WestPtr = byCity.TryGetValue(route.West, out Route west) ? west : null;
                route.EastPtr = byCity.TryGetValue(route.East, out Route east) ? east : null;
            }
        }

        private static Dictionary<City, Route> IndexRoutes(IEnumerable<Route> routes)
        {
            var byCity = new Dictionary<City, Route>();
            foreach (Route route in routes)
            {
                if (route != null && route.Destination != City.Null)
                {
                    byCity[route.Destination] = route;
                }
            }

            return byCity;
        }

        ///
        /// \brief Tracks a driver's working day so rest periods are added when limits are hit.
        ///
        private sealed class DriverClock
        {
            private double drivingToday;
            private double onDutyToday;

            public double ElapsedHours { get; private set; }

            public int RestDays { get; private set; }

            public void Work(double hours, bool driving)
            {
                double remaining = hours;
                while (remaining > 1e-9)
                {
                    double allowed = MaxOnDutyHoursPerDay - onDutyToday;
                    if (driving)
                    {
                        allowed = Math.Min(allowed, MaxDrivingHoursPerDay - drivingToday);
                    }

                    if (allowed <= 1e-9)
                    {
                        Rest();
                        continue;
                    }

                    double chunk = Math.Min(allowed, remaining);
                    ElapsedHours += chunk;
                    onDutyToday += chunk;
                    if (driving)
                    {
                        drivingToday += chunk;
                    }

                    remaining -= chunk;
                }
            }

            private void Rest()
            {
                ElapsedHours += HoursPerDay - onDutyToday;
                drivingToday = 0;
                onDutyToday = 0;
                RestDays++;
            }
        }
    }

    ///
    /// \brief One leg of a trip between two neighbouring cities.
    ///
    public sealed class RouteLeg
    {
        public RouteLeg(City from, City to, int distance, double drivingHours)
        {
            From = from;
            To = to;
            Distance = distance;
            DrivingHours = drivingHours;
        }

        public City From { get; }

        public City To { get; }

        public int Distance { get; }

        public double DrivingHours { get; }
    }

    ///
    /// \brief Result of a route calculation.
    ///
    public sealed class RouteEstimate
    {
        public RouteEstimate(int totalDistance, double totalHours, double drivingHours, int restDays)
        {
            TotalDistance = totalDistance;
            TotalHours = totalHours;
            DrivingHours = drivingHours;
            RestDays = restDays;
        }

        /// Kilometres travelled.
        public int TotalDistance { get; }

        /// Elapsed hours including loading, stops and overnight rest.
        public double TotalHours { get; }

        /// Hours spent driving.
        public double DrivingHours { get; }

        /// Number of overnight rests needed.
        public int RestDays { get; }

        /// Elapsed time in days, rounded to one decimal.
        public double TotalDays => Math.Round(TotalHours / TripManager.HoursPerDay, 1);
    }
}
