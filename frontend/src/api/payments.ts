import { apiFetch } from "./apiClient";

export type PaymentMethod = 1 | 2 | 3;
export type PaymentStatus = "Unpaid" | "PartiallyPaid" | "Paid";
export interface PaymentRequest {
  method: PaymentMethod;
  amount: number;
  reference: string | null;
  idempotencyKey: string;
}
export interface Payment extends Omit<PaymentRequest, "method"> {
  id: number;
  saleId: number;
  method: string;
  createdAtUtc: string;
}
export interface SalePayments {
  saleId: number;
  totalAmount: number;
  paidAmount: number;
  returnedAmount: number;
  refundedAmount: number;
  refundDue: number;
  netTotal: number;
  balanceAmount: number;
  paymentStatus: PaymentStatus;
  payments: Payment[];
}
export class PaymentApiError extends Error {
  readonly status: number;
  constructor(message: string, status: number) { super(message); this.status = status; }
}
async function result<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new PaymentApiError(body?.message ?? body?.title ?? `Payment request failed (${response.status}).`, response.status);
  }
  return response.json() as Promise<T>;
}
export const paymentsApi = {
  async get(saleId: number): Promise<SalePayments> {
    return result(await apiFetch(`/api/sales/${saleId}/payments`));
  },
  async record(saleId: number, request: PaymentRequest): Promise<Payment> {
    return result(await apiFetch(`/api/sales/${saleId}/payments`, {
      method: "POST", body: JSON.stringify(request),
    }));
  },
};
