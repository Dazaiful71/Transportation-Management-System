-- Runs last in the Docker container: give the app account access to both databases.
GRANT SELECT, INSERT, UPDATE, DELETE, LOCK TABLES, SHOW VIEW, TRIGGER, EVENT ON tms.* TO 'tms_app'@'%';
GRANT SELECT ON cmp.* TO 'tms_app'@'%';
FLUSH PRIVILEGES;
