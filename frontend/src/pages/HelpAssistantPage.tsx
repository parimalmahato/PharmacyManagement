import { useEffect, useRef, useState } from "react";
import { askHelp, type HelpAnswer } from "../api/helpAssistant";
import "./HelpAssistantPage.css";

const examples = ["How do I record a split payment?", "How do I return medicines safely?", "How can an employee reset a forgotten password?"];

export default function HelpAssistantPage() {
  const [question, setQuestion] = useState("");
  const [submitted, setSubmitted] = useState("");
  const [answer, setAnswer] = useState<HelpAnswer | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const pending = useRef<AbortController | null>(null);
  useEffect(() => () => pending.current?.abort(), []);

  async function submit(value: string) {
    if (busy || !value.trim()) return;
    const controller = new AbortController();
    pending.current = controller;
    setQuestion(value); setSubmitted(value); setAnswer(null); setError(""); setBusy(true);
    try { setAnswer(await askHelp(value.trim(), controller.signal)); }
    catch (err) {
      if (!controller.signal.aborted) setError(err instanceof Error ? err.message : "Unable to load help.");
    } finally { if (!controller.signal.aborted) setBusy(false); }
  }

  return <div className="help-page">
    <header><span className="help-eyebrow">YOUR WORKSPACE COMPANION</span><h1>Application help</h1>
      <p>Get step-by-step guidance for pharmacy workflows, with the relevant user-guide sections.</p></header>
    <section className="help-card">
      <h2>What would you like to do?</h2>
      <p className="help-note">Ask about using the application. Do not include customer details or payment credentials. This assistant cannot access live records, perform actions or provide medical advice.</p>
      <div className="help-examples">{examples.map(example => <button type="button" key={example} disabled={busy} onClick={() => void submit(example)}>{example}</button>)}</div>
      <form onSubmit={event => { event.preventDefault(); void submit(question); }}>
        <label htmlFor="help-question">Your application question</label>
        <textarea id="help-question" value={question} onChange={event => setQuestion(event.target.value)} maxLength={1000} rows={3} required disabled={busy} placeholder="For example: How do I cancel an incorrect sale?" />
        <div className="help-submit"><small>{question.length}/1000 characters</small><button type="submit" disabled={busy || !question.trim()}>{busy ? "Finding guidance…" : "Ask for help"}</button></div>
      </form>
    </section>
    {error && <p role="alert" className="help-error">{error}</p>}
    <div aria-live="polite" aria-busy={busy}>
      {busy && <p>Searching the application guide…</p>}
      {answer && <section className="help-card">
        <span className="help-mode">{answer.mode === "langflow" || answer.mode === "ollama" ? "AI-generated guidance · verify against the sources" : answer.mode === "guide" ? "User-guide retrieval · no AI generation" : "No matching guidance"} · {answer.retrieval === "vector" ? "Semantic vector search" : "Keyword search"}</span>
        <h2>{submitted}</h2><p className="help-answer">{answer.answer}</p>
        {answer.sources.length > 0 && <><h3>Guide sources</h3><p>These are the retrieved sections. Your account permissions still apply.</p>
          {answer.sources.map(source => <details className="help-source" key={source.id} open={answer.mode === "guide"} id={source.id}>
            <summary>[{source.id}] {source.title}</summary><div className="help-source-content">{source.content}</div>
          </details>)}
        </>}
      </section>}
    </div>
  </div>;
}
