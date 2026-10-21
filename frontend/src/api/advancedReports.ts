import { apiFetch } from "./apiClient";
export type ReportKind = "sales" | "gst" | "medicines" | "profit" | "stock";
export interface AdvancedReport {
  kind: ReportKind; startDate: string; endDate: string; notes: string;
  tables: { title: string; columns: string[]; rows: (string | number | null)[][] }[];
}
export const advancedReportsApi = {
  async get(kind: ReportKind, startDate: string, endDate: string): Promise<AdvancedReport> {
    const response = await apiFetch(`/api/reports/advanced/${kind}?${new URLSearchParams({ startDate, endDate })}`);
    if (!response.ok) { const error = await response.json().catch(() => null); throw new Error(error?.message ?? error?.title ?? "Unable to load report."); }
    return response.json() as Promise<AdvancedReport>;
  },
  async download(report: AdvancedReport, format: "xlsx" | "pdf") {
    const { kind, startDate, endDate } = report;
    const response = await apiFetch(`/api/reports/advanced/${kind}/export/${format}?${new URLSearchParams({ startDate, endDate })}`);
    if (!response.ok) { const error = await response.json().catch(() => null); throw new Error(error?.message ?? error?.title ?? "Unable to export report."); }
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a"); link.href = url; link.download = `pharmacy-${kind}-${startDate}-${endDate}.${format}`;
    document.body.appendChild(link); link.click(); link.remove();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
  },
};
