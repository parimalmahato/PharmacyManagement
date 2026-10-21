
import { tokenStore } from "../auth/tokenStore";

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/$/, "");

export async function apiFetch(
  path: string,
  options: RequestInit = {}
): Promise<Response> {
  const headers = new Headers(options.headers);

  const token = tokenStore.get();

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  if (
    options.body != null &&
    !(options.body instanceof FormData) &&
    !headers.has("Content-Type")
  ) {
    headers.set("Content-Type", "application/json");
  }

  let response: Response;
  try { response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
  }); } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") throw error;
    throw new Error("Unable to reach the server. Check your connection. Before retrying a transaction, refresh its history.", { cause: error });
  }
  if (response.status === 401 && token && tokenStore.get() === token) window.dispatchEvent(new Event("pharmacy-session-ended"));
  return response;
}
