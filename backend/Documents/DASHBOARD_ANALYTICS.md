Dashboard and sales analytics
=============================

Reporting is read-only. It does not restock returns, approve inspections, or
change invoices. Explicit restocking remains opt-in and requires inspection
approval. No schema migration is needed for this reporting update; the existing
payment, return, refund and inspection migrations must already be applied.

GET /api/reports/dashboard provides today's invoiced sales, return credits, net
sales, collections, refunds and net collections, plus current balances across all
completed invoices. It also shows active medicines, low-stock batches, out-of-stock
medicines, batches expiring within 30 days, saleable inventory cost value, draft
purchases and received purchases whose invoice date is today.

GET /api/reports/sales?startDate=2026-10-01&endDate=2026-10-21 provides the same
period activity, a complete daily series including zero-activity days, collections
and refunds by method, original top-selling medicine volume and purchasing totals.
Existing report authorization remains Admin/Pharmacist.

Definitions
-----------

* All calendar dates use Asia/Kolkata (UTC+05:30), including today's dashboard,
  sales filters, daily grouping and low-stock eligibility.
* Invoiced sales are original totals of completed sales created in the period.
  Existing totalRevenue, todayRevenue and revenue API fields retain this meaning
  for compatibility; the React UI and CSV label them as invoiced sales.
* Return credits use the return's creation date, even for invoices from earlier
  periods. Net sales = invoiced sales - period return credits.
* Collections and refunds use their own receipt timestamps. Net collections =
  collections - refunds. These are cash movements, not profit. Net sales or net
  collections can be negative on days with returns/refunds but no new sales.
* Period outstanding and refund-due values are CURRENT settlement balances for
  invoices originally created in the selected period. They use all payments,
  credits and refunds for those invoices, including later transactions. They are
  not historical balances as of the selected end date. Dashboard balances cover
  all completed invoices.
* Top medicines show gross invoiced quantities and values. They are explicitly
  labelled original invoiced volume; later returns do not rewrite their rankings.
* Received purchase totals use supplier invoice dates and current Received status,
  not receipt timestamps (the purchase model has no received-at field). Draft and
  cancelled purchases are excluded from purchase value. Draft count is current.
* Inventory valuation is quantity times purchase price for active medicines in
  unexpired batches. Expired stock is excluded. Out-of-stock means no positive
  unexpired batch for an active medicine. Low-stock is 1–10 units per eligible
  batch; expiring soon excludes batches that expire today or are already expired.

React displays the summary, filtered financial totals, daily comparison chart,
daily transaction table, method breakdown, purchase metrics, and inventory alerts.
CSV export includes the same financial metrics and scope labels. Date ranges are
limited to 366 inclusive days; missing dates and unsupported boundary dates fail
with 400. The report endpoints perform no writes.

Validation: run the normal frontend build/lint and backend unit tests. SQL Server
integration tests require PHARMACY_TEST_SQLSERVER with create/drop permissions on
a disposable test server. They cover transaction-date attribution, India midnight
boundaries, current cohort balances, zero-activity days and unchanged inventory.
