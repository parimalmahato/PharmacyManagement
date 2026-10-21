
import { useState } from "react";
import { authApi } from "../api/auth";
import { useAuth } from "../auth/useAuth";
import "./ChangePasswordPage.css";
import { Link, useNavigate } from "react-router-dom";

export default function ChangePasswordPage() {
  const { logout, user } = useAuth();
  const navigate = useNavigate();
  function signOut() { logout(); navigate("/", { replace: true }); }
  const [showPasswords, setShowPasswords] = useState(false);

  const [currentPassword, setCurrentPassword] =
    useState("");
  const [newPassword, setNewPassword] =
    useState("");
  const [confirmPassword, setConfirmPassword] =
    useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [success, setSuccess] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();

    setError("");

    if (newPassword !== confirmPassword) {
      setError("New passwords do not match.");
      return;
    }

    if (newPassword.length < 12) {
      setError("Password must be at least 12 characters.");
      return;
    }

    setBusy(true);

    try {
      await authApi.changePassword(
        currentPassword,
        newPassword
      );

      setSuccess(true);
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Password change failed."
      );
    } finally {
      setBusy(false);
    }
  }

  if (success) {
    return (
      <main className="password-page">
        <section className="password-card password-success" aria-labelledby="password-success-heading">
        <div className="password-emblem" aria-hidden="true">✓</div>
        <span className="password-eyebrow">Account security</span>
        <h1 id="password-success-heading">You’re all set</h1>
        <p role="status">
          Your password was changed successfully.
          Sign in again with your new password to continue.
        </p>
        <button className="password-submit" type="button" onClick={signOut}>
          Back to sign in
        </button>
        </section>
      </main>
    );
  }

  return (
    <main className="password-page">
      <section className="password-card" aria-labelledby="password-heading">
      <div className="password-brand"><span aria-hidden="true">+</span> Pharmacy <span className="password-brand-caption">Management</span></div>
      <header className="password-heading">
        <span className="password-eyebrow">Account security</span>
        <h1 id="password-heading">{user?.mustChangePassword ? "Set your new password" : "Change your password"}</h1>
        <p>{user?.mustChangePassword
          ? "Welcome aboard. Replace your temporary password to access your pharmacy workspace."
          : "Choose a strong password to keep your pharmacy account secure."}</p>
      </header>

      {error && <p className="password-error" role="alert">{error}</p>}

      <form className="password-form" onSubmit={submit} aria-busy={busy}>
        <label htmlFor="current-password">Current password</label>
          <input
            id="current-password"
            type={showPasswords ? "text" : "password"}
            autoComplete="current-password"
            placeholder="Enter your current or temporary password"
            disabled={busy}
            value={currentPassword}
            onChange={(e) =>
              setCurrentPassword(e.target.value)
            }
            required
          />

        <label htmlFor="new-password">New password</label>
          <input
            id="new-password"
            type={showPasswords ? "text" : "password"}
            autoComplete="new-password"
            placeholder="Create a strong password"
            aria-describedby="password-requirements"
            disabled={busy}
            value={newPassword}
            onChange={(e) =>
              setNewPassword(e.target.value)
            }
            required
            minLength={12}
          />
        <p className="password-requirements" id="password-requirements">Use at least 12 characters with uppercase and lowercase letters, a number and a special character.</p>

        <label htmlFor="confirm-password">Confirm new password</label>
          <input
            id="confirm-password"
            type={showPasswords ? "text" : "password"}
            autoComplete="new-password"
            placeholder="Re-enter your new password"
            disabled={busy}
            value={confirmPassword}
            onChange={(e) =>
              setConfirmPassword(e.target.value)
            }
            required
          />
        <label className="password-visibility">
          <input type="checkbox" checked={showPasswords} disabled={busy} onChange={event => setShowPasswords(event.target.checked)} />
          Show passwords
        </label>

        <button className="password-submit" type="submit" disabled={busy}>
          {busy ? "Updating password…" : "Update password"}
        </button>
      </form>
      <footer className="password-footer">
        {!user?.mustChangePassword && !busy && <p><Link to="/">Back to workspace</Link></p>}
        Signed in to the wrong account? <button type="button" disabled={busy} onClick={signOut}>Sign out</button>
      </footer>
      </section>
    </main>
  );
}
