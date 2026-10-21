import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import { aiRequest, type AiStatus, type InventoryAnswer, type Reorder, type SalesInsight, type Sentiment } from "../api/ai";
import "./HelpAssistantPage.css";
import "./AiWorkspacePage.css";

type Tab = "inventory" | "reorder" | "sales" | "feedback" | "knowledge";
export default function AiWorkspacePage() {
  const { hasRole } = useAuth();
  const manager = hasRole("Admin", "Pharmacist");
  const [tab, setTab] = useState<Tab>("inventory");
  const [status, setStatus] = useState<AiStatus | null>(null);
  const [question, setQuestion] = useState("Which medicines are near expiry?");
  const [search, setSearch] = useState("");
  const [days, setDays] = useState(30);
  const [history, setHistory] = useState(30);
  const [coverage, setCoverage] = useState(30);
  const [feedback, setFeedback] = useState("");
  const [inventory, setInventory] = useState<InventoryAnswer | null>(null);
  const [reorders, setReorders] = useState<Reorder[] | null>(null);
  const [sales, setSales] = useState<SalesInsight | null>(null);
  const [sentiment, setSentiment] = useState<Sentiment | null>(null);
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const pending = useRef<AbortController | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    void aiRequest<AiStatus>("status", undefined, controller.signal).then(setStatus).catch(() => {});
    return () => { controller.abort(); pending.current?.abort(); };
  }, []);
  async function run() {
    if (busy) return;
    const controller = new AbortController(); pending.current = controller;
    setBusy(true); setError(""); setNotice("");
    if (tab === "inventory") setInventory(null);
    if (tab === "reorder") setReorders(null);
    if (tab === "sales") setSales(null);
    if (tab === "feedback") setSentiment(null);
    try {
      if (tab === "inventory") setInventory(await aiRequest<InventoryAnswer>("inventory", { question, search, days }, controller.signal));
      if (tab === "reorder") setReorders(await aiRequest<Reorder[]>(`reorders?historyDays=${history}&horizonDays=${coverage}`, undefined, controller.signal));
      if (tab === "sales") setSales(await aiRequest<SalesInsight>(`sales-insights?days=${days}`, undefined, controller.signal));
      if (tab === "feedback") setSentiment(await aiRequest<Sentiment>("sentiment", { text: feedback }, controller.signal));
      if (tab === "knowledge") { const result = await aiRequest<{ indexedChunks: number }>("knowledge/index", {}, controller.signal); setNotice(`Indexed ${result.indexedChunks} user-guide chunks. Semantic search is ready for this guide version.`); }
    } catch (err) { if (!controller.signal.aborted) setError(err instanceof Error ? err.message : "Unable to complete the request."); }
    finally { if (!controller.signal.aborted) setBusy(false); }
  }
  const tabs: [Tab, string][] = [["inventory", "Inventory questions"], ...(manager ? [["reorder", "Reorder planner"], ["sales", "Sales insights"], ["feedback", "Feedback sentiment"]] as [Tab, string][] : []), ...(hasRole("Admin") ? [["knowledge", "Knowledge index"]] as [Tab, string][] : [])];
  return <div className="help-page ai-workspace">
    <header><span className="help-eyebrow">BRANCH INTELLIGENCE</span><h1>AI workspace</h1><p>Explore verified branch data and review AI suggestions before acting.</p><Link to="/help">Open application help and document Q&amp;A →</Link></header>
    {status && <p className="help-note">Model: {status.modelConfigured ? "configured" : "not configured"} · Invoice vision: {status.visionConfigured ? "configured" : "not configured"} · Vector search: {status.vectorConfigured ? "configured" : "not configured"}. These flags do not confirm service connectivity.</p>}
    {manager && <p><Link to="/purchasing">Extract a supplier invoice in Purchasing →</Link></p>}
    <nav className="ai-tabs" aria-label="AI features">{tabs.map(([key, label]) => <button key={key} aria-current={tab === key ? "page" : undefined} disabled={busy} onClick={() => { setTab(key); setError(""); }}>{label}</button>)}</nav>
    <section className="help-card">
      <form onSubmit={event => { event.preventDefault(); void run(); }}>
        {tab === "inventory" && <><h2>Ask about stock</h2><p>Supports stock, expiry and low-stock questions. Use the controls for the exact medicine and horizon; dates or medicine names written only in the question are not applied as filters.</p><label>Question<textarea required maxLength={1000} value={question} onChange={e => setQuestion(e.target.value)} /></label><label>Medicine or batch filter<input maxLength={150} value={search} onChange={e => setSearch(e.target.value)} /></label></>}
        {(tab === "inventory" || tab === "sales") && <label>{tab === "inventory" ? "Expiry horizon (days)" : "Sales period (days)"}<input type="number" min={1} max={tab === "sales" ? 90 : 365} required value={days} onChange={e => setDays(Number(e.target.value))} /></label>}
        {tab === "reorder" && <><h2>Reorder planner</h2><p>Transparent demand calculation, not a trained forecast. It excludes cancelled/returned demand, subtracts pending draft purchases and conservatively excludes stock expiring within the coverage period. Review supplier lead times, seasonality and pack sizes yourself.</p><label>Sales history (days)<input type="number" required min={7} max={365} value={history} onChange={e => setHistory(Number(e.target.value))} /></label><label>Target coverage (days)<input type="number" required min={1} max={180} value={coverage} onChange={e => setCoverage(Number(e.target.value))} /></label></>}
        {tab === "sales" && <><h2>Explain sales performance</h2><p>The model receives active-branch aggregate sales data. Generated explanations may be inaccurate; check the figures below.</p></>}
        {tab === "feedback" && <><h2>Understand service feedback</h2><p>Paste anonymized feedback only. It is sent to the configured model service for analysis and is not saved by this application. Results are suggestions, not decisions about staff or patients.</p><label>Customer feedback<textarea required maxLength={4000} rows={5} value={feedback} onChange={e => setFeedback(e.target.value)} /></label></>}
        {tab === "knowledge" && <><h2>Build semantic knowledge index</h2><p>Embeds the packaged user guide into Qdrant. Rebuild after guide or embedding-model changes. No customer or transaction records are indexed. This requires configured, running Ollama and Qdrant services.</p></>}
        <button className="ai-primary" disabled={busy}>{busy ? "Working…" : tab === "knowledge" ? "Index current user guide" : "Run analysis"}</button>
      </form>
    </section>
    {error && <p role="alert" className="help-error">{error}</p>}{notice && <p role="status">{notice}</p>}
    <div aria-live="polite" aria-busy={busy}>
      {tab === "inventory" && inventory && <section className="help-card"><h2>Inventory results</h2><p>{inventory.mode} · {inventory.interpretation}</p><p>{inventory.notes}</p><div className="ai-table"><table><thead><tr><th>Medicine</th><th>Batch</th><th>Expiry</th><th>Quantity</th></tr></thead><tbody>{inventory.batches.map((b, i) => <tr key={i}><td>{b.medicine}</td><td>{b.batch}</td><td>{b.expiry}</td><td>{b.quantity}</td></tr>)}</tbody></table></div>{inventory.batches.length === 0 && <p>No matching batches in this branch.</p>}</section>}
      {tab === "reorder" && reorders && <section className="help-card"><h2>Purchase suggestions</h2><p>Nothing has been ordered. Daily demand = net sold units ÷ history days. Target = rounded-up demand × coverage days.</p><div className="ai-table"><table><thead><tr><th>Medicine</th><th>Available</th><th>Expiring</th><th>Net sold</th><th>Draft orders</th><th>Daily demand</th><th>Target</th><th>Suggested</th></tr></thead><tbody>{reorders.map(r => <tr key={r.medicineId}><td>{r.medicine}</td><td>{r.available}</td><td>{r.expiring}</td><td>{r.sold}</td><td>{r.pendingPurchase}</td><td>{r.dailyDemand}</td><td>{r.targetQuantity}</td><td><strong>{r.suggestedQuantity}</strong></td></tr>)}</tbody></table></div></section>}
      {tab === "sales" && sales && <section className="help-card"><h2>Sales insights</h2><p className="help-mode">{sales.mode}</p><p>{sales.facts.startDate} – {sales.facts.endDate}</p><p className="help-answer">{sales.explanation}</p><dl className="ai-metrics">{[["Invoices", sales.facts.totalSales], ["Invoiced INR", sales.facts.totalRevenue], ["Credits INR", sales.facts.returnCredits], ["Net sales INR", sales.facts.netSales], ["Collections INR", sales.facts.collections], ["Refunds INR", sales.facts.refunds], ["Net collections INR", sales.facts.netCollections]].map(([name, value]) => <div key={name}><dt>{name}</dt><dd>{Number(value).toLocaleString("en-IN", { maximumFractionDigits: 2 })}</dd></div>)}</dl></section>}
      {tab === "feedback" && sentiment && <section className="help-card"><span className="help-mode">AI interpretation · {sentiment.sentiment}</span><h2>Feedback summary</h2><p>{sentiment.summary}</p><ul>{sentiment.themes.map((t, i) => <li key={i}>{t}</li>)}</ul></section>}
    </div>
  </div>;
}
