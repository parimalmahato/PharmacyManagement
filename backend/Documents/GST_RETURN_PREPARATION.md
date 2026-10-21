# GST return preparation workspace

This module produces **branch-level accountant review worksheets**. It does not
file GSTR-1/GSTR-3B, claim ITC, calculate a payable amount or generate GST Portal
upload JSON. Do not upload these Excel files as the official offline utility.

## Setup and workflow

Apply `AddGstPreparationWorkspace` and restart the API. Sign in as Admin and open
**GST Returns** (`/gst-returns`). All endpoints require Admin and enforce the active
branch. Set a branch GSTIN and enable GST in Pharmacy Settings before review.

1. Select a month and load preparation. Dates follow India Standard Time.
2. Review the complete source register: issued sales invoices, operational return
   credits, cancellations and received supplier invoices. Refund payments are cash
   movements and are not a second tax adjustment.
3. Add tax details transcribed from the actual invoice or issued credit note.
   Declare registration GSTIN, recipient/supplier GSTIN, place of supply, supply
   classification and actual CGST/SGST-or-UTGST/IGST/cess heads. Annotations are
   versioned and audited separately; no invoice, stock, payment or refund changes.
4. Sales amounts must reconcile exactly to original tax snapshots. Purchase prices
   are stock costs: supplier tax and gross amounts must be entered from the actual
   supplier document; they are never inferred from a medicine's selling GST rate.
5. Import normalized GSTR-2B invoice data, compare each purchase, and explicitly
   select eligible ITC only after statutory review. Exact supplier GSTIN, invoice
   number/date, supply value and each tax head must match an available statement
   entry. Duplicate book keys, mismatches or unavailable credit yield no matched ITC.
6. Download draft Excel/PDF working registers. Resolve all blocking issues, confirm
   accountant review, then save a reviewed snapshot. Review locks annotation edits
   and statement replacement. Reopen with an audited reason to make corrections.

The review is **not an operational posting lock**. Later backdated documents or
changed source/profile data invalidate the current review fingerprint and block
reviewed exports until the worksheet is reopened and reviewed again. Old snapshots
remain retained in GstReturnReviews with reviewer, UTC timestamp and fingerprint.
Review/reopen/import/edit operations are transactional and audited. Optimistic
versions and serializable workspace locks prevent lost updates and duplicate review.

## Normalized reconciliation CSV

The UI downloads the exact header template:

```csv
SupplierGstin,InvoiceNumber,InvoiceDate,TaxableValue,Cgst,Sgst,Igst,Cess,ItcAvailable
```

Use UTF-8 CSV, one invoice per row, `yyyy-MM-dd` dates, plain INR decimals (no
grouping separators), and `true`/`false` for ITC availability. Standard quoted CSV
fields are supported. Maximum 5,000 rows and 1 MB. Duplicate keys or invalid rows
reject the entire import; the existing statement remains intact. A successful
import replaces only the current draft's prior normalized statement.

This is not a parser for the GST Portal's official JSON/XLSX or a live GST API.
Convert and reconcile the downloaded statement with an accountant. Imported credit
notes/debit notes, amendments, reversals and carry-forward claims are not supported.
Older invoice dates can appear as unmatched statement rows but are excluded from
current-period matched ITC. Matching does not establish all statutory eligibility
conditions, receipt, payment, time limits, reversals or blocked-credit exceptions.

## Outputs and scope

The workbook/PDF includes status, scope, source fingerprint, review metadata,
registered/unregistered recipient working registers, credit-note register linked
to original invoices, supplier tax facts, outward category/head totals, normalized
2B reconciliation, original HSN snapshots, validation issues and completeness list.
Invalid or missing annotations are excluded from annotated totals and remain visible
in the completeness list. Original HSN summaries include all source activity and can
differ from incomplete annotated totals. Negative credits are retained, not hidden.

Every worksheet is **active-branch only**. Multiple branches sharing a GSTIN must be
consolidated before filing. Reviewed status means internal review, not statutory
approval or a declaration that the return is complete. B2CS/B2CL/table mappings,
turnover-specific HSN/UQC requirements, credit-note legal eligibility, registration
history and complete tax-head utilization need accountant handling.

Only ordinary domestic supplies are supported. Exports/SEZ, reverse charge, special
schemes, e-commerce classifications, amendments, tax adjustments unsupported by
original snapshots, and mixed zero-tax/taxed invoice lines need a separate workflow.
Credit notes require an actual note number and the recorded return/cancellation
event date; later issuance dates need accountant handling. This module does not
issue statutory notes or treat every operational return as legally deductible.

Legacy invoice numbers longer than 16 characters and missing HSN snapshots are
reported as blockers. Do not rename issued invoices or invent HSN/note numbers to
clear validation. Resolve historical compliance outside this worksheet. GSTIN checks
are format checks, not portal registration or checksum verification.

## API

All routes start `/api/gst-preparation/{year}/{month}`:

* GET: current preparation and validation.
* PUT `/documents/{kind}/{id}`: tax annotation plus its last revision.
* POST `/2b/import`: normalized CSV text plus worksheet revision.
* POST `/review`: current fingerprint and worksheet revision.
* POST `/reopen`: reason and worksheet revision.
* GET `/export?format=xlsx|pdf`: review workbook/PDF; no statutory JSON export.

Source kinds are SalesInvoice, SalesReturn, SaleCancellation and PurchaseInvoice.
At most 2,000 source documents per branch/month are supported. Missing or foreign
branch source IDs return 404. No GST portal credentials are collected or stored.

## Official references

Implementation scope was checked against the official
[GSTR-1 guide](https://tutorial.gst.gov.in/userguide/returns/GSTR_1.htm),
[GSTR-3B guide](https://tutorial.gst.gov.in/userguide/returns/GSTR3B.htm) and
[GSTR-2B FAQs](https://tutorial.gst.gov.in/userguide/returns/FAQ_gstr2b.htm).
Use the current GST Portal utility and accountant-approved classifications for filing.
