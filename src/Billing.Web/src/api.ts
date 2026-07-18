const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5088";

export type BillingCycle = "Monthly" | "Annual";
export type PlanPricingModel = "Flat" | "Metered" | "Hybrid";
export type InvoiceStatus = "Draft" | "Open" | "Paid" | "Void" | "Uncollectible";
export type SubscriptionStatus = "Trialing" | "Active" | "PastDue" | "Canceled";
export type PaymentStatus = "Pending" | "Succeeded" | "Failed";

export interface MoneyDto {
  minorUnits: number;
  currency: string;
  majorUnits: number;
  formatted: string;
}

export interface Customer {
  id: string;
  name: string;
  email: string;
  currency: string;
  createdAt: string;
}

export interface Plan {
  id: string;
  code: string;
  name: string;
  cycle: BillingCycle;
  pricingModel: PlanPricingModel;
  basePrice: MoneyValue;
  includedUnits: number;
  unitPrice: MoneyValue;
  isActive: boolean;
  createdAt: string;
}

export interface MoneyValue {
  minorUnits: number;
  currency: string;
  majorUnits: number;
}

export interface Coupon {
  id: string;
  code: string;
  name: string;
  percentOff: number | null;
  amountOff: MoneyValue | null;
  expiresAt: string | null;
  createdAt: string;
}

export interface Subscription {
  id: string;
  customerId: string;
  planId: string;
  couponId: string | null;
  status: SubscriptionStatus;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  createdAt: string;
}

export interface UsageEvent {
  id: string;
  subscriptionId: string;
  quantity: number;
  metric: string;
  occurredAt: string;
  invoiceId: string | null;
  createdAt: string;
}

export interface InvoiceLine {
  description: string;
  quantity: number;
  unitAmount: MoneyDto;
  amount: MoneyDto;
  kind: string;
}

export interface Invoice {
  id: string;
  number: string;
  customerId: string;
  subscriptionId: string;
  status: InvoiceStatus;
  lines: InvoiceLine[];
  subtotal: MoneyDto;
  discount: MoneyDto;
  total: MoneyDto;
  periodStart: string;
  periodEnd: string;
  dueAt: string;
  createdAt: string;
  paidAt: string | null;
}

export interface Payment {
  id: string;
  invoiceId: string;
  status: PaymentStatus;
  amount: MoneyValue;
  provider: string;
  providerReference: string | null;
  failureReason: string | null;
  createdAt: string;
  completedAt: string | null;
}

export interface WebhookEvent {
  id: string;
  eventType: string;
  destination: string;
  payloadJson: string;
  status: "Pending" | "Succeeded" | "Failed";
  attempt: number;
  availableAt: string;
  createdAt: string;
  deliveredAt: string | null;
  lastError: string | null;
}

export interface DashboardMetrics {
  activeCustomers: number;
  activeSubscriptions: number;
  openInvoices: number;
  monthlyRecurringRevenue: MoneyDto;
  outstandingRevenue: MoneyDto;
  paidInvoiceRate: number;
}

export interface DashboardResponse {
  customers: Customer[];
  plans: Plan[];
  coupons: Coupon[];
  subscriptions: Subscription[];
  invoices: Invoice[];
  payments: Payment[];
  usageEvents: UsageEvent[];
  webhookEvents: WebhookEvent[];
  metrics: DashboardMetrics;
}

export async function getDashboard(): Promise<DashboardResponse> {
  return request<DashboardResponse>("/api/dashboard");
}

export async function postJson<T>(path: string, body: unknown): Promise<T> {
  return request<T>(path, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": newIdempotencyKey()
    },
    body: JSON.stringify(body)
  });
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, init);
  if (!response.ok) {
    const payload = await safeJson(response);
    const message = payload?.error ?? `${response.status} ${response.statusText}`;
    throw new Error(message);
  }

  return response.json() as Promise<T>;
}

async function safeJson(response: Response): Promise<{ error?: string } | null> {
  try {
    return (await response.json()) as { error?: string };
  } catch {
    return null;
  }
}

function newIdempotencyKey(): string {
  if ("randomUUID" in crypto) {
    return crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

