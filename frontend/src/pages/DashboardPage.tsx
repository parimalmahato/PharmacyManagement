
import { useCallback, useEffect, useRef, useState } from "react";

import {
  reportsApi,
  type DashboardSummary,
  type SalesReport,
  type LowStockBatch,
} from "../api/reports";

import DailyRevenueChart from "../components/DailyRevenueChart";
import AdvancedReports from "../components/AdvancedReports";
import { exportSalesReport } from "../utils/exportSalesReport";

import "./DashboardPage.css";

// Format date as YYYY-MM-DD
function formatDate(date: Date): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Kolkata", year: "numeric", month: "2-digit", day: "2-digit" }).format(date);
}

// Default report range: last 7 calendar days
function getDefaultStartDate(): string {
  const date = new Date();
  date.setTime(date.getTime() - 6 * 86400000);
  return formatDate(date);
}

const currencyFormatter = new Intl.NumberFormat("en-IN", {
  style: "currency",
  currency: "INR",
  maximumFractionDigits: 2,
});

export default function DashboardPage() {
  const latestRequest = useRef(0);
  // Dashboard summary
  const [summary, setSummary] =
    useState<DashboardSummary | null>(null);

  // Sales report
  const [report, setReport] =
    useState<SalesReport | null>(null);

  // Low-stock batches
  const [lowStockBatches, setLowStockBatches] =
    useState<LowStockBatch[]>([]);

  // Date filters
  const [startDate, setStartDate] =
    useState(getDefaultStartDate);

  const [endDate, setEndDate] =
    useState(() => formatDate(new Date()));

  // UI state
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // Load all dashboard data
  const loadReports = useCallback(
    async (start: string, end: string) => {
      const request = ++latestRequest.current;
      setLoading(true);
      setError("");

      try {
        const [
          dashboardData,
          salesData,
          lowStockData,
        ] = await Promise.all([
          reportsApi.getDashboard(),
          reportsApi.getSalesReport(start, end),
          reportsApi.getLowStock(),
        ]);

        if (request !== latestRequest.current) return;
        setSummary(dashboardData);
        setReport(salesData);
        setLowStockBatches(lowStockData);
      } catch (err) {
        if (request !== latestRequest.current) return;
        setError(
          err instanceof Error
            ? err.message
            : "Unable to load dashboard."
        );
      } finally {
        if (request === latestRequest.current) setLoading(false);
      }
    },
    []
  );

  // Initial data load
  useEffect(() => {
    Promise.resolve().then(() => {
    const today = formatDate(new Date());
    const start = getDefaultStartDate();

    void loadReports(start, today);
    });
  }, [loadReports]);

  // Apply date filter
  function handleFilter() {
    if (!startDate || !endDate) {
      setError("Please select both dates.");
      return;
    }

    if (startDate > endDate) {
      setError("Start date cannot be after end date.");
      return;
    }

    const start = new Date(`${startDate}T00:00:00Z`);
    const end = new Date(`${endDate}T00:00:00Z`);

    if (
      Number.isNaN(start.getTime()) ||
      Number.isNaN(end.getTime())
    ) {
      setError("Please select valid dates.");
      return;
    }

    const days = Math.round(
      (end.getTime() - start.getTime()) / 86400000
    );

    if (days > 365) {
      setError(
        "Select a date range of at most 366 days."
      );
      return;
    }

    void loadReports(startDate, endDate);
  }

  // Export current sales report
  function handleExport() {
    if (!report) {
      setError("No sales report available to export.");
      return;
    }

    exportSalesReport(report);
  }

  return (
    <div className="dashboard-page">

      {/* Dashboard heading */}
      <div className="dashboard-heading">
        <div>
          <h1>Pharmacy Dashboard</h1>
          <p>
            Monitor your pharmacy's sales and inventory.
          </p>
        </div>

        <button
          type="button"
          onClick={handleFilter}
          disabled={loading}
        >
          {loading ? "Refreshing..." : "Refresh"}
        </button>
      </div>

      {/* Error message */}
      {error && (
        <div
          className="dashboard-error"
          role="alert"
        >
          {error}
        </div>
      )}

      {loading && <p>Loading reports...</p>}

      {/* Dashboard summary cards */}
      {summary && (
        <div className="dashboard-cards">

          <div className="dashboard-card">
            <span>Today's Invoiced Sales (IST)</span>
            <strong>
              {currencyFormatter.format(
                summary.todayRevenue
              )}
            </strong>
          </div>

          <div className="dashboard-card">
            <span>Today's Sales</span>
            <strong>
              {summary.todaySalesCount}
            </strong>
          </div>

          <div className="dashboard-card">
            <span>Active Medicines</span>
            <strong>
              {summary.totalActiveMedicines}
            </strong>
          </div>

          <div className="dashboard-card warning">
            <span>Low Stock Batches</span>
            <strong>
              {summary.lowStockBatchCount}
            </strong>
          </div>
          {([
            ["Today's return/cancellation credits", summary.todayReturnCredits], ["Today's net sales", summary.todayNetSales],
            ["Today's collections", summary.todayCollections], ["Today's refunds", summary.todayRefunds],
            ["Today's net collections", summary.todayNetCollections], ["Outstanding — all invoices", summary.outstandingBalance],
            ["Refund due — all invoices", summary.refundDue], ["Received purchase value — today's invoice date", summary.todayPurchaseValue],
            ["Saleable inventory at cost", summary.inventoryCostValue],
          ] as const).map(([label, value]) => <div className="dashboard-card" key={label}><span>{label}</span><strong>{currencyFormatter.format(value)}</strong></div>)}
          {([
            ["Received purchases — today's invoice date", summary.todayReceivedPurchases], ["Draft purchases", summary.draftPurchases],
            ["Out-of-stock medicines", summary.outOfStockMedicines], ["Batches expiring in 30 days", summary.expiringWithin30Days],
          ] as const).map(([label, value]) => <div className="dashboard-card" key={label}><span>{label}</span><strong>{value}</strong></div>)}

        </div>
      )}

      {/* Sales report section */}
      <section className="dashboard-section">

        <h2>Sales Report</h2>
        <p>Dates use India Standard Time. Net sales = invoiced sales − return/cancellation credits. Net collections = receipts − refunds.
          Returns and cash movements use their own transaction dates; top medicines show original invoiced volume.</p>

        {/* Date filters */}
        <div className="dashboard-filters">

          <label>
            Start Date
            <input
              type="date"
              value={startDate}
              onChange={(e) =>
                setStartDate(e.target.value)
              }
            />
          </label>

          <label>
            End Date
            <input
              type="date"
              value={endDate}
              onChange={(e) =>
                setEndDate(e.target.value)
              }
            />
          </label>

          <button
            type="button"
            onClick={handleFilter}
            disabled={
              loading || !startDate || !endDate
            }
          >
            Apply Filter
          </button>

        </div>

        {/* Render only when report exists */}
        {report && (
          <>

            {/* Report summary */}
            <div className="dashboard-report-summary">

              <div>
                <span>Total Sales</span>
                <strong>
                  {report.totalSales}
                </strong>
              </div>

              <div>
                <span>Invoiced Sales</span>
                <strong>
                  {currencyFormatter.format(
                    report.totalRevenue
                  )}
                </strong>
              </div>
              {([
                ["Return / cancellation credits", report.returnCredits], ["Net sales", report.netSales], ["Collections", report.collections],
                ["Refunds", report.refunds], ["Net collections", report.netCollections],
                ["Current outstanding — invoices in range", report.outstandingBalance], ["Current refund due — invoices in range", report.refundDue],
                ["Received purchase value — invoice date", report.purchaseValue],
              ] as const).map(([label, value]) => <div key={label}><span>{label}</span><strong>{currencyFormatter.format(value)}</strong></div>)}
              <div><span>Received purchase invoices</span><strong>{report.receivedPurchases}</strong></div>

            </div>

            {/* CSV export */}
            <div className="dashboard-export-actions">
              <button
                type="button"
                onClick={handleExport}
              >
                Export CSV
              </button>
            </div>

            {/* Daily revenue chart */}
            <section className="dashboard-chart-section">
              <h3>Daily Sales and Collections</h3>

              <DailyRevenueChart
                data={report.dailySales}
              />
            </section>

            {/* Daily sales table */}
            <h3>Daily Sales</h3>

            <div className="dashboard-table-wrapper">
              <table className="dashboard-table">

                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Sales Count</th>
                    <th>Invoiced sales</th><th>Return / cancellation credits</th><th>Net sales</th><th>Collections</th><th>Refunds</th><th>Net collections</th>
                  </tr>
                </thead>

                <tbody>
                  {report.dailySales.length === 0 ? (
                    <tr>
                      <td colSpan={8}>
                        No sales found for the selected dates.
                      </td>
                    </tr>
                  ) : (
                    report.dailySales.map((day) => (
                      <tr key={day.date}>
                        <td>{day.date}</td>
                        <td>{day.salesCount}</td>
                        <td>
                          {currencyFormatter.format(
                            day.revenue
                          )}
                        </td>
                        <td>{currencyFormatter.format(day.returnCredits)}</td><td>{currencyFormatter.format(day.netSales)}</td>
                        <td>{currencyFormatter.format(day.collections)}</td><td>{currencyFormatter.format(day.refunds)}</td><td>{currencyFormatter.format(day.netCollections)}</td>
                      </tr>
                    ))
                  )}
                </tbody>

              </table>
            </div>

            {/* Top selling medicines */}
            <h3>Collections and Refunds by Method</h3>
            <div className="dashboard-table-wrapper"><table className="dashboard-table"><thead><tr><th>Method</th><th>Collections</th><th>Refunds</th><th>Net collections</th></tr></thead>
              <tbody>{report.collectionsByMethod.map(row => <tr key={row.method}><td>{row.method === "Upi" ? "UPI" : row.method}</td>
                <td>{currencyFormatter.format(row.collections)}</td><td>{currencyFormatter.format(row.refunds)}</td><td>{currencyFormatter.format(row.netCollections)}</td></tr>)}</tbody></table></div>
            <h3>Top Selling Medicines — Original Invoiced Volume</h3>

            <div className="dashboard-table-wrapper">
              <table className="dashboard-table">

                <thead>
                  <tr>
                    <th>Medicine</th>
                    <th>Quantity Sold</th>
                    <th>Invoiced sales</th>
                  </tr>
                </thead>

                <tbody>
                  {report.topSellingMedicines.length === 0 ? (
                    <tr>
                      <td colSpan={3}>
                        No medicine sales found.
                      </td>
                    </tr>
                  ) : (
                    report.topSellingMedicines.map(
                      (medicine) => (
                        <tr key={medicine.medicineId}>
                          <td>
                            {medicine.medicineName}
                          </td>
                          <td>
                            {medicine.quantitySold}
                          </td>
                          <td>
                            {currencyFormatter.format(
                              medicine.revenue
                            )}
                          </td>
                        </tr>
                      )
                    )
                  )}
                </tbody>

              </table>
            </div>

          </>
        )}

      </section>

      {/* Low-stock alerts */}
      <AdvancedReports />
      <section className="dashboard-section dashboard-alert-section">

        <h2>Low Stock Alerts</h2>

        <p>
          Medicine batches with 1 to {summary?.lowStockThreshold ?? 10} units remaining,
          excluding expired batches.
        </p>

        <div className="dashboard-table-wrapper">
          <table className="dashboard-table">

            <thead>
              <tr>
                <th>Medicine</th>
                <th>Batch Number</th>
                <th>Available Quantity</th>
                <th>Expiry Date</th>
                <th>Status</th>
              </tr>
            </thead>

            <tbody>
              {lowStockBatches.length === 0 ? (
                <tr>
                  <td colSpan={5}>
                    No low-stock batches found.
                  </td>
                </tr>
              ) : (
                lowStockBatches.map((batch) => (
                  <tr key={batch.batchId}>
                    <td>
                      {batch.medicineName}
                    </td>
                    <td>
                      {batch.batchNumber}
                    </td>
                    <td>
                      {batch.quantity}
                    </td>
                    <td>
                      {batch.expiryDate}
                    </td>
                    <td>
                      <span className="dashboard-low-stock-badge">
                        Low Stock
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>

          </table>
        </div>

      </section>

    </div>
  );
}
