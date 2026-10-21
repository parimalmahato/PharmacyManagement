import type { Supplier } from "./suppliers";
import { apiFetch } from "./apiClient";


export interface MedicineOption {
  id: number;
  name: string;
  genericName: string;
  manufacturer: string;
  category: string;
  isActive: boolean;
}

export interface PurchaseItem {
  medicineId: number;
  batchNumber: string;
  expiryDate: string;
  quantity: number;
  purchasePrice: number;
  sellingPrice: number;
}

export interface Purchase {
  id: number;
  supplierId: number;
  invoiceNumber: string;
  invoiceDate: string;
  status: "Draft" | "Received" | "Cancelled" | string;
  totalAmount: number;
  items: PurchaseItem[];
}

export interface CreatePurchaseRequest {
  supplierId: number;
  invoiceNumber: string;
  invoiceDate: string;
  items: PurchaseItem[];
}

async function request<T>(
  path: string,
  options?: RequestInit
): Promise<T> {
  const response = await apiFetch(path, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options?.headers,
    },
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(
      body?.message ?? `Request failed with status ${response.status}`
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export const purchasesApi = {
  getAll: () => request<Purchase[]>("/api/purchases"),

  create: (purchase: CreatePurchaseRequest) =>
    request<Purchase>("/api/purchases", {
      method: "POST",
      body: JSON.stringify(purchase),
    }),

  receive: (id: number) =>
    request<{ message: string; purchaseId: number }>(
      `/api/purchases/${id}/receive`,
      { method: "POST" }
    ),
};

// Reuse your existing medicine endpoint.
export const medicinesApi = {
  getAll: () => request<MedicineOption[]>("/api/medicines"),
};

// Supplier type can also be imported by the page.
export type { Supplier };