import { apiFetch } from "./apiClient";

export interface HelpSource { id: string; title: string; content: string }
export interface HelpAnswer { answer: string; mode: "guide" | "langflow" | "ollama" | "no-match"; retrieval: "keyword" | "vector"; sources: HelpSource[] }

export async function askHelp(question: string, signal: AbortSignal): Promise<HelpAnswer> {
  const response = await apiFetch("/api/help-assistant/ask", {
    method: "POST", body: JSON.stringify({ question }), signal,
  });
  if (!response.ok) {
    const error = await response.json().catch(() => null);
    throw new Error(error?.message ?? error?.title ?? "Help is unavailable. Please try again.");
  }
  return response.json();
}
