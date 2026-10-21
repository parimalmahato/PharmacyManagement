
import { lazy, Suspense, useEffect, useState } from "react";
import {
  BrowserRouter,
  Navigate,
  NavLink,
  Route,
  Routes,
  useLocation,
} from "react-router-dom";

import "./App.css";

import MedicinesPage from "./pages/MedicinesPage";
import InventoryPage from "./pages/InventoryPage";
import PurchasingPage from "./pages/PurchasingPage";
import SalesPage from "./pages/SalesPage";
import CustomersPage from "./pages/CustomersPage";
import ExpiryPage from "./pages/ExpiryPage";
import NotificationsPage from "./pages/NotificationsPage";
import LoginPage from "./pages/LoginPage";
import UsersPage from "./pages/UsersPage";
import GstReturnsPage from "./pages/GstReturnsPage";
import ChangePasswordPage from "./pages/ChangePasswordPage";
import AuditLogsPage from "./pages/AuditLogsPage";
import PharmacySettingsPage from "./pages/PharmacySettingsPage";
import SalesInvoicePage from "./pages/SalesInvoicePage";
import { notificationsApi } from "./api/notifications";
import { useAuth } from "./auth/useAuth";
import type { UserRole } from "./api/auth";
import BranchesPage from "./pages/BranchesPage";
import HelpAssistantPage from "./pages/HelpAssistantPage";
import AiWorkspacePage from "./pages/AiWorkspacePage";
import { branchesApi, type Branch } from "./api/branches";
const DashboardPage = lazy(() => import("./pages/DashboardPage"));

interface NavigationItem {
  path: string;
  label: string;
  roles: UserRole[];
}

const allRoles: UserRole[] = [
  "Admin",
  "Pharmacist",
  "Cashier",
];

const navigationItems: NavigationItem[] = [
  { path: "/ai", label: "AI Workspace", roles: allRoles },
  { path: "/help", label: "Application Help", roles: allRoles },
  { path: "/gst-returns", label: "GST Returns", roles: ["Admin"] },
  { path: "/branches", label: "Branches", roles: ["Admin"] },
  {
    path: "/dashboard",
    label: "Dashboard",
    roles: ["Admin", "Pharmacist"],
  },
  {
    path: "/medicines",
    label: "Medicines",
    roles: allRoles,
  },
  {
    path: "/inventory",
    label: "Inventory",
    roles: allRoles,
  },
  {
    path: "/purchasing",
    label: "Purchasing",
    roles: ["Admin", "Pharmacist"],
  },
  {
    path: "/sales",
    label: "Sales / Billing",
    roles: allRoles,
  },
  {
    path: "/customers",
    label: "Customers",
    roles: allRoles,
  },
  {
    path: "/expiry",
    label: "Expiry Management",
    roles: ["Admin", "Pharmacist"],
  },
  {
    path: "/notifications",
    label: "🔔 Notifications",
    roles: ["Admin", "Pharmacist"],
  },
  {
    path: "/users",
    label: "Employee Management",
    roles: ["Admin"],
  },
  {
    path: "/audit-logs",
    label: "Audit Logs",
    roles: ["Admin"],
  },
  {
    path: "/settings/pharmacy",
    label: "Pharmacy Settings",
    roles: ["Admin"],
  },
];

// Protect individual routes by role.
function ProtectedRoute({
  roles,
  children,
}: {
  roles: UserRole[];
  children: React.ReactNode;
}) {
  const { hasRole } = useAuth();

  if (!hasRole(...roles)) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}

