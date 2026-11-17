-- =====================================================================
-- Sample data for a fresh TMS database. Run after 01_schema.sql.
--
-- Sign-in accounts (password = username). Change them before real use:
--   admin / admin      (Admin)
--   buyer / buyer      (Buyer)
--   planner / planner  (Planner)
-- =====================================================================

USE tms;

INSERT INTO Users (FirstName, LastName, Username, PasswordHash, Email, UserType) VALUES
    ('System', 'Admin',   'admin',   '$2a$11$MwyJdVoItJ70B2BsaOhyYuEysE8B/KvSeb.nPOzYv6QHf.KGTzfZe', 'admin@example.com',   2),
    ('Sample', 'Buyer',   'buyer',   '$2a$11$cDIaPieYjSynZar3ZuI53uObhhdGjWB272fZfxC1N6zHTbUIsukLm', 'buyer@example.com',   0),
    ('Sample', 'Planner', 'planner', '$2a$11$.OO0.TF5G2Emd1ZPf.T2TuqpOxTJCj.QFYSVyWb3aQ2aPr05S3MYi', 'planner@example.com', 1);

-- Corridor legs, west to east. Each row: leg from Destination to its East neighbour.
INSERT INTO Routes (Destination, Distance, Time, West, East) VALUES
    (0, 191, 2.50, NULL, 1),  -- Windsor    -> London
    (1, 128, 1.75, 0,    2),  -- London     -> Hamilton
    (2,  68, 1.25, 1,    3),  -- Hamilton   -> Toronto
    (3,  60, 1.30, 2,    4),  -- Toronto    -> Oshawa
    (4, 134, 1.65, 3,    5),  -- Oshawa     -> Belleville
    (5,  82, 1.20, 4,    6),  -- Belleville -> Kingston
    (6, 196, 2.50, 5,    7),  -- Kingston   -> Ottawa
    (7,   0, 0.00, 6,    NULL); -- Ottawa (end of line)

-- OSHT mark-up on top of carrier rates.
INSERT INTO Rates (Name, RateType, Rate) VALUES
    ('OSHT', 0, 0.0800),  -- FTL +8%
    ('OSHT', 1, 0.0500);  -- LTL +5%

INSERT INTO Carriers (CarrierName, FTLRate, LTLRate, reefCharge) VALUES
    ('Planet Express',    5.2100, 0.3621, 0.0800),
    ('Schooner''s',       5.0500, 0.3434, 0.0700),
    ('Tillman Transport', 5.1100, 0.3012, 0.0900),
    ('We Haul',           5.2000, 0.0000, 0.0650);

INSERT INTO CarrierCity (CarrierID, DepotCity, FTLAval, LTLAval)
SELECT c.CarrierID, d.DepotCity, d.FTLAval, d.LTLAval
FROM Carriers c
JOIN (
    SELECT 'Planet Express' AS CarrierName, 'Windsor' AS DepotCity, 50 AS FTLAval, 640 AS LTLAval
    UNION ALL SELECT 'Planet Express',    'Hamilton',   50, 640
    UNION ALL SELECT 'Planet Express',    'Oshawa',     50, 640
    UNION ALL SELECT 'Planet Express',    'Belleville', 50, 640
    UNION ALL SELECT 'Planet Express',    'Ottawa',     50, 640
    UNION ALL SELECT 'Schooner''s',       'London',     18,  98
    UNION ALL SELECT 'Schooner''s',       'Toronto',    18,  98
    UNION ALL SELECT 'Schooner''s',       'Kingston',   18,  98
    UNION ALL SELECT 'Tillman Transport', 'Windsor',    24,  35
    UNION ALL SELECT 'Tillman Transport', 'London',     18,  98
    UNION ALL SELECT 'Tillman Transport', 'Hamilton',   18,  98
    UNION ALL SELECT 'We Haul',           'Ottawa',     11,   0
    UNION ALL SELECT 'We Haul',           'Toronto',    11,   0
) d ON d.CarrierName = c.CarrierName;
