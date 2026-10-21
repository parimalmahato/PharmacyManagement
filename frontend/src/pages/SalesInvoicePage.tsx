import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getSaleInvoice, type SaleInvoice } from "../api/salesInvoice";
import "./SalesInvoicePage.css";
import SalePayments from "../components/SalePayments";
import SaleCancellation from "../components/SaleCancellation";

const money = (amount: number) => new Intl.NumberFormat("en-IN", {
  style: "currency", currency: "INR", minimumFractionDigits: 2,
}).format(amount);
const date = (value: string) => new Date(value).toLocaleString("en-IN", {
  dateStyle: "medium", timeStyle: "short", timeZone: "Asia/Kolkata",
});

export default function SalesInvoicePage() {
  const { saleId } = useParams<{ saleId: string }>();
  const [invoice, setInvoice] = useState<SaleInvoice | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      setError("");
      try {
        if (!saleId || !/^\d+$/.test(saleId) || Number(saleId) <= 0) throw new Error("Invalid sale ID.");
        const result = await getSaleInvoice(Number(saleId));
        if (!cancelled) setInvoice(result);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "Unable to load invoice.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void load();
    return () => { cancelled = true; };
  }, [saleId]);
  if (loading) return <p>Loading invoice...</p>;
  if (error) return <div className="invoice-error"><p role="alert">{error}</p><Link to="/sales">Back to Sales</Link></div>;
  if (!invoice) return <p>Invoice not found.</p>;
  const { pharmacy, sale } = invoice;
  return <div className="invoice-page">
    <div className="invoice-actions">
      <Link to="/sales" className="invoice-back">← Back to Sales</Link>
      <button type="button" className="invoice-print-button" onClick={() => window.print()}>Print Invoice</button>
    </div>
    <article className="invoice-paper">
      <header className="invoice-header">
        <div><h1>{pharmacy.pharmacyName}</h1><p>{pharmacy.address}</p>
          <p>Phone: {pharmacy.phoneNumber}</p>
          {pharmacy.email && <p>Email: {pharmacy.email}</p>}
          {pharmacy.gstin && <p>GSTIN: {pharmacy.gstin}</p>}
        </div>
        <div className="invoice-heading"><h2>SALES INVOICE</h2>
          <p><strong>Invoice No:</strong> {sale.invoiceNumber}</p>
          <p><strong>Date:</strong> {date(sale.createdAtUtc)}</p>
        </div>
      </header>
      <SaleCancellation key={sale.id} sale={sale} onCancelled={updated => setInvoice(current => current ? { ...current, sale: updated } : null)} />
      <section className="invoice-customer"><h3>Customer Details</h3>
        <p><strong>Name:</strong> {sale.customerName || "Walk-in Customer"}</p>
        {pharmacy.receiptShowCustomerPhone && sale.customerPhone && <p><strong>Phone:</strong> {sale.customerPhone}</p>}
      </section>
      <div className="invoice-table-scroll"><table className="invoice-table">
        <thead><tr><th>Medicine</th><th>HSN</th><th>Batch</th><th>Expiry</th>
          <th className="number">Qty</th><th className="number">Price incl. GST</th>
          <th className="number">GST %</th><th className="number">Disc. %</th>
          <th className="number">Discount</th><th className="number">Taxable</th>
          <th className="number">GST included</th><th className="number">Total</th>
        </tr></thead>
        <tbody>{sale.items.map((item, index) => <tr key={`${item.medicineId}-${item.batchNumber}-${index}`}>
          <td>{item.medicineName}</td><td>{item.hsnCode || "—"}</td>
          <td>{item.batchNumber}</td><td>{item.expiryDate}</td>
          <td className="number">{item.quantity}</td><td className="number">{money(item.unitPrice)}</td>
          <td className="number">{item.gstPercentage}%</td><td className="number">{item.discountPercentage}%</td>
          <td className="number">{money(item.discountAmount)}</td><td className="number">{money(item.taxableAmount)}</td>
          <td className="number">{money(item.gstAmount)}</td><td className="number">{money(item.lineTotal)}</td>
        </tr>)}</tbody>
      </table></div>
      <section className="invoice-totals"><div className="invoice-totals-list">
        <div><span>Subtotal (GST included)</span><strong>{money(sale.subtotalAmount)}</strong></div>
        <div><span>Discount</span><strong>−{money(sale.discountAmount)}</strong></div>
        <div><span>Taxable value</span><strong>{money(sale.taxableAmount)}</strong></div>
        <div><span>GST included</span><strong>{money(sale.gstAmount)}</strong></div>
        <div className="invoice-grand-total"><span>Grand Total</span><strong>{money(sale.totalAmount)}</strong></div>
      </div></section>
      <SalePayments key={`${sale.id}:${sale.status}`} saleId={sale.id} saleStatus={sale.status} printHistory={pharmacy.receiptShowPaymentHistory} onChange={summary => {
        setInvoice(current => current ? { ...current, sale: { ...current.sale,
          paidAmount: summary.paidAmount, balanceAmount: summary.balanceAmount, paymentStatus: summary.paymentStatus,
          returnedAmount: summary.returnedAmount, refundedAmount: summary.refundedAmount, refundDue: summary.refundDue, netTotal: summary.netTotal } } : null);
      }} />
      <footer className="invoice-footer"><p style={{ whiteSpace: "pre-wrap" }}>{pharmacy.receiptFooter}</p>
        <p>Please retain this invoice for your records.</p></footer>
    </article>
  </div>;
}
