
import { apiFetch } from "./apiClient";

export type EmployeeRole = "Pharmacist" | "Cashier";

export interface Employee {
  id: string;
  fullName: string;
  email: string;
  isActive: boolean;
  roles: string[];
  createdAtUtc: string;
  branchId: number;
}

export interface CreateEmployeeRequest {
  fullName: string;
  email: string;
  password: string;
  role: EmployeeRole;
  branchId?: number;
}

async function request<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const response = await apiFetch(path, options);

  if (!response.ok) {
    const body = await response.json().catch(() => null);

    throw new Error(
      body?.message ??
      body?.title ??
      `HTTP ${response.status}`
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export const usersApi = {
  resetPassword: (id: string, newPassword: string) => request<void>(`/api/users/${encodeURIComponent(id)}/reset-password`, {
    method: "POST", body: JSON.stringify({ newPassword }),
  }),
  assignBranch: (id: string, branchId: number) => request<void>(`/api/users/${encodeURIComponent(id)}/branch`, { method: "PUT", body: JSON.stringify({ branchId }) }),
  getAll: () =>
    request<Employee[]>("/api/users"),

  create: (data: CreateEmployeeRequest) =>
    request<Employee>("/api/users", {
      method: "POST",
      body: JSON.stringify(data),
    }),

  updateStatus: (id: string, isActive: boolean) =>
    request<void>(
      `/api/users/${encodeURIComponent(id)}/status`,
      {
        method: "PUT",
        body: JSON.stringify({ isActive }),
      }
    ),
};
