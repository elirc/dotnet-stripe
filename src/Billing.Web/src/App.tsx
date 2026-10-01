import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { api, ApiError, label, money, operation, send } from "./api";
import type { Dashboard, Entity, Operation, Page, Session } from "./api";
import { fields, initial, localTime, payload } from "./forms";
import type { Field, Values } from "./forms";

const tabs = [
  ["customers", "Customers", "Create a customer, then choose a plan and start their subscription."],
  ["plans", "Plans", "Flat fees, metered usage, or a hybrid. Referenced pricing stays immutable."],
  ["coupons", "Coupons", "A percentage or a fixed amount. Archive an offer to stop new use."],
  ["subscriptions", "Subscriptions", "Billing periods advance with the simulator clock. Cancellation takes effect at period end."],
  ["usage-events", "Usage", "Record and correct unbilled usage. Invoicing locks each event to its billing period."],
  ["invoices", "Invoices", "Inspect the calculation, queue a simulated payment, or void an eligible open invoice."],
  ["payments", "Payments", "Each attempt has one identity. Declines stay in history when a new attempt succeeds."],
  ["webhooks", "Outbox", "Durable events for the local simulator. No external HTTP delivery takes place."],
  ["members", "People", "Owners manage local workshop accounts. Operators can write; viewers can read."],
  ["audit", "Activity", "The latest 100 committed workspace actions."]
] as const;
const emptyPage: Page = { items: [], total: 0, offset: 0, limit: 20 };
const writable = ["customers", "plans", "coupons", "subscriptions", "usage-events", "members"];

function Reference({ field, value, token, onChange, disabled }: { field: Field; value: string; token: string; onChange: (value: string) => void; disabled: boolean }) {
  const [query, setQuery] = useState(""); const [rows, setRows] = useState<Entity[]>([]); const [error, setError] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      api<Page>(token, "/" + field.relation + "?limit=100&search=" + encodeURIComponent(query), { signal: controller.signal })
        .then(async page => {
          if (value && !page.items.some(item => item.id === value)) {
            const selected = await api<Entity>(token, "/" + field.relation + "/" + value, { signal: controller.signal });
            page.items.unshift(selected);
          }
          if (!controller.signal.aborted) { setRows(page.items); setError(""); }
        }).catch(e => { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : "Could not load choices."); });
    }, 200);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [token, field.relation, query, value]);
  return <div className="reference">
    <label>Find {field.label.toLowerCase()}<input value={query} disabled={disabled} onChange={e => setQuery(e.target.value)} placeholder="Search name or ID" /></label>
    <label>{field.label}<select aria-label={field.label} value={value} disabled={disabled} required={!field.optional} onChange={e => onChange(e.target.value)}>
      <option value="">{field.optional ? "No coupon" : "Choose " + field.label.toLowerCase()}</option>
      {rows.map(row => <option key={row.id} value={row.id}>{label(row)}{row.currency ? " · " + String(row.currency) : ""} · {row.id.slice(0, 8)}</option>)}
    </select></label>
    <small>Up to 100 matches. Search to find records beyond the first page.</small>
    {error && <p role="alert">{error}</p>}
  </div>;
}
function FormFields({ schema, values, setValues, token, disabled }: { schema: Field[]; values: Values; setValues: (value: Values) => void; token: string; disabled: boolean }) {
  return <div className="form-grid">{schema.map(field => {
    const update = (value: string | boolean) => setValues({ ...values, [field.key]: value });
    if (field.relation) return <Reference key={field.key} field={field} value={String(values[field.key] ?? "")} token={token} disabled={disabled} onChange={update} />;
    return <label key={field.key} className={field.type === "checkbox" ? "check-field" : ""}>
      {field.type === "checkbox" ? <><input aria-label={field.label} type="checkbox" checked={Boolean(values[field.key])} disabled={disabled} onChange={e => update(e.target.checked)} />{field.label}</> :
        <>{field.label}{field.optional && <span className="optional"> optional</span>}
          {field.options ? <select aria-label={field.label} value={String(values[field.key] ?? "")} disabled={disabled} onChange={e => update(e.target.value)}>{field.options.map(item => <option key={item}>{item}</option>)}</select> :
          <input aria-label={field.label} type={field.type ?? "text"} value={String(values[field.key] ?? "")} disabled={disabled} required={!field.optional}
            min={field.type === "number" ? 0 : undefined} step={field.type === "number" ? (["quantity", "includedUnits"].includes(field.key) ? "1" : "0.01") : field.type === "datetime-local" ? "0.001" : undefined}
            maxLength={field.type === "password" ? 128 : 254} minLength={field.type === "password" ? 12 : undefined}
            onChange={e => update(e.target.value)} />}
        </>}
      {field.hint && <small>{field.hint}</small>}
    </label>;
  })}</div>;
}
function display(value: unknown): string {
  if (value === null || value === undefined) return "—";
  if (typeof value === "boolean") return value ? "Yes" : "No";
  if (typeof value === "object") return "minorUnits" in value ? money(value) : JSON.stringify(value, null, 2);
  if (typeof value === "string" && /^\d{4}-\d\d-\d\dT/.test(value)) return new Date(value).toLocaleString();
  return String(value);
}
function title(value: string) { return value.replace(/([A-Z])/g, " $1").replace(/^./, char => char.toUpperCase()); }

