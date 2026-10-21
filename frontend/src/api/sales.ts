import { apiFetch } from "./apiClient";

export interface SaleItemRequest {
  medicineId: number;
  quantity: number;
  discountPercentage: number;
}
export interface CreateSaleRequest {
  idempotencyKey?: string;
  items: SaleItemRequest[];
  customerId: number | null;
}
export interface SaleItem {
  medicineId: number;
  medicineName: string;
  batchNumber: string;
  expiryDate: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  hsnCode: string | null;
  gstPercentage: number;
  discountPercentage: number;
  grossAmount: number;
  discountAmount: number;
  taxableAmount: number;
  gstAmount: number;
}
export interface Sale {
  cancelledAtUtc: string | null;
  cancelledByUserId: string | null;
  cancellationReason: string | null;
  cancellationStockRestored: boolean;
  paidAmount: number;
  returnedAmount: number;
  refundedAmount: number;
  refundDue: number;
  netTotal: number;
  balanceAmount: number;
  paymentStatus: import("./payments").PaymentStatus;
  id: number;
  invoiceNumber: string;
  createdAtUtc: string;
  totalAmount: number;
  status: string;
  items: SaleItem[];
  customerId: number | null;
  customerName: string | null;
  customerPhone: string | null;
  subtotalAmount: number;
  discountAmount: number;
  taxableAmount: number;
  gstAmount: number;
}
async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    throw new SalesApiError(error?.message ?? error?.title ?? `Request failed with status ${response.status}.`, response.status);
  }
  return response.json() as Promise<T>;
}
export class SalesApiError extends Error {
  readonly status: number;
  constructor(message: string, status: number) { super(message); this.status = status; }
}
export const salesApi = {
  async cancel(id: number, request: CancelSaleRequest): Promise<Sale> {
    return handleResponse<Sale>(await apiFetch(`/api/sales/${id}/cancel`, { method: "POST", body: JSON.stringify(request) }));
  },
  async getAll(): Promise<Sale[]> {
    return handleResponse<Sale[]>(await apiFetch("/api/sales"));
  },
  async create(request: CreateSaleRequest): Promise<Sale> {
    return handleResponse<Sale>(await apiFetch("/api/sales", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
    }));
  },
};
export interface CancelSaleRequest {
  reason: string; idempotencyKey: string; medicinesNotDispensed: boolean; restock: boolean; inspectionApproved: boolean;
}
