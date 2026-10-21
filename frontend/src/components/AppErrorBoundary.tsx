import { Component, type ReactNode } from "react";
export default class AppErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  render() {
    if (this.state.failed) return <main style={{ maxWidth: 560, margin: "15vh auto", padding: 28 }}><h1>Unable to display this screen</h1>
      <p>Your saved records remain on the server. Reload to sign in again, then check transaction history before retrying a payment or sale.</p>
      <button type="button" onClick={() => window.location.reload()}>Reload application</button></main>;
    return this.props.children;
  }
}
