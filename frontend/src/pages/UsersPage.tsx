
import { useCallback, useEffect, useRef, useState } from "react";
import {
  usersApi,
  type Employee,
  type EmployeeRole,
} from "../api/users";
import "./UsersPage.css";
import { branchesApi, type Branch } from "../api/branches";
import { useAuth } from "../auth/useAuth";

export default function UsersPage() {
  const { user } = useAuth();
  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchId, setBranchId] = useState(user?.branchId ?? 1);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [resetEmployee, setResetEmployee] = useState<Employee | null>(null);
  const [temporaryPassword, setTemporaryPassword] = useState("");
  const [confirmTemporaryPassword, setConfirmTemporaryPassword] = useState("");
  const [resetError, setResetError] = useState("");
  const temporaryPasswordInput = useRef<HTMLInputElement>(null);
  useEffect(() => { if (resetEmployee) temporaryPasswordInput.current?.focus(); }, [resetEmployee]);
  useEffect(() => { let active = true; branchesApi.getAll().then(data => { if (active) setBranches(data); }).catch(err => { if (active) setError(err.message); }); return () => { active = false; }; }, []);

  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState<EmployeeRole>("Cashier");

  const loadEmployees = useCallback(async () => {
    try {
      const data = await usersApi.getAll();
      setEmployees(data);
      setError("");
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to load employees."
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    Promise.resolve().then(() => {
    void loadEmployees();
    });
  }, [loadEmployees]);

  async function createEmployee(
    event: React.FormEvent<HTMLFormElement>
  ) {
    event.preventDefault();

    setBusy(true);
    setError("");
    setMessage("");

    try {
      await usersApi.create({
        fullName: fullName.trim(),
        email: email.trim(),
        password,
        role,
        branchId,
      });

      setFullName("");
      setEmail("");
      setPassword("");
      setRole("Cashier");

      await loadEmployees();

      setMessage("Employee created successfully.");
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to create employee."
      );
    } finally {
      setBusy(false);
    }
  }

  async function toggleStatus(employee: Employee) {
    setBusy(true);
    setError("");
    setMessage("");

    try {
      await usersApi.updateStatus(
        employee.id,
        !employee.isActive
      );

      await loadEmployees();

      setMessage("Employee status updated successfully.");
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to update employee."
      );
    } finally {
      setBusy(false);
    }
  }

  async function resetPassword(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!resetEmployee || busy) return;
    setResetError("");
    if (temporaryPassword !== confirmTemporaryPassword) { setResetError("Temporary passwords do not match."); return; }
    if (temporaryPassword.length < 12 || !/[A-Z]/.test(temporaryPassword) || !/[a-z]/.test(temporaryPassword) || !/[0-9]/.test(temporaryPassword) || !/[^a-zA-Z0-9]/.test(temporaryPassword)) {
      setResetError("Use at least 12 characters with uppercase and lowercase letters, a number and a special character."); return;
    }
    setBusy(true); setError(""); setMessage("");
    try {
      await usersApi.resetPassword(resetEmployee.id, temporaryPassword);
      setMessage(`Password reset for ${resetEmployee.fullName}. Share the temporary password privately. They must sign in again and choose a new password.`);
      setResetEmployee(null); setTemporaryPassword(""); setConfirmTemporaryPassword("");
    } catch (err) { setResetError(err instanceof Error ? err.message : "Unable to reset password."); }
    finally { setBusy(false); }
  }

  return (
    <main className="users-page">
      <h1>Employee Management</h1>

      {error && (
        <p className="users-error" role="alert">
          {error}
        </p>
      )}

      {message && (
        <p className="users-success" role="status">
          {message}
        </p>
      )}

      <section className="users-panel">
        <h2>Create Employee</h2>

        <form onSubmit={createEmployee} className="users-form">
          <label>
            Full Name
            <input
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              required
              maxLength={150}
            />
          </label>

          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </label>

          <label>
            Initial Password
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="new-password"
              minLength={12}
              required
            />
          </label>

          <label>
            Role
            <select
              value={role}
              onChange={(e) =>
                setRole(e.target.value as EmployeeRole)
              }
            >
              <option value="Cashier">Cashier</option>
              <option value="Pharmacist">Pharmacist</option>
            </select>
          </label>

          <label>Assigned branch<select value={branchId} disabled={busy} onChange={e => setBranchId(Number(e.target.value))}>{branches.filter(b => b.isActive).map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select></label>
          <button type="submit" disabled={busy}>
            {busy ? "Saving..." : "Create Employee"}
          </button>
        </form>
      </section>

      <section className="users-panel">
        <h2>Employees</h2>
        <p className="users-help">Forgotten password? Reset the employee’s password here, then share the temporary password privately. They will choose a new password at next sign-in.</p>

        {resetEmployee && <section className="users-reset-panel" aria-labelledby="employee-reset-heading">
          <h3 id="employee-reset-heading">Reset password for {resetEmployee.fullName}</h3>
          <p className="users-help">{resetEmployee.email} · Their current password will stop working.</p>
          {resetError && <p className="users-error" role="alert">{resetError}</p>}
          <form className="users-form" onSubmit={resetPassword} aria-busy={busy}>
            <label>Temporary password<input ref={temporaryPasswordInput} type="password" autoComplete="new-password" value={temporaryPassword} onChange={event => setTemporaryPassword(event.target.value)} minLength={12} required disabled={busy} aria-describedby="temporary-password-help" /></label>
            <label>Confirm temporary password<input type="password" autoComplete="new-password" value={confirmTemporaryPassword} onChange={event => setConfirmTemporaryPassword(event.target.value)} minLength={12} required disabled={busy} /></label>
            <p className="users-help users-full-row" id="temporary-password-help">At least 12 characters, including uppercase, lowercase, a number and a special character. Resetting signs the employee out.</p>
            <div className="users-actions users-full-row">
              <button type="submit" disabled={busy}>{busy ? "Resetting…" : "Reset password"}</button>
              <button type="button" className="users-cancel" disabled={busy} onClick={() => { setResetEmployee(null); setTemporaryPassword(""); setConfirmTemporaryPassword(""); setResetError(""); }}>Cancel</button>
            </div>
          </form>
        </section>}

        {loading ? (
          <p>Loading employees...</p>
        ) : employees.length === 0 ? (
          <p>No employees found.</p>
        ) : (
          <div className="users-table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Email</th>
                  <th>Role</th>
                  <th>Assigned branch</th>
                  <th>Status</th>
                  <th>Action</th>
                </tr>
              </thead>

              <tbody>
                {employees.map((employee) => {
                  const isAdmin =
                    employee.roles.includes("Admin");

                  return (
                    <tr key={employee.id}>
                      <td>{employee.fullName}</td>
                      <td>{employee.email}</td>
                      <td>{employee.roles.join(", ")}</td>
                      <td><select aria-label={`Branch for ${employee.fullName}`} disabled={busy || isAdmin} value={employee.branchId} onChange={async e => {
                        setBusy(true); setError("");
                        try { await usersApi.assignBranch(employee.id, Number(e.target.value)); await loadEmployees(); setMessage("Branch assignment saved. The employee must sign in again."); }
                        catch (err) { setError(err instanceof Error ? err.message : "Unable to assign branch."); } finally { setBusy(false); }
                      }}>{branches.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select></td>
                      <td>
                        {employee.isActive
                          ? "Active"
                          : "Inactive"}
                      </td>
                      <td>
                        <div className="users-actions">
                        <button
                          type="button"
                          disabled={busy || isAdmin}
                          onClick={() =>
                            void toggleStatus(employee)
                          }
                        >
                          {employee.isActive
                            ? "Deactivate"
                            : "Activate"}
                        </button>
                        {!isAdmin && <button type="button" disabled={busy} onClick={() => { setResetEmployee(employee); setTemporaryPassword(""); setConfirmTemporaryPassword(""); setResetError(""); setMessage(""); }}>Reset password</button>}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  );
}
