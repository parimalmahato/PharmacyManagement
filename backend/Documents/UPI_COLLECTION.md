# Direct UPI QR collection

This feature prepares an unsigned UPI payment request. It is not a payment gateway
and does not verify settlement or initiate refunds. QR generation uses a local
open-source renderer; no external QR image service receives the payment details.
The application adds no gateway service or gateway processing fee. The acquiring
bank's merchant onboarding, supported flows and charges remain its own terms.

## Configure each branch

Apply `AddBranchUpiCollection`, then restart the API. Existing branches start with
UPI disabled. As Admin, select the branch and open **Pharmacy Settings → UPI QR
Collection**. Enter the branch's bank-issued merchant UPI ID and business/payee
name. Enter a merchant category code only if supplied by that bank. Save and verify
the recipient in the intended UPI apps before customer use. Format validation
cannot establish whether the UPI ID exists or belongs to your pharmacy.

## Collect against an invoice

1. Open **Sales → Payments**, or **View invoice**.
2. Under **Collect with UPI**, choose the outstanding balance or a partial amount
   and select **Show UPI QR**. The server checks the current branch, invoice status
   and balance, and supplies the payee details. Cancelled/settled invoices are blocked.
3. The customer scans the code with a supported app, confirms the recipient and
   amount, and authorizes payment in their own app. An optional UPI link is available
   for a device with a compatible UPI handler. The pharmacy application never asks
   for a UPI PIN or bank credentials.
4. Admin or Cashier independently checks the pharmacy's bank or merchant app for
   the actual credited amount and UTR/transaction ID. Select UPI in the receipt form,
   enter the received amount and actual bank reference, confirm verification and
   record the receipt. The existing ledger handles split payments and balances.

A QR request reference is not a UTR. A customer screenshot, successful scan, app
redirect or generic success response does not establish receipt. QR generation and
opening a payment app never mark an invoice paid. Refresh balances if another
operator collects money. Replacing or hiding a displayed QR cannot revoke a copied
UPI link; independently reconcile unexpected/duplicate bank receipts.

## API

`GET /api/sales/{id}/upi-request?amount=12.50` requires ViewSales authorization.
Omitting amount requests the current outstanding balance. Amounts must be positive,
have at most two decimal places and not exceed the current balance. Branch filters
apply to the sale and settings. This endpoint creates no receipt, stock movement,
refund or bank transaction, and its response is not payment evidence.

Payee, INR amount, invoice note and request reference are URI-encoded. QR fields
follow the generic UPI intent convention documented in
[Google's UPI integration reference](https://developers.google.com/pay/india/api/android/in-app-payments).
The frontend renderer is [qrcode.react](https://github.com/zpao/qrcode.react), licensed
under ISC with its bundled MIT-licensed QR generator.

## Acceptance limits

No real payment was executed by automated tests. QR/intent acceptance depends on
the bank, merchant enrollment and payer app; some require bank-approved or signed
merchant QR flows. This generic unsigned request is not certified interoperable
with every app. Test your actual merchant account and devices before deployment;
use the bank-issued merchant QR or an approved provider integration if rejected.
Do not fake callback success or weaken checks to work around rejection.

Automatic bank confirmation, reconciliation APIs, gateway webhooks, card acquiring
and online refunds require a separate bank/provider integration. Manual refunds
continue through the existing refund ledger after the payout is actually made.
