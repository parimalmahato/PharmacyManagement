import { apiFetch } from "./apiClient";


export interface Supplier {
  id: number;
  name: string;
  phone: string | null;
  email: string | null;
  address: string | null;
  isActive: boolean;
}

export interface CreateSupplierRequest {
  name: string;
  phone: string;
  email: string;
  address: string;
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

  return response.json() as Promise<T>;
}

export const suppliersApi = {
  getAll: () => request<Supplier[]>("/api/suppliers"),

  create: (supplier: CreateSupplierRequest) =>
    request<Supplier>("/api/suppliers", {
      method: "POST",
      body: JSON.stringify(supplier),
    }),
};