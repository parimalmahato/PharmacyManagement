-- Run in sqlcmd mode with DatabaseName and BackupPath supplied by the operator.
-- Use a SQL Server service-account-writable path and restrict access to the file.
-- This script is provided for review; it is not run automatically by the app.
BACKUP DATABASE [$(DatabaseName)]
TO DISK = N'$(BackupPath)'
WITH COPY_ONLY, CHECKSUM, COMPRESSION;
GO
RESTORE VERIFYONLY FROM DISK = N'$(BackupPath)' WITH CHECKSUM;
GO
-- VERIFYONLY checks backup readability; it does not replace a full restore drill.
