import { apiFetch } from "./apiClient";

export type Medicine = {
  id: number;
  name: string;
  genericName: string;
  manufacturer: string;
  category: string;
  isActive: boolean;
  hsnCode: string | null;
  gstPercentage: number;
};

export type MedicineRequest = {
  name: string;
  genericName: string;
  manufacturer: string;
  category: string;
  hsnCode: string | null;
  gstPercentage: number;
};

async function request<T>(path: string, options?: RequestInit): Promise<T> {
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
      body?.message ?? body?.title ?? `Request failed with status ${response.status}`
    );
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const medicineApi = {
  getAll: () => request<Medicine[]>("/api/medicines"),
  getById: (id: number) => request<Medicine>(`/api/medicines/${id}`),
  create: (data: MedicineRequest) =>
    request<Medicine>("/api/medicines", {
      method: "POST",
      body: JSON.stringify(data),
    }),
  update: (id: number, data: MedicineRequest) =>
    request<Medicine>(`/api/medicines/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    }),
  deactivate: (id: number) =>
    request<void>(`/api/medicines/${id}`, { method: "DELETE" }),
};
