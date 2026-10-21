import { apiFetch } from "./apiClient";


export interface DashboardSummary {
  lowStockThreshold: number;
  todayReturnCredits: number;
  todayNetSales: number;
  todayCollections: number;
  todayRefunds: number;
  todayNetCollections: number;
  outstandingBalance: number;
  refundDue: number;
  todayReceivedPurchases: number;
  todayPurchaseValue: number;
  draftPurchases: number;
  outOfStockMedicines: number;
  expiringWithin30Days: number;
  inventoryCostValue: number;
  todayRevenue: number;
  todaySalesCount: number;
  totalActiveMedicines: number;
  lowStockBatchCount: number;
}

export interface DailySales {
  returnCredits: number;
  netSales: number;
  collections: number;
  refunds: number;
  netCollections: number;
  date: string;
  salesCount: number;
  revenue: number;
}

export interface TopSellingMedicine {
  medicineId: number;
  medicineName: string;
  quantitySold: number;
  revenue: number;
}

export interface SalesReport {
  returnCredits: number;
  netSales: number;
  collections: number;
  refunds: number;
  netCollections: number;
  outstandingBalance: number;
  refundDue: number;
  receivedPurchases: number;
  purchaseValue: number;
  collectionsByMethod: { method: string; collections: number; refunds: number; netCollections: number }[];
  startDate: string;
  endDate: string;
  totalSales: number;
  totalRevenue: number;
  dailySales: DailySales[];
  topSellingMedicines: TopSellingMedicine[];
}
export interface LowStockBatch {
  batchId: number;
  medicineId: number;
  medicineName: string;
  batchNumber: string;
  quantity: number;
  expiryDate: string;
}

export interface ExpirySummary {
  expiredBatches: number;
  expiringWithin30Days: number;
  expiringWithin60Days: number;
  expiringWithin90Days: number;
}

export interface ExpiringBatch {
  batchId: number;
  medicineId: number;
  medicineName: string;
  batchNumber: string;
  expiryDate: string;
  quantity: number;
  daysUntilExpiry: number;
  status: string;
}

export interface ExpiryReport {
  summary: ExpirySummary;
  batches: ExpiringBatch[];
}

async function handleResponse<T>(
  response: Response
): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => null);

    throw new Error(
      error?.message ??
      error?.title ??
      `Request failed: ${response.status}`
    );
  }

  return response.json() as Promise<T>;
}

export const reportsApi = {
  async getDashboard(): Promise<DashboardSummary> {
    const response = await apiFetch(
      `/api/reports/dashboard`
    );

    return handleResponse<DashboardSummary>(response);
  },

  async getSalesReport(
    startDate: string,
    endDate: string
  ): Promise<SalesReport> {
    const query = new URLSearchParams({
      startDate,
      endDate,
    });

    const response = await apiFetch(
      `/api/reports/sales?${query}`
    );

    return handleResponse<SalesReport>(response);
  },
  async getLowStock(): Promise<LowStockBatch[]> {
    const response = await apiFetch(
        `/api/reports/low-stock`
    );

    return handleResponse<LowStockBatch[]>(response);
    },
    async getExpiryReport(): Promise<ExpiryReport> {
    const response = await apiFetch(
        `/api/reports/expiry`
    );

    return handleResponse<ExpiryReport>(response);
    },
};
