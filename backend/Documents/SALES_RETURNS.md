Sales returns and refunds (Step 23)
===================================

Returns, return lines, refund receipts and inventory movements are separate
append-only records. A return never changes Sale, SaleItem, original invoice
number, prices, quantities, tax amounts, or the original payment receipts.
The invoice screen prints the original invoice values plus separate adjustment
history. Existing sales reports continue to show original invoiced sales totals.

Workflow
--------

Open Payments on Sales History or open a full invoice, then use Returns and
refunds. Admins and cashiers can create returns and record refunds; pharmacists
can view their histories using the existing sales policies.

Choose the quantity for each original batch line and enter a reason. Quantities
cannot exceed sold quantity minus all previous returns. Return value is based
on the original discounted, GST-inclusive line total, with cumulative rounding
so repeated partial returns credit exactly the original total on full return.
Free items may be returned with zero credit. Arbitrary credit amounts are not
accepted from the browser.

Each line has a ReturnDisposition: Saleable=1, Damaged=2, Expired=3,
Quarantine=4, NonResaleable=5. Missing disposition defaults to Quarantine.
Backend domain validation and a database check constraint reject Restock=true
for every disposition except Saleable. Expiry is independently checked against
the original batch before stock is added. The UI clears/disables restocking
when the disposition is unsafe. Physical condition still requires staff inspection;
software cannot detect incorrectly classified medicines.

Inspection approval is a separate explicit confirmation, not implied by choosing
Saleable. Each restocked line must send inspectionApproved=true. The backend
requires a signed-in Admin or Cashier approver and captures the JWT subject ID
and UTC approval timestamp itself; these values cannot be supplied by the client.
Return history exposes inspectionApproved, inspectedByUserId and inspectedAtUtc.
Changing quantity or disposition in React clears approval and the restock flag.
Selecting an unsafe disposition disables approval and restocking. Approvals are
recorded with the return and audited in the same transaction as inventory changes.
Unapproved returns may be recorded without restocking; their stock stays unchanged.

Restock defaults to false. Enable it only after inspecting the returned medicine
and confirming it is suitable for sale. Restocked units go back to the original
batch with a positive SalesReturn stock movement referencing the return ID.
Expired batches cannot be restocked. Non-restocked returns are recorded in the
return ledger without adding saleable inventory; disposal/quarantine inventory
is not introduced by this phase. Refreshing returns also refreshes cart inventory
on the sales screen.

Settlement
----------

* Net payable = original invoice total - return credits.
* Net received = payment receipts - refund receipts.
* Outstanding balance = max(0, net payable - net received).
* Refund due = max(0, net received - net payable).

Returns first offset outstanding balances. For a 100 invoice with 50 collected,
a 40 return leaves 10 outstanding and no refund due. With 100 collected, the same
return creates 40 refund due. Refunds cannot exceed refund due; new payments
cannot exceed the adjusted outstanding balance. Multiple partial refunds and
cash/UPI/card receipts are supported. UPI/card refund references are required.

Refund recording does not initiate bank, card or UPI transfers. Complete the
actual payout first and record its receipt. Credits here are internal return
records; automated statutory tax-credit document generation is outside this phase.

API
---

GET /api/sales/{saleId}/returns returns returnable line IDs, batch details, remaining
quantities, return history, refund history and refund due.

POST /api/sales/{saleId}/returns:

```json
{
  "reason": "Unopened package returned",
  "idempotencyKey": "4b20a3ea-9d6c-4390-8cb3-c36758f46b19",
  "items": [{ "saleItemId": 12, "quantity": 1, "restock": true, "disposition": 1, "inspectionApproved": true }]
}
```

POST /api/sales/{saleId}/refunds:

```json
{
  "method": 1,
  "amount": 40.00,
  "reference": null,
  "idempotencyKey": "6ea4701e-0939-45ee-b830-153dd78d983e"
}
```

To save a return and confirmed refund atomically, add a refund object to the
POST returns body:

```json
"refund": { "method": 1, "amount": 40.00, "reference": null }
```

This is an optional property in the request object, alongside reason, items and
idempotencyKey. The combined operation uses the return key for its refund receipt.
Return, all stock changes, movement entries, refund receipt, and both audit entries
commit in one SQL transaction. Invalid refund amounts or audit failures roll back
everything. Identical retries return the same return and linked refund; changed
refund details conflict. The response includes a refund object, and its SaleReturnId
links the ledger receipt to the return. Standalone refunds remain available for
later payouts. Their RefundMethod enum uses Cash=1, Upi=2, Card=3, preserving the
existing API numeric values and stored history.

The React return form offers Record a confirmed refund with this return. The
server validates its amount against refund due after applying the return credit.
External transfers are outside the SQL transaction and are not initiated here.

All successful writes return 200 with the original result on an identical retry.
Keys are unique per sale and operation type; changed payloads return 409. Invalid
input returns 400, missing sales 404, ineligible restocking or excessive refunds
409. Return requests are atomic across all their lines. Each return/refund commits
with its audit entry; restocking and movements commit with the return. SQL Server
sale locks serialize payments, returns and refunds. Batch updates use the same
locking pattern as checkout. The UI preserves unresolved operations and keys in
session storage across reloads so retries cannot double-restock or double-refund.

Deployment and validation
-------------------------

Stop the running backend before building/restarting with the new model. Apply
the generated AddSalesReturnsAndRefunds and EnforceReturnDispositionAndAtomicRefund
and AddReturnInspectionApproval migrations to the intended database using
the normal configured connection and credentials:

```powershell
dotnet ef database update --project backend/Pharmacy.Infrastructure --startup-project backend/Pharmacy.Api
```

The migration adds SaleReturns, SaleReturnItems and SaleRefunds. It does not
rewrite invoices or backfill historical credits. Restart the backend and frontend
after updating the schema.

The second migration preserves existing return/refund rows. Historical restocked
lines are classified as Saleable based on the previous restock flag; other lines
become Quarantine. This backfill is not evidence of a past inspection and does not
change existing inventory. Historical refunds keep a null return link. Older
clients must send disposition=1 explicitly when requesting restocking; missing
or unsafe disposition cannot add sellable stock.

AddReturnInspectionApproval adds approval metadata without inventing approvals
for historical returns or changing stock. Historical rows remain unapproved
with null approver/time; the UI labels these as having no recorded inspection
approval. New restocking requests require explicit approval. Clients retaining
older pending requests must refresh and provide approval before creating any
new restocking record.

Unit tests:
`dotnet test backend/Pharmacy.Tests --filter "FullyQualifiedName!~IntegrationTests"`

Integration tests require PHARMACY_TEST_SQLSERVER pointing to a disposable test
server with create/drop database permissions. Run `dotnet test backend/Pharmacy.Tests`
with it configured. Coverage includes invoice immutability, restocking, unpaid
returns, expired batch rejection, retries, refund limits, concurrent returns/refunds
and transactional rollback when auditing fails.
