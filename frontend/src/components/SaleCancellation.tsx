import { useRef, useState } from "react";
import { salesApi, SalesApiError, type Sale, type CancelSaleRequest } from "../api/sales";
import { useAuth } from "../auth/useAuth";
import "./SaleCancellation.css";

export default function SaleCancellation({ sale, onCancelled }: { sale: Sale; onCancelled: (sale: Sale) => void }) {
  const { hasRole, user } = useAuth();
  const storageKey = `pharmacy-cancellation:${user?.email}:${user?.branchId}:${sale.id}`;
  const [pending, setPending] = useState<CancelSaleRequest | null>(() => {
    try { return JSON.parse(sessionStorage.getItem(storageKey) ?? "null") as CancelSaleRequest | null; } catch { return null; }
  });
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [custody, setCustody] = useState(false);
  const [restock, setRestock] = useState(false);
  const [inspection, setInspection] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const lock = useRef(false);
  async function cancel(request: CancelSaleRequest) {
    if (lock.current) return;
    lock.current = true; setBusy(true); setError("");
    try {
      sessionStorage.setItem(storageKey, JSON.stringify(request)); setPending(request);
      const result = await salesApi.cancel(sale.id, request);
      sessionStorage.removeItem(storageKey); setPending(null);
      onCancelled(result); setOpen(false);
    } catch (err) {
      if (err instanceof SalesApiError && [400, 404, 409].includes(err.status)) { sessionStorage.removeItem(storageKey); setPending(null); }
      setError(err instanceof Error ? err.message : "Cancellation could not be confirmed. Retry the pending request.");
    } finally { lock.current = false; setBusy(false); }
  }
  if (sale.status === "Cancelled") return <section className="sale-cancelled-banner" aria-label="Cancelled invoice">
    <strong>Cancelled invoice</strong><p>{sale.cancellationReason}</p>
    <p>{sale.cancelledAtUtc && new Date(sale.cancelledAtUtc.endsWith("Z") ? sale.cancelledAtUtc : `${sale.cancelledAtUtc}Z`).toLocaleString("en-IN", { timeZone: "Asia/Kolkata" })} · Approved by {sale.cancelledByUserId}</p>
    <p>{sale.cancellationStockRestored ? "Inspected stock restored to the original batches." : "Stock was not restored. Keep the affected medicines outside sellable stock."}</p>
    <p>The original invoice is retained. No further payments or returns can be recorded. {sale.refundDue > 0 ? "A refund remains due; record the actual payout in Returns and refunds below." : "No refund remains due."}</p>
  </section>;
  if (!hasRole("Admin", "Pharmacist") || sale.status !== "Completed") return null;
  return <section className="sale-cancellation no-print" aria-label="Sale cancellation">
    {error && <p role="alert" className="cancellation-error">{error}</p>}
    {pending ? <><p role="status">A cancellation needs confirmation: {pending.reason}. Retry with the same details to prevent duplicate stock adjustments.</p>
      <button type="button" disabled={busy} onClick={() => void cancel(pending)}>{busy ? "Confirming…" : "Retry pending cancellation"}</button></> : !open ?
      <button type="button" onClick={() => setOpen(true)}>Cancel this sale</button> :
      <form onSubmit={event => { event.preventDefault(); void cancel({ reason: reason.trim(), medicinesNotDispensed: custody, restock, inspectionApproved: inspection, idempotencyKey: crypto.randomUUID() }); }}>
        <h3>Cancel this sale</h3><p>The invoice and payment history are preserved. A paid invoice creates a refund due; cancelling does not transfer money. Sales with existing returns or refunds cannot be cancelled.</p>
        <fieldset disabled={busy}>
          <label className="cancellation-reason">Cancellation reason<textarea required maxLength={500} rows={2} value={reason} onChange={event => setReason(event.target.value)} /></label>
          <label><input type="checkbox" required checked={custody} onChange={event => setCustody(event.target.checked)} />The medicines never left pharmacy custody. If they were handed to the customer, use the returns workflow instead.</label>
          <label><input type="checkbox" checked={restock} onChange={event => { setRestock(event.target.checked); setInspection(false); }} />Restore quantities to the original batches</label>
          {restock ? <label><input type="checkbox" required checked={inspection} onChange={event => setInspection(event.target.checked)} />I inspected every item and approve it as undamaged, unexpired and saleable. Expired batches will be rejected.</label> : <p>Quantities will remain outside sellable stock. Segregate the affected medicines physically.</p>}
        </fieldset>
        <div className="cancellation-actions"><button type="submit" disabled={busy || !custody || !reason.trim() || (restock && !inspection)}>{busy ? "Cancelling…" : "Confirm cancellation"}</button>
          <button type="button" disabled={busy} onClick={() => { setOpen(false); setReason(""); setCustody(false); setRestock(false); setInspection(false); }}>Keep sale</button></div>
      </form>}
  </section>;
}
