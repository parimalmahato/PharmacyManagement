# SQL integration test connection troubleshooting

The integration suite requires `PHARMACY_TEST_SQLSERVER` in the terminal that
runs `dotnet test`. Use a disposable SQL Server instance and a login allowed to
create/drop databases. Never use the customer database connection. Each test
creates, migrates and drops a uniquely named `Pharmacy_IT_...` database.

For the local SQLEXPRESS test instance on this Windows workstation, run from the
repository root in a normal PowerShell terminal:

```powershell
.\scripts\test-sql.ps1 -LocalSqlExpress
```

This explicitly uses `Server=lpc:.\SQLEXPRESS` with Windows Authentication and
native SqlClient networking. It checks that the SQL Express service is running,
rebuilds the test host without the managed-networking override, and restores the
previous environment variable afterwards. It changes no SQL service/protocol
settings. Use this switch only for the local test instance. For another test
server, set `PHARMACY_TEST_SQLSERVER` and run the script without that switch.

**Named Pipes Provider, error 40 / system cannot find the file specified** means
the endpoint could not be reached. It does not identify a missing project file.
Confirm the server/instance and running service. An explicit local `lpc:` endpoint
avoids network instance discovery; it requires Shared Memory enabled on that SQL
Server. For a remote test server, use its configured TCP hostname and port, or
verify SQL Browser and named-instance discovery. Do not assume SQLEXPRESS uses
TCP port 1433. See [Microsoft's connection formats](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/logging-in-to-sql-server).

If the suite reports **requires encryption but this machine does not support it**
from `SqlServerTestDatabase.CreateAsync`, database setup failed before the test
could exercise pharmacy behavior. This is a client/server TLS connection failure,
not a failed stock or payment assertion. The message alone does not establish
which OS, driver, certificate or SQL Server setting caused it.

For this workspace, the native connection to `localhost\SQLEXPRESS` reproduced
that message inside the restricted execution sandbox, then passed the same
checkout integration test outside the sandbox with identical connection settings.
When running through the coding agent, approve the SQL test command outside the
sandbox. When running manually, use a normal PowerShell terminal under your
Windows account. An environment variable set in your terminal is not inherited
by an already-running IDE/agent process; set it in the terminal running the tests.
No managed-networking override is needed for the successful native connection.
The full suite subsequently passed: 103 tests, including 26 SQL integration tests,
with no failures or skips (2026-10-21).

On Windows, compare the native networking path with Microsoft's managed path:

```powershell
dotnet test backend/Pharmacy.Tests/Pharmacy.Tests.csproj -c ReturnValidation -p:SqlClientUseManagedNetworking=true --filter 'FullyQualifiedName~IntegrationTests'
```

This opt-in build property affects only the test host. It leaves `Encrypt` and
certificate validation unchanged and never retries with weaker security settings.
Rebuild without the property to return to native networking. Do not use `--no-build`
when changing this property. Use a TCP-enabled SQL Server endpoint for this
comparison; managed networking is a testing/debugging option, not a production
fix. It does not support non-domain Windows Authentication. If using a local
Windows account or LocalDB, investigate the native path instead.

If managed networking succeeds, investigate the native SNI/Windows TLS setup.
If both fail, check SQL Server/Windows patch levels, TLS compatibility, certificate
configuration and the Windows Schannel event log. A certificate-chain error is a
different diagnostic: install/trust the issuing CA and use the matching hostname.
`TrustServerCertificate=True` does not add missing TLS support. Do not disable
encryption or enable obsolete TLS protocols to hide this failure.

For unit tests without a database:

```powershell
dotnet test backend/Pharmacy.Tests/Pharmacy.Tests.csproj --filter 'FullyQualifiedName!~IntegrationTests'
```

References: [Microsoft's AppContext networking switches](https://learn.microsoft.com/en-us/sql/connect/ado-net/appcontext-switches)
and [SqlClient encryption and certificate validation](https://learn.microsoft.com/en-us/sql/connect/ado-net/encryption-and-certificate-validation).
