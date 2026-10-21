
import { useCallback, useEffect, useState } from "react";
import {
  customersApi,
  type Customer,
  type CustomerRequest,
} from "../api/customers";
import "./CustomersPage.css";

const emptyForm = {
  name: "",
  phone: "",
  email: "",
  address: "",
};

export default function CustomersPage() {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState(false);

  const loadCustomers = useCallback(async () => {
    setLoading(true);

    try {
      const data = await customersApi.getAll();
      setCustomers(data);
      setError(false);
    } catch (err) {
      setError(true);
      setMessage(
        err instanceof Error
          ? err.message
          : "Unable to load customers."
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    Promise.resolve().then(() => {
    void loadCustomers();
    });
  }, [loadCustomers]);

  function updateField(
    field: keyof typeof emptyForm,
    value: string
  ) {
    setForm((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function resetForm() {
    setForm({ ...emptyForm });
    setEditingId(null);
  }

  function editCustomer(customer: Customer) {
    setEditingId(customer.id);
    setForm({
      name: customer.name,
      phone: customer.phone ?? "",
      email: customer.email ?? "",
      address: customer.address ?? "",
    });

    setMessage("");
    setError(false);

    window.scrollTo({
      top: 0,
      behavior: "smooth",
    });
  }

  async function saveCustomer(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!form.name.trim()) {
      setError(true);
      setMessage("Customer name is required.");
      return;
    }

    const request: CustomerRequest = {
      name: form.name.trim(),
      phone: form.phone.trim() || null,
      email: form.email.trim() || null,
      address: form.address.trim() || null,
    };

    setSaving(true);
    setMessage("");
    setError(false);

    try {
      if (editingId !== null) {
        await customersApi.update(editingId, request);
        setMessage("Customer updated successfully.");
      } else {
        await customersApi.create(request);
        setMessage("Customer registered successfully.");
      }

      resetForm();
      await loadCustomers();
    } catch (err) {
      setError(true);
      setMessage(
        err instanceof Error
          ? err.message
          : "Unable to save customer."
      );
    } finally {
      setSaving(false);
    }
  }

  async function deactivateCustomer(customer: Customer) {
    const confirmed = window.confirm(
      `Deactivate customer "${customer.name}"?`
    );

    if (!confirmed) {
      return;
    }

    setMessage("");
    setError(false);

    try {
      await customersApi.deactivate(customer.id);

      if (editingId === customer.id) {
        resetForm();
      }

      setMessage(`${customer.name} was deactivated.`);
      await loadCustomers();
    } catch (err) {
      setError(true);
      setMessage(
        err instanceof Error
          ? err.message
          : "Unable to deactivate customer."
      );
    }
  }

  const filteredCustomers = customers.filter((customer) => {
    const term = search.trim().toLowerCase();

    return (
      customer.name.toLowerCase().includes(term) ||
      (customer.phone ?? "").toLowerCase().includes(term) ||
      (customer.email ?? "").toLowerCase().includes(term)
    );
  });

  return (
    <main className="customers-page">
      <h1>Customer Management</h1>

      {message && (
        <p
          className={`customer-message ${
            error ? "customer-error" : "customer-success"
          }`}
          role="status"
        >
          {message}
        </p>
      )}

      <section className="customer-panel">
        <h2>
          {editingId !== null ? "Edit Customer" : "Register Customer"}
        </h2>

        <form onSubmit={saveCustomer}>
          <div className="customer-form-grid">
            <label>
              Customer Name *
              <input
                type="text"
                maxLength={150}
                value={form.name}
                onChange={(event) =>
                  updateField("name", event.target.value)
                }
                placeholder="Enter customer name"
                required
              />
            </label>

            <label>
              Phone Number
              <input
                type="tel"
                maxLength={20}
                value={form.phone}
                onChange={(event) =>
                  updateField("phone", event.target.value)
                }
                placeholder="Enter phone number"
              />
            </label>

            <label>
              Email
              <input
                type="email"
                maxLength={254}
                value={form.email}
                onChange={(event) =>
                  updateField("email", event.target.value)
                }
                placeholder="Enter email address"
              />
            </label>

            <label>
              Address
              <input
                type="text"
                maxLength={500}
                value={form.address}
                onChange={(event) =>
                  updateField("address", event.target.value)
                }
                placeholder="Enter address"
              />
            </label>
          </div>

          <div className="customer-form-actions">
            <button type="submit" disabled={saving}>
              {saving
                ? "Saving..."
                : editingId !== null
                  ? "Update Customer"
                  : "Register Customer"}
            </button>

            {editingId !== null && (
              <button
                type="button"
                className="customer-secondary-button"
                onClick={resetForm}
                disabled={saving}
              >
                Cancel Edit
              </button>
            )}
          </div>
        </form>
      </section>

      <section className="customer-panel">
        <div className="customer-list-header">
          <h2>Customer List</h2>

          <button
            type="button"
            className="customer-secondary-button"
            onClick={() => void loadCustomers()}
            disabled={loading || saving}
          >
            Refresh
          </button>
        </div>

        <input
          className="customer-search"
          type="search"
          placeholder="Search by name, phone, or email..."
          aria-label="Search customers"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />

        {loading ? (
          <p>Loading customers...</p>
        ) : filteredCustomers.length === 0 ? (
          <p className="customer-empty">
            No customers found.
          </p>
        ) : (
          <div className="customer-table-wrapper">
            <table className="customer-table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Phone</th>
                  <th>Email</th>
                  <th>Address</th>
                  <th>Actions</th>
                </tr>
              </thead>

              <tbody>
                {filteredCustomers.map((customer) => (
                  <tr key={customer.id}>
                    <td>{customer.name}</td>
                    <td>{customer.phone ?? "—"}</td>
                    <td>{customer.email ?? "—"}</td>
                    <td>{customer.address ?? "—"}</td>
                    <td>
                      <div className="customer-row-actions">
                        <button
                          type="button"
                          className="customer-secondary-button"
                          onClick={() => editCustomer(customer)}
                        >
                          Edit
                        </button>

                        <button
                          type="button"
                          className="customer-deactivate-button"
                          onClick={() =>
                            void deactivateCustomer(customer)
                          }
                        >
                          Deactivate
                        </button>
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
