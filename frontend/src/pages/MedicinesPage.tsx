import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import {
  medicineApi,
  type Medicine,
  type MedicineRequest,
} from "../api/medicines";
import "./MedicinesPage.css";

type MedicineForm = {
  name: string;
  genericName: string;
  manufacturer: string;
  category: string;
  hsnCode: string;
  gstPercentage: string;
};

const newForm = (): MedicineForm => ({
  name: "",
  genericName: "",
  manufacturer: "",
  category: "",
  hsnCode: "",
  gstPercentage: "",
});

export default function MedicinesPage() {
  const [medicines, setMedicines] = useState<Medicine[]>([]);
  const [search, setSearch] = useState("");
  const [form, setForm] = useState<MedicineForm>(newForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [deactivatingId, setDeactivatingId] = useState<number | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");

  const loadMedicines = useCallback(async () => {
    setLoading(true);
    try {
      const data = await medicineApi.getAll();
      setMedicines(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load medicines.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    Promise.resolve().then(() => {
    void loadMedicines();
    });
  }, [loadMedicines]);

  const filteredMedicines = useMemo(() => {
    const query = search.trim().toLowerCase();
    return medicines.filter((medicine) =>
      [
        medicine.name,
        medicine.genericName,
        medicine.manufacturer,
        medicine.category,
        medicine.hsnCode ?? "",
        String(medicine.gstPercentage),
      ].some((value) => value.toLowerCase().includes(query))
    );
  }, [medicines, search]);

  function openCreateForm() {
    setEditingId(null);
    setForm(newForm());
    setError("");
    setMessage("");
    setFormOpen(true);
  }

  function openEditForm(medicine: Medicine) {
    setEditingId(medicine.id);
    setForm({
      name: medicine.name,
      genericName: medicine.genericName,
      manufacturer: medicine.manufacturer,
      category: medicine.category,
      hsnCode: medicine.hsnCode ?? "",
      gstPercentage: String(medicine.gstPercentage ?? 0),
    });
    setError("");
    setMessage("");
    setFormOpen(true);
  }

  function updateField(field: keyof MedicineForm, value: string) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  function toRequest(): MedicineRequest {
    const hsnCode = form.hsnCode.trim();
    const gstText = form.gstPercentage.trim();
    if (!gstText) {
      throw new Error("Please enter a GST percentage. Use 0 only for a verified zero-rated or exempt medicine.");
    }
    const gstPercentage = Number(gstText);
    if (!Number.isFinite(gstPercentage) || gstPercentage < 0 || gstPercentage > 100) {
      throw new Error("GST percentage must be between 0 and 100.");
    }
    if (!/^\d{1,2}(\.\d{1,2})?$/.test(gstText) && gstPercentage !== 100) {
      throw new Error("GST percentage can have at most two decimal places.");
    }
    if (hsnCode && !/^\d{4}(\d{2})?(\d{2})?$/.test(hsnCode)) {
      throw new Error("HSN code must contain 4, 6, or 8 digits.");
    }
    return {
      name: form.name.trim(),
      genericName: form.genericName.trim(),
      manufacturer: form.manufacturer.trim(),
      category: form.category.trim(),
      hsnCode: hsnCode || null,
      gstPercentage,
    };
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (saving) return;
    setError("");
    setMessage("");
    let payload: MedicineRequest;
    try {
      payload = toRequest();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Invalid medicine details.");
      return;
    }
    setSaving(true);
    try {
      if (editingId === null) {
        await medicineApi.create(payload);
        setMessage("Medicine added successfully.");
      } else {
        await medicineApi.update(editingId, payload);
        setMessage("Medicine updated successfully.");
      }
      setFormOpen(false);
      setForm(newForm());
      setEditingId(null);
      await loadMedicines();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to save medicine.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDeactivate(medicine: Medicine) {
    if (!window.confirm(`Deactivate ${medicine.name}? It will no longer be available as an active medicine.`)) return;
    setError("");
    setMessage("");
    setDeactivatingId(medicine.id);
    try {
      await medicineApi.deactivate(medicine.id);
      setMessage(`${medicine.name} was deactivated.`);
      await loadMedicines();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to deactivate medicine.");
    } finally {
      setDeactivatingId(null);
    }
  }

  return (
    <main className="medicines-page">
      <header className="page-heading">
        <div>
          <p className="eyebrow">INVENTORY MANAGEMENT</p>
          <h1>Medicines</h1>
          <p className="page-description">Manage your pharmacy's medicine catalogue and GST configuration.</p>
        </div>
        <button type="button" className="primary-button" onClick={openCreateForm}>+ Add medicine</button>
      </header>

      <section className="medicine-toolbar">
        <label className="search-field">
          <span>Search medicines</span>
          <input type="search" placeholder="Name, manufacturer, HSN code..." value={search} onChange={(event) => setSearch(event.target.value)} />
        </label>
        <div className="medicine-count"><strong>{filteredMedicines.length}</strong> medicines shown</div>
      </section>

      {message && <p className="success-message" role="status">{message}</p>}
      {error && <p className="error-message" role="alert">{error}</p>}

      {formOpen && (
        <section className="medicine-form-card">
          <h2>{editingId === null ? "Add medicine" : "Edit medicine"}</h2>
          <form onSubmit={(event) => void handleSubmit(event)}>
            <div className="form-grid">
              <label>Medicine name
                <input required maxLength={150} value={form.name} onChange={(e) => updateField("name", e.target.value)} />
              </label>
              <label>Generic name
                <input required maxLength={150} value={form.genericName} onChange={(e) => updateField("genericName", e.target.value)} />
              </label>
              <label>Manufacturer
                <input required maxLength={150} value={form.manufacturer} onChange={(e) => updateField("manufacturer", e.target.value)} />
              </label>
              <label>Category
                <input required maxLength={100} value={form.category} onChange={(e) => updateField("category", e.target.value)} />
              </label>
              <label>HSN code
                <input inputMode="numeric" maxLength={8} pattern="[0-9]{4}([0-9]{2})?([0-9]{2})?" title="Enter a 4, 6, or 8 digit HSN code" placeholder="e.g. 3004" value={form.hsnCode} onChange={(e) => updateField("hsnCode", e.target.value)} />
              </label>
              <label>GST percentage (%)
                <input type="number" required min="0" max="100" step="0.01" placeholder="Enter verified GST rate" value={form.gstPercentage} onChange={(e) => updateField("gstPercentage", e.target.value)} />
              </label>
            </div>
            <p className="medicine-tax-note">Selling prices are GST-inclusive. Verify each medicine's applicable HSN and GST rate before billing. Zero does not automatically mean exempt.</p>
            <div className="form-actions">
              <button type="button" className="secondary-button" onClick={() => setFormOpen(false)} disabled={saving}>Cancel</button>
              <button type="submit" className="primary-button" disabled={saving}>
                {saving ? "Saving..." : editingId === null ? "Save medicine" : "Update medicine"}
              </button>
            </div>
          </form>
        </section>
      )}

      <section className="medicine-table-card">
        {loading ? (
          <p className="empty-state">Loading medicines...</p>
        ) : filteredMedicines.length === 0 ? (
          <p className="empty-state">{search ? "No medicines match your search." : "No medicines yet. Select Add medicine to get started."}</p>
        ) : (
          <div className="table-scroll">
            <table className="medicine-table">
              <thead><tr><th>Name</th><th>Generic name</th><th>Manufacturer</th><th>Category</th><th>HSN code</th><th>GST</th><th>Status</th><th>Actions</th></tr></thead>
              <tbody>
                {filteredMedicines.map((medicine) => (
                  <tr key={medicine.id}>
                    <td><strong>{medicine.name}</strong></td>
                    <td>{medicine.genericName}</td>
                    <td>{medicine.manufacturer}</td>
                    <td>{medicine.category}</td>
                    <td>{medicine.hsnCode || "—"}</td>
                    <td>{medicine.gstPercentage == null ? "Not set" : `${medicine.gstPercentage}%`}</td>
                    <td><span className={`medicine-status ${medicine.isActive ? "active" : "inactive"}`}>{medicine.isActive ? "Active" : "Inactive"}</span></td>
                    <td><div className="table-actions">
                      <button type="button" className="text-button" onClick={() => openEditForm(medicine)}>Edit</button>
                      {medicine.isActive && <button type="button" className="text-button danger" disabled={deactivatingId === medicine.id} onClick={() => void handleDeactivate(medicine)}>{deactivatingId === medicine.id ? "Deactivating..." : "Deactivate"}</button>}
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
