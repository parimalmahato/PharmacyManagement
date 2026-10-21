import { apiFetch } from "./apiClient";
export interface Branch { id: number; code: string; name: string; isActive: boolean; }
export const branchesApi = {
  async getAll(): Promise<Branch[]> {
    const response = await apiFetch("/api/branches");
    if (!response.ok) throw new Error("Unable to load branches.");
    return response.json() as Promise<Branch[]>;
  },
  async create(code: string, name: string): Promise<Branch> {
    const response = await apiFetch("/api/branches", { method: "POST", body: JSON.stringify({ code, name }) });
    if (!response.ok) { const error = await response.json().catch(() => null); throw new Error(error?.message ?? "Unable to create branch."); }
    const branch: Branch = await response.json();
    window.dispatchEvent(new Event("pharmacy-branches-updated"));
    return branch;
  },
};
