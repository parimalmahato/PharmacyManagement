import { useState } from "react";
import { advancedReportsApi, type AdvancedReport, type ReportKind } from "../api/advancedReports";

const today = () => new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Kolkata", year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date());
export default function AdvancedReports() {
  const [kind, setKind] = useState<ReportKind>("sales");
  const [start, setStart] = useState(today);
  const [end, setEnd] = useState(today);
  const [report, setReport] = useState<AdvancedReport | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function load() {
    const from = kind === "stock" ? today() : start;
    const to = kind === "stock" ? from : end;
    if (!from || !to || from > to || (Date.parse(to) - Date.parse(from)) / 86400000 > 365) {
      setError("Choose a valid date range of at most 366 days."); return;
    }
    setBusy(true); setError(""); setReport(null);
    try { setReport(await advancedReportsApi.get(kind, from, to)); }
    catch (err) { setError(err instanceof Error ? err.message : "Unable to load report."); }
    finally { setBusy(false); }
  }
  async function download(format: "xlsx" | "pdf") {
    if (!report) return;
    setBusy(true); setError("");
    try { await advancedReportsApi.download(report, format); }
    catch (err) { setError(err instanceof Error ? err.message : "Download failed."); }
    finally { setBusy(false); }
  }
  return <section className="dashboard-section" aria-busy={busy}>
    <h2>Advanced Reports and Downloads</h2>
    <div className="dashboard-filters">
      <label>Report<select value={kind} disabled={busy} onChange={event => { setKind(event.target.value as ReportKind); setReport(null); }}>
        <option value="sales">Date-range sales</option><option value="gst">GST by HSN and rate</option><option value="medicines">Medicine-wise sales</option>
        <option value="profit">Gross profit</option><option value="stock">Current stock valuation</option>
      </select></label>
      {kind !== "stock" && <><label>From<input type="date" required value={start} disabled={busy} onChange={event => { setStart(event.target.value); setReport(null); }} /></label>
        <label>To<input type="date" required value={end} disabled={busy} onChange={event => { setEnd(event.target.value); setReport(null); }} /></label></>}
      <button type="button" disabled={busy} onClick={() => void load()}>{busy ? "Processing..." : "Generate report"}</button>
    </div>
    {kind === "stock" && <p>Current batch quantities and prices, not historical inventory.</p>}
    {error && <p role="alert" className="dashboard-error">{error}</p>}
    {report && <>
      <p><strong>{report.kind === "stock" ? "Current snapshot" : `${report.startDate} to ${report.endDate}`} · IST · INR</strong></p>
      <p>{report.notes}</p>
      <div className="dashboard-export-actions"><button type="button" disabled={busy} onClick={() => void download("xlsx")}>Download Excel (.xlsx)</button>{" "}
        <button type="button" disabled={busy} onClick={() => void download("pdf")}>Download PDF</button></div>
      {report.tables.map((table, index) => <div key={index}><h3>{table.title}</h3>
        {table.rows.length > 200 && <p>Showing the first 200 of {table.rows.length} rows. Downloads include all rows.</p>}
        <div className="dashboard-table-wrapper"><table className="dashboard-table"><thead><tr>{table.columns.map((column, c) => <th key={c}>{column}</th>)}</tr></thead>
          <tbody>{table.rows.length ? table.rows.slice(0, 200).map((row, r) => <tr key={r}>{row.map((value, c) => <td key={c}>{typeof value === "number" ? value.toLocaleString("en-IN", { maximumFractionDigits: 2 }) : value ?? "—"}</td>)}</tr>) :
            <tr><td colSpan={table.columns.length}>No records found.</td></tr>}</tbody></table></div>
      </div>)}
    </>}
  </section>;
}
