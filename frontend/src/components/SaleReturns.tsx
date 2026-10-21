import { useEffect, useRef, useState } from "react";
import { salesReturnsApi, type ReturnRequest, type RefundRequest, type RefundMethod, type ReturnDisposition, type SaleReturns as ReturnSummary } from "../api/salesReturns";
import { PaymentApiError } from "../api/payments";
import { useAuth } from "../auth/useAuth";

const money = (amount: number) => new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR" }).format(amount);
const date = (value: string) => new Date(value.endsWith("Z") ? value : `${value}Z`).toLocaleString("en-IN", { timeZone: "Asia/Kolkata" });
type Pending = { kind: "return"; request: ReturnRequest } | { kind: "refund"; request: RefundRequest };
const dispositions = { 1: "Saleable (inspected)", 2: "Damaged", 3: "Expired", 4: "Quarantine / uninspected", 5: "Non-resaleable" };

export default function SaleReturns({ saleId, saleStatus, version, disabled, onChanged }: {
  saleId: number; saleStatus: string; version: string; disabled: boolean; onChanged: () => Promise<unknown>;
}) {
  const { hasRole, user } = useAuth();
  const canManage = hasRole("Admin", "Cashier");
  const storageKey = `pharmacy-return:${user?.email}:${saleId}`;
  const [pending, setPending] = useState<Pending | null>(() => {
    try { return JSON.parse(sessionStorage.getItem(storageKey) ?? "null") as Pending | null; } catch { return null; }
  });
  const [data, setData] = useState<ReturnSummary | null>(null);
  const [selected, setSelected] = useState<Record<number, { quantity: string; restock: boolean; disposition: ReturnDisposition }>>({});
  const [refundWithReturn, setRefundWithReturn] = useState(false);
  const [inspection, setInspection] = useState<Record<number, boolean>>({});
  const [reason, setReason] = useState("");
  const [amount, setAmount] = useState("");
  const [method, setMethod] = useState<RefundMethod>(1);
  const [reference, setReference] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const lock = useRef(false);
  const loadVersion = useRef(0);
  useEffect(() => {
    let cancelled = false;
    const generation = ++loadVersion.current;
    salesReturnsApi.get(saleId).then(value => { if (!cancelled && loadVersion.current === generation) setData(value); })
      .catch(err => { if (!cancelled && loadVersion.current === generation) setError(err instanceof Error ? err.message : "Unable to load returns."); });
    return () => { cancelled = true; };
  }, [saleId, version]);

  async function refresh() {
    loadVersion.current++;
    setData(await salesReturnsApi.get(saleId));
    await onChanged();
  }
  async function run(command: Pending) {
    if (lock.current || disabled) return;
    lock.current = true; setBusy(true); setError(""); setNotice("");
    try {
      sessionStorage.setItem(storageKey, JSON.stringify(command));
      setPending(command);
      if (command.kind === "return") await salesReturnsApi.create(saleId, command.request);
      else await salesReturnsApi.refund(saleId, command.request);
      sessionStorage.removeItem(storageKey); setPending(null);
      setSelected({}); setInspection({}); setReason(""); setAmount(""); setReference(""); setRefundWithReturn(false);
      setNotice(command.kind === "return" ? "Return recorded. Original invoice preserved." : "Refund receipt recorded.");
    } catch (err) {
      if (err instanceof PaymentApiError && [400, 404, 409].includes(err.status)) {
        sessionStorage.removeItem(storageKey); setPending(null);
      }
      setError(err instanceof Error ? err.message : "Request failed. Retry the pending operation safely.");
    } finally {
      try { await refresh(); } catch { setData(null); setError(current => `${current} Unable to refresh returns. Refresh before continuing.`.trim()); }
      lock.current = false; setBusy(false);
    }
  }
  function submitReturn() {
    const items = Object.entries(selected).filter(([, row]) => Number(row.quantity) > 0).map(([id, row]) => ({ saleItemId: Number(id), quantity: Number(row.quantity), restock: row.restock, disposition: row.disposition, inspectionApproved: inspection[Number(id)] ?? false }));
    if (!reason.trim() || !items.length || items.some(item => !Number.isInteger(item.quantity) || item.quantity <= 0)) {
      setError("Select at least one item with a whole-number return quantity and enter a reason."); return;
    }
    if (refundWithReturn && (!/^\d+(\.\d{1,2})?$/.test(amount) || Number(amount) <= 0 || (method !== 1 && !reference.trim()))) {
      setError("Enter a positive refund amount and a transaction reference for UPI/card."); return;
    }
    void run({ kind: "return", request: { reason: reason.trim(), items, idempotencyKey: crypto.randomUUID(),
      ...(refundWithReturn ? { refund: { method, amount: Number(amount), reference: reference.trim() || null } } : {}) } });
  }
  function submitRefund() {
    if (!/^\d+(\.\d{1,2})?$/.test(amount) || Number(amount) <= 0 || !data || Number(amount) > data.refundDue ||
      (method !== 1 && !reference.trim())) { setError("Enter an amount within the refund due and a reference for UPI/card."); return; }
    void run({ kind: "refund", request: { method, amount: Number(amount), reference: reference.trim() || null, idempotencyKey: crypto.randomUUID() } });
  }
  return <section aria-label="Sales returns and refunds" aria-busy={busy}>
    <div className="payment-heading"><h2>Returns and refunds</h2>
      <button type="button" className="no-print" disabled={busy || disabled} onClick={() => {
        setError(""); void refresh().catch(err => setError(err instanceof Error ? err.message : "Unable to refresh returns."));
      }}>Refresh returns</button></div>
    {error && <p role="alert" className="payment-error no-print">{error}</p>}
    {notice && <p role="status" className="payment-success no-print">{notice}</p>}
    {pending && canManage && <div className="no-print"><p>A {pending.kind} attempt needs confirmation. Retry with the same details to avoid duplicate records.</p>
      <p>{pending.kind === "refund" ? `${money(pending.request.amount)} refund` : `${pending.request.items.reduce((sum, item) => sum + item.quantity, 0)} returned unit(s): ${pending.request.reason}`}</p>
      <button type="button" disabled={busy || disabled} onClick={() => void run(pending)}>Retry pending {pending.kind}</button></div>}
    {!data ? <p>Return details unavailable. Use Refresh returns to retry.</p> : <>
      <dl className="payment-summary"><div><dt>Return credits</dt><dd>{money(data.returnedAmount)}</dd></div>
        <div><dt>Refunded</dt><dd>{money(data.refundedAmount)}</dd></div><div><dt>Refund due</dt><dd>{money(data.refundDue)}</dd></div></dl>
      {canManage && (saleStatus === "Completed" || saleStatus === "Cancelled") && !pending && <div className="no-print">
        {saleStatus === "Completed" && data.items.some(item => item.remainingQuantity > 0) && <form className="payment-form" onSubmit={event => { event.preventDefault(); submitReturn(); }}>
          <h3>Return medicines</h3><p>Choose each item's inspected disposition. Only saleable, unexpired medicines can be restocked. All other dispositions remain outside sellable stock.</p>
          <fieldset disabled={busy || disabled}>
            <legend>Returned items</legend>
            <div className="payment-history"><table><thead><tr><th>Medicine / batch</th><th>Sold</th><th>Already returned</th><th>Return quantity</th><th>Disposition</th><th>Inspection approval</th><th>Restock</th></tr></thead>
              <tbody>{data.items.map(item => <tr key={item.saleItemId}>
                <td>{item.medicineName}<br />{item.batchNumber} · Expiry {item.expiryDate}</td><td>{item.soldQuantity}</td><td>{item.returnedQuantity}</td>
                <td><input aria-label={`Return quantity for ${item.medicineName} batch ${item.batchNumber}`} type="number" min={0} max={item.remainingQuantity} step={1}
                  disabled={!item.remainingQuantity} value={selected[item.saleItemId]?.quantity ?? ""} onChange={event => {
                    setInspection(current => ({ ...current, [item.saleItemId]: false }));
                    setSelected(current => ({ ...current, [item.saleItemId]: { quantity: event.target.value, restock: false, disposition: current[item.saleItemId]?.disposition ?? 4 } }));
                  }} /></td>
                <td><select aria-label={`Disposition for ${item.medicineName} batch ${item.batchNumber}`} disabled={!item.remainingQuantity}
                  value={selected[item.saleItemId]?.disposition ?? 4} onChange={event => {
                    const disposition = Number(event.target.value) as ReturnDisposition;
                    setInspection(current => ({ ...current, [item.saleItemId]: false }));
                    setSelected(current => ({ ...current, [item.saleItemId]: { quantity: current[item.saleItemId]?.quantity ?? "", disposition,
                      restock: false } }));
                  }}>{Object.entries(dispositions).map(([id, label]) => <option key={id} value={id} disabled={id === "1" && !item.canRestock}>{label}</option>)}</select></td>
                <td><label><input type="checkbox" aria-label={`Approve inspection for ${item.medicineName} batch ${item.batchNumber}`}
                  disabled={!item.canRestock || selected[item.saleItemId]?.disposition !== 1 || !(Number(selected[item.saleItemId]?.quantity) > 0)}
                  checked={inspection[item.saleItemId] ?? false} onChange={event => {
                    setInspection(current => ({ ...current, [item.saleItemId]: event.target.checked }));
                    if (!event.target.checked) setSelected(current => ({ ...current, [item.saleItemId]: { ...current[item.saleItemId], restock: false } }));
                  }} />Inspected and approved for resale</label></td>
                <td><label><input type="checkbox" aria-label={`Restock ${item.medicineName} batch ${item.batchNumber}`} disabled={!item.canRestock || !item.remainingQuantity || selected[item.saleItemId]?.disposition !== 1 || !inspection[item.saleItemId]}
                  checked={selected[item.saleItemId]?.restock ?? false} onChange={event => setSelected(current => ({ ...current, [item.saleItemId]: { quantity: current[item.saleItemId]?.quantity ?? "", restock: event.target.checked, disposition: current[item.saleItemId]?.disposition ?? 4 } }))} />Restock</label></td>
              </tr>)}</tbody></table></div>
            <label>Return reason<input required maxLength={500} value={reason} onChange={event => setReason(event.target.value)} /></label>
            <label><input type="checkbox" checked={refundWithReturn} onChange={event => setRefundWithReturn(event.target.checked)} />Record a confirmed refund with this return</label>
            {refundWithReturn && <>
              <p>The return, refund receipt and stock changes save together. The server checks the refund against the adjusted balance. This records a payout; it does not transfer money.</p>
              <label>Refund method<select value={method} onChange={event => setMethod(Number(event.target.value) as RefundMethod)}><option value={1}>Cash</option><option value={2}>UPI</option><option value={3}>Card</option></select></label>
              <label>Refund amount (INR)<input required type="number" min="0.01" step="0.01" value={amount} onChange={event => setAmount(event.target.value)} /></label>
              <label>Refund reference<input required={method !== 1} maxLength={150} value={reference} onChange={event => setReference(event.target.value)} /></label>
            </>}
          </fieldset><button type="submit" disabled={busy || disabled}>{refundWithReturn ? "Record return and refund" : "Record return"}</button>
        </form>}
        {data.refundDue > 0 && <form className="payment-form" onSubmit={event => { event.preventDefault(); submitRefund(); }}>
          <h3>Record refund</h3><p>Record money already returned to the customer. This does not initiate a bank or card transfer.</p>
          <fieldset disabled={busy || disabled}><legend>Refund receipt</legend>
            <label>Method<select value={method} onChange={event => { setMethod(Number(event.target.value) as RefundMethod); setReference(""); }}><option value={1}>Cash</option><option value={2}>UPI</option><option value={3}>Card</option></select></label>
            <label>Amount (INR)<input required type="number" min="0.01" max={data.refundDue} step="0.01" value={amount} onChange={event => setAmount(event.target.value)} /></label>
            <label>Reference {method === 1 ? "(optional)" : "(required)"}<input required={method !== 1} maxLength={150} value={reference} onChange={event => setReference(event.target.value)} /></label>
          </fieldset><button disabled={busy || disabled} type="submit">Record refund</button>
        </form>}
      </div>}
      <h3>Return history</h3>
      {!data.returns.length ? <p>No returns recorded.</p> : data.returns.map(record => <div key={record.id} className="return-history-entry">
        <p><strong>Return #{record.id}</strong> · {date(record.createdAtUtc)} · {money(record.totalAmount)}</p><p>{record.reason}</p>
        <ul>{record.items.map(item => { const original = data.items.find(x => x.saleItemId === item.saleItemId); return <li key={item.saleItemId}>
          {original?.medicineName} / {original?.batchNumber}: {item.quantity} unit(s), {money(item.amount)} — {item.disposition} — {item.restock ? "Restocked" : "Not restocked"}
          {item.inspectionApproved && item.inspectedAtUtc ? ` — Approved by ${item.inspectedByUserId} at ${date(item.inspectedAtUtc)}` : " — No recorded inspection approval"}
        </li>; })}</ul>
      </div>)}
      <h3>Refund history</h3>
      {!data.refunds.length ? <p>No refunds recorded.</p> : <div className="payment-history"><table><thead><tr><th>Refund</th><th>Date</th><th>Method</th><th>Reference</th><th>Amount</th></tr></thead>
        <tbody>{data.refunds.map(refund => <tr key={refund.id}><td>#{refund.id}</td><td>{date(refund.createdAtUtc)}</td><td>{refund.method === "Upi" ? "UPI" : refund.method}</td><td>{refund.reference ?? "—"}</td><td>{money(refund.amount)}</td></tr>)}</tbody></table></div>}
    </>}
  </section>;
}
