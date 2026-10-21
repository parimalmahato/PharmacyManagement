
import { apiFetch } from "./apiClient";

export interface AuditLog {
  id: number;
  userId: string | null;
  userName: string;
  action: string;
  entityType: string;
  entityId: string | null;
  oldValues: string | null;
  newValues: string | null;
  createdAtUtc: string;
}

export interface AuditLogResponse {
  totalCount: number;
  page: number;
  pageSize: number;
  items: AuditLog[];
}

export const auditLogsApi = {
  async getAll(
    page = 1,
    pageSize = 25,
    action = "",
    entityType = ""
  ): Promise<AuditLogResponse> {
    const params = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
    });

    if (action) params.set("action", action);
    if (entityType) params.set("entityType", entityType);

    const response = await apiFetch(
      `/api/audit-logs?${params.toString()}`
    );

    if (!response.ok) {
      throw new Error(
        `Unable to load audit logs. HTTP ${response.status}`
      );
    }

    return response.json() as Promise<AuditLogResponse>;
  },
};
