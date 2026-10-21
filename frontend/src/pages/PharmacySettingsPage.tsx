
import { useEffect, useState } from "react";

import {
  getPharmacySettings,
  updatePharmacySettings,
  type UpdatePharmacySettingsRequest,
} from "../api/pharmacySettingsApi";

const initialForm: UpdatePharmacySettingsRequest = {
  upiEnabled: false, upiId: "", upiPayeeName: "", upiMerchantCode: "",
  pharmacyName: "",
  address: "",
  phoneNumber: "",
  email: "",
  gstin: "",
  invoicePrefix: "SAL",
  gstEnabled: false,
  defaultGstPercentage: 0,
  invoiceNumberDigits: 6,
  lowStockThreshold: 10,
  expiryAlertsEnabled: true,
  expiryAlertDays: 90,
  receiptFooter: "Thank you for your purchase!",
  receiptShowCustomerPhone: true,
  receiptShowPaymentHistory: true,
};

export default function PharmacySettingsPage() {
  const [form, setForm] =
    useState<UpdatePharmacySettingsRequest>(initialForm);

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [nextInvoiceNumber, setNextInvoiceNumber] = useState(1);

  useEffect(() => {
    let active = true;

    async function loadSettings() {
      try {
        const settings = await getPharmacySettings();

        if (!active) return;

        if (settings) {
          setNextInvoiceNumber(settings.nextInvoiceNumber);
          setForm({
            upiEnabled: settings.upiEnabled, upiId: settings.upiId ?? "", upiPayeeName: settings.upiPayeeName ?? "", upiMerchantCode: settings.upiMerchantCode ?? "",
            pharmacyName: settings.pharmacyName,
            address: settings.address,
            phoneNumber: settings.phoneNumber,
            email: settings.email ?? "",
            gstin: settings.gstin ?? "",
            invoicePrefix: settings.invoicePrefix,
            gstEnabled: settings.gstEnabled,
            defaultGstPercentage:
              settings.defaultGstPercentage,
            revision: settings.revision,
            invoiceNumberDigits: settings.invoiceNumberDigits,
            lowStockThreshold: settings.lowStockThreshold,
            expiryAlertsEnabled: settings.expiryAlertsEnabled,
            expiryAlertDays: settings.expiryAlertDays,
            receiptFooter: settings.receiptFooter,
            receiptShowCustomerPhone: settings.receiptShowCustomerPhone,
            receiptShowPaymentHistory: settings.receiptShowPaymentHistory,
          });
        }
      } catch (err) {
        if (active) {
          setError(
            err instanceof Error
              ? err.message
              : "Failed to load pharmacy settings."
          );
        }
      } finally {
        if (active) setLoading(false);
      }
    }

    void loadSettings();

    return () => {
      active = false;
    };
  }, []);

  function updateField<K extends keyof UpdatePharmacySettingsRequest>(
    field: K,
    value: UpdatePharmacySettingsRequest[K]
  ) {
    setForm((previous) => ({
      ...previous,
      [field]: value,
    }));

    setSuccess("");
  }

  async function handleSubmit(
    event: React.FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();
    setError("");
    setSuccess("");

    if (
      !form.pharmacyName.trim() ||
      !form.address.trim() ||
      !form.phoneNumber.trim() ||
      !form.invoicePrefix.trim()
    ) {
      setError("Please complete all required fields.");
      return;
    }

    if (
      form.defaultGstPercentage < 0 ||
      form.defaultGstPercentage > 100
    ) {
      setError("GST percentage must be between 0 and 100.");
      return;
    }

    setSaving(true);

    try {
      const saved = await updatePharmacySettings({
        ...form,
        email: form.email?.trim() || null,
        gstin: form.gstin?.trim() || null,
      });
      setForm(current => ({ ...current, revision: saved.revision }));
      setNextInvoiceNumber(saved.nextInvoiceNumber);

      setSuccess("Pharmacy settings saved successfully.");
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to save pharmacy settings."
      );
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <p>Loading pharmacy settings...</p>;
  }

  return (
    <div style={styles.container}>
      <h1>Pharmacy Settings</h1>
      <p>Manage your pharmacy and billing configuration.</p>

      {error && (
        <div role="alert" style={styles.error}>
          {error}
        </div>
      )}

      {success && (
        <div role="status" style={styles.success}>
          {success}
        </div>
      )}

      <form onSubmit={handleSubmit}>
        <fieldset disabled={saving} style={{ border: 0, margin: 0, padding: 0 }}>
        <section style={styles.section}>
          <h2>Pharmacy Information</h2>

          <label style={styles.label}>
            Pharmacy Name *
            <input
              style={styles.input}
              required
              maxLength={200}
              value={form.pharmacyName}
              onChange={(e) =>
                updateField("pharmacyName", e.target.value)
              }
            />
          </label>

          <label style={styles.label}>
            Address *
            <textarea
              style={styles.input}
              required
              maxLength={500}
              rows={3}
              value={form.address}
              onChange={(e) =>
                updateField("address", e.target.value)
              }
            />
          </label>

          <label style={styles.label}>
            Phone Number *
            <input
              style={styles.input}
              required
              maxLength={20}
              value={form.phoneNumber}
              onChange={(e) =>
                updateField("phoneNumber", e.target.value)
              }
            />
          </label>

          <label style={styles.label}>
            Email
            <input
              type="email"
              style={styles.input}
              maxLength={150}
              value={form.email ?? ""}
              onChange={(e) =>
                updateField("email", e.target.value)
              }
            />
          </label>
        </section>

        <section style={styles.section}>
          <h2>GST Configuration</h2>

          <label style={styles.label}>
            GSTIN
            <input
              style={styles.input}
              maxLength={15}
              value={form.gstin ?? ""}
              onChange={(e) =>
                updateField("gstin", e.target.value.toUpperCase())
              }
            />
          </label>

          <label style={styles.checkboxLabel}>
            <input
              type="checkbox"
              checked={form.gstEnabled}
              onChange={(e) =>
                updateField("gstEnabled", e.target.checked)
              }
            />
            Enable GST
          </label>

          <label style={styles.label}>
            Default GST Percentage
            <input
              type="number"
              min={0}
              max={100}
              step={0.01}
              style={styles.input}
              disabled={!form.gstEnabled}
              value={form.defaultGstPercentage}
              onChange={(e) =>
                updateField(
                  "defaultGstPercentage",
                  Number(e.target.value)
                )
              }
            />
          </label>
        </section>

        <section style={styles.section}>
          <h2>Invoice Configuration</h2>

          <label style={styles.label}>
            Invoice Prefix *
            <input
              style={styles.input}
              required
              maxLength={20}
              value={form.invoicePrefix}
              onChange={(e) =>
                updateField(
                  "invoicePrefix",
                  e.target.value.toUpperCase()
                )
              }
            />
          </label>

          <p style={styles.helpText}>
            Next invoice preview: {form.invoicePrefix}-{String(nextInvoiceNumber).padStart(form.invoiceNumberDigits, "0")}. Numbers are allocated at checkout and cannot be reset here.
          </p>
          <label style={styles.label}>Minimum number digits<input style={styles.input} type="number" min={3} max={12} required value={form.invoiceNumberDigits} onChange={e => updateField("invoiceNumberDigits", Number(e.target.value))} /></label>
        </section>
        <section style={styles.section}><h2>Stock and Expiry Alerts</h2>
          <label style={styles.label}>Low-stock threshold (units per batch)<input style={styles.input} type="number" min={1} max={100000} required value={form.lowStockThreshold} onChange={e => updateField("lowStockThreshold", Number(e.target.value))} /></label>
          <label style={styles.checkboxLabel}><input type="checkbox" checked={form.expiryAlertsEnabled} onChange={e => updateField("expiryAlertsEnabled", e.target.checked)} />Enable expiry notifications</label>
          <label style={styles.label}>Notify within days<input style={styles.input} type="number" min={1} max={365} required value={form.expiryAlertDays} onChange={e => updateField("expiryAlertDays", Number(e.target.value))} /></label>
          <p style={styles.helpText}>The background worker reads these preferences every 15 minutes. Existing notifications remain in history.</p>
        </section>
        <section style={styles.section}><h2>UPI QR Collection</h2>
          <p style={styles.helpText}>These payment details belong to the active branch. Use its bank-issued merchant UPI ID and confirm the recipient name in the payment app before collecting money. QR codes do not automatically verify payments.</p>
          <label style={styles.checkboxLabel}><input type="checkbox" checked={form.upiEnabled} onChange={e => updateField("upiEnabled", e.target.checked)} />Enable UPI QR collection for this branch</label>
          <label style={styles.label}>Merchant UPI ID<input style={styles.input} required={form.upiEnabled} maxLength={100} placeholder="pharmacy@bank" value={form.upiId ?? ""} onChange={e => updateField("upiId", e.target.value)} autoCapitalize="none" spellCheck={false} /></label>
          <label style={styles.label}>Payee / business name<input style={styles.input} required={form.upiEnabled} maxLength={100} value={form.upiPayeeName ?? ""} onChange={e => updateField("upiPayeeName", e.target.value)} /></label>
          <label style={styles.label}>Merchant category code (optional)<input style={styles.input} inputMode="numeric" pattern="[0-9]{4}|" maxLength={4} placeholder="Only if supplied by your bank" value={form.upiMerchantCode ?? ""} onChange={e => updateField("upiMerchantCode", e.target.value)} /></label>
          <p style={styles.helpText}>Do not invent a merchant code. Confirm QR acceptance with your acquiring bank and the UPI apps your customers use.</p>
        </section>
        <section style={styles.section}><h2>Receipt Preferences</h2>
          <label style={styles.label}>Footer text<textarea style={styles.input} maxLength={500} value={form.receiptFooter} onChange={e => updateField("receiptFooter", e.target.value)} /></label>
          <label style={styles.checkboxLabel}><input type="checkbox" checked={form.receiptShowCustomerPhone} onChange={e => updateField("receiptShowCustomerPhone", e.target.checked)} />Show customer phone on receipts</label>
          <label style={styles.checkboxLabel}><input type="checkbox" checked={form.receiptShowPaymentHistory} onChange={e => updateField("receiptShowPaymentHistory", e.target.checked)} />Include payment and return history when printing</label>
        </section>

        <button
          type="submit"
          disabled={saving}
          style={styles.button}
        >
          {saving ? "Saving..." : "Save Settings"}
        </button>
        </fieldset>
      </form>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  container: {
    maxWidth: 800,
    margin: "0 auto",
    padding: 24,
  },
  section: {
    padding: 24,
    marginBottom: 20,
    border: "1px solid #ddd",
    borderRadius: 8,
  },
  label: {
    display: "flex",
    flexDirection: "column",
    gap: 8,
    marginBottom: 16,
    fontWeight: 500,
  },
  input: {
    padding: 10,
    border: "1px solid #ccc",
    borderRadius: 6,
    fontSize: 14,
    fontFamily: "inherit",
  },
  checkboxLabel: {
    display: "flex",
    alignItems: "center",
    gap: 10,
    marginBottom: 16,
  },
  button: {
    padding: "12px 24px",
    background: "#2563eb",
    color: "#fff",
    border: "none",
    borderRadius: 6,
    cursor: "pointer",
  },
  error: {
    padding: 12,
    marginBottom: 16,
    color: "#991b1b",
    background: "#fee2e2",
    borderRadius: 6,
  },
  success: {
    padding: 12,
    marginBottom: 16,
    color: "#166534",
    background: "#dcfce7",
    borderRadius: 6,
  },
  helpText: {
    color: "#666",
    fontSize: 13,
  },
};
