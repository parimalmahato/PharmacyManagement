import { useEffect, useMemo, useState } from "react";
import {
  suppliersApi,
  type Supplier,
} from "../api/suppliers";
import {
  medicinesApi,
  purchasesApi,
  type MedicineOption,
  type Purchase,
  type PurchaseItem,
} from "../api/purchases";
import "./PurchasingPage.css";
import InvoiceAiImport from "../components/InvoiceAiImport";

const today = () => {
  const date = new Date();
  const localDate = new Date(
    date.getTime() - date.getTimezoneOffset() * 60_000
  );
  return localDate.toISOString().slice(0, 10);
};

const emptyItem = (): PurchaseItem => ({
  medicineId: 0,
  batchNumber: "",
  expiryDate: "",
  quantity: 1,
  purchasePrice: 0,
  sellingPrice: 0,
});

export default function PurchasingPage() {
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [medicines, setMedicines] = useState<MedicineOption[]>([]);
  const [purchases, setPurchases] = useState<Purchase[]>([]);

  const [supplierName, setSupplierName] = useState("");
  const [supplierPhone, setSupplierPhone] = useState("");
  const [supplierEmail, setSupplierEmail] = useState("");
  const [supplierAddress, setSupplierAddress] = useState("");

  const [supplierId, setSupplierId] = useState("");
  const [invoiceNumber, setInvoiceNumber] = useState("");
  const [invoiceDate, setInvoiceDate] = useState(today());
  const [items, setItems] = useState<PurchaseItem[]>([emptyItem()]);

  const [loading, setLoading] = useState(true);
  const [savingSupplier, setSavingSupplier] = useState(false);
  const [savingPurchase, setSavingPurchase] = useState(false);
  const [receivingId, setReceivingId] = useState<number | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  const totalAmount = useMemo(
    () =>
      items.reduce(
        (total, item) =>
          total + Number(item.quantity) * Number(item.purchasePrice),
        0
      ),
    [items]
  );

  async function loadData() {
    setLoading(true);
    setError("");

    try {
      const [supplierData, medicineData, purchaseData] =
        await Promise.all([
          suppliersApi.getAll(),
          medicinesApi.getAll(),
          purchasesApi.getAll(),
        ]);

      setSuppliers(supplierData);
      setMedicines(medicineData.filter((m) => m.isActive));
      setPurchases(purchaseData);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Unable to load purchasing data."
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
     Promise.resolve().then(() => {
        void loadData();
     });
  }, []);

  function updateItem(
    index: number,
    field: keyof PurchaseItem,
    value: string
  ) {
    setItems((current) =>
      current.map((item, itemIndex) => {
        if (itemIndex !== index) return item;

        if (field === "medicineId") {
          return { ...item, medicineId: Number(value) };
        }

        if (field === "quantity") {
          return { ...item, quantity: Number(value) };
        }

        if (field === "purchasePrice" || field === "sellingPrice") {
          return { ...item, [field]: Number(value) };
        }

        return { ...item, [field]: value };
      })
    );
  }

  async function handleCreateSupplier(
    event: React.FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();
    setError("");
    setMessage("");
    setSavingSupplier(true);

    try {
      const supplier = await suppliersApi.create({
        name: supplierName.trim(),
        phone: supplierPhone.trim(),
        email: supplierEmail.trim(),
        address: supplierAddress.trim(),
      });

      setSuppliers((current) =>
        [...current, supplier].sort((a, b) =>
          a.name.localeCompare(b.name)
        )
      );
      setSupplierId(String(supplier.id));

      setSupplierName("");
      setSupplierPhone("");
      setSupplierEmail("");
      setSupplierAddress("");

      setMessage("Supplier created successfully.");
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Unable to create supplier."
      );
    } finally {
      setSavingSupplier(false);
    }
  }

  async function handleCreatePurchase(
    event: React.FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();
    setError("");
    setMessage("");

    if (!supplierId) {
      setError("Please select a supplier.");
      return;
    }

    if (
      items.some(
        (item) =>
          item.medicineId <= 0 ||
          !item.batchNumber.trim() ||
          !item.expiryDate ||
          item.expiryDate <= today() ||
          item.quantity <= 0 ||
          item.purchasePrice < 0 ||
          item.sellingPrice < 0
      )
    ) {
      setError(
        "Complete every item and provide a future expiry date, valid quantity, and non-negative prices."
      );
      return;
    }

    const batchKeys = items.map(
      (item) =>
        `${item.medicineId}:${item.batchNumber.trim().toLowerCase()}`
    );

    if (new Set(batchKeys).size !== batchKeys.length) {
      setError(
        "The same medicine and batch number cannot appear more than once in this invoice."
      );
      return;
    }

    setSavingPurchase(true);

    try {
      await purchasesApi.create({
        supplierId: Number(supplierId),
        invoiceNumber: invoiceNumber.trim(),
        invoiceDate,
        items: items.map((item) => ({
          ...item,
          batchNumber: item.batchNumber.trim(),
        })),
      });

      setInvoiceNumber("");
      setInvoiceDate(today());
      setItems([emptyItem()]);

      setMessage(
        "Purchase draft created. Inventory has not changed yet."
      );

      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Unable to create purchase."
      );
    } finally {
      setSavingPurchase(false);
    }
  }

  async function handleReceive(purchase: Purchase) {
    if (purchase.status !== "Draft") return;

    const confirmed = window.confirm(
      `Receive purchase ${purchase.invoiceNumber}? This will update inventory.`
    );

    if (!confirmed) return;

    setError("");
    setMessage("");
    setReceivingId(purchase.id);

    try {
      const result = await purchasesApi.receive(purchase.id);
      setMessage(result.message);
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Unable to receive purchase."
      );
    } finally {
      setReceivingId(null);
    }
  }

  const currency = (value: number) =>
    new Intl.NumberFormat("en-IN", {
      style: "currency",
      currency: "INR",
    }).format(value);

  if (loading) {
    return <main className="purchasing-page">Loading purchasing data…</main>;
  }

  return (
    <main className="purchasing-page">
      <header className="purchasing-header">
        <div>
          <h1>Purchasing</h1>
          <p>Manage suppliers, purchase invoices and stock receiving.</p>
        </div>
        <button type="button" className="secondary-button" onClick={() => void loadData()}>
          Refresh
        </button>
      </header>

      {error && <div className="notice error-notice">{error}</div>}
      {message && <div className="notice success-notice">{message}</div>}

      <InvoiceAiImport medicines={medicines} onUse={(number, date, importedItems) => {
        setInvoiceNumber(number); setInvoiceDate(date); setItems(importedItems);
        setSupplierId(""); setError(""); setMessage("Reviewed values loaded. Select the supplier, check all fields and create the draft when ready. Inventory has not changed.");
      }} />
      <section className="purchase-card">
        <h2>Add supplier</h2>

        <form onSubmit={handleCreateSupplier} className="supplier-form">
          <label>
            Supplier name *
            <input
              required
              maxLength={150}
              value={supplierName}
              onChange={(e) => setSupplierName(e.target.value)}
              placeholder="ABC Pharma Distributors"
            />
          </label>

          <label>
            Phone
            <input
              maxLength={30}
              value={supplierPhone}
              onChange={(e) => setSupplierPhone(e.target.value)}
              placeholder="Phone number"
            />
          </label>

          <label>
            Email
            <input
              type="email"
              maxLength={254}
              value={supplierEmail}
              onChange={(e) => setSupplierEmail(e.target.value)}
              placeholder="supplier@example.com"
            />
          </label>

          <label>
            Address
            <input
              maxLength={500}
              value={supplierAddress}
              onChange={(e) => setSupplierAddress(e.target.value)}
              placeholder="Supplier address"
            />
          </label>

          <button disabled={savingSupplier} type="submit">
            {savingSupplier ? "Saving…" : "Add supplier"}
          </button>
        </form>
      </section>

      <section className="purchase-card">
        <h2>Create purchase invoice</h2>

        <form onSubmit={handleCreatePurchase}>
          <div className="purchase-form-grid">
            <label>
              Supplier *
              <select
                required
                value={supplierId}
                onChange={(e) => setSupplierId(e.target.value)}
              >
                <option value="">Select supplier</option>
                {suppliers
                  .filter((supplier) => supplier.isActive)
                  .map((supplier) => (
                    <option key={supplier.id} value={supplier.id}>
                      {supplier.name}
                    </option>
                  ))}
              </select>
            </label>

            <label>
              Supplier invoice number *
              <input
                required
                maxLength={100}
                value={invoiceNumber}
                onChange={(e) => setInvoiceNumber(e.target.value)}
                placeholder="SUP-INV-1001"
              />
            </label>

            <label>
              Invoice date *
              <input
                type="date"
                required
                max={today()}
                value={invoiceDate}
                onChange={(e) => setInvoiceDate(e.target.value)}
              />
            </label>
          </div>

          <div className="items-heading">
            <h3>Medicine items</h3>
            <button
              type="button"
              className="secondary-button"
              onClick={() => setItems((current) => [...current, emptyItem()])}
            >
              + Add item
            </button>
          </div>

          {items.map((item, index) => (
            <div className="purchase-item" key={index}>
              <div className="purchase-item-heading">
                <strong>Item {index + 1}</strong>
                {items.length > 1 && (
                  <button
                    type="button"
                    className="remove-button"
                    onClick={() =>
                      setItems((current) =>
                        current.filter((_, i) => i !== index)
                      )
                    }
                  >
                    Remove
                  </button>
                )}
              </div>

              <div className="purchase-item-grid">
                <label>
                  Medicine *
                  <select
                    required
                    value={item.medicineId || ""}
                    onChange={(e) =>
                      updateItem(index, "medicineId", e.target.value)
                    }
                  >
                    <option value="">Select medicine</option>
                    {medicines.map((medicine) => (
                      <option key={medicine.id} value={medicine.id}>
                        {medicine.name}
                      </option>
                    ))}
                  </select>
                </label>

                <label>
                  Batch number *
                  <input
                    required
                    maxLength={50}
                    value={item.batchNumber}
                    onChange={(e) =>
                      updateItem(index, "batchNumber", e.target.value)
                    }
                    placeholder="PCM-2026-01"
                  />
                </label>

                <label>
                  Expiry date *
                  <input
                    required
                    type="date"
                    min={today()}
                    value={item.expiryDate}
                    onChange={(e) =>
                      updateItem(index, "expiryDate", e.target.value)
                    }
                  />
                </label>

                <label>
                  Quantity *
                  <input
                    required
                    type="number"
                    min="1"
                    step="1"
                    value={item.quantity}
                    onChange={(e) =>
                      updateItem(index, "quantity", e.target.value)
                    }
                  />
                </label>

                <label>
                  Purchase price (₹) *
                  <input
                    required
                    type="number"
                    min="0"
                    step="0.01"
                    value={item.purchasePrice}
                    onChange={(e) =>
                      updateItem(index, "purchasePrice", e.target.value)
                    }
                  />
                </label>

                <label>
                  Selling price (₹) *
                  <input
                    required
                    type="number"
                    min="0"
                    step="0.01"
                    value={item.sellingPrice}
                    onChange={(e) =>
                      updateItem(index, "sellingPrice", e.target.value)
                    }
                  />
                </label>
              </div>
            </div>
          ))}

          <div className="purchase-total">
            <span>Estimated invoice total</span>
            <strong>{currency(totalAmount)}</strong>
          </div>

          <button
            type="submit"
            disabled={savingPurchase || suppliers.length === 0 || medicines.length === 0}
          >
            {savingPurchase ? "Creating…" : "Create draft purchase"}
          </button>

          {(suppliers.length === 0 || medicines.length === 0) && (
            <p className="form-hint">
              Add at least one active supplier and one active medicine before
              creating a purchase.
            </p>
          )}
        </form>
      </section>

      <section className="purchase-card">
        <h2>Purchase history</h2>

        {purchases.length === 0 ? (
          <p>No purchase invoices found.</p>
        ) : (
          <div className="table-wrapper">
            <table className="purchase-table">
              <thead>
                <tr>
                  <th>Invoice</th>
                  <th>Date</th>
                  <th>Supplier</th>
                  <th>Items</th>
                  <th>Total</th>
                  <th>Status</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {purchases.map((purchase) => {
                  const supplier = suppliers.find(
                    (x) => x.id === purchase.supplierId
                  );

                  return (
                    <tr key={purchase.id}>
                      <td>{purchase.invoiceNumber}</td>
                      <td>{purchase.invoiceDate}</td>
                      <td>{supplier?.name ?? `Supplier #${purchase.supplierId}`}</td>
                      <td>{purchase.items.length}</td>
                      <td>{currency(purchase.totalAmount)}</td>
                      <td>
                        <span
                          className={`status-pill status-${purchase.status.toLowerCase()}`}
                        >
                          {purchase.status}
                        </span>
                      </td>
                      <td>
                        {purchase.status === "Draft" ? (
                          <button
                            type="button"
                            disabled={receivingId === purchase.id}
                            onClick={() => void handleReceive(purchase)}
                          >
                            {receivingId === purchase.id
                              ? "Receiving…"
                              : "Receive stock"}
                          </button>
                        ) : (
                          <span>—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  );
}
