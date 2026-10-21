
import { createContext } from "react";
import type {
  LoginRequest,
  LoginResponse,
  UserRole,
} from "../api/auth";

export interface AuthContextType {
  user: LoginResponse | null;
  isAuthenticated: boolean;
  login: (request: LoginRequest) => Promise<void>;
  logout: () => void;
  switchBranch: (branchId: number) => Promise<void>;
  hasRole: (...roles: UserRole[]) => boolean;
}

export const AuthContext =
  createContext<AuthContextType | undefined>(undefined);
