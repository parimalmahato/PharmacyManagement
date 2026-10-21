import { useEffect, useRef, useState } from "react";
import { aiRequest, type InvoiceExtraction, type InvoiceLine } from "../api/ai";
import type { MedicineOption, PurchaseItem } from "../api/purchases";

export default function InvoiceAiImport({ medicines, onUse }: {
  medicines: MedicineOption[];
  onUse: (number: string, date: string, items: PurchaseItem[]) => void;
}) {
  const [text, setText] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<InvoiceExtraction | null>(null);
  const [number, setNumber] = useState("");
  const [date, setDate] = useState("");
  const [lines, setLines] = useState<{ extracted: InvoiceLine; suggestedMedicineId: number | null }[]>([]);
  const [reviewed, setReviewed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const pending = useRef<AbortController | null>(null);
  useEffect(() => () => pending.current?.abort(), []);
  async function extract() {
    setError(""); setResult(null); setReviewed(false); setBusy(true);
    const controller = new AbortController(); pending.current = controller;
    try {
      let imageBase64: string | undefined;
      if (file) {
        if (file.size > 4_000_000 || !["image/png", "image/jpeg"].includes(file.type)) throw new Error("Select a PNG/JPEG image up to 4 MB. For PDF, copy text or use a page image.");
        imageBase64 = await new Promise<string>((resolve, reject) => {
          const reader = new FileReader(); reader.onload = () => resolve(String(reader.result).split(",")[1]); reader.onerror = () => reject(new Error("Cannot read image.")); reader.readAsDataURL(file);
        });
      }
      if (controller.signal.aborted) return;
      const extracted = await aiRequest<InvoiceExtraction>("extract-invoice", { text, imageBase64 }, controller.signal);
      setResult(extracted); setNumber(extracted.invoice.invoiceNumber ?? ""); setDate(extracted.invoice.invoiceDate ?? ""); setLines(extracted.lines);
    } catch (err) { if (!controller.signal.aborted) setError(err instanceof Error ? err.message : "Extraction failed."); }
    finally { if (!controller.signal.aborted) setBusy(false); }
  }
  function edit(index: number, field: keyof InvoiceLine, value: string) {
    setReviewed(false);
    setLines(current => current.map((row, i) => i !== index ? row : { ...row, extracted: { ...row.extracted, [field]: ["quantity", "purchasePrice", "sellingPrice"].includes(field) ? value === "" ? null : Number(value) : value } }));
  }
  const [today] = useState(() => new Date(Date.now() + 330 * 60000).toISOString().slice(0, 10));
  const [tomorrow] = useState(() => new Date(Date.now() + (330 + 1440) * 60000).toISOString().slice(0, 10));
  return <section className="purchase-card">
    <h2>AI supplier invoice extraction</h2>
    <p>Paste invoice text or upload a PNG/JPEG page image. The document is sent to the configured model service. Remove unnecessary personal information. Extraction does not save a purchase or receive stock.</p>
    <label>Invoice text<textarea rows={4} maxLength={20000} value={text} disabled={busy} onChange={e => setText(e.target.value)} style={{ width: "100%", boxSizing: "border-box" }} /></label>
    <label>Invoice page image (optional)<input type="file" accept="image/png,image/jpeg" disabled={busy} onChange={e => setFile(e.target.files?.[0] ?? null)} /></label>
    <button type="button" disabled={busy || (!text.trim() && !file)} onClick={() => void extract()}>{busy ? "Extracting…" : "Extract for review"}</button>
    {error && <p role="alert" className="notice error-notice">{error}</p>}
    {result && <form onSubmit={e => {
      e.preventDefault();
      if (!reviewed || lines.some(l => !l.suggestedMedicineId || !l.extracted.batchNumber?.trim() || !l.extracted.expiryDate || l.extracted.expiryDate <= today || l.extracted.quantity === null || l.extracted.purchasePrice === null || l.extracted.sellingPrice === null)) return;
      if (!window.confirm("Replace the unsaved purchase form with these reviewed values? Select the supplier and check the draft before saving.")) return;
      onUse(number, date, lines.map(row => ({ medicineId: row.suggestedMedicineId!, batchNumber: row.extracted.batchNumber!, expiryDate: row.extracted.expiryDate!, quantity: row.extracted.quantity!, purchasePrice: row.extracted.purchasePrice!, sellingPrice: row.extracted.sellingPrice! })));
      setResult(null);
    }}>
      <h3>Review extracted values</h3><p>Supplier on document: {result.invoice.supplierName ?? "Unknown"}. Select the actual supplier in the purchase form below.</p>
      <ul>{result.warnings.map((warning, i) => <li key={i}>{warning}</li>)}</ul>
      <div className="purchase-form-grid"><label>Invoice number<input required maxLength={100} value={number} onChange={e => { setNumber(e.target.value); setReviewed(false); }} /></label><label>Invoice date<input required type="date" max={today} value={date} onChange={e => { setDate(e.target.value); setReviewed(false); }} /></label></div>
      {lines.map((row, index) => <fieldset className="purchase-item" key={index}><legend>Line {index + 1}: {row.extracted.medicineName ?? "Unrecognized medicine"}</legend><div className="purchase-item-grid">
        <label>Exact catalog medicine<select required value={row.suggestedMedicineId ?? ""} onChange={e => { setReviewed(false); setLines(current => current.map((r, i) => i === index ? { ...r, suggestedMedicineId: Number(e.target.value) || null } : r)); }}><option value="">Select medicine</option>{medicines.map(m => <option key={m.id} value={m.id}>{m.name} — {m.manufacturer} — {m.genericName}</option>)}</select></label>
        <label>Batch<input required maxLength={50} value={row.extracted.batchNumber ?? ""} onChange={e => edit(index, "batchNumber", e.target.value)} /></label>
        <label>Expiry<input required type="date" min={tomorrow} value={row.extracted.expiryDate ?? ""} onChange={e => edit(index, "expiryDate", e.target.value)} /></label>
        <label>Quantity (inventory units)<input required type="number" min={1} max={1000000} step={1} value={row.extracted.quantity ?? ""} onChange={e => edit(index, "quantity", e.target.value)} /></label>
        <label>Purchase price per unit<input required type="number" min={0} max={100000000} step="0.01" value={row.extracted.purchasePrice ?? ""} onChange={e => edit(index, "purchasePrice", e.target.value)} /></label>
        <label>Selling price per unit<input required type="number" min={row.extracted.purchasePrice ?? 0} max={100000000} step="0.01" value={row.extracted.sellingPrice ?? ""} onChange={e => edit(index, "sellingPrice", e.target.value)} /></label>
      </div></fieldset>)}
      <label style={{ display: "flex", alignItems: "flex-start", gap: 10, margin: "18px 0" }}><input type="checkbox" checked={reviewed} onChange={e => setReviewed(e.target.checked)} required style={{ width: "auto", flex: "0 0 auto", marginTop: 4 }} />I checked every medicine identity, batch, expiry, quantity unit and price against the original document.</label>
      <button type="submit" disabled={!reviewed}>Use reviewed values in purchase form</button>
    </form>}
  </section>;
}
