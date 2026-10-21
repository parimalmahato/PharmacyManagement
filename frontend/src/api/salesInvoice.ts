
import { apiFetch } from "./apiClient";
import type { Sale } from "./sales";
import type { PharmacySettings } from "./pharmacySettingsApi";

export interface InvoiceSaleItem {
  medicineId: number;
  medicineName: string;
  batchNumber: string;
  expiryDate: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface InvoiceSale {
  id: number;
  invoiceNumber: string;
  createdAtUtc: string;
  totalAmount: number;
  status: string;
  customerId: number | null;
  customerName: string | null;
  customerPhone: string | null;
  items: InvoiceSaleItem[];
}

export interface SaleInvoice {
  sale: Sale;
  pharmacy: PharmacySettings;
}

export async function getSaleInvoice(
  saleId: number
): Promise<SaleInvoice> {
  const response = await apiFetch(
    `/api/sales/${saleId}/invoice`
  );

  if (!response.ok) {
    let message = `Failed to load invoice (${response.status})`;

    try {
      const error = await response.json();
      message = error.message ?? error.title ?? message;
    } catch {
      // Preserve the default error message.
    }

    throw new Error(message);
  }

  return (await response.json()) as SaleInvoice;
}
