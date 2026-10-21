
import { apiFetch } from "./apiClient";

export type MedicineOption = {
  id: number;
  name: string;
  genericName: string;
  manufacturer: string;
  category: string;
  isActive: boolean;
};

export type MedicineBatch = {
  id: number;
  medicineId: number;
  medicineName: string;
  batchNumber: string;
  expiryDate: string;
  purchasePrice: number;
  sellingPrice: number;
  quantity: number;
  isExpired: boolean;
  isNearExpiry: boolean;
  isLowStock: boolean;
};

export type CreateBatchRequest = {
  medicineId: number;
  batchNumber: string;
  expiryDate: string;
  purchasePrice: number;
  sellingPrice: number;
  quantity: number;
};

async function apiRequest<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const response = await apiFetch(path, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options.headers,
    },
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null);

    throw new Error(
      body?.message ??
      body?.title ??
      `Request failed with HTTP ${response.status}`
    );
  }

  return (await response.json()) as T;
}

export const inventoryApi = {
  getBatches: () =>
    apiRequest<MedicineBatch[]>(
      "/api/inventory/batches"
    ),

  getMedicines: () =>
    apiRequest<MedicineOption[]>(
      "/api/medicines"
    ),

  createBatch: (request: CreateBatchRequest) =>
    apiRequest<MedicineBatch>(
      "/api/inventory/batches",
      {
        method: "POST",
        body: JSON.stringify(request),
      }
    ),
};
