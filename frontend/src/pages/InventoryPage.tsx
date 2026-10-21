
import { useEffect, useState } from "react";
import {
  inventoryApi,
  type MedicineBatch,
  type MedicineOption,
  type CreateBatchRequest,
} from "../api/inventory";
import "./InventoryPage.css";

const initialForm = {
  medicineId: "",
  batchNumber: "",
  expiryDate: "",
  purchasePrice: "",
  sellingPrice: "",
  quantity: "",
};

export default function InventoryPage() {
  const [batches, setBatches] = useState<MedicineBatch[]>([]);
  const [medicines, setMedicines] = useState<MedicineOption[]>([]);
  const [form, setForm] = useState(initialForm);
  const [search, setSearch] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  async function loadData() {
    setLoading(true);
    setError("");

    try {
      const [batchData, medicineData] = await Promise.all([
        inventoryApi.getBatches(),
        inventoryApi.getMedicines(),
      ]);

      setBatches(batchData);
      setMedicines(medicineData.filter((m) => m.isActive));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load inventory.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    Promise.resolve().then(() => {
        void loadData();
    });
  }, []);

  const filteredBatches = batches.filter((batch) =>
    `${batch.medicineName} ${batch.batchNumber}`
      .toLowerCase()
      .includes(search.trim().toLowerCase())
  );

  const lowStockCount = batches.filter(
    (b) => b.isLowStock && !b.isExpired
  ).length;

  const nearExpiryCount = batches.filter(
    (b) => b.isNearExpiry && !b.isExpired
  ).length;

  const expiredCount = batches.filter((b) => b.isExpired).length;

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaving(true);
    setError("");
    setMessage("");

    try {
      const request: CreateBatchRequest = {
        medicineId: Number(form.medicineId),
        batchNumber: form.batchNumber.trim(),
        expiryDate: form.expiryDate,
        purchasePrice: Number(form.purchasePrice),
        sellingPrice: Number(form.sellingPrice),
        quantity: Number(form.quantity),
      };

      await inventoryApi.createBatch(request);

      setMessage("Batch registered successfully.");
      setForm(initialForm);
      setShowForm(false);
      await loadData();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Unable to register batch."
      );
    } finally {
      setSaving(false);
    }
  }

  return (
    <main className="inventory-page">
      <header className="inventory-heading">
        <div>
          <p className="eyebrow">PHARMACY OPERATIONS</p>
          <h1>Batch Inventory</h1>
          <p>Track stock quantities, batch numbers and expiry dates.</p>
        </div>

        <button
          className="inventory-primary"
          onClick={() => {
            setError("");
            setMessage("");
            setShowForm((value) => !value);
          }}
        >
          {showForm ? "Close form" : "+ Register batch"}
        </button>
      </header>

      <section className="inventory-summary">
        <article>
          <span>Total batches</span>
          <strong>{batches.length}</strong>
        </article>
        <article>
          <span>Low-stock batches</span>
          <strong>{lowStockCount}</strong>
        </article>
        <article>
          <span>Near expiry</span>
          <strong>{nearExpiryCount}</strong>
        </article>
        <article>
          <span>Expired batches</span>
          <strong>{expiredCount}</strong>
        </article>
      </section>

      {message && <p className="inventory-success" role="status">{message}</p>}
      {error && <p className="inventory-error" role="alert">{error}</p>}

      {showForm && (
        <section className="inventory-form-card">
          <h2>Register opening stock</h2>
          <p>
            Use this form for initial stock entry. Purchase receiving will be
            connected to the purchasing module later.
          </p>

          <form onSubmit={handleSubmit}>
            <div className="inventory-form-grid">
              <label>
                Medicine
                <select
                  required
                  value={form.medicineId}
                  onChange={(e) =>
                    setForm({ ...form, medicineId: e.target.value })
                  }
                >
                  <option value="">Select a medicine</option>
                  {medicines.map((medicine) => (
                    <option key={medicine.id} value={medicine.id}>
                      {medicine.name}
                    </option>
                  ))}
                </select>
              </label>

              <label>
                Batch number
                <input
                  required
                  maxLength={50}
                  value={form.batchNumber}
                  onChange={(e) =>
                    setForm({ ...form, batchNumber: e.target.value })
                  }
                />
              </label>

              <label>
                Expiry date
                <input
                  required
                  type="date"
                  value={form.expiryDate}
                  min={new Date().toLocaleDateString("en-CA")}
                  onChange={(e) =>
                    setForm({ ...form, expiryDate: e.target.value })
                  }
                />
              </label>

              <label>
                Purchase price per unit (₹)
                <input
                  required
                  type="number"
                  min="0"
                  step="0.01"
                  value={form.purchasePrice}
                  onChange={(e) =>
                    setForm({ ...form, purchasePrice: e.target.value })
                  }
                />
              </label>

              <label>
                Selling price per unit (₹)
                <input
                  required
                  type="number"
                  min="0"
                  step="0.01"
                  value={form.sellingPrice}
                  onChange={(e) =>
                    setForm({ ...form, sellingPrice: e.target.value })
                  }
                />
              </label>

              <label>
                Opening quantity
                <input
                  required
                  type="number"
                  min="0"
                  step="1"
                  value={form.quantity}
                  onChange={(e) =>
                    setForm({ ...form, quantity: e.target.value })
                  }
                />
              </label>
            </div>

            <div className="inventory-form-actions">
              <button
                type="button"
                className="inventory-secondary"
                onClick={() => setShowForm(false)}
                disabled={saving}
              >
                Cancel
              </button>
              <button
                type="submit"
                className="inventory-primary"
                disabled={saving || medicines.length === 0}
              >
                {saving ? "Saving..." : "Register batch"}
              </button>
            </div>
          </form>
        </section>
      )}

      <section className="inventory-table-card">
        <div className="inventory-search">
          <label htmlFor="inventory-search">Search inventory</label>
          <input
            id="inventory-search"
            type="search"
            placeholder="Medicine or batch number"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>

        {loading ? (
          <p className="inventory-empty">Loading inventory...</p>
        ) : filteredBatches.length === 0 ? (
          <p className="inventory-empty">No batches found.</p>
        ) : (
          <div className="inventory-table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Medicine</th>
                  <th>Batch</th>
                  <th>Expiry</th>
                  <th>Purchase price</th>
                  <th>Selling price</th>
                  <th>Quantity</th>
                  <th>Alert</th>
                </tr>
              </thead>
              <tbody>
                {filteredBatches.map((batch) => (
                  <tr key={batch.id}>
                    <td>{batch.medicineName}</td>
                    <td>{batch.batchNumber}</td>
                    <td>{batch.expiryDate}</td>
                    <td>₹{batch.purchasePrice.toFixed(2)}</td>
                    <td>₹{batch.sellingPrice.toFixed(2)}</td>
                    <td>{batch.quantity}</td>
                    <td>
                      <div className="inventory-alerts">
                        {batch.isExpired && (
                          <span className="alert expired">Expired</span>
                        )}
                        {!batch.isExpired && batch.isNearExpiry && (
                          <span className="alert warning">Near expiry</span>
                        )}
                        {batch.isLowStock && (
                          <span className="alert low-stock">Low stock</span>
                        )}
                        {!batch.isExpired &&
                          !batch.isNearExpiry &&
                          !batch.isLowStock && (
                            <span className="alert normal">Normal</span>
                          )}
                      </div>
                    </td>
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
