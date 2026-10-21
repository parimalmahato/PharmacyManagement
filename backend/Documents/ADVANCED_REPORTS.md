Advanced reports and Excel/PDF export
====================================

Open Dashboard → Advanced Reports and Downloads. Choose a report and an inclusive
date range (maximum 366 days), generate a preview, then download Excel or PDF.
The preview shows at most 200 rows per table; downloads contain all rows up to
the 50,000-row export limit. Shorten the range for larger reports. Stock valuation
is a current snapshot; its date range is not a historical inventory filter.

Endpoints (existing ViewReports policy: Admin or Pharmacist):

* GET /api/reports/advanced/{kind}?startDate=2026-10-01&endDate=2026-10-21
* GET /api/reports/advanced/{kind}/export/{format}?startDate=2026-10-01&endDate=2026-10-21

Kinds: sales, gst, medicines, profit, stock. Formats: xlsx, pdf. Unsupported
types/formats and invalid date ranges return 400. Downloads use authenticated
fetch requests and attachment filenames; no public file URLs are created.

Application defines report DTOs, service validation, repository and export
interfaces. Infrastructure implements EF reads, Open XML Excel packaging and
PDF generation with PDFsharp 6.2.0. API controllers authorize requests and return
JSON or file results. React provides the filters, previews and download controls.
The module does not mutate invoices, credits, refunds, stock or inspections.
No new database migration is required.

Report definitions
------------------

All dates use IST and amounts use INR. Financial reports use each sale/return's
own transaction date; a return of an older invoice is included on its return date.
Refunds are cash movements and are not deducted from sales a second time.

* Sales: invoiced totals, return credits, collections/refunds, current outstanding
  for the selected invoice cohort, original invoice breakdown and daily activity.
* GST: HSN/rate grouping from original invoice tax snapshots. Taxable sales and
  GST, return adjustments, net taxable value and net GST are shown separately.
  Return GST is allocated from the original stored GST proportion using cumulative
  credit rounding so full returns reverse the full original GST. Zero-value lines
  do not divide by zero. Unclassified HSN is shown explicitly. The data model does
  not capture place of supply or purchase input tax, so the report does not invent
  CGST/SGST/IGST splits, input tax credits or statutory filing documents.
* Medicines: all medicines with sales or returns in the period, including sales,
  returned and net quantities, invoice values, credits and net sales. Names come
  from original sale snapshots. This is not limited to the dashboard's top ten.
* Profit: gross profit = taxable sales - taxable return credits - original batch
  purchase cost of sold units + purchase cost of restocked units. Non-restocked
  damaged/expired returns do not recover cost. Purchase prices are the stored
  original batch prices, not current medicine selling prices. Operating expenses
  and purchase input-tax data are unavailable; the result is not net profit.
* Stock: all current batches, with medicine, batch, expiry, quantity, unit cost,
  retail price and valuations. Summary distinguishes all inventory at cost from
  saleable inventory at cost/retail. Inactive medicines and batches expiring today
  or earlier are not counted as saleable. This is not inventory as of a past date.

Exports and deployment
----------------------

Excel exports are genuine .xlsx Open XML ZIP workbooks, with a worksheet per
table, frozen headings, column filters, numeric cells and literal string cells.
User text is never emitted as spreadsheet formulas. PDF exports are landscape
documents with wrapped cells, repeating headers, notes and numbered pages.
Each download regenerates the report using current data and the preview's filters.

PDFsharp requires a TrueType font. Configure PHARMACY_REPORT_FONT with a readable
.ttf path or deploy Fonts/Report.ttf under the application output directory.
The resolver otherwise checks Windows Arial and Linux DejaVu Sans/Liberation Sans.
Use a font covering the characters in your medicine names. The font is embedded
by PDFsharp; ensure your deployment's font redistribution/embedding permissions.
Resolver details: https://docs.pdfsharp.net/PDFsharp/Topics/Fonts/Font-Resolving.html

Restore packages and restart the backend after deploying. Frontend production
build/lint and backend tests include workbook validation and PDF parsing/pagination.
Full database integration tests require PHARMACY_TEST_SQLSERVER configured against
a disposable SQL Server with create/drop database permissions.
