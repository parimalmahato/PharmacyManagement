import { apiFetch } from "./apiClient";
export interface GstTaxDetails {
  registrationGstin: string; counterpartyGstin: string | null; placeOfSupply: string; supplyCategory: string;
  documentNumber: string; documentDate: string; grossAmount: number; taxableValue: number;
  cgst: number; sgst: number; igst: number; cess: number; itcEligible: boolean;
}
export interface GstDocument {
  kind: string; id: number; date: string; number: string; counterparty: string; bookAmount: number;
  recordedTaxable: number | null; recordedGst: number | null; missingHsn: boolean; mixedZeroTax: boolean;
  taxDetails: GstTaxDetails | null; revision: string | null;
  relatedInvoice: string | null;
}
export interface GstPreparation {
  year: number; month: number; gstin: string | null; status: string; revision: string | null;
  fingerprint: string; reviewId: number | null; changedSinceReview: boolean;
  reviewedBy: string | null; reviewedAtUtc: string | null;
  documents: GstDocument[]; issues: { severity: string; code: string; message: string; sourceKind: string | null; sourceId: number | null }[];
  reconciliation: { supplierGstin: string; invoiceNumber: string; invoiceDate: string; status: string; bookTax: number; statementTax: number; eligibleMatchedItc: number }[];
  netOutwardValue: number; netCgst: number; netSgst: number; netIgst: number; netCess: number; matchedEligibleItc: number;
}
async function request<T>(period: string, path = "", body?: unknown, method = "POST"): Promise<T> {
  const [year, month] = period.split("-");
  const response = await apiFetch(`/api/gst-preparation/${year}/${Number(month)}${path}`, body === undefined ? {} : { method, body: JSON.stringify(body) });
  if (!response.ok) { const error = await response.json().catch(() => null); throw new Error(error?.message ?? error?.title ?? `GST preparation failed (${response.status}).`); }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}
export const gstPreparationApi = {
  get: (period: string) => request<GstPreparation>(period),
  save: (period: string, doc: GstDocument, details: GstTaxDetails) => request<void>(period, `/documents/${doc.kind}/${doc.id}`, { details, revision: doc.revision }, "PUT"),
  import: (period: string, csv: string, revision: string | null) => request<void>(period, "/2b/import", { csv, revision }),
  review: (period: string, data: GstPreparation) => request<GstPreparation>(period, "/review", { fingerprint: data.fingerprint, revision: data.revision }),
  reopen: (period: string, reason: string, revision: string) => request<void>(period, "/reopen", { reason, revision }),
  async download(period: string, format: "xlsx" | "pdf") {
    const [year, month] = period.split("-"); const response = await apiFetch(`/api/gst-preparation/${year}/${Number(month)}/export?format=${format}`);
    if (!response.ok) { const error = await response.json().catch(() => null); throw new Error(error?.message ?? error?.title ?? "Export failed."); }
    downloadBlob(await response.blob(), `gst-preparation-${period}.${format}`);
  },
};
export function downloadBlob(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob); const a = document.createElement("a"); a.href = url; a.download = name; a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
