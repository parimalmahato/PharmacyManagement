import { apiFetch } from "./apiClient";
//const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export interface Customer {
  id: number;
  name: string;
  phone: string | null;
  email: string | null;
  address: string | null;
  createdAtUtc: string;
  isActive: boolean;
}

export interface CustomerRequest {
  name: string;
  phone: string | null;
  email: string | null;
  address: string | null;
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let message = `Request failed with status ${response.status}.`;

    try {
      const body = await response.json();
      message = body.message ?? body.title ?? message;
    } catch {
      // Keep the fallback message if the response has no JSON body.
    }

    throw new Error(message);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export const customersApi = {
  async getAll(): Promise<Customer[]> {
    const response = await apiFetch("/api/customers");
    return handleResponse<Customer[]>(response);
  },

  async getById(id: number): Promise<Customer> {
    const response = await apiFetch(
      `/api/customers/${id}`
    );

    return handleResponse<Customer>(response);
  },

  async create(request: CustomerRequest): Promise<Customer> {
    const response = await apiFetch(`/api/customers`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
    });

    return handleResponse<Customer>(response);
  },

  async update(
    id: number,
    request: CustomerRequest
  ): Promise<Customer> {
    const response = await apiFetch(
      `$/api/customers/${id}`,
      {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(request),
      }
    );

    return handleResponse<Customer>(response);
  },

  async deactivate(id: number): Promise<void> {
    const response = await apiFetch(
      `/api/customers/${id}`,
      {
        method: "DELETE",
      }
    );

    await handleResponse<void>(response);
  },
};
