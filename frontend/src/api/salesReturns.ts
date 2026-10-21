import { apiFetch } from "./apiClient";
import { PaymentApiError, type Payment } from "./payments";

export type ReturnDisposition = 1 | 2 | 3 | 4 | 5;
export type RefundMethod = 1 | 2 | 3;
export interface RefundDetails { method: RefundMethod; amount: number; reference: string | null; }
export interface RefundRequest extends RefundDetails { idempotencyKey: string; }
export interface ReturnItemRequest { saleItemId: number; quantity: number; restock: boolean; disposition: ReturnDisposition; inspectionApproved: boolean; }
export interface ReturnRequest { reason: string; idempotencyKey: string; items: ReturnItemRequest[]; refund?: RefundDetails; }
export interface SaleReturn {
  id: number; saleId: number; reason: string; idempotencyKey: string; createdAtUtc: string;
  totalAmount: number; items: (Omit<ReturnItemRequest, "disposition"> & { amount: number; disposition: string; inspectedByUserId: string | null; inspectedAtUtc: string | null })[];
}
export interface SaleReturns {
  saleId: number;
  items: { saleItemId: number; medicineName: string; batchNumber: string; expiryDate: string;
    soldQuantity: number; returnedQuantity: number; remainingQuantity: number; lineTotal: number; canRestock: boolean }[];
  returns: SaleReturn[]; refunds: Payment[]; returnedAmount: number; refundedAmount: number; refundDue: number;
}
async function result<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    throw new PaymentApiError(error?.message ?? error?.title ?? `Request failed (${response.status}).`, response.status);
  }
  return response.json() as Promise<T>;
}
export const salesReturnsApi = {
  async get(saleId: number): Promise<SaleReturns> { return result(await apiFetch(`/api/sales/${saleId}/returns`)); },
  async create(saleId: number, request: ReturnRequest): Promise<SaleReturn> {
    return result(await apiFetch(`/api/sales/${saleId}/returns`, { method: "POST", body: JSON.stringify(request) }));
  },
  async refund(saleId: number, request: RefundRequest): Promise<Payment> {
    return result(await apiFetch(`/api/sales/${saleId}/refunds`, { method: "POST", body: JSON.stringify(request) }));
  },
};
