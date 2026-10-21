import { apiFetch } from "./apiClient";



export type UserRole = "Admin" | "Pharmacist" | "Cashier";

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  branchId: number;
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  fullName: string;
  email: string;
  roles: UserRole[];
  mustChangePassword: boolean;
}

export const authApi = {
  async login(request: LoginRequest): Promise<LoginResponse> {
    const response = await apiFetch(`/api/auth/login`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => null);

      throw new Error(
        response.status === 401
          ? "Invalid email or password."
          : error?.message ?? "Unable to sign in."
      );
    }

    return response.json() as Promise<LoginResponse>;
  },
  changePassword: async (
  currentPassword: string,
  newPassword: string
  ): Promise<void> => {
    const response = await apiFetch(
      "/api/auth/change-password",
      {
        method: "POST",
        body: JSON.stringify({
          currentPassword,
          newPassword,
        }),
      }
    );

    if (!response.ok) {
      const body = await response.json().catch(() => null);

      throw new Error(
        body?.message ?? "Unable to change password."
      );
    }
  },
};
