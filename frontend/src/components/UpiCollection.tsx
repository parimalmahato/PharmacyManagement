import { useRef, useState } from "react";
import { QRCodeSVG } from "qrcode.react";
import { getUpiPaymentRequest, type UpiPaymentRequest } from "../api/upi";
import "./UpiCollection.css";

const money = (value: number) => new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR" }).format(value);
export default function UpiCollection({ saleId, balance, disabled }: { saleId: number; balance: number; disabled: boolean }) {
  const [amount, setAmount] = useState(balance.toFixed(2));
  const [request, setRequest] = useState<UpiPaymentRequest | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const lock = useRef(false);
  async function generate() {
    if (lock.current || disabled) return;
    setError(""); setRequest(null);
    if (!/^\d+(\.\d{1,2})?$/.test(amount) || Number(amount) <= 0 || Number(amount) > balance) {
      setError("Enter a positive amount within the outstanding balance."); return;
    }
    lock.current = true; setBusy(true);
    try { setRequest(await getUpiPaymentRequest(saleId, amount)); }
    catch (err) { setError(err instanceof Error ? err.message : "Unable to generate QR."); }
    finally { lock.current = false; setBusy(false); }
  }
  return <section className="upi-collection no-print" aria-label="UPI QR collection" aria-busy={busy}>
    <div className="upi-collection-heading"><div><span className="upi-kicker">Customer collection</span><h3>Collect with UPI</h3></div><span className="upi-manual-badge">Staff verification</span></div>
    <p>Prepare a QR for the outstanding balance or a partial payment. No payment is recorded until staff verifies the receipt.</p>
    <div className="upi-request-controls"><label>Amount (INR)<input type="number" min="0.01" step="0.01" max={balance} value={amount} disabled={busy || disabled} onChange={event => { setAmount(event.target.value); setRequest(null); setError(""); }} /></label>
      <button type="button" disabled={busy || disabled} onClick={() => void generate()}>{busy ? "Preparing…" : request ? "Refresh QR" : "Show UPI QR"}</button></div>
    {error && <p className="upi-error" role="alert">{error}</p>}
    {request && !disabled && <div className="upi-request-result">
      <div className="upi-qr"><QRCodeSVG value={request.paymentUri} size={216} level="M" marginSize={4} title={`UPI payment of ${money(request.amount)} to ${request.payeeName}`} /></div>
      <div className="upi-request-details"><strong>{money(request.amount)}</strong><p>{request.payeeName}<br /><span className="upi-payee">{request.payeeUpiId}</span></p>
        <p>Invoice: {request.invoiceNumber}</p>
        <a href={request.paymentUri}>Open a UPI app on this device</a>
        <p className="upi-receipt-note">Check the payee and amount in the customer’s app. Verify receipt in the pharmacy’s bank or merchant app, then record a UPI receipt below using its actual UTR / transaction ID.</p>
        <p className="upi-receipt-note">The QR reference is not a bank transaction ID. A scan, screenshot or app redirect does not confirm payment. Refresh balances if another cashier collects money.</p>
      </div>
    </div>}
  </section>;
}
