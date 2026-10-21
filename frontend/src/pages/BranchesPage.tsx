import { useEffect, useState } from "react";
import { branchesApi, type Branch } from "../api/branches";
export default function BranchesPage() {
  const [branches, setBranches] = useState<Branch[]>([]);
  const [code, setCode] = useState(""); const [name, setName] = useState("");
  const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  useEffect(() => { let active = true; branchesApi.getAll().then(rows => { if (active) setBranches(rows); }).catch(e => { if (active) setError(e.message); }); return () => { active = false; }; }, []);
  return <div className="dashboard-page"><h1>Branches</h1><p>Shared medicine catalog. Separate inventory, invoices, customers, settings and financial records.</p>
    {error && <p role="alert" className="dashboard-error">{error}</p>}
    <section className="dashboard-section"><h2>Add a branch</h2><form className="dashboard-filters" onSubmit={async event => {
      event.preventDefault(); setBusy(true); setError("");
      try { await branchesApi.create(code, name); setBranches(await branchesApi.getAll()); setCode(""); setName(""); }
      catch (e) { setError(e instanceof Error ? e.message : "Unable to create branch."); } finally { setBusy(false); }
    }}><label>Branch code<input required maxLength={20} pattern="[A-Za-z0-9-]+" value={code} disabled={busy} onChange={e => setCode(e.target.value.toUpperCase())} /></label>
      <label>Branch name<input required maxLength={150} value={name} disabled={busy} onChange={e => setName(e.target.value)} /></label><button disabled={busy}>{busy ? "Saving..." : "Create branch"}</button></form>
      <p>After creating a branch, select it in the workspace header, configure Pharmacy Settings, and assign employees. New branches start with empty stock.</p></section>
    <section className="dashboard-section"><h2>Branch directory</h2><div className="dashboard-table-wrapper"><table className="dashboard-table"><thead><tr><th>Code</th><th>Name</th><th>Status</th></tr></thead>
      <tbody>{branches.map(b => <tr key={b.id}><td>{b.code}</td><td>{b.name}</td><td>{b.isActive ? "Active" : "Inactive"}</td></tr>)}</tbody></table></div></section>
  </div>;
}
