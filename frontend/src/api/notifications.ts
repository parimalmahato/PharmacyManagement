import { apiFetch } from "./apiClient";


export interface PharmacyNotification {
  id: number;
  medicineBatchId: number;
  alertType: string;
  message: string;
  createdAtUtc: string;
  isRead: boolean;
}

export interface NotificationSummary {
  totalCount: number;
  unreadCount: number;
}

async function handleResponse<T>(
  response: Response
): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => null);

    throw new Error(
      error?.message ??
      error?.title ??
      `Request failed with status ${response.status}.`
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export const notificationsApi = {
  async getAll(
    unreadOnly = false
  ): Promise<PharmacyNotification[]> {
    const response = await apiFetch(
      `/api/notifications?unreadOnly=${unreadOnly}`
    );

    return handleResponse<PharmacyNotification[]>(response);
  },

  async getSummary(): Promise<NotificationSummary> {
    const response = await apiFetch(
      `/api/notifications/summary`
    );

    return handleResponse<NotificationSummary>(response);
  },

  async markAsRead(id: number): Promise<void> {
    const response = await apiFetch(
      `/api/notifications/${id}/read`,
      { method: "PUT" }
    );

    return handleResponse<void>(response);
  },

  async markAllAsRead(): Promise<number> {
    const response = await apiFetch(
      `/api/notifications/read-all`,
      { method: "PUT" }
    );

    const result = await handleResponse<{
      updatedCount: number;
    }>(response);

    return result.updatedCount;
  },
};
