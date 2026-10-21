# Sales cancellation

Apply `20261021123324_AddSaleCancellation` before running the updated API.
The migration adds cancellation metadata to Sales; it does not delete or rewrite
invoice lines, prices, taxes, receipts or refunds.

## Operator workflow

Before checkout, **Clear cart** discards an unsubmitted cart without changing stock.
For a saved invoice, Admin or Pharmacist opens **Sales → View invoice → Cancel this
sale**. Enter a reason and explicitly confirm the medicines never left pharmacy
custody. Use the returns workflow if the customer received the medicines.

Stock restoration defaults to off. If all items have been inspected, are undamaged
and approved for resale, select restoration and confirm inspection. The server
rejects any expired batch and quantity overflow, and restores each original batch
with a positive `SaleCancellation` stock movement. Without restoration, stock stays
outside sellable inventory; segregate the medicines physically. There is no later
automatic restoration or cancellation undo. Mixed item dispositions should use
the controlled returns workflow instead.

Invoices with existing returns or refunds cannot be cancelled. A cancellation
preserves its original invoice and records the reason, approver, UTC timestamp,
stock handling and audit event. Cashiers cannot cancel invoices.

## Payments and refunds

Cancellation makes net payable and outstanding balance zero. Existing receipts
remain visible and create a refund due. It does not transfer money. An Admin or
Cashier records the actual payout under **Returns and refunds** on the cancelled
invoice. Partial refunds are allowed up to the remaining paid amount. New receipts
and medicine returns are blocked on a cancelled invoice. Existing receipt/return
retry keys may still retrieve their original result, without creating new entries.

## API and guarantees

`POST /api/sales/{id}/cancel` requires an Admin or Pharmacist token:

```json
{
  "reason": "Incorrect billing; medicines retained in pharmacy",
  "idempotencyKey": "a728fbde-e217-4b9c-b744-7fe56703120d",
  "medicinesNotDispensed": true,
  "restock": true,
  "inspectionApproved": true
}
```

Use a new UUID for each cancellation operation, then reuse the exact request on
uncertain network outcomes. The UI retains pending requests per account, branch
and sale. Matching retries retrieve the cancelled invoice; changed details or a
different key on an already cancelled invoice conflict.

Cancellation uses the same sale row lock as receipts, returns and refunds, plus
stable ordered stock locks. Cancellation, stock movements and audit commit in one
transaction. All queries use branch isolation; a foreign-branch ID returns 404.

## Reports

Original invoiced activity remains on its original date. Full cancellation credit
uses the cancellation date, including GST and medicine quantities; cost recovery
applies only when stock was restored. Existing `ReturnCredits` API fields now
include return and cancellation credits, as indicated by UI/export labels. Sales
counts and rankings remain original invoiced activity, independent of later credits.
Collections/refunds retain their own cash-event dates. Dashboard refund liabilities
include cancelled invoices awaiting payout. These reports are internal summaries,
not statutory credit-note filing.
