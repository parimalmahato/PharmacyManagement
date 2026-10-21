Pharmacy settings and configuration
===================================

The existing Admin-only Pharmacy Settings page now manages the pharmacy profile,
GST details, invoice numbering, low-stock threshold, expiry notifications and
receipt preferences. The API remains protected by ManagePharmacySettings.
Settings are stored in SQL Server and saved with an audit entry in one transaction.
After AddBranchIsolation, each branch has its own profile, invoice sequence and
preferences. A new branch needs its own settings before issuing invoices. The
singleton key is unique within a branch. See ../PRODUCT_READINESS.md for rollout.

Apply CompletePharmacyConfiguration with the normal configured connection and
restart the backend/frontend. It preserves existing pharmacy details and issued
invoices, adds defaults for new preferences, and enforces a unique singleton key.
If older data contains multiple settings rows, migration stops with an explicit
message; resolve that data rather than silently deleting a profile.

API
---

GET /api/pharmacy-settings returns the profile, preferences, revision and current
nextInvoiceNumber, or 404 before initial setup. PUT uses the existing profile
fields plus invoiceNumberDigits, lowStockThreshold, expiryAlertsEnabled,
expiryAlertDays, receiptFooter, receiptShowCustomerPhone,
receiptShowPaymentHistory and revision. Send the last loaded revision on updates;
omit it only for first setup. A stale/missing revision on an existing profile
returns 409. Reload before editing again. The server owns revision and sequence
values; clients cannot reset or overwrite the sequence through this endpoint.

Validation checks required profile fields, storage lengths, email format,
15-character GSTIN structure, invoice prefix characters, GST precision/range,
invoice digits (3–12), stock threshold (1–100000), alert window (1–365), and footer
length (500). GSTIN is required when GST is enabled; format validation does not
claim registration verification.

Consumers
---------

* Checkout uses PREFIX-000001 formatting with configurable minimum digits and a
  monotonic, non-resetting sequence. Sequence reservation, invoice, stock movements
  and audit commit together under SQL Server locks. Failed checkout rolls back the
  reservation. Changing prefix or digit width never rewrites previous invoices.
  Before initial setup, the legacy SAL-GUID format remains available for checkout.
* Disabled GST makes new sale lines zero-rated by the billing calculation;
  existing invoices retain their stored taxes. Enabled GST uses each medicine's
  explicit rate. Default GST is used when a create-medicine API request omits its
  rate; explicit zero remains exempt and existing medicine rates are not replaced.
  GET /api/sales/billing-configuration exposes only the effective GST switch to
  authorized sales viewers so the React cart preview follows billing settings.
* Dashboard and low-stock reports read the configured per-batch threshold. Their
  summary count and table use the same active/unexpired eligibility. Default is 10.
* The notification worker rereads expiry settings every 15 minutes. Disabled
  notifications stop new alerts, preserving existing notification history. The
  configurable window defaults to 90 days and includes expired batches. Wider
  windows use an additional alert category; existing 30/60/90 categories remain.
  The expiry inventory report keeps its explicit 30/60/90 analytical buckets;
  notification preferences do not hide expired stock from inventory reporting.
* Both the full invoice and checkout receipt use the configured pharmacy profile,
  customer-phone visibility, footer and printed payment/return-history preference.
  Hidden print history remains available interactively. Receipt preferences and
  pharmacy display details use current configuration; historical sale prices,
  quantities, taxes and invoice numbers remain immutable.

Operational notes
-----------------

Concurrent configuration changes are detected via revision tokens. Invoice
allocation does not change the configuration revision, so normal checkout does
not invalidate an administrator's settings form. The preview of the next number
can advance while the form is open, and checkout always allocates the actual value
on the server. Numbering does not reset annually. No automatic restocking or
inspection settings are introduced by this module.

Backend tests cover validation; SQL Server integration tests additionally cover
configuration persistence, stale updates, invoice numbering, billing GST behavior,
threshold consumption and disabled expiry alerts. Those tests require
PHARMACY_TEST_SQLSERVER with disposable-database create/drop permissions.
