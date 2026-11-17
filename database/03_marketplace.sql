-- =====================================================================
-- Optional: a local Contract Marketplace for development and demos.
--
-- The app reads new contracts from a separate `cmp` database. If you don't
-- have access to a shared marketplace server, run this script and point
-- CMPServer / CMPUser / CMPPassword at your local MySQL.
-- =====================================================================

DROP DATABASE IF EXISTS cmp;
CREATE DATABASE cmp CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE cmp;

-- JOB_TYPE: 0 = FTL, 1 = LTL.  VAN_TYPE: 0 = Dry Van, 1 = Reefer.
-- ORIGIN / DESTINATION are city names on the Windsor - Ottawa corridor.
CREATE TABLE Contract (
    CONTRACT_ID INT          NOT NULL AUTO_INCREMENT,
    CLIENT_NAME VARCHAR(100) NOT NULL,
    JOB_TYPE    TINYINT      NOT NULL,
    QUANTITY    INT          NOT NULL DEFAULT 0,
    ORIGIN      VARCHAR(20)  NOT NULL,
    DESTINATION VARCHAR(20)  NOT NULL,
    VAN_TYPE    TINYINT      NOT NULL,
    PRIMARY KEY (CONTRACT_ID)
);

INSERT INTO Contract (CLIENT_NAME, JOB_TYPE, QUANTITY, ORIGIN, DESTINATION, VAN_TYPE) VALUES
    ('Northern Grocers',      0,  0, 'Windsor',    'Toronto',    1),
    ('Lakeshore Hardware',    1, 12, 'London',     'Kingston',   0),
    ('Capital Pharma',        0,  0, 'Toronto',    'Ottawa',     1),
    ('Maple Leaf Paper',      1, 25, 'Hamilton',   'Belleville', 0),
    ('Great Lakes Beverages', 0,  0, 'Ottawa',     'Windsor',    0),
    ('Huron Fresh Produce',   1,  8, 'Oshawa',     'London',     1),
    ('Rideau Electronics',    1, 40, 'Kingston',   'Hamilton',   0),
    ('Bay Street Office Co.', 0,  0, 'Belleville', 'Toronto',    0);
