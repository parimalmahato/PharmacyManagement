import { useEffect, useRef, useState } from "react";
import { PaymentApiError, paymentsApi, type PaymentMethod, type PaymentRequest, type SalePayments as Summary } from "../api/payments";
import { useAuth } from "../auth/useAuth";
import "./SalePayments.css";
import SaleReturns from "./SaleReturns";
import UpiCollection from "./UpiCollection";

const money = (amount: number) => new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR" }).format(amount);
const statusLabel = { Unpaid: "Unpaid", PartiallyPaid: "Partially paid", Paid: "Paid" };
interface Row { method: PaymentMethod; amount: string; reference: string; }
const emptyRow = (): Row => ({ method: 1, amount: "", reference: "" });

// Mount with key={saleId} so requests and form state belong to one sale.
export default function SalePayments({ saleId, saleStatus, onChange, onInventoryChanged, printHistory = true }: {
  saleId: number; saleStatus: string; onChange?: (summary: Summary) => void; onInventoryChanged?: () => Promise<void>; printHistory?: boolean;
}) {
  const { hasRole, user } = useAuth();
  const canRecord = hasRole("Admin", "Cashier");
  const storageKey = `pharmacy-payment:${user?.email}:${saleId}`;
  const [pending, setPending] = useState<PaymentRequest[]>(() => {
    try { return JSON.parse(sessionStorage.getItem(storageKey) ?? "[]") as PaymentRequest[]; }
    catch { return []; }
  });
  const [summary, setSummary] = useState<Summary | null>(null);
  const [rows, setRows] = useState<Row[]>([emptyRow()]);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [upiVerified, setUpiVerified] = useState(false);
  const lock = useRef(false);
  const onChangeRef = useRef(onChange);
  useEffect(() => { onChangeRef.current = onChange; }, [onChange]);
  useEffect(() => {
    let cancelled = false;
    paymentsApi.get(saleId).then(value => {
      if (!cancelled) { setSummary(value); onChangeRef.current?.(value); }
    }).catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : "Unable to load payments."); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [saleId]);

  function remember(queue: PaymentRequest[]) {
    // Persist before sending so a reload can retry with the same receipt keys.
    if (queue.length) sessionStorage.setItem(storageKey, JSON.stringify(queue));
    else sessionStorage.removeItem(storageKey);
    setPending(queue);
  }
  function update(index: number, change: Partial<Row>) {
    setUpiVerified(false);
    setRows(current => current.map((row, i) => i === index ? { ...row, ...change } : row));
  }
  async function refresh() {
    const value = await paymentsApi.get(saleId);
    setSummary(value);
    onChangeRef.current?.(value);
    return value;
  }
  async function submit() {
    if (lock.current || !summary) return;
    let queue = [...pending];
    setError(""); setNotice("");
    if (!queue.length) {
      if (rows.some(row => row.method === 2) && !upiVerified) { setError("Verify the UPI receipt in the pharmacy's bank or merchant app before recording it."); return; }
      if (rows.some(row => !/^\d+(\.\d{1,2})?$/.test(row.amount) || Number(row.amount) <= 0 ||
        !Number.isSafeInteger(Math.round(Number(row.amount) * 100)) ||
        (row.method !== 1 && !row.reference.trim()) || row.reference.trim().length > 150)) {
        setError("Enter positive amounts with up to two decimal places and a reference for each UPI/card receipt."); return;
      }
      const cents = rows.reduce((sum, row) => sum + Math.round(Number(row.amount) * 100), 0);
      if (cents > Math.round(summary.balanceAmount * 100)) {
        setError("Combined payment exceeds the outstanding balance."); return;
      }
      queue = rows.map(row => ({ method: row.method, amount: Number(row.amount),
        reference: row.reference.trim() || null, idempotencyKey: crypto.randomUUID() }));
    }
    lock.current = true; setBusy(true);
    let recorded = 0;
    try {
      remember(queue);
      while (queue.length) {
        try { await paymentsApi.record(saleId, queue[0]); }
        catch (err) {
          // A definitive rejection allows editing. Network/server failures retain
          // the exact requests for safe retry, including a possibly saved receipt.
          if (err instanceof PaymentApiError && [400, 404, 409].includes(err.status)) {
            setRows(queue.map(item => ({ method: item.method, amount: item.amount.toFixed(2), reference: item.reference ?? "" })));
            remember([]);
          }
          throw err;
        }
        recorded++;
        queue = queue.slice(1);
        remember(queue);
      }
      setRows([emptyRow()]);
      setUpiVerified(false);
      setNotice("Payment receipts recorded successfully.");
    } catch (err) {
      setError(`${recorded ? `${recorded} receipt(s) saved. ` : ""}${err instanceof Error ? err.message : "Unable to record payment."}`);
    } finally {
      try { await refresh(); }
      catch { setError(current => `${current} Unable to refresh balances. Refresh payment history before entering another payment.`.trim()); setSummary(null); }
      lock.current = false; setBusy(false);
    }
  }

  const splitTotal = rows.reduce((sum, row) => sum + Math.round((Number(row.amount) || 0) * 100), 0) / 100;
  return <section id={`sale-payments-${saleId}`} className={`payment-panel ${printHistory ? "" : "no-print"}`} aria-label="Sale payments" aria-busy={busy || loading}>
    <div className="payment-heading"><h2>Payments</h2><button className="no-print" type="button" disabled={busy || loading}
      onClick={() => { setError(""); void refresh().catch(err => setError(err instanceof Error ? err.message : "Unable to refresh payments.")); }}>Refresh payments</button></div>
    {loading && <p>Loading payments...</p>}
    {error && <p role="alert" className="payment-error no-print">{error}</p>}
    {notice && <p role="status" className="payment-success no-print">{notice}</p>}
    {summary && <>
      <dl className="payment-summary">
        <div><dt>Payment status</dt><dd>{saleStatus === "Cancelled" ? "Cancelled" : statusLabel[summary.paymentStatus]}</dd></div>
        <div><dt>Original invoice total</dt><dd>{money(summary.totalAmount)}</dd></div>
        <div><dt>Net payable after adjustments</dt><dd>{money(summary.netTotal)}</dd></div>
        <div><dt>Payments received</dt><dd>{money(summary.paidAmount)}</dd></div>
        <div><dt>Refunded</dt><dd>{money(summary.refundedAmount)}</dd></div>
        <div><dt>Refund due</dt><dd>{money(summary.refundDue)}</dd></div>
        <div><dt>Outstanding</dt><dd>{money(summary.balanceAmount)}</dd></div>
      </dl>
      {saleStatus === "Completed" && summary.balanceAmount > 0 && <UpiCollection key={`${saleId}:${summary.balanceAmount}`} saleId={saleId} balance={summary.balanceAmount} disabled={busy || loading || pending.length > 0} />}
      {canRecord && (pending.length > 0 || (summary.balanceAmount > 0 && saleStatus === "Completed")) &&
        <form className="payment-form no-print" onSubmit={event => { event.preventDefault(); void submit(); }}>
          <h3>Record payment</h3><p>Record confirmed receipts. Split the amount across methods or leave a balance to pay later. Each receipt is saved separately.</p>
          {pending.length > 0 ? <div role="status"><p>A previous payment attempt needs confirmation. Retry safely before entering another payment.</p>
            <ul>{pending.map(item => <li key={item.idempotencyKey}>{({ 1: "Cash", 2: "UPI", 3: "Card" })[item.method]}: {money(item.amount)} {item.reference && `(${item.reference})`}</li>)}</ul></div> :
            <>{rows.map((row, index) => <fieldset key={index} disabled={busy}>
              <legend>Receipt {index + 1}</legend>
              <label>Method<select value={row.method} onChange={event => update(index, { method: Number(event.target.value) as PaymentMethod, reference: "" })}>
                <option value={1}>Cash</option><option value={2}>UPI</option><option value={3}>Card</option>
              </select></label>
              <label>Amount (INR)<input type="number" min="0.01" step="0.01" max={summary.balanceAmount} required value={row.amount} onChange={event => update(index, { amount: event.target.value })} /></label>
              <label>{row.method === 1 ? "Reference (optional)" : "Transaction reference"}<input maxLength={150} required={row.method !== 1} value={row.reference} onChange={event => update(index, { reference: event.target.value })} /></label>
              {rows.length > 1 && <button type="button" onClick={() => setRows(current => current.filter((_, i) => i !== index))}>Remove receipt {index + 1}</button>}
            </fieldset>)}
            <div className="payment-actions"><button disabled={busy} type="button" onClick={() => setRows(current => [...current, emptyRow()])}>Add split payment</button>
              <button disabled={busy} type="button" onClick={() => {
                const otherCents = rows.slice(0, -1).reduce((sum, row) => sum + Math.round((Number(row.amount) || 0) * 100), 0);
                update(rows.length - 1, { amount: (Math.max(0, Math.round(summary.balanceAmount * 100) - otherCents) / 100).toFixed(2) });
              }}>Fill remaining balance</button></div>
            <p>Recording: <strong>{money(splitTotal)}</strong> · Balance after payment: <strong>{money(summary.balanceAmount - splitTotal)}</strong></p></>}
          {!pending.length && rows.some(row => row.method === 2) && <label className="upi-verified-checkbox"><input type="checkbox" checked={upiVerified} disabled={busy} onChange={event => setUpiVerified(event.target.checked)} />I verified the UPI amount and transaction ID in the pharmacy’s bank or merchant app.</label>}
          <button type="submit" disabled={busy || (!pending.length && rows.some(row => row.method === 2) && !upiVerified)}>{busy ? "Recording..." : pending.length ? "Retry pending receipts" : "Record payment"}</button>
        </form>}
      {!canRecord && <p className="no-print">Payment recording is available to administrators and cashiers.</p>}
      <h3>Payment history</h3>
      {summary.payments.length === 0 ? <p>No payments recorded.</p> : <div className="payment-history"><table>
        <thead><tr><th>Receipt</th><th>Date</th><th>Method</th><th>Reference</th><th>Amount</th></tr></thead>
        <tbody>{summary.payments.map(payment => <tr key={payment.id}><td>#{payment.id}</td>
          <td>{new Date(payment.createdAtUtc.endsWith("Z") ? payment.createdAtUtc : `${payment.createdAtUtc}Z`).toLocaleString("en-IN", { timeZone: "Asia/Kolkata" })}</td>
          <td>{payment.method === "Upi" ? "UPI" : payment.method}</td><td>{payment.reference ?? "—"}</td><td>{money(payment.amount)}</td></tr>)}</tbody>
      </table></div>}
    </>}
    <SaleReturns saleId={saleId} saleStatus={saleStatus} disabled={busy || pending.length > 0}
      version={`${summary?.paidAmount}:${summary?.returnedAmount}:${summary?.refundedAmount}`} onChanged={async () => {
        await refresh();
        await onInventoryChanged?.();
      }} />
  </section>;
}
