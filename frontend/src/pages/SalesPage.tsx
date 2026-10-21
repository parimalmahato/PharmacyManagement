import { apiFetch } from "../api/apiClient";

import { useCallback, useEffect, useRef, useState } from "react";
import { medicineApi } from "../api/medicines";
import {
  salesApi,
  type Sale,
  type SaleItemRequest,
  type CreateSaleRequest,
  SalesApiError,
} from "../api/sales";
import { customersApi } from "../api/customers";
import type { Customer } from "../api/customers";
import { Link } from "react-router-dom";
import "./SalesPage.css";
import SalePayments from "../components/SalePayments";
import SaleCancellation from "../components/SaleCancellation";
import { useAuth } from "../auth/useAuth";
import type { SalePayments as PaymentSummary } from "../api/payments";
import { getSaleInvoice } from "../api/salesInvoice";
import type { PharmacySettings } from "../api/pharmacySettingsApi";
interface Medicine {
  id: number;
  name: string;
  isActive: boolean;
  hsnCode: string | null;
  gstPercentage: number;
}

interface CartItem {
  medicineId: number;
  quantity: number;
  discountPercentage: number;
}

interface InventoryBatch {
  id: number;
  medicineId: number;
  batchNumber: string;
  expiryDate: string;
  sellingPrice: number;
  quantity: number;
}

