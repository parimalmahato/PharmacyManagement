
import { useCallback, useEffect, useMemo, useState } from "react";

import {
  reportsApi,
  type ExpiryReport,
  type ExpiringBatch,
} from "../api/reports";

import "./ExpiryPage.css";

type ExpiryFilter = "all" | "expired" | "30" | "60" | "90";

export default function ExpiryPage() {
  const [report, setReport] = useState<ExpiryReport | null>(null);

  const [filter, setFilter] = useState<ExpiryFilter>("all");
  const [search, setSearch] = useState("");

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const loadExpiryReport = useCallback(async () => {
    setLoading(true);
    setError("");

    try {
      const data = await reportsApi.getExpiryReport();
      setReport(data);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to load medicine expiry report."
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    Promise.resolve().then(() => {
    void loadExpiryReport();
    });
  }, [loadExpiryReport]);

  const filteredBatches = useMemo(() => {
    if (!report) {
      return [];
    }

    const searchText = search.trim().toLowerCase();

    return report.batches.filter((batch: ExpiringBatch) => {
      const matchesSearch =
        batch.medicineName.toLowerCase().includes(searchText) ||
        batch.batchNumber.toLowerCase().includes(searchText);

      if (!matchesSearch) {
        return false;
      }

      if (filter === "expired") {
        return batch.daysUntilExpiry < 0;
      }

      if (filter === "30") {
        return (
          batch.daysUntilExpiry >= 0 &&
          batch.daysUntilExpiry <= 30
        );
      }

      if (filter === "60") {
        return (
          batch.daysUntilExpiry >= 0 &&
          batch.daysUntilExpiry <= 60
        );
      }

      if (filter === "90") {
        return (
          batch.daysUntilExpiry >= 0 &&
          batch.daysUntilExpiry <= 90
        );
      }

      return true;
    });
  }, [report, filter, search]);

  function getStatusClass(daysUntilExpiry: number): string {
    if (daysUntilExpiry < 0) {
      return "expiry-status-expired";
    }

    if (daysUntilExpiry <= 30) {
      return "expiry-status-urgent";
    }

    if (daysUntilExpiry <= 60) {
      return "expiry-status-warning";
    }

    return "expiry-status-upcoming";
  }

  function getDisplayStatus(batch: ExpiringBatch): string {
    if (batch.daysUntilExpiry < 0) {
      return `Expired ${Math.abs(batch.daysUntilExpiry)} days ago`;
    }

    if (batch.daysUntilExpiry === 0) {
      return "Expires Today";
    }

    return `${batch.daysUntilExpiry} days remaining`;
  }

  return (
    <div className="expiry-page">
      <div className="expiry-heading">
        <div>
          <h1>Medicine Expiry Management</h1>
          <p>
            Track expired and soon-to-expire medicine batches.
          </p>
        </div>

        <button
          type="button"
          onClick={() => void loadExpiryReport()}
          disabled={loading}
        >
          {loading ? "Refreshing..." : "Refresh"}
        </button>
      </div>

      {error && (
        <div className="expiry-error" role="alert">
          {error}
        </div>
      )}

      {loading && <p>Loading expiry report...</p>}

      {report && (
        <>
          <div className="expiry-summary-cards">
            <div className="expiry-summary-card expired">
              <span>Expired Batches</span>
              <strong>{report.summary.expiredBatches}</strong>
            </div>

            <div className="expiry-summary-card urgent">
              <span>Expiring Within 30 Days</span>
              <strong>
                {report.summary.expiringWithin30Days}
              </strong>
            </div>

            <div className="expiry-summary-card warning">
              <span>Expiring Within 60 Days</span>
              <strong>
                {report.summary.expiringWithin60Days}
              </strong>
            </div>

            <div className="expiry-summary-card upcoming">
              <span>Expiring Within 90 Days</span>
              <strong>
                {report.summary.expiringWithin90Days}
              </strong>
            </div>
          </div>

          <section className="expiry-section">
            <div className="expiry-section-heading">
              <div>
                <h2>Expiry Alerts</h2>
                <p>
                  Showing {filteredBatches.length} of{" "}
                  {report.batches.length} batches.
                </p>
              </div>
            </div>

            <div className="expiry-filters">
              <label>
                Search Medicine or Batch
                <input
                  type="search"
                  placeholder="Enter medicine name or batch..."
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </label>

              <label>
                Expiry Filter
                <select
                  value={filter}
                  onChange={(e) =>
                    setFilter(e.target.value as ExpiryFilter)
                  }
                >
                  <option value="all">All Expiry Alerts</option>
                  <option value="expired">Expired</option>
                  <option value="30">Within 30 Days</option>
                  <option value="60">Within 60 Days</option>
                  <option value="90">Within 90 Days</option>
                </select>
              </label>
            </div>

            <div className="expiry-table-wrapper">
              <table className="expiry-table">
                <thead>
                  <tr>
                    <th>Medicine</th>
                    <th>Batch Number</th>
                    <th>Expiry Date</th>
                    <th>Quantity</th>
                    <th>Status</th>
                  </tr>
                </thead>

                <tbody>
                  {filteredBatches.length === 0 ? (
                    <tr>
                      <td colSpan={5} className="expiry-empty">
                        No medicine batches found.
                      </td>
                    </tr>
                  ) : (
                    filteredBatches.map((batch) => (
                      <tr key={batch.batchId}>
                        <td>{batch.medicineName}</td>
                        <td>{batch.batchNumber}</td>
                        <td>{batch.expiryDate}</td>
                        <td>{batch.quantity}</td>
                        <td>
                          <span
                            className={`expiry-status ${getStatusClass(
                              batch.daysUntilExpiry
                            )}`}
                          >
                            {getDisplayStatus(batch)}
                          </span>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}
    </div>
  );
}
