import { useEffect, useMemo, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import {
  Coupon,
  Customer,
  DashboardResponse,
  Invoice,
  Plan,
  Subscription,
  getDashboard,
  postJson
} from "./api";

type CustomerForm = { name: string; email: string; currency: string };
type PlanForm = {
  code: string;
  name: string;
  cycle: "Monthly" | "Annual";
  pricingModel: "Flat" | "Metered" | "Hybrid";
  basePrice: string;
  includedUnits: string;
  unitPrice: string;
  currency: string;
};
type CouponForm = { code: string; name: string; percentOff: string; amountOff: string; currency: string };

export default function App() {
  const [dashboard, setDashboard] = useState<DashboardResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [customerForm, setCustomerForm] = useState<CustomerForm>({ name: "", email: "", currency: "USD" });
  const [planForm, setPlanForm] = useState<PlanForm>({
    code: "",
    name: "",
    cycle: "Monthly",
    pricingModel: "Hybrid",
    basePrice: "99",
    includedUnits: "10000",
    unitPrice: "0.01",
    currency: "USD"
  });
  const [couponForm, setCouponForm] = useState<CouponForm>({ code: "", name: "", percentOff: "10", amountOff: "", currency: "USD" });
  const [subscriptionCustomerId, setSubscriptionCustomerId] = useState("");
  const [subscriptionPlanId, setSubscriptionPlanId] = useState("");
  const [subscriptionCouponId, setSubscriptionCouponId] = useState("");
  const [usageSubscriptionId, setUsageSubscriptionId] = useState("");
  const [usageQuantity, setUsageQuantity] = useState("25000");

  const customersById = useMemo(() => indexBy(dashboard?.customers ?? [], "id"), [dashboard]);
  const plansById = useMemo(() => indexBy(dashboard?.plans ?? [], "id"), [dashboard]);
  const couponsById = useMemo(() => indexBy(dashboard?.coupons ?? [], "id"), [dashboard]);

  useEffect(() => {
    void refresh();
  }, []);

  useEffect(() => {
    if (!dashboard) return;
    setSubscriptionCustomerId((current) => current || dashboard.customers[0]?.id || "");
    setSubscriptionPlanId((current) => current || dashboard.plans[0]?.id || "");
    setUsageSubscriptionId((current) => current || dashboard.subscriptions[0]?.id || "");
  }, [dashboard]);

  async function refresh() {
    try {
      setError(null);
      setDashboard(await getDashboard());
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : "Unable to load dashboard.");
    }
  }

  async function runAction(action: () => Promise<void>) {
    setBusy(true);
    setError(null);
    try {
      await action();
      await refresh();
    } catch (ex) {
      setError(ex instanceof Error ? ex.message : "Action failed.");
    } finally {
      setBusy(false);
    }
  }

  function createCustomer(event: FormEvent) {
    event.preventDefault();
    void runAction(async () => {
      await postJson<Customer>("/api/customers", customerForm);
      setCustomerForm({ name: "", email: "", currency: "USD" });
    });
  }

  function createPlan(event: FormEvent) {
    event.preventDefault();
    void runAction(async () => {
      await postJson<Plan>("/api/plans", {
        ...planForm,
        basePrice: Number(planForm.basePrice),
        includedUnits: Number(planForm.includedUnits),
        unitPrice: Number(planForm.unitPrice)
      });
      setPlanForm({ ...planForm, code: "", name: "" });
    });
  }

  function createCoupon(event: FormEvent) {
    event.preventDefault();
    void runAction(async () => {
      await postJson<Coupon>("/api/coupons", {
        code: couponForm.code,
        name: couponForm.name,
        percentOff: couponForm.percentOff ? Number(couponForm.percentOff) : null,
        amountOff: couponForm.amountOff ? Number(couponForm.amountOff) : null,
        currency: couponForm.currency,
        expiresAt: null
      });
      setCouponForm({ code: "", name: "", percentOff: "10", amountOff: "", currency: "USD" });
    });
  }

  function createSubscription(event: FormEvent) {
    event.preventDefault();
    void runAction(async () => {
      await postJson<Subscription>("/api/subscriptions", {
        customerId: subscriptionCustomerId,
        planId: subscriptionPlanId,
        couponId: subscriptionCouponId || null
      });
    });
  }

  function recordUsage(event: FormEvent) {
    event.preventDefault();
    void runAction(async () => {
      await postJson("/api/usage-events", {
        subscriptionId: usageSubscriptionId,
        quantity: Number(usageQuantity),
        metric: "api_calls",
        occurredAt: new Date().toISOString()
      });
    });
  }

  function runBillingCycle() {
    void runAction(async () => {
      const billingDate = new Date(Date.now() + 35 * 24 * 60 * 60 * 1000).toISOString();
      await postJson("/api/simulator/run-cycle", { billingDate });
    });
  }

  function payInvoice(invoiceId: string) {
    void runAction(async () => {
      await postJson(`/api/invoices/${invoiceId}/pay`, { provider: "simulated" });
    });
  }

  if (!dashboard) {
    return (
      <main className="shell">
        <div className="state">{error ? `Load failed: ${error}` : "Loading billing workspace..."}</div>
      </main>
    );
  }

  return (
    <main className="shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">Subscription Billing</p>
          <h1>Billing Operations Dashboard</h1>
        </div>
        <div className="topbar-actions">
          <button onClick={refresh} disabled={busy}>Refresh</button>
          <button className="primary" onClick={runBillingCycle} disabled={busy}>Run cycle</button>
        </div>
      </header>

      {error && <div className="error">{error}</div>}

      <section className="metrics">
        <Metric label="Active customers" value={dashboard.metrics.activeCustomers.toString()} />
        <Metric label="Active subscriptions" value={dashboard.metrics.activeSubscriptions.toString()} />
        <Metric label="MRR" value={dashboard.metrics.monthlyRecurringRevenue.formatted} />
        <Metric label="Outstanding" value={dashboard.metrics.outstandingRevenue.formatted} />
        <Metric label="Paid invoice rate" value={`${dashboard.metrics.paidInvoiceRate}%`} />
      </section>

      <section className="workspace">
        <div className="panel">
          <h2>Create customer</h2>
          <form onSubmit={createCustomer} className="form-grid">
            <input placeholder="Company name" value={customerForm.name} onChange={(e) => setCustomerForm({ ...customerForm, name: e.target.value })} required />
            <input placeholder="billing@example.com" type="email" value={customerForm.email} onChange={(e) => setCustomerForm({ ...customerForm, email: e.target.value })} required />
            <input placeholder="USD" value={customerForm.currency} onChange={(e) => setCustomerForm({ ...customerForm, currency: e.target.value.toUpperCase() })} required />
            <button className="primary" disabled={busy}>Add</button>
          </form>
        </div>

        <div className="panel">
          <h2>Create plan</h2>
          <form onSubmit={createPlan} className="form-grid">
            <input placeholder="code" value={planForm.code} onChange={(e) => setPlanForm({ ...planForm, code: e.target.value })} required />
            <input placeholder="Plan name" value={planForm.name} onChange={(e) => setPlanForm({ ...planForm, name: e.target.value })} required />
            <select value={planForm.pricingModel} onChange={(e) => setPlanForm({ ...planForm, pricingModel: e.target.value as PlanForm["pricingModel"] })}>
              <option>Flat</option>
              <option>Metered</option>
              <option>Hybrid</option>
            </select>
            <select value={planForm.cycle} onChange={(e) => setPlanForm({ ...planForm, cycle: e.target.value as PlanForm["cycle"] })}>
              <option>Monthly</option>
              <option>Annual</option>
            </select>
            <input type="number" step="0.01" value={planForm.basePrice} onChange={(e) => setPlanForm({ ...planForm, basePrice: e.target.value })} />
            <input type="number" value={planForm.includedUnits} onChange={(e) => setPlanForm({ ...planForm, includedUnits: e.target.value })} />
            <input type="number" step="0.01" value={planForm.unitPrice} onChange={(e) => setPlanForm({ ...planForm, unitPrice: e.target.value })} />
            <button className="primary" disabled={busy}>Create</button>
          </form>
        </div>

        <div className="panel">
          <h2>Coupons and subscriptions</h2>
          <form onSubmit={createCoupon} className="form-grid">
            <input placeholder="SAVE10" value={couponForm.code} onChange={(e) => setCouponForm({ ...couponForm, code: e.target.value.toUpperCase() })} />
            <input placeholder="Coupon name" value={couponForm.name} onChange={(e) => setCouponForm({ ...couponForm, name: e.target.value })} />
            <input type="number" step="0.01" placeholder="Percent off" value={couponForm.percentOff} onChange={(e) => setCouponForm({ ...couponForm, percentOff: e.target.value })} />
            <button disabled={busy}>Add coupon</button>
          </form>
          <form onSubmit={createSubscription} className="form-grid">
            <select value={subscriptionCustomerId} onChange={(e) => setSubscriptionCustomerId(e.target.value)} required>
              {dashboard.customers.map((customer) => <option value={customer.id} key={customer.id}>{customer.name}</option>)}
            </select>
            <select value={subscriptionPlanId} onChange={(e) => setSubscriptionPlanId(e.target.value)} required>
              {dashboard.plans.map((plan) => <option value={plan.id} key={plan.id}>{plan.name}</option>)}
            </select>
            <select value={subscriptionCouponId} onChange={(e) => setSubscriptionCouponId(e.target.value)}>
              <option value="">No coupon</option>
              {dashboard.coupons.map((coupon) => <option value={coupon.id} key={coupon.id}>{coupon.code}</option>)}
            </select>
            <button className="primary" disabled={busy}>Subscribe</button>
          </form>
        </div>

        <div className="panel">
          <h2>Usage simulator</h2>
          <form onSubmit={recordUsage} className="form-grid">
            <select value={usageSubscriptionId} onChange={(e) => setUsageSubscriptionId(e.target.value)} required>
              {dashboard.subscriptions.map((subscription) => (
                <option value={subscription.id} key={subscription.id}>
                  {customersById.get(subscription.customerId)?.name ?? subscription.customerId} / {plansById.get(subscription.planId)?.code ?? "plan"}
                </option>
              ))}
            </select>
            <input type="number" min="1" value={usageQuantity} onChange={(e) => setUsageQuantity(e.target.value)} />
            <button className="primary" disabled={busy}>Record usage</button>
          </form>
          <p className="hint">The cycle runner jumps 35 days ahead so open learning data invoices immediately.</p>
        </div>
      </section>

      <section className="tables">
        <Table title="Invoices">
          <thead>
            <tr>
              <th>Number</th>
              <th>Customer</th>
              <th>Status</th>
              <th>Total</th>
              <th>Due</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {dashboard.invoices.map((invoice) => (
              <tr key={invoice.id}>
                <td>{invoice.number}</td>
                <td>{customersById.get(invoice.customerId)?.name ?? invoice.customerId}</td>
                <td><Status value={invoice.status} /></td>
                <td>{invoice.total.formatted}</td>
                <td>{dateShort(invoice.dueAt)}</td>
                <td>{invoice.status === "Open" && <button onClick={() => payInvoice(invoice.id)} disabled={busy}>Pay</button>}</td>
              </tr>
            ))}
          </tbody>
        </Table>

        <Table title="Subscriptions">
          <thead>
            <tr>
              <th>Customer</th>
              <th>Plan</th>
              <th>Status</th>
              <th>Coupon</th>
              <th>Period end</th>
            </tr>
          </thead>
          <tbody>
            {dashboard.subscriptions.map((subscription) => (
              <tr key={subscription.id}>
                <td>{customersById.get(subscription.customerId)?.name ?? subscription.customerId}</td>
                <td>{plansById.get(subscription.planId)?.name ?? subscription.planId}</td>
                <td><Status value={subscription.status} /></td>
                <td>{subscription.couponId ? couponsById.get(subscription.couponId)?.code : "None"}</td>
                <td>{dateShort(subscription.currentPeriodEnd)}</td>
              </tr>
            ))}
          </tbody>
        </Table>

        <Table title="Plans">
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Model</th>
              <th>Base</th>
              <th>Included</th>
              <th>Unit</th>
            </tr>
          </thead>
          <tbody>
            {dashboard.plans.map((plan) => (
              <tr key={plan.id}>
                <td>{plan.code}</td>
                <td>{plan.name}</td>
                <td>{plan.pricingModel}</td>
                <td>{formatMoney(plan.basePrice)}</td>
                <td>{plan.includedUnits.toLocaleString()}</td>
                <td>{formatMoney(plan.unitPrice)}</td>
              </tr>
            ))}
          </tbody>
        </Table>

        <Table title="Webhooks">
          <thead>
            <tr>
              <th>Event</th>
              <th>Status</th>
              <th>Attempts</th>
              <th>Created</th>
              <th>Last note</th>
            </tr>
          </thead>
          <tbody>
            {dashboard.webhookEvents.slice(0, 8).map((event) => (
              <tr key={event.id}>
                <td>{event.eventType}</td>
                <td><Status value={event.status} /></td>
                <td>{event.attempt}</td>
                <td>{dateShort(event.createdAt)}</td>
                <td className="truncate">{event.lastError ?? "Queued"}</td>
              </tr>
            ))}
          </tbody>
        </Table>
      </section>
    </main>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="metric">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function Table({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="table-card">
      <h2>{title}</h2>
      <div className="table-scroll">
        <table>{children}</table>
      </div>
    </div>
  );
}

function Status({ value }: { value: string }) {
  return <span className={`status status-${value.toLowerCase()}`}>{value}</span>;
}

function indexBy<T extends Record<K, string>, K extends keyof T>(items: T[], key: K): Map<string, T> {
  return new Map(items.map((item) => [item[key], item]));
}

function dateShort(value: string): string {
  return new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric", year: "numeric" }).format(new Date(value));
}

function formatMoney(value: { minorUnits: number; currency: string }): string {
  return new Intl.NumberFormat(undefined, { style: "currency", currency: value.currency }).format(value.minorUnits / 100);
}
