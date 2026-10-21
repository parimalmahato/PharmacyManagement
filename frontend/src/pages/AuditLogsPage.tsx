
import { useEffect, useState } from "react";
import {
  auditLogsApi,
  type AuditLogResponse,
} from "../api/auditLogs";
import "./UsersPage.css";

export default function AuditLogsPage() {
  const [data, setData] =
    useState<AuditLogResponse | null>(null);

  const [page, setPage] = useState(1);
  const [action, setAction] = useState("");
  const [entityType, setEntityType] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const result = await auditLogsApi.getAll(
          page,
          25,
          action,
          entityType
        );

        if (!cancelled) {
          setData(result);
          setError("");
        }
      } catch (err) {
        if (!cancelled) {
          setError(
            err instanceof Error
              ? err.message
              : "Unable to load audit logs."
          );
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [page, action, entityType]);

  const totalPages = Math.max(
    1,
    Math.ceil((data?.totalCount ?? 0) / 25)
  );

  return (
    <main className="users-page">
      <h1>Audit Logs</h1>

      <section className="users-panel">
        <h2>Filter Audit History</h2>

        <div className="users-form">
          <label>
            Action
            <select
              value={action}
              onChange={(e) => {
                setAction(e.target.value);
                setPage(1);
              }}
            >
              <option value="">All Actions</option>
              <option value="EmployeeCreated">
                Employee Created
              </option>
              <option value="EmployeeActivated">
                Employee Activated
              </option>
              <option value="EmployeeDeactivated">
                Employee Deactivated
              </option>
              <option value="EmployeePasswordReset">
                Password Reset
              </option>
              <option value="MedicineCreated">Medicine Created</option>
            <option value="MedicineUpdated">Medicine Updated</option>
            <option value="MedicineDeactivated">Medicine Deactivated</option>
            <option value="BatchCreated">Batch Created</option>
            <option value="StockAdjusted">Stock Adjusted</option>
            <option value="PurchaseReceived">Purchase Received</option>
            <option value="SaleCompleted">Sale Completed</option>
            <option value="SaleCancelled">Sale Cancelled</option>
            </select>
          </label>

          <label>
            Entity
            <select
              value={entityType}
              onChange={(e) => {
                setEntityType(e.target.value);
                setPage(1);
              }}
            >
              <option value="">All Entities</option>
              <option value="ApplicationUser">
                Employees
              </option>
              <option value="Medicine">
                Medicines
              </option>
              <option value="MedicineBatch">
                Inventory
              </option>
              <option value="Sale">
                Sales
              </option>
              <option value="PurchaseInvoice">Purchasing</option>
            

            </select>
          </label>
        </div>
      </section>

      {error && <p role="alert">{error}</p>}

      <section className="users-panel">
        <h2>Activity History</h2>

        {loading && !data ? (
          <p>Loading audit logs...</p>
        ) : (
          <>
            <div className="users-table-wrapper">
              <table>
                <thead>
                  <tr>
                    <th>Date / Time</th>
                    <th>Employee</th>
                    <th>Action</th>
                    <th>Entity</th>
                    <th>Entity ID</th>
                    <th>Previous Values</th>
                    <th>New Values</th>
                  </tr>
                </thead>

                <tbody>
                  {data?.items.map((log) => (
                    <tr key={log.id}>
                      <td>
                        {new Date(
                          log.createdAtUtc
                        ).toLocaleString()}
                      </td>
                      <td>{log.userName}</td>
                      <td>{log.action}</td>
                      <td>{log.entityType}</td>
                      <td>{log.entityId ?? "-"}</td>
                      <td>
                        <pre>{log.oldValues ?? "-"}</pre>
                      </td>
                      <td>
                        <pre>{log.newValues ?? "-"}</pre>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <p>
              Total records: {data?.totalCount ?? 0}
            </p>

            <div className="invoice-actions">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage(page - 1)}
              >
                Previous
              </button>

              <span>
                Page {page} of {totalPages}
              </span>

              <button
                type="button"
                disabled={page >= totalPages}
                onClick={() => setPage(page + 1)}
              >
                Next
              </button>
            </div>
          </>
        )}
      </section>
    </main>
  );
}