function PharmacyApp() {
  const { user, logout, hasRole, switchBranch } = useAuth();
  const location = useLocation();

  const [unreadCount, setUnreadCount] = useState(0);
  const [menuOpen, setMenuOpen] = useState(false);
  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchBusy, setBranchBusy] = useState(false);
  const [branchError, setBranchError] = useState("");
  useEffect(() => {
    let active = true;
    const load = () => { branchesApi.getAll().then(data => { if (active) setBranches(data); }).catch(err => { if (active) setBranchError(err.message); }); };
    load(); window.addEventListener("pharmacy-branches-updated", load);
    return () => { active = false; window.removeEventListener("pharmacy-branches-updated", load); };
  }, [location.pathname]);
  const activeModule = availableModule(location.pathname);
  function availableModule(path: string) { return navigationItems.find(item => path.startsWith(item.path))?.label ?? "Workspace"; }

  const canViewNotifications = hasRole(
    "Admin",
    "Pharmacist"
  );

  const availableNavigation = navigationItems.filter(
    (item) => hasRole(...item.roles)
  );

  const defaultPath =
    hasRole("Cashier") &&
    !hasRole("Admin", "Pharmacist")
      ? "/sales"
      : "/dashboard";

  
    useEffect(() => {
      if (!canViewNotifications) {
        return;
      }

      let cancelled = false;

      async function loadUnreadCount() {
        try {
          const summary =
            await notificationsApi.getSummary();

          if (!cancelled) {
            setUnreadCount(summary.unreadCount);
          }
        } catch (error) {
          if (!cancelled) {
            console.error(
              "Unable to load notification count.",
              error
            );
          }
        }
      }

      void loadUnreadCount();

      const intervalId = window.setInterval(() => {
        void loadUnreadCount();
      }, 60000);

      return () => {
        cancelled = true;
        window.clearInterval(intervalId);
      };
    }, [canViewNotifications, location.pathname]);


  return (
    <div className={`app app-workspace ${menuOpen ? "menu-open" : ""}`}>
      <a className="skip-link" href="#workspace-content">Skip to content</a>
      <header className="app-header app-sidebar">
        <div className="app-header-top">
          <div className="workspace-brand"><span className="brand-mark" aria-hidden="true">+</span><div><strong>Pharmacy</strong><span>Operations workspace</span></div></div>

          <div className="app-user-info">
            <NavLink className="account-password-link" to="/change-password" onClick={() => setMenuOpen(false)}>Change password</NavLink>
            <div className="app-user-details">
              <span className="app-user-name">
                {user?.fullName}
              </span>

              <span className="app-user-role">
                {user?.roles.join(", ")}
              </span>
            </div>

            <button
              type="button"
              className="logout-button"
              onClick={logout}
            >
              Logout
            </button>
          </div>
        </div>

        <nav className="app-nav" id="workspace-navigation" aria-label="Main navigation">
          {availableNavigation.map((item) => (
            <NavLink
              key={item.path}
              to={item.path}
              onClick={() => setMenuOpen(false)}
              className={({ isActive }) =>
                isActive ? "active" : ""
              }
            >
              {item.label}

              {item.path === "/notifications" &&
                unreadCount > 0 && (
                  <span className="notification-badge">
                    {unreadCount}
                  </span>
                )}
            </NavLink>
          ))}
        </nav>
      </header>

      <main id="workspace-content" tabIndex={-1}>
        <div className="workspace-topbar no-print">
          <button type="button" className="menu-toggle" aria-expanded={menuOpen} aria-controls="workspace-navigation" onClick={() => setMenuOpen(value => !value)}>Menu</button>
          <div className="workspace-location"><span>Workspace</span><strong>{activeModule}</strong></div>
          <label className="branch-picker">Active branch<select aria-label="Active branch" disabled={!hasRole("Admin") || branchBusy} value={user?.branchId ?? 1} onChange={async event => {
            setBranchBusy(true); setBranchError("");
            try { await switchBranch(Number(event.target.value)); } catch (err) { setBranchError(err instanceof Error ? err.message : "Unable to switch branch."); setBranchBusy(false); }
          }}>{branches.map(branch => <option key={branch.id} value={branch.id}>{branch.name}</option>)}</select></label>
          <span className="workspace-date">{new Intl.DateTimeFormat("en-IN", { timeZone: "Asia/Kolkata", dateStyle: "medium" }).format(new Date())}</span>
        </div>
        {branchError && <p role="alert" className="workspace-error">{branchError}</p>}
        <p className="branch-context-note no-print">Stock, transactions and settings belong to the selected branch. Switching branches clears unsaved forms. Medicine and supplier catalogs are shared.</p>
        <Suspense fallback={<p role="status" style={{ padding: 32 }}>Loading workspace...</p>}><Routes>
          <Route path="/branches" element={<ProtectedRoute roles={["Admin"]}><BranchesPage /></ProtectedRoute>} />
          <Route
            path="/"
            element={
              <Navigate to={defaultPath} replace />
            }
          />

          <Route
            path="/dashboard"
            element={
              <ProtectedRoute
                roles={["Admin", "Pharmacist"]}
              >
                <DashboardPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/medicines"
            element={
              <ProtectedRoute roles={allRoles}>
                <MedicinesPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/inventory"
            element={
              <ProtectedRoute roles={allRoles}>
                <InventoryPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/purchasing"
            element={
              <ProtectedRoute
                roles={["Admin", "Pharmacist"]}
              >
                <PurchasingPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/sales"
            element={
              <ProtectedRoute roles={allRoles}>
                <SalesPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/customers"
            element={
              <ProtectedRoute roles={allRoles}>
                <CustomersPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/expiry"
            element={
              <ProtectedRoute
                roles={["Admin", "Pharmacist"]}
              >
                <ExpiryPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/notifications"
            element={
              <ProtectedRoute
                roles={["Admin", "Pharmacist"]}
              >
                <NotificationsPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/users"
            element={
              <ProtectedRoute roles={["Admin"]}>
                <UsersPage />
              </ProtectedRoute>
            }
          />
          <Route path="/help" element={<ProtectedRoute roles={allRoles}><HelpAssistantPage /></ProtectedRoute>} />
          <Route path="/ai" element={<ProtectedRoute roles={allRoles}><AiWorkspacePage /></ProtectedRoute>} />
          <Route path="/gst-returns" element={<ProtectedRoute roles={["Admin"]}><GstReturnsPage /></ProtectedRoute>} />

          <Route
            path="/audit-logs"
            element={
              <ProtectedRoute roles={["Admin"]}>
                <AuditLogsPage />
              </ProtectedRoute>
            }
          />

          <Route
            path="/settings/pharmacy"
            element={
              <ProtectedRoute roles={["Admin"]}>
                <PharmacySettingsPage />
              </ProtectedRoute>
            }
          />
          <Route
            path="/sales/:saleId/invoice"
            element={
              <ProtectedRoute
                roles={["Admin", "Pharmacist", "Cashier"]}
              >
                <SalesInvoicePage />
              </ProtectedRoute>
            }
          />
          <Route
            path="*"
            element={
              <Navigate to={defaultPath} replace />
            }
          />
        </Routes></Suspense>
      </main>
    </div>
  );
}

function AppContent() {
  const { isAuthenticated, user } = useAuth();
  const location = useLocation();

  if (!isAuthenticated || !user) {
    return <LoginPage />;
  }

  if (user.mustChangePassword || location.pathname === "/change-password") {
    return <ChangePasswordPage />;
  }

  return <PharmacyApp key={`${user.email}:${user.branchId}`} />;
}

function App() {
  return (
    <BrowserRouter>
      <AppContent />
    </BrowserRouter>
  );
}

export default App;
