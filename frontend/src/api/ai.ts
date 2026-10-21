import { apiFetch } from "./apiClient";
export interface AiStatus { modelConfigured: boolean; visionConfigured: boolean; vectorConfigured: boolean; note: string }
export interface InventoryFact { medicineId: number; medicine: string; batch: string; expiry: string; quantity: number }
export interface InventoryAnswer { interpretation: string; mode: string; batches: InventoryFact[]; notes: string }
export interface Reorder { medicineId: number; medicine: string; available: number; expiring: number; sold: number; pendingPurchase: number; dailyDemand: number; targetQuantity: number; suggestedQuantity: number }
export interface Sentiment { sentiment: string; summary: string; themes: string[] }
export interface SalesInsight { explanation: string; mode: string; facts: { startDate: string; endDate: string; totalSales: number; totalRevenue: number; returnCredits: number; netSales: number; collections: number; refunds: number; netCollections: number } }
export interface InvoiceLine { medicineName: string | null; batchNumber: string | null; expiryDate: string | null; quantity: number | null; purchasePrice: number | null; sellingPrice: number | null }
export interface InvoiceExtraction { invoice: { supplierName: string | null; invoiceNumber: string | null; invoiceDate: string | null; items: InvoiceLine[] }; lines: { extracted: InvoiceLine; suggestedMedicineId: number | null }[]; warnings: string[] }
export async function aiRequest<T>(path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const response = await apiFetch(`/api/ai/${path}`, { method: body === undefined ? "GET" : "POST", body: body === undefined ? undefined : JSON.stringify(body), signal });
  if (!response.ok) { const error = await response.json().catch(() => null); throw new Error(error?.message ?? error?.title ?? "The AI request failed."); }
  return response.json();
}