export default function SalesPage() {
  const { hasRole, user } = useAuth();
  const checkoutStorageKey = `pharmacy-checkout:${user?.email}:${user?.branchId}`;
  const [pendingCheckout, setPendingCheckout] = useState<CreateSaleRequest | null>(() => {
    try { return JSON.parse(sessionStorage.getItem(checkoutStorageKey) ?? "null") as CreateSaleRequest | null; } catch { return null; }
  });
  const checkoutLock = useRef(false);
  const canCreateSale = hasRole("Admin", "Cashier");
  const [paymentSale, setPaymentSale] = useState<Sale | null>(null);
  const [receiptSettings, setReceiptSettings] = useState<PharmacySettings | null>(null);
  const [billingGstEnabled, setBillingGstEnabled] = useState(true);
  useEffect(() => {
    if (paymentSale) document.getElementById(`sale-payments-${paymentSale.id}`)?.scrollIntoView({ behavior: "smooth" });
  }, [paymentSale]);
  function updatePaymentSummary(summary: PaymentSummary) {
    const update = (sale: Sale): Sale => sale.id === summary.saleId
      ? { ...sale, paidAmount: summary.paidAmount, balanceAmount: summary.balanceAmount, paymentStatus: summary.paymentStatus,
        returnedAmount: summary.returnedAmount, refundedAmount: summary.refundedAmount, refundDue: summary.refundDue, netTotal: summary.netTotal }
      : sale;
    setSales(current => current.map(update));
    setInvoiceToPrint(current => current ? update(current) : null);
  }
  async function refreshInventoryAfterReturn() {
    const response = await apiFetch("/api/inventory/batches");
    if (!response.ok) throw new Error("Unable to refresh inventory after return.");
    setBatches(await response.json() as InventoryBatch[]);
  }
  const [medicines, setMedicines] = useState<Medicine[]>([]);
  const [cart, setCart] = useState<CartItem[]>([]);
  const [sales, setSales] = useState<Sale[]>([]);
  const [batches, setBatches] = useState<InventoryBatch[]>([]);
  const [customers, setCustomers] = useState<Customer[]>([]);

  const [selectedId, setSelectedId] = useState("");
  const [selectedCustomerId, setSelectedCustomerId] = useState("");
  const [quantity, setQuantity] = useState(1);
  const [discountPercentage, setDiscountPercentage] = useState(0);

  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState(false);
  const [invoiceToPrint, setInvoiceToPrint] = useState<Sale | null>(null);

  

 const loadData = useCallback(async () => {
  setLoading(true);

  try {
    const [
      medicineData,
      salesData,
      customerData,
      batchResponse,
      billingResponse,
    ] = await Promise.all([
      medicineApi.getAll(),
      salesApi.getAll(),
      customersApi.getAll(),
      apiFetch("/api/inventory/batches"),
      apiFetch("/api/sales/billing-configuration"),
    ]);

    if (!batchResponse.ok) {
      throw new Error("Unable to load inventory batches.");
    }

    const batchData: InventoryBatch[] =
      await batchResponse.json();
    if (!billingResponse.ok) throw new Error("Unable to load billing configuration.");
    const billing: { gstEnabled: boolean } = await billingResponse.json();
    setBillingGstEnabled(billing.gstEnabled);

    setMedicines(medicineData as Medicine[]);
    setSales(salesData);
    setCustomers(customerData);
    setBatches(batchData);
    setError(false);
    setMessage("");
  } catch (err) {
    setError(true);
    setMessage(
      err instanceof Error
        ? err.message
        : "Unable to load billing information."
    );
  } finally {
    setLoading(false);
  }
}, [setLoading, setMedicines, setSales, setCustomers, setBatches, setError, setMessage, setBillingGstEnabled]);

  useEffect(() => {
    Promise.resolve().then(() => {
      void loadData();
    });
  }, [loadData]);

  // Return eligible batches ordered by First Expiry, First Out (FEFO).
  function getEligibleBatches(medicineId: number): InventoryBatch[] {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    return batches
      .filter((batch) => {
        const expiry = new Date(
          `${batch.expiryDate.slice(0, 10)}T00:00:00`
        );

        return (
          batch.medicineId === medicineId &&
          batch.quantity > 0 &&
          expiry > today
        );
      })
      .sort(
        (a, b) =>
          a.expiryDate.localeCompare(b.expiryDate) || a.id - b.id
      );
  }

  function getAvailableStock(medicineId: number): number {
    return getEligibleBatches(medicineId).reduce(
      (total, batch) => total + batch.quantity,
      0
    );
  }

  // Estimate only; the backend calculates actual batch allocation.
  function getEstimatedUnitPrice(medicineId: number): number {
    return getEligibleBatches(medicineId)[0]?.sellingPrice ?? 0;
  }

  const money = (value: number) => `₹${value.toFixed(2)}`;
  const roundMoney = (value: number) => Math.round((value + Number.EPSILON) * 100) / 100;

  // Preview mirrors per-batch GST-inclusive backend calculations.
  function estimateLine(item: CartItem) {
    const medicine = medicines.find(m => m.id === item.medicineId);
    const rate = billingGstEnabled ? medicine?.gstPercentage ?? 0 : 0;
    let remaining = item.quantity;
    let gross = 0, discount = 0, taxable = 0, gst = 0, net = 0;
    for (const batch of getEligibleBatches(item.medicineId)) {
      const allocated = Math.min(remaining, batch.quantity);
      if (!allocated) break;
      const batchGross = roundMoney(allocated * batch.sellingPrice);
      const batchDiscount = roundMoney(batchGross * item.discountPercentage / 100);
      const batchNet = batchGross - batchDiscount;
      const batchTaxable = roundMoney(batchNet * 100 / (100 + rate));
      gross += batchGross;
      discount += batchDiscount;
      taxable += batchTaxable;
      gst += batchNet - batchTaxable;
      net += batchNet;
      remaining -= allocated;
    }
    return { gross, discount, taxable, gst, net, remaining };
  }

  function addToCart() {
    const medicineId = Number(selectedId);

    if (
      !medicineId ||
      !Number.isInteger(quantity) ||
      quantity <= 0
    ) {
      setError(true);
      setMessage(
        "Select a medicine and enter a valid whole-number quantity."
      );
      return;
    }

    if (!Number.isFinite(discountPercentage) || discountPercentage < 0 ||
        discountPercentage > 100 || Math.round(discountPercentage * 100) !== discountPercentage * 100) {
      setError(true);
      setMessage("Discount must be between 0 and 100 with up to two decimal places.");
      return;
    }

    const availableStock = getAvailableStock(medicineId);

    if (availableStock <= 0) {
      setError(true);
      setMessage("This medicine has no available, unexpired stock.");
      return;
    }

    if (quantity > availableStock) {
      setError(true);
      setMessage(`Only ${availableStock} unit(s) are available.`);
      return;
    }

    if (cart.some((item) => item.medicineId === medicineId)) {
      setError(true);
      setMessage("This medicine is already in the cart.");
      return;
    }

    setCart((current) => [...current, { medicineId, quantity, discountPercentage }]);
    setSelectedId("");
    setQuantity(1);
    setDiscountPercentage(0);
    setError(false);
    setMessage("");
  }

  function updateQuantity(medicineId: number, newQuantity: number) {
    if (!Number.isInteger(newQuantity) || newQuantity <= 0) {
      return;
    }

    const availableStock = getAvailableStock(medicineId);

    if (newQuantity > availableStock) {
      setError(true);
      setMessage(`Only ${availableStock} unit(s) are available.`);
      return;
    }

    setCart((current) =>
      current.map((item) =>
        item.medicineId === medicineId
          ? { ...item, quantity: newQuantity }
          : item
      )
    );

    setError(false);
    setMessage("");
  }

  function updateDiscount(medicineId: number, discount: number) {
    if (!Number.isFinite(discount) || discount < 0 || discount > 100 ||
        Math.round(discount * 100) !== discount * 100) return;
    setCart(current => current.map(item => item.medicineId === medicineId
      ? { ...item, discountPercentage: discount } : item));
  }

  function removeFromCart(medicineId: number) {
    setCart((current) =>
      current.filter((item) => item.medicineId !== medicineId)
    );

    setError(false);
    setMessage("");
  }

  async function checkout() {
    if (checkoutLock.current) return;
    if (!pendingCheckout && cart.length === 0) {
      setError(true);
      setMessage("Add at least one medicine before checkout.");
      return;
    }

    // Client-side check only. The backend must validate stock again.
    for (const item of pendingCheckout ? [] : cart) {
      const availableStock = getAvailableStock(item.medicineId);

      if (item.quantity > availableStock) {
        setError(true);
        setMessage(
          `Insufficient stock for ${
            medicines.find(
              (medicine) => medicine.id === item.medicineId
            )?.name ?? "a medicine"
          }. Refresh inventory and update the cart.`
        );
        return;
      }
    }

    checkoutLock.current = true;
    setBusy(true);
    setError(false);
    setMessage("");

    try {
      const items: SaleItemRequest[] = cart.map((item) => ({
        medicineId: item.medicineId,
        quantity: item.quantity,
        discountPercentage: item.discountPercentage,
      }));

      const request = pendingCheckout ?? {
        items,
        customerId: selectedCustomerId
          ? Number(selectedCustomerId)
          : null,
        idempotencyKey: crypto.randomUUID(),
      };
      sessionStorage.setItem(checkoutStorageKey, JSON.stringify(request)); setPendingCheckout(request);
      const sale = await salesApi.create(request);
      sessionStorage.removeItem(checkoutStorageKey); setPendingCheckout(null);

      setInvoiceToPrint(sale);
      setReceiptSettings(null);
      try { setReceiptSettings((await getSaleInvoice(sale.id)).pharmacy); }
      catch { /* Sale remains completed if receipt configuration is unavailable. */ }
      setPaymentSale(null);
      setCart([]);
      setSelectedId("");
      setQuantity(1);
      setDiscountPercentage(0);
      setSelectedCustomerId("");

      await loadData();

      setError(false);
      setMessage(
        `Sale completed successfully. Invoice: ${sale.invoiceNumber}. ` +
          `Total: ₹${sale.totalAmount.toFixed(2)}`
      );
    } catch (err) {
      if (err instanceof SalesApiError && [400, 404, 409].includes(err.status)) { sessionStorage.removeItem(checkoutStorageKey); setPendingCheckout(null); }
      setError(true);
      setMessage(
        err instanceof Error ? err.message : "Checkout failed."
      );
    } finally {
      checkoutLock.current = false;
      setBusy(false);
    }
  }

  const estimated = cart.reduce((totals, item) => {
    const line = estimateLine(item);
    return {
      gross: totals.gross + line.gross,
      discount: totals.discount + line.discount,
      taxable: totals.taxable + line.taxable,
      gst: totals.gst + line.gst,
      net: totals.net + line.net,
    };
  }, { gross: 0, discount: 0, taxable: 0, gst: 0, net: 0 });

  if (loading) {
    return (
      <main className="sales-page">
        <p className="sales-empty">
          Loading billing and inventory information...
        </p>
      </main>
    );
  }

  return (
    <main className="sales-page">
      <h1>Billing and Sales</h1>

      {message && (
        <p
          className={`sales-message ${
            error ? "sales-message-error" : "sales-message-success"
          }`}
          role="status"
        >
          {message}
        </p>
      )}

      {/* New sale */}
      {canCreateSale && <section className="sales-panel">
        <h2>New Sale</h2>
        {pendingCheckout && <p role="status">An earlier checkout needs confirmation ({pendingCheckout.items.length} medicine line(s)). Retry it with the same request before starting another sale.</p>}
        <fieldset disabled={busy || pendingCheckout !== null} style={{ border: 0, margin: 0, padding: 0, minWidth: 0 }}>

        <div className="sales-customer-field">
          <label htmlFor="sale-customer">Select Customer</label>

          <select
            id="sale-customer"
            value={selectedCustomerId}
            onChange={(event) =>
              setSelectedCustomerId(event.target.value)
            }
            disabled={busy}
          >
            <option value="">Walk-in Customer</option>

            {customers
              .filter((customer) => customer.isActive)
              .map((customer) => (
                <option key={customer.id} value={customer.id}>
                  {customer.name}
                  {customer.phone ? ` — ${customer.phone}` : ""}
                </option>
              ))}
          </select>

          {selectedCustomerId && (
            <p className="sales-note">
              Billing customer:{" "}
              <strong>
                {customers.find(
                  (customer) =>
                    customer.id === Number(selectedCustomerId)
                )?.name ?? "Selected customer"}
              </strong>
            </p>
          )}
        </div>

        <div className="sales-form-row sales-entry-grid">
          <label className="sales-entry-field sales-entry-medicine" htmlFor="sale-medicine">
            Medicine
          <select
            id="sale-medicine"
            aria-label="Select medicine"
            value={selectedId}
            onChange={(event) => setSelectedId(event.target.value)}
            disabled={busy}
          >
            <option value="">Select medicine</option>

            {medicines.filter(medicine => medicine.isActive).map((medicine) => {
              const stock = getAvailableStock(medicine.id);

              return (
                <option
                  key={medicine.id}
                  value={medicine.id}
                  disabled={stock === 0}
                >
                  {medicine.name} — Stock: {stock}
                </option>
              );
            })}
          </select>
          </label>

          <label className="sales-entry-field" htmlFor="sale-quantity">
            Quantity
          <input
            id="sale-quantity"
            aria-label="Quantity"
            type="number"
            min={1}
            step={1}
            value={quantity}
            disabled={busy}
            onChange={(event) =>
              setQuantity(Number(event.target.value))
            }
          />
          </label>

          <label className="sales-entry-field sales-discount-field" htmlFor="sale-discount">
            Discount %
            <input id="sale-discount" type="number" min={0} max={100} step="0.01"
              value={discountPercentage} disabled={busy}
              onChange={event => setDiscountPercentage(Number(event.target.value))} />
          </label>

          <button
            className="sales-add-to-cart"
            type="button"
            onClick={addToCart}
            disabled={busy || !selectedId}
          >
            Add to Cart
          </button>
        </div>

        {selectedId && (
          <p className="sales-note">
            Available stock:{" "}
            <strong>{getAvailableStock(Number(selectedId))}</strong>{" "}
            unit(s). Estimated unit price: ₹
            {getEstimatedUnitPrice(Number(selectedId)).toFixed(2)}
          </p>
        )}

        <div className="sales-section-header"><h3>Cart Items</h3>
          {cart.length > 0 && <button type="button" className="sales-secondary-button" onClick={() => setCart([])} disabled={busy}>Clear cart</button>}
        </div>

        {cart.length === 0 ? (
          <p className="sales-empty">
            Your cart is empty. Select a medicine to begin billing.
          </p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Medicine</th>
                <th>Available Stock</th>
                <th>Quantity</th>
                <th>Estimated Unit Price</th>
                <th>GST %</th>
                <th>Discount %</th>
                <th>Est. GST Included</th>
                <th>Estimated Total</th>
                <th>Action</th>
              </tr>
            </thead>

            <tbody>
              {cart.map((item) => {
                const medicine = medicines.find(
                  (current) => current.id === item.medicineId
                );

                const unitPrice = getEstimatedUnitPrice(item.medicineId);
                const line = estimateLine(item);

                return (
                  <tr key={item.medicineId}>
                    <td>{medicine?.name ?? "Unknown"}</td>

                    <td>{getAvailableStock(item.medicineId)}</td>

                    <td>
                      <input
                        aria-label={`Quantity for ${
                          medicine?.name ?? "medicine"
                        }`}
                        type="number"
                        min={1}
                        max={getAvailableStock(item.medicineId)}
                        step={1}
                        value={item.quantity}
                        disabled={busy}
                        onChange={(event) =>
                          updateQuantity(
                            item.medicineId,
                            Number(event.target.value)
                          )
                        }
                      />
                    </td>

                    <td>₹{unitPrice.toFixed(2)}</td>

                    <td>{billingGstEnabled ? medicine?.gstPercentage ?? 0 : 0}%</td>
                    <td><input type="number" aria-label={`Discount for ${medicine?.name ?? "medicine"}`}
                      min={0} max={100} step="0.01" value={item.discountPercentage}
                      disabled={busy} onChange={event => updateDiscount(item.medicineId, Number(event.target.value))} /></td>
                    <td>{money(line.gst)}</td>
                    <td>{money(line.net)}</td>

                    <td>
                      <button
                        type="button"
                        className="sales-remove-button"
                        disabled={busy}
                        onClick={() =>
                          removeFromCart(item.medicineId)
                        }
                      >
                        Remove
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}

        <div className="sales-estimated-breakdown">
          <div><span>Subtotal (GST included)</span><strong>{money(estimated.gross)}</strong></div>
          <div><span>Discount</span><strong>−{money(estimated.discount)}</strong></div>
          <div><span>Taxable value</span><strong>{money(estimated.taxable)}</strong></div>
          <div><span>GST included</span><strong>{money(estimated.gst)}</strong></div>
          <div className="sales-estimated-grand"><span>Estimated payable</span><strong>{money(estimated.net)}</strong></div>
        </div>

        <p className="sales-note">
          The displayed total is an estimate based on the earliest-expiring
          eligible batch. The backend determines actual batch allocation
          and final prices. Inventory can change if another user completes
          a sale.
        </p>

        </fieldset>
        <button
          type="button"
          disabled={busy || (!pendingCheckout && cart.length === 0)}
          onClick={checkout}
        >
          {busy ? "Processing Sale..." : pendingCheckout ? "Retry pending checkout" : "Complete Sale"}
        </button>
      </section>}
      {paymentSale && paymentSale.id !== invoiceToPrint?.id && <section className="sales-panel no-print">
        <div className="sales-section-header"><h2>Invoice {paymentSale.invoiceNumber}</h2>
          <button type="button" onClick={() => setPaymentSale(null)}>Close payments</button></div>
        <SalePayments key={paymentSale.id} saleId={paymentSale.id} saleStatus={paymentSale.status} onChange={updatePaymentSummary} onInventoryChanged={refreshInventoryAfterReturn} />
      </section>}

      {/* Printable invoice */}
      {invoiceToPrint && (
        <section className="sales-panel print-invoice">
          <div className="invoice-header">
            <div>
              <h2>{receiptSettings?.pharmacyName ?? "Pharmacy Management"}</h2>
              {receiptSettings && <><p>{receiptSettings.address}</p><p>{receiptSettings.phoneNumber}</p>{receiptSettings.gstin && <p>GSTIN: {receiptSettings.gstin}</p>}</>}
              <p>Sales Invoice</p>
            </div>

            <div className="invoice-meta">
              <p>
                <strong>Invoice:</strong>{" "}
                {invoiceToPrint.invoiceNumber}
              </p>

              <p>
                <strong>Date:</strong>{" "}
                {new Date(
                  invoiceToPrint.createdAtUtc
                ).toLocaleString()}
              </p>

              <p>
                <strong>Customer:</strong>{" "}
                {invoiceToPrint.customerName ?? "Walk-in Customer"}
              </p>

              {(receiptSettings?.receiptShowCustomerPhone ?? true) && invoiceToPrint.customerPhone && (
                <p>
                  <strong>Phone:</strong>{" "}
                  {invoiceToPrint.customerPhone}
                </p>
              )}
            </div>
          </div>

          <hr />

          <SaleCancellation key={invoiceToPrint.id} sale={invoiceToPrint} onCancelled={updated => {
            setInvoiceToPrint(updated);
            setPaymentSale(current => current?.id === updated.id ? updated : current);
            setSales(current => current.map(sale => sale.id === updated.id ? updated : sale));
            void refreshInventoryAfterReturn().catch(() => { setError(true); setMessage("Sale cancelled. Refresh inventory before billing again."); });
          }} />
          <h3>Invoice Items</h3>

          <table className="invoice-table">
            <thead>
              <tr>
                <th>Medicine</th>
                <th>Batch Number</th>
                <th>Expiry Date</th>
                <th>Qty</th>
                <th>Unit Price</th>
                <th>HSN</th>
                <th>GST %</th>
                <th>Discount</th>
                <th>GST Included</th>
                <th>Line Total</th>
              </tr>
            </thead>

            <tbody>
              {invoiceToPrint.items.map((item, index) => (
                <tr
                  key={`${item.medicineId}-${item.batchNumber}-${index}`}
                >
                  <td>{item.medicineName}</td>
                  <td>{item.batchNumber}</td>
                  <td>{item.expiryDate}</td>
                  <td>{item.quantity}</td>
                  <td>{money(item.unitPrice)}</td>
                  <td>{item.hsnCode ?? "—"}</td>
                  <td>{item.gstPercentage}%</td>
                  <td>{money(item.discountAmount)}</td>
                  <td>{money(item.gstAmount)}</td>
                  <td>{money(item.lineTotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="sales-estimated-breakdown">
            <div><span>Subtotal (GST included)</span><strong>{money(invoiceToPrint.subtotalAmount)}</strong></div>
            <div><span>Discount</span><strong>−{money(invoiceToPrint.discountAmount)}</strong></div>
            <div><span>Taxable value</span><strong>{money(invoiceToPrint.taxableAmount)}</strong></div>
            <div><span>GST included</span><strong>{money(invoiceToPrint.gstAmount)}</strong></div>
            <div className="sales-estimated-grand"><span>Total payable</span><strong>{money(invoiceToPrint.totalAmount)}</strong></div>
          </div>

          <p className="invoice-thank-you">
            {receiptSettings?.receiptFooter ?? "Thank you for your purchase!"}
          </p>
          <SalePayments key={`${invoiceToPrint.id}:${invoiceToPrint.status}`} saleId={invoiceToPrint.id} saleStatus={invoiceToPrint.status} onChange={updatePaymentSummary} onInventoryChanged={refreshInventoryAfterReturn} printHistory={receiptSettings?.receiptShowPaymentHistory ?? true} />

          <div className="invoice-actions no-print">
            <button
              type="button"
              onClick={() => window.print()}
            >
              Print Invoice
            </button>

            <Link
              to={`/sales/${invoiceToPrint.id}/invoice`}
              className="sales-secondary-button"
            >
              Open Full Invoice
            </Link>

            <button
              type="button"
              className="sales-secondary-button"
              onClick={() => setInvoiceToPrint(null)}
            >
              Close Invoice
            </button>
          </div>
        </section>
      )}

      {/* Sales history */}
      <section className="sales-panel sales-history-panel">
        <div className="sales-section-header">
          <div><h2>Sales history <span className="sales-history-count">{sales.length}</span></h2>
            <p className="sales-history-description">Invoices, collections and outstanding balances</p></div>

          <button
            type="button"
            className="sales-secondary-button"
            onClick={() => void loadData()}
            disabled={busy}
          >
            Refresh
          </button>
        </div>

        {sales.length === 0 ? (
          <p className="sales-empty">
            No sales found. Completed invoices will appear here.
          </p>
        ) : (
          <div className="sales-history-scroll" role="region" aria-label="Sales history" tabIndex={0}>
          <table className="sales-history-table">
            <caption className="sales-visually-hidden">Sales invoices with payments, balances and invoice actions</caption>
            <thead>
              <tr>
                <th scope="col">Invoice / Date</th>
                <th scope="col">Customer</th>
                <th scope="col" className="sales-number">Total</th>
                <th scope="col" className="sales-number">Paid</th>
                <th scope="col" className="sales-number">Balance</th>
                <th scope="col">Status</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>

            <tbody>
              {sales.map((sale) => (
                <tr key={sale.id}>
                  <td>
                    <Link className="sales-invoice-reference" title={sale.invoiceNumber} to={`/sales/${sale.id}/invoice`}>{sale.invoiceNumber}</Link>
                    <time className="sales-cell-detail" dateTime={sale.createdAtUtc}>
                      {new Date(sale.createdAtUtc).toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" })}
                      {" · "}{new Date(sale.createdAtUtc).toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit" })}
                    </time>
                  </td>

                  <td>
                    {sale.customerName ?? "Walk-in Customer"}
                    <span className="sales-cell-detail">{sale.items.length} {sale.items.length === 1 ? "line item" : "line items"}</span>
                  </td>

                  <td className="sales-number"><strong>{money(sale.totalAmount)}</strong>
                    {sale.returnedAmount > 0 && <span className="sales-cell-detail">Returned {money(sale.returnedAmount)}</span>}
                  </td>
                  <td className="sales-number">{money(sale.paidAmount)}</td>
                  <td className={`sales-number ${sale.balanceAmount > 0 ? "sales-balance-due" : ""}`}>
                    {money(sale.balanceAmount)}
                    {sale.refundDue > 0 && <span className="sales-cell-detail sales-refund-due">Refund due {money(sale.refundDue)}</span>}
                  </td>
                  <td><span className={`sales-payment-badge sales-payment-${sale.status === "Cancelled" ? "cancelled" : sale.paymentStatus.toLowerCase()}`}>
                    {sale.status === "Cancelled" ? "Cancelled" : sale.paymentStatus === "PartiallyPaid" ? "Partially paid" : sale.paymentStatus}
                  </span></td>
                  <td><div className="sales-history-actions">
                    <button type="button" className="sales-history-payment no-print" aria-label={`Payments for ${sale.invoiceNumber}`} onClick={() => {
                      if (invoiceToPrint?.id !== sale.id) setPaymentSale(sale);
                      document.getElementById(`sale-payments-${sale.id}`)?.scrollIntoView({ behavior: "smooth" });
                    }}>Payments</button>{" "}
                    <Link
                      to={`/sales/${sale.id}/invoice`}
                      className="sales-history-invoice no-print"
                      aria-label={`View or print invoice ${sale.invoiceNumber}`}
                    >
                      View invoice
                    </Link>
                  </div></td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        )}
      </section>
    </main>
  );
}
