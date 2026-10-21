
import {
  useCallback,
  useEffect,
  useState,
  type ReactNode,
} from "react";

import {
  authApi,
  type LoginRequest,
  type LoginResponse,
  type UserRole,
} from "../api/auth";

import { tokenStore } from "./tokenStore";
import { AuthContext } from "./authContextDefinition";
import { apiFetch } from "../api/apiClient";

export function AuthProvider({
  children,
}: {
  children: ReactNode;
}) {
  const [user, setUser] = useState<LoginResponse | null>(null);

  const logout = useCallback(() => {
    tokenStore.clear();
    setUser(null);
  }, []);

  const login = useCallback(
    async (request: LoginRequest) => {
      tokenStore.clear();
      setUser(null);

      const result = await authApi.login(request);

      const expiresAt = new Date(
        result.expiresAtUtc
      ).getTime();

      if (
        !Number.isFinite(expiresAt) ||
        expiresAt <= Date.now()
      ) {
        throw new Error(
          "The access token is invalid or has already expired."
        );
      }

      tokenStore.set(result.accessToken);
      setUser(result);
    },
    []
  );

  const hasRole = (...roles: UserRole[]): boolean => {
    if (!user) {
      return false;
    }

    return roles.some((role) =>
      user.roles.includes(role)
    );
  };
  const switchBranch = useCallback(async (branchId: number) => {
    const response = await apiFetch(`/api/branches/${branchId}/switch`, { method: "POST" });
    if (!response.ok) { const error = await response.json().catch(() => null); throw new Error(error?.message ?? "Unable to switch branch."); }
    const result: LoginResponse = await response.json();
    tokenStore.set(result.accessToken); setUser(result);
  }, []);
  useEffect(() => {
    window.addEventListener("pharmacy-session-ended", logout);
    return () => window.removeEventListener("pharmacy-session-ended", logout);
  }, [logout]);

  
useEffect(() => {
  if (!user) {
    return;
  }

  const remainingMilliseconds =
    new Date(user.expiresAtUtc).getTime() - Date.now();

  // Schedule logout asynchronously instead of
  // calling setState directly inside the effect.
  const timeoutId = window.setTimeout(
    logout,
    Math.max(0, remainingMilliseconds)
  );

  return () => {
    window.clearTimeout(timeoutId);
  };
}, [user, logout]);


  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: user !== null,
        login,
        logout,
        switchBranch,
        hasRole,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
