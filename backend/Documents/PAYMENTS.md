Payment recording
=================

React workflow
--------------

After Complete Sale, use the Payments section below the invoice. For an older
sale, choose Payments in Sales History, or open its full invoice. Both screens
show live receipt history, paid amount, outstanding balance and payment status.
Admins and cashiers can record receipts; pharmacists can view payment details.

Choose Cash, UPI or Card, enter the received amount, and provide the transaction
reference for UPI/card. Add split payment creates another receipt row. Fill
remaining balance fills the last row after subtracting previous rows. Recording
less than the balance leaves the sale partially paid.

Split receipts save sequentially, rather than as one atomic operation. A failure
can leave earlier receipts saved; the UI refreshes the balance and reports errors.
Uncertain requests retain their UUIDs in session storage for the same account and
sale, including across a reload. Use Retry pending receipts to confirm them safely
before entering additional payments. Do not clear browser storage while a payment
attempt is unresolved. Refresh payments retrieves current balances and history.

Printed invoices include the payment summary and receipt history. Payment entry
forms and buttons are excluded from printing.

Backend API
-----------

The payment ledger records receipts against existing completed sales. It does not
charge cards or initiate/verify UPI transfers; record electronic payments only
after confirming receipt with the payment provider. Never send card credentials.

POST /api/sales/{saleId}/payments (Admin or Cashier)

```json
{
  "method": 2,
  "amount": 100.00,
  "reference": "provider-transaction-reference",
  "idempotencyKey": "dc285b98-0ac1-40f6-8ddb-dc066355fcb5"
}
```

Methods: 1 Cash, 2 Upi, 3 Card. UPI and card require a reference (150 characters
maximum). Amounts must be positive, with at most two decimal places, and cannot
exceed the outstanding balance. Multiple receipts can settle one sale.

Generate one UUID per receipt and retain it when retrying an uncertain response.
Identical retries return the original receipt (HTTP 200), even after settlement.
Reusing the UUID with different details returns 409. Validation returns 400;
missing sales return 404; overpayment or ineligible sales return 409.

GET /api/sales/{saleId}/payments (roles with ViewSales) returns the receipt history,
total, paid amount, balance and Unpaid/PartiallyPaid/Paid status. Sale history,
sale details and invoice responses also include paidAmount, balanceAmount and
paymentStatus. Sale completion describes inventory checkout independently of
payment settlement. Zero-total sales are Paid without requiring a receipt.

Apply the existing AddPaymentLedger migration using the project's normal database
migration procedure before using these endpoints. No additional schema change is
required. Receipt and audit writes commit in one transaction; SQL Server sale locks
serialize concurrent payments and the unique UUID index protects retries.

Run unit tests:
`dotnet test PharmacyManagement.sln --filter "FullyQualifiedName!~IntegrationTests"`

SQL Server integration tests require PHARMACY_TEST_SQLSERVER pointing to a test
server where the login can create/drop disposable databases. With that configured,
run `dotnet test PharmacyManagement.sln` to include transaction/concurrency tests.
