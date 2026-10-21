
import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import "./LoginPage.css";

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    setLoading(true);
    setError("");

    try {
      await login({
        email: email.trim(),
        password,
      });
      // The root route selects this account's role home instead of reusing the previous URL.
      navigate("/", { replace: true });
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : "Unable to sign in."
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={handleSubmit}>
        <div className="login-header">
          <div className="login-icon">+</div>
          <h1>Pharmacy Management</h1>
          <p>Sign in to your account</p>
        </div>

        {error && (
          <div className="login-error" role="alert">
            {error}
          </div>
        )}

        <label htmlFor="email">Email Address</label>
        <input
          id="email"
          type="email"
          autoComplete="username"
          placeholder="Enter your email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
        />

        <label htmlFor="password">Password</label>
        <input
          id="password"
          type="password"
          autoComplete="current-password"
          placeholder="Enter your password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />

        <button type="submit" disabled={loading}>
          {loading ? "Signing in..." : "Sign In"}
        </button>
        <p className="login-recovery-help">Forgot your password? Contact your pharmacy administrator for a temporary password. You’ll choose a new password after signing in.</p>
      </form>
    </div>
  );
}
