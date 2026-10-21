import { apiFetch } from "./apiClient";
export interface UpiPaymentRequest {
  saleId: number; invoiceNumber: string; payeeUpiId: string; payeeName: string;
  amount: number; outstandingBalance: number; reference: string; paymentUri: string; generatedAtUtc: string;
}
export async function getUpiPaymentRequest(saleId: number, amount: string): Promise<UpiPaymentRequest> {
  const response = await apiFetch(`/api/sales/${saleId}/upi-request?amount=${encodeURIComponent(amount)}`);
  const body = await response.json().catch(() => null);
  if (!response.ok) throw new Error(body?.message ?? body?.title ?? "Unable to prepare UPI payment request.");
  return body as UpiPaymentRequest;
}
