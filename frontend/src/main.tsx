
import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import { AuthProvider } from "./auth/AuthContext";
import "./index.css";
import "./Workspace.css";
import AppErrorBoundary from "./components/AppErrorBoundary";

ReactDOM.createRoot(
  document.getElementById("root")!
).render(
  <React.StrictMode>
    <AppErrorBoundary><AuthProvider>
      <App />
    </AuthProvider></AppErrorBoundary>
  </React.StrictMode>
);
