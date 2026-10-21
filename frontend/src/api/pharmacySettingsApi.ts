
import { apiFetch } from "./apiClient";

export interface PharmacySettings {
  upiEnabled: boolean;
  upiId: string | null;
  upiPayeeName: string | null;
  upiMerchantCode: string | null;
  id: number;
  pharmacyName: string;
  address: string;
  phoneNumber: string;
  email: string | null;
  gstin: string | null;
  invoicePrefix: string;
  gstEnabled: boolean;
  defaultGstPercentage: number;
  updatedAtUtc: string;
  revision: string;
  nextInvoiceNumber: number;
  invoiceNumberDigits: number;
  lowStockThreshold: number;
  expiryAlertsEnabled: boolean;
  expiryAlertDays: number;
  receiptFooter: string;
  receiptShowCustomerPhone: boolean;
  receiptShowPaymentHistory: boolean;
}

export type UpdatePharmacySettingsRequest = Omit<
  PharmacySettings,
  "id" | "updatedAtUtc" | "nextInvoiceNumber" | "revision"
> & { revision?: string };

async function parseResponse(
  response: Response
): Promise<PharmacySettings> {
  if (!response.ok) {
    let message = `Request failed (${response.status})`;

    try {
      const error = await response.json();
      message = error.message ?? error.title ?? message;
    } catch {
      // Keep the default error message.
    }

    throw new Error(message);
  }

  return (await response.json()) as PharmacySettings;
}

// GET: /api/pharmacy-settings
export async function getPharmacySettings():
  Promise<PharmacySettings | null> {
  const response = await apiFetch(
    "/api/pharmacy-settings"
  );

  if (response.status === 404) {
    return null;
  }

  return parseResponse(response);
}

// PUT: /api/pharmacy-settings
export async function updatePharmacySettings(
  request: UpdatePharmacySettingsRequest
): Promise<PharmacySettings> {
  const response = await apiFetch(
    "/api/pharmacy-settings",
    {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
    }
  );

  return parseResponse(response);
}