export default function App() {
  const [session, setSession] = useState<Session | null>(null); const [register, setRegister] = useState(false);
  const [credentials, setCredentials] = useState<Values>({ email: "", password: "", slug: "", name: "", workspaceName: "" });
  const [tab, setTab] = useState("customers"); const [page, setPage] = useState<Page>(emptyPage);
  const [dashboard, setDashboard] = useState<Dashboard | null>(null); const [query, setQuery] = useState(""); const [offset, setOffset] = useState(0);
  const [editor, setEditor] = useState<{ kind: string; row: Entity | null } | null>(null); const [values, setValues] = useState<Values>({});
  const [selected, setSelected] = useState<Entity | null>(null); const [deleteConfirm, setDeleteConfirm] = useState(false);
  const [busy, setBusy] = useState(false); const [loading, setLoading] = useState(false);
  const [notice, setNotice] = useState(""); const [error, setError] = useState(""); const [pending, setPending] = useState<Operation | null>(null);
  const [cycle, setCycle] = useState(""); const generation = useRef(0); const editorHeading = useRef<HTMLHeadingElement>(null);
  const token = session?.token ?? ""; const canWrite = session?.actor.role !== "Viewer"; const locked = busy || pending !== null;
  const schemaKind = editor?.row && editor.kind === "subscriptions" ? "subscriptionEdit" : editor?.row && editor.kind === "members" ? "memberEdit" : editor?.kind ?? "";
  const currentTab = tabs.find(item => item[0] === tab)!;

  async function refresh() {
    if (!session) return;
    const current = ++generation.current; setLoading(true);
    try {
      const [overview, rows] = await Promise.all([
        api<Dashboard>(token, "/dashboard"),
        tab === "members" || tab === "audit" ? api<Entity[]>(token, "/" + tab).then(items => ({ ...emptyPage, items, total: items.length })) :
          api<Page>(token, "/" + tab + "?offset=" + offset + "&limit=20&search=" + encodeURIComponent(query))
      ]);
      if (current !== generation.current) return;
      setDashboard(overview); setPage(rows);
      setSession(previous => previous ? { ...previous, actor: overview.actor } : previous);
      setCycle(previous => previous || localTime(overview.billingTime));
    } catch (e) {
      if (current === generation.current) {
        setError(e instanceof Error ? e.message : "Could not refresh.");
        if (e instanceof ApiError && e.status === 401) { setSession(null); setEditor(null); setPending(null); }
      }
      throw e;
    } finally { if (current === generation.current) setLoading(false); }
  }
  useEffect(() => {
    if (!token) return;
    const timer = setTimeout(() => { void refresh().catch(() => {}); }, 150);
    return () => { clearTimeout(timer); ++generation.current; };
  }, [token, tab, offset, query]);
  useEffect(() => { if (editor) editorHeading.current?.focus(); }, [editor]);
  function open(row: Entity | null) {
    setEditor({ kind: tab, row }); setSelected(row); setDeleteConfirm(false); setError(""); setNotice("");
    const key = row && tab === "subscriptions" ? "subscriptionEdit" : row && tab === "members" ? "memberEdit" : tab;
    setValues(initial(key, row, dashboard?.billingTime ?? new Date().toISOString()));
  }
  async function run(value: Operation) {
    setBusy(true); setPending(value); setError(""); setNotice("");
    try {
      await send(token, value);
      setPending(null); setEditor(null); setSelected(null); setDeleteConfirm(false); setNotice("Saved. Workspace data has been updated.");
      try { await refresh(); } catch { setError("Saved successfully, but refresh failed. Use Refresh; do not submit a new operation."); }
    } catch (e) {
      if (e instanceof ApiError && e.status < 500 && e.status !== 429) setPending(null);
      setError(e instanceof ApiError ? e.message : "The response was lost. Keep this tab open and retry the exact operation below.");
    } finally { setBusy(false); }
  }
  function save(event: FormEvent) {
    event.preventDefault(); if (!editor) return;
    try { void run(operation(editor.row ? "PUT" : "POST", "/" + editor.kind + (editor.row ? "/" + editor.row.id : ""), payload(schemaKind, values), editor.row?.version)); }
    catch (e) { setError(e instanceof Error ? e.message : "Check the form values."); }
  }
  async function signIn(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError("");
    try {
      const body = register ? credentials : { email: credentials.email, password: credentials.password, slug: credentials.slug };
      const result = await api<Session>("", "/auth/" + (register ? "register" : "login"), { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      setSession(result); setCredentials({ email: "", password: "", slug: "", name: "", workspaceName: "" }); setNotice(""); setDashboard(null); setCycle("");
    } catch (e) { setError(e instanceof Error ? e.message : "Could not sign in. If registration was interrupted, try signing in."); }
    finally { setBusy(false); }
  }
  async function signOut() {
    setBusy(true);
    try {
      await api(token, "/auth/logout", { method: "POST" }); ++generation.current;
      setSession(null); setPage(emptyPage); setDashboard(null); setEditor(null); setSelected(null); setPending(null); setError(""); setNotice("");
    } catch (e) { setError(e instanceof Error ? e.message : "Could not sign out; retry."); }
    finally { setBusy(false); }
  }
  async function inspect(row: Entity) {
    setBusy(true); setError("");
    try { const latest = await api<Entity>(token, "/" + tab + "/" + row.id); setSelected(latest); }
    catch (e) { setError(e instanceof Error ? e.message : "Could not open record."); }
    finally { setBusy(false); }
  }
  async function reloadEditor() {
    if (!editor?.row) return;
    setBusy(true);
    try {
      const latest = editor.kind === "members" ? (await api<Entity[]>(token, "/members")).find(row => row.id === editor.row?.id) :
        await api<Entity>(token, "/" + editor.kind + "/" + editor.row.id);
      if (!latest) throw new Error("Record no longer exists.");
      open(latest);
    } catch (e) { setError(e instanceof Error ? e.message : "Could not reload."); }
    finally { setBusy(false); }
  }
  const feedback = <div className="feedback" aria-live="polite">
    {notice && <p className="success" role="status">{notice}</p>}
    {error && <p className="error" role="alert">{error}</p>}
    {pending && <div className="retry"><p>This operation has an uncertain outcome. Its request body and key are retained in this tab for an exact retry. Do not reload the page.</p>
      <button disabled={busy} onClick={() => void run(pending)}>Retry exact operation</button></div>}
  </div>;

  if (!session) return <main className="auth-shell">
    <section className="auth-intro"><p className="eyebrow">THE BILLING WORKSHOP</p><h1>From a customer record<br />to a paid invoice.</h1>
      <p>Practice the full subscription lifecycle in a local workspace: catalog, usage, billing, payments and audit history.</p>
      <div className="simulation-note"><strong>Everything here is simulated.</strong><p>No cards, charges, emails or external webhook deliveries.</p></div>
      <ol><li>Create your workspace.</li><li>Add a customer and plan.</li><li>Trace a billing cycle from start to finish.</li></ol>
    </section>
    <section className="auth-card"><p className="eyebrow">YOUR LOCAL WORKSPACE</p><h2>{register ? "Create a workspace" : "Welcome back"}</h2>
      <p>{register ? "Use fictional workshop data and a unique local password." : "Sign in with your email and workspace code."}</p>
      <form onSubmit={signIn}>
        <FormFields token="" disabled={busy} values={credentials} setValues={setCredentials} schema={[
          ...(register ? [{ key: "name", label: "Your name" }, { key: "workspaceName", label: "Workspace name" }] : []),
          { key: "slug", label: "Workspace code", hint: "For example: billing-lab" }, { key: "email", label: "Email", type: "email" },
          { key: "password", label: "Password", type: "password" }
        ]} />
        {feedback}<button className="primary" disabled={busy}>{busy ? "Please wait…" : register ? "Create workspace" : "Sign in"}</button>
      </form>
      <button className="link-button" disabled={busy} onClick={() => { setRegister(!register); setError(""); }}>{register ? "Already have a workspace? Sign in" : "Create a new workspace"}</button>
      <small>Sessions last eight hours and stay in memory. Reloading requires sign-in.</small>
    </section>
  </main>;

  return <div className="app-shell">
    <aside className="sidebar"><a href="#" className="brand" onClick={e => e.preventDefault()}>B<span>Billing workshop</span></a>
      <div className="workspace"><strong>{session.actor.workspaceName}</strong><small>{session.actor.slug} · {session.actor.role}</small></div>
      <nav aria-label="Workspace sections">{tabs.filter(item => item[0] !== "members" || session.actor.role === "Owner").map(item =>
        <button key={item[0]} className={tab === item[0] ? "active" : ""} aria-current={tab === item[0] ? "page" : undefined} disabled={locked}
          onClick={() => { setTab(item[0]); setOffset(0); setQuery(""); setEditor(null); setSelected(null); setError(""); setNotice(""); }}>{item[1]}</button>)}</nav>
      <div className="sidebar-footer"><span className="badge">LOCAL SIMULATOR</span><p>No external payments or messages.</p><button disabled={locked} onClick={() => void signOut()}>Sign out</button></div>
    </aside>
    <main className="workspace-main">
      <header className="topbar"><div><p className="eyebrow">SUBSCRIPTION OPERATIONS</p><h1>{currentTab[1]}</h1><p>{currentTab[2]}</p></div>
        <button disabled={locked || loading} onClick={() => { setError(""); void refresh().catch(() => {}); }}>{loading ? "Refreshing…" : "Refresh"}</button></header>
      <section className="metrics" aria-label="Workspace overview">
        {[["Customers", dashboard?.counts.customers], ["Subscriptions", dashboard?.counts.subscriptions], ["Invoices", dashboard?.counts.invoices]].map(([name, count]) =>
          <div className="metric" key={String(name)}><span>{name}</span><strong>{count ?? "—"}</strong></div>)}
        <div className="metric clock"><span>Simulator clock</span><strong>{dashboard ? new Date(dashboard.billingTime).toLocaleDateString() : "—"}</strong><small>{dashboard ? new Date(dashboard.billingTime).toLocaleTimeString() : ""}</small></div>
      </section>
      {dashboard && dashboard.totals.length > 0 && <div className="currency-totals">{dashboard.totals.map(total =>
        <p key={total.currency}><strong>{total.currency}</strong> Open {money({ minorUnits: total.openMinorUnits, currency: total.currency })} · Paid {money({ minorUnits: total.paidMinorUnits, currency: total.currency })}</p>)}</div>}
      {!editor && feedback}
      {canWrite && <details className="simulator"><summary>Billing simulator controls</summary><p>Advance billing time to create due invoices. Process queued payments with a reproducible success or decline; no real provider is contacted.</p>
        <div className="simulation-controls"><label>Advance to (local time)<input type="datetime-local" step="0.001" value={cycle} disabled={locked} onChange={e => setCycle(e.target.value)} /></label>
          <button disabled={locked || !cycle} onClick={() => void run(operation("POST", "/simulator/run-cycle", { billingDate: new Date(cycle).toISOString() }))}>Advance billing clock</button>
          <button disabled={locked} onClick={() => void run(operation("POST", "/simulator/process", { succeeded: true }))}>Process as success</button>
          <button disabled={locked} onClick={() => void run(operation("POST", "/simulator/process", { succeeded: false }))}>Process as decline</button>
        </div></details>}
      <section className="records-panel" aria-label={currentTab[1] + " records"}>
        <div className="list-toolbar"><label>Search {currentTab[1].toLowerCase()}<input placeholder="Name, email, code or ID" value={query} disabled={locked || tab === "members" || tab === "audit"}
          onChange={e => { setQuery(e.target.value); setOffset(0); }} /></label>
          {canWrite && writable.includes(tab) && <button className="primary" disabled={locked} onClick={() => open(null)}>Add {tab === "usage-events" ? "usage" : tab === "members" ? "member" : tab === "subscriptions" ? "subscription" : tab.slice(0, -1)}</button>}
        </div>
        {loading && <p className="loading" role="status">Loading workspace records…</p>}
        {!loading && page.items.length === 0 ? <div className="empty"><span>01 / START HERE</span><h2>{query ? "No matching records" : "No " + currentTab[1].toLowerCase() + " yet"}</h2><p>{query ? "Try another name, code or ID." : currentTab[2]}</p></div> :
          <div className="record-list">{page.items.map(row => <article className="record-row" key={row.id}>
            <div className="record-main"><strong>{tab === "audit" ? String(row.action) : label(row)}</strong>
              <small>{row.email ? String(row.email) : row.code ? String(row.code) : row.id}</small></div>
            <div className="record-facts"><span>{row.status ? String(row.status) : row.role ? String(row.role) : row.isArchived ? "Archived" : row.isActive === false ? "Inactive" : "Active"}</span>
              <small>{row.total ? money(row.total) : row.basePrice ? money(row.basePrice) : row.amount ? money(row.amount) : row.quantity ? String(row.quantity) + " units" : row.currency ? String(row.currency) : row.at ? display(row.at) : ""}</small></div>
            <div className="row-actions">{tab !== "members" && tab !== "audit" && <button disabled={locked} onClick={() => void inspect(row)}>Inspect<span className="sr-only"> {label(row)}</span></button>}
              {canWrite && writable.includes(tab) && row.role !== "Owner" && <button disabled={locked} onClick={() => open(row)}>Edit<span className="sr-only"> {label(row)}</span></button>}</div>
          </article>)}</div>}
        <div className="pagination"><span>{page.total === 0 ? "0 records" : (offset + 1) + "–" + Math.min(offset + page.items.length, page.total) + " of " + page.total}</span>
          {tab !== "members" && tab !== "audit" && <div><button disabled={locked || loading || offset === 0} onClick={() => setOffset(Math.max(0, offset - 20))}>Previous</button>
            <button disabled={locked || loading || offset + 20 >= page.total} onClick={() => setOffset(offset + 20)}>Next</button></div>}</div>
      </section>
      {editor && <section className="editor" aria-label="Record editor"><div className="section-heading"><h2 ref={editorHeading} tabIndex={-1}>{editor.row ? "Edit " + label(editor.row) : "New " + currentTab[1].toLowerCase()}</h2>
        <button disabled={locked} onClick={() => { setEditor(null); setSelected(null); setError(""); }}>Close editor</button></div>
        {editor.row && <p className="muted">Version {editor.row.version}. Your draft is preserved if another edit wins.</p>}
        <form onSubmit={save}><FormFields token={token} disabled={locked} schema={fields[schemaKind] ?? []} values={values} setValues={setValues} />
          {feedback}<div className="form-actions"><button className="primary" disabled={locked}>{busy ? "Saving…" : "Save changes"}</button>
            {editor.row && <button type="button" disabled={locked} onClick={() => void reloadEditor()}>Discard draft and reload latest</button>}</div>
        </form>
        {editor.row && <div className="delete-section"><p>Delete only an unused record. Referenced billing history is protected; archive or cancel it instead.</p>
          {!deleteConfirm ? <button className="danger" disabled={locked} onClick={() => setDeleteConfirm(true)}>Delete record…</button> :
            <div role="group" aria-label="Confirm deletion"><p>Delete “{label(editor.row)}”? This cannot be undone.</p>
              <button className="danger" disabled={locked} onClick={() => void run(operation("DELETE", "/" + editor.kind + "/" + editor.row!.id, {}, editor.row!.version))}>Confirm delete</button>
              <button disabled={locked} onClick={() => setDeleteConfirm(false)}>Keep record</button></div>}</div>}
      </section>}
      {selected && !editor && <section className="detail-panel" aria-label="Record details"><div className="section-heading"><h2>{label(selected)}</h2><button disabled={locked} onClick={() => setSelected(null)}>Close details</button></div>
        <dl>{Object.entries(selected).filter(([key]) => key !== "workspaceId").map(([key, value]) => <div key={key}><dt>{title(key)}</dt><dd>{display(value)}</dd></div>)}</dl>
        {tab === "invoices" && canWrite && selected.status === "Open" && <div className="form-actions">
          <button className="primary" disabled={locked} onClick={() => void run(operation("POST", "/invoices/" + selected.id + "/pay", {}, selected.version))}>Queue simulated payment</button>
          <button disabled={locked} onClick={() => void run(operation("POST", "/invoices/" + selected.id + "/void", {}, selected.version))}>Void invoice</button>
        </div>}</section>}
      <footer className="page-footer">Billing workshop · Local SQLite workspace · Fictional data only</footer>
    </main>
  </div>;
}
