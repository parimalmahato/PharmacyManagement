import { useEffect, useRef, useState } from "react";
import { tokenStore } from "../auth/tokenStore";
import { downloadBlob, gstPreparationApi, type GstDocument, type GstPreparation, type GstTaxDetails } from "../api/gstPreparation";
import "./GstReturnsPage.css";

const money = (value: number) => value.toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const monthNow = () => {
  const parts = new Intl.DateTimeFormat("en", { timeZone: "Asia/Kolkata", year: "numeric", month: "2-digit" }).formatToParts(new Date());
  return `${parts.find(x => x.type === "year")!.value}-${parts.find(x => x.type === "month")!.value}`;
};
const template = "SupplierGstin,InvoiceNumber,InvoiceDate,TaxableValue,Cgst,Sgst,Igst,Cess,ItcAvailable\r\n";
export default function GstReturnsPage() {
  const [period, setPeriod] = useState(monthNow);
  const [data, setData] = useState<GstPreparation | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [selected, setSelected] = useState<GstDocument | null>(null);
  const [reason, setReason] = useState("");
  const [reviewConfirmed, setReviewConfirmed] = useState(false);
  const active = useRef(true);
  useEffect(() => { active.current = true; return () => { active.current = false; }; }, []);
  async function run(action: () => Promise<unknown>, message = "", reload = true) {
    if (busy || !active.current) return; setBusy(true); setError(""); setNotice("");
    try { await action(); if (!active.current) return; if (reload) { const next = await gstPreparationApi.get(period); if (!active.current) return; setData(next); setSelected(null); setReviewConfirmed(false); } setNotice(message); }
    catch (err) { if (active.current) setError(err instanceof Error ? err.message : "Unable to complete GST operation."); }
    finally { if (active.current) setBusy(false); }
  }
  const blocking = data?.issues.filter(x => x.severity === "Error") ?? [];
  const locked = data?.status === "Reviewed";
  return <main className="gst-page">
    <header className="gst-page-heading"><span>Tax workspace</span><h1>GST return preparation</h1><p>Prepare monthly registers, reconcile purchase tax and save an accountant-reviewed worksheet.</p></header>
    <aside className="gst-scope-note"><strong>Active branch worksheet · not a filed GST return</strong><p>Consolidate all branches of the GSTIN before filing. This workspace supports ordinary domestic supplies; it does not submit returns, generate GST Portal upload JSON, calculate tax payable or infer input tax credit.</p></aside>
    <section className="gst-panel gst-toolbar"><label>Return month<input type="month" value={period} disabled={busy} onChange={event => { setPeriod(event.target.value); setData(null); setSelected(null); setError(""); setNotice(""); setReviewConfirmed(false); }} /></label>
      <button type="button" disabled={busy || !period} onClick={() => void run(async () => { setData(await gstPreparationApi.get(period)); setSelected(null); }, "Preparation loaded.", false)}>{busy ? "Processing…" : "Load preparation"}</button></section>
    {error && <p className="gst-error" role="alert">{error}</p>}{notice && <p className="gst-success" role="status">{notice}</p>}
    {data && <>
      <section className="gst-panel"><div className="gst-section-heading"><div><h2>{data.gstin ?? "GST registration not configured"}</h2><p>{period} · {data.documents.length} source documents · {blocking.length} blocking issues</p></div><span className={`gst-status ${locked ? "gst-reviewed" : ""}`}>{data.status}{data.reviewId ? ` · Review #${data.reviewId}` : ""}</span></div>
        {data.changedSinceReview && locked && <p className="gst-error">Source data changed after review. Reopen, regenerate and review before exporting.</p>}
        <div className="gst-metrics">{[["Net outward supply value", data.netOutwardValue], ["Net CGST", data.netCgst], ["Net SGST / UTGST", data.netSgst], ["Net IGST", data.netIgst], ["Matched eligible ITC", data.matchedEligibleItc]].map(([label, value]) => <div key={label}><span>{label}</span><strong>₹{money(Number(value))}</strong></div>)}</div>
        <p className="gst-help">Totals exclude incomplete or invalid documents. They are not a GSTR-3B tax-payable calculation. ITC requires an explicit eligibility decision and exact tax-head matching; portal availability alone is insufficient.</p>
        <div className="gst-actions"><button disabled={busy || (locked && data.changedSinceReview)} onClick={() => void run(() => gstPreparationApi.download(period, "xlsx"), "", false)}>Download review Excel</button><button disabled={busy || (locked && data.changedSinceReview)} onClick={() => void run(() => gstPreparationApi.download(period, "pdf"), "", false)}>Download review PDF</button></div>
      </section>
      <section className="gst-panel"><h2>Validation and completeness</h2><ul className="gst-issues">{data.issues.map((issue, index) => <li key={index} className={issue.severity === "Error" ? "gst-issue-error" : ""}><strong>{issue.severity}</strong> {issue.sourceKind && `${issue.sourceKind} #${issue.sourceId}: `}{issue.message}</li>)}</ul></section>
      <section className="gst-panel"><h2>Source document register</h2><p className="gst-help">Tax details are separate, audited annotations. Original invoices, stock, receipts and refunds remain intact. Credit notes require the actual issued note number and event date.</p>
        <div className="gst-table-scroll"><table><thead><tr><th>Source</th><th>Date / document</th><th>Counterparty</th><th>Book amount</th><th>Recorded GST</th><th>Tax details</th></tr></thead><tbody>{data.documents.map(doc => <tr key={`${doc.kind}:${doc.id}`}><td>{doc.kind}<small>#{doc.id}</small></td><td>{doc.number}<small>{doc.date}</small></td><td>{doc.counterparty}</td><td className="gst-number">{money(doc.bookAmount)}</td><td className="gst-number">{doc.recordedGst === null ? "Not captured" : money(doc.recordedGst)}</td><td><button disabled={busy || locked} onClick={() => setSelected(doc)}>{doc.taxDetails ? "Edit tax details" : "Add tax details"}</button></td></tr>)}</tbody></table></div>
        {!data.documents.length && <p>No source documents in this branch and month.</p>}
      </section>
      {selected && !locked && <TaxDetailsForm key={`${selected.kind}:${selected.id}:${selected.revision}`} doc={selected} gstin={data.gstin ?? ""} busy={busy} onCancel={() => setSelected(null)} onSave={details => void run(() => gstPreparationApi.save(period, selected, details), "Tax details saved; preparation regenerated.")} />}
      <section className="gst-panel"><h2>GSTR-2B reconciliation import</h2><p className="gst-help">Import a normalized CSV prepared from your downloaded statement. This is not the GST Portal JSON or Excel format. Current-period book invoices match by supplier GSTIN, invoice number and date; earlier-period or unmatched rows remain visible and are excluded from ITC.</p>
        <p className="gst-help">Columns: SupplierGstin, InvoiceNumber, InvoiceDate (yyyy-MM-dd), TaxableValue, Cgst, Sgst, Igst, Cess, ItcAvailable (true/false). INR decimals only, up to 5,000 rows / 1 MB. A successful import replaces this draft’s previous statement.</p>
        <div className="gst-actions"><button disabled={busy} onClick={() => downloadBlob(new Blob([template], { type: "text/csv;charset=utf-8" }), "gst-2b-normalized-template.csv")}>Download CSV headers</button>
          <label className="gst-file-label">Replace draft statement<input type="file" accept=".csv,text/csv" disabled={busy || locked} onChange={event => {
            const file = event.target.files?.[0]; event.target.value = ""; if (!file) return;
            if (file.size > 1_000_000) { setError("Choose a CSV file smaller than 1 MB."); return; }
            const session = tokenStore.get();
            void run(async () => {
              const csv = await file.text();
              if (!active.current || tokenStore.get() !== session) throw new Error("Session or branch changed while reading the file. Reload the intended branch before importing.");
              await gstPreparationApi.import(period, csv, data.revision);
            }, "Statement imported and reconciled.");
          }} /></label></div>
        <div className="gst-table-scroll"><table><thead><tr><th>Supplier GSTIN</th><th>Invoice / date</th><th>Result</th><th>Book tax</th><th>2B tax</th><th>Matched eligible ITC</th></tr></thead><tbody>{data.reconciliation.map((row, index) => <tr key={index}><td>{row.supplierGstin}</td><td>{row.invoiceNumber}<small>{row.invoiceDate}</small></td><td>{row.status}</td><td className="gst-number">{money(row.bookTax)}</td><td className="gst-number">{money(row.statementTax)}</td><td className="gst-number">{money(row.eligibleMatchedItc)}</td></tr>)}</tbody></table></div>
      </section>
      <section className="gst-panel"><h2>Review and lock worksheet</h2><p className="gst-help">Review saves an immutable snapshot and locks tax edits and statement replacement. It does not close operational posting or submit a GST return. Later backdated activity is detected and requires reopening and a new review.</p>
        {data.reviewedBy && <p className="gst-help">Last review #{data.reviewId} by {data.reviewedBy} · {data.reviewedAtUtc && new Date(data.reviewedAtUtc.endsWith("Z") ? data.reviewedAtUtc : `${data.reviewedAtUtc}Z`).toLocaleString("en-IN", { timeZone: "Asia/Kolkata" })}</p>}
        {!locked ? <><label className="gst-check"><input type="checkbox" disabled={busy} checked={reviewConfirmed} onChange={event => setReviewConfirmed(event.target.checked)} />I reviewed the source documents, tax classifications, credit notes and ITC reconciliation. I understand this branch worksheet needs registration-level consolidation and statutory review before filing.</label>
          <button disabled={busy || blocking.length > 0 || !reviewConfirmed} onClick={() => void run(() => gstPreparationApi.review(period, data), "Reviewed snapshot saved; worksheet locked.")}>Save reviewed snapshot</button></> : <div className="gst-toolbar"><label>Reason to reopen<input maxLength={500} value={reason} disabled={busy} onChange={event => setReason(event.target.value)} /></label><button disabled={busy || !reason.trim()} onClick={() => void run(() => gstPreparationApi.reopen(period, reason.trim(), data.revision!), "Worksheet reopened. Previous snapshot retained.")}>Reopen worksheet</button></div>}
      </section>
    </>}
  </main>;
}

function TaxDetailsForm({ doc, gstin, busy, onSave, onCancel }: { doc: GstDocument; gstin: string; busy: boolean; onSave: (details: GstTaxDetails) => void; onCancel: () => void }) {
  const purchase = doc.kind === "PurchaseInvoice"; const credit = doc.kind === "SalesReturn" || doc.kind === "SaleCancellation";
  const [values, setValues] = useState<GstTaxDetails>(doc.taxDetails ?? {
    registrationGstin: gstin, counterpartyGstin: "", placeOfSupply: gstin.slice(0, 2), supplyCategory: "Taxable", documentNumber: credit ? "" : doc.number, documentDate: doc.date,
    grossAmount: purchase ? 0 : doc.bookAmount, taxableValue: doc.recordedTaxable ?? 0, cgst: 0, sgst: 0, igst: 0, cess: 0, itcEligible: false,
  });
  const [confirmed, setConfirmed] = useState(false);
  function set<K extends keyof GstTaxDetails>(key: K, value: GstTaxDetails[K]) { setValues(previous => ({ ...previous, [key]: value })); setConfirmed(false); }
  return <section className="gst-panel gst-tax-form"><h2>Tax details: {doc.kind} #{doc.id}</h2><p>{doc.number} · {doc.date}</p><p className="gst-help">Transcribe actual document facts. Purchase prices are stock costs, so supplier tax and gross amounts must be entered independently. Sales tax totals must match the immutable invoice snapshots.</p>
    {doc.relatedInvoice && <p className="gst-help">Related original invoice: {doc.relatedInvoice}</p>}
    <form onSubmit={event => { event.preventDefault(); onSave({ ...values, registrationGstin: values.registrationGstin.trim().toUpperCase(), counterpartyGstin: values.counterpartyGstin?.trim().toUpperCase() || null }); }}>
      <fieldset disabled={busy}><div className="gst-fields">
        <label>Pharmacy registration GSTIN<input autoFocus required maxLength={15} value={values.registrationGstin} onChange={event => set("registrationGstin", event.target.value.toUpperCase())} /></label>
        <label>{purchase ? "Supplier GSTIN" : "Recipient GSTIN (blank for unregistered)"}<input maxLength={15} value={values.counterpartyGstin ?? ""} onChange={event => set("counterpartyGstin", event.target.value.toUpperCase())} /></label>
        <label>Place of supply (state code)<input required pattern="[0-9]{2}" maxLength={2} placeholder="27" value={values.placeOfSupply} onChange={event => set("placeOfSupply", event.target.value)} /></label>
        <label>Supply category<select value={values.supplyCategory} onChange={event => set("supplyCategory", event.target.value)}><option value="Taxable">Taxable domestic supply</option><option value="NilRated">Nil-rated</option><option value="Exempt">Exempt</option><option value="NonGst">Non-GST</option></select></label>
        <label>{credit ? "Actual issued credit note number" : "Original invoice number"}<input required readOnly={!credit} maxLength={credit ? 16 : undefined} value={values.documentNumber} onChange={event => set("documentNumber", event.target.value)} /></label>
        <label>Document date<input type="date" required readOnly value={values.documentDate} /></label>
        {(["grossAmount", "taxableValue", "cgst", "sgst", "igst", "cess"] as const).map(key => <label key={key}>{({ grossAmount: "Document gross (INR)", taxableValue: "Supply value before tax", cgst: "CGST", sgst: "SGST / UTGST", igst: "IGST", cess: "Cess" })[key]}<input type="number" min="0" step="0.01" required readOnly={!purchase && (key === "grossAmount" || key === "taxableValue")} value={values[key]} onChange={event => set(key, Number(event.target.value))} /></label>)}
      </div>
      {purchase && <label className="gst-check"><input type="checkbox" checked={values.itcEligible} onChange={event => set("itcEligible", event.target.checked)} />Accountant confirms this invoice is eligible for ITC, subject to exact 2B matching and statutory conditions.</label>}
      <label className="gst-check"><input type="checkbox" required checked={confirmed} onChange={event => setConfirmed(event.target.checked)} />I transcribed these facts from the actual invoice or issued credit note. This annotation does not issue a credit note or amend the original invoice.</label>
      </fieldset><div className="gst-actions"><button type="submit" disabled={busy || !confirmed}>Save tax details</button><button type="button" disabled={busy} onClick={onCancel}>Close</button></div>
    </form>
  </section>;
}
