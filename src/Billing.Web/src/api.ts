export type Actor = { workspaceId: string; userId: string; role: "Owner" | "Operator" | "Viewer"; name: string; workspaceName: string; slug: string };
export type Session = { token: string; actor: Actor; expiresAt: string };
export type Entity = { id: string; version: number; [key: string]: unknown };
export type Page = { items: Entity[]; total: number; offset: number; limit: number };
export type Dashboard = { actor: Actor; billingTime: string; counts: { customers: number; subscriptions: number; invoices: number };
  totals: { currency: string; openMinorUnits: number; paidMinorUnits: number }[] };
export type Operation = { method: string; path: string; body: string; key: string; version?: number };

export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}
export async function api<T>(token: string, path: string, options: RequestInit = {}): Promise<T> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 15000);
  try {
    const response = await fetch("/api" + path, { ...options, signal: options.signal ?? controller.signal,
      headers: { ...(token ? { Authorization: "Bearer " + token } : {}), ...options.headers } });
    const value = await response.json();
    if (!response.ok) throw new ApiError(response.status, value.message ?? "Request failed (" + response.status + ").");
    return value as T;
  } finally { clearTimeout(timeout); }
}
export function operation(method: string, path: string, body: unknown, version?: number): Operation {
  return { method, path, body: JSON.stringify(body), version, key: crypto.randomUUID() };
}
export function send<T>(token: string, value: Operation): Promise<T> {
  return api<T>(token, value.path, { method: value.method, body: value.body,
    headers: { "Content-Type": "application/json", "Idempotency-Key": value.key,
      ...(value.version === undefined ? {} : { "If-Match": '\"v' + value.version + '\"' }) } });
}
export function label(value: Entity): string {
  return String(value.name ?? value.number ?? value.eventType ?? value.email ?? value.id.slice(0, 8));
}
export function money(value: unknown): string {
  if (value && typeof value === "object" && "minorUnits" in value && "currency" in value) {
    const amount = value as { minorUnits: number; currency: string };
    return new Intl.NumberFormat("en", { style: "currency", currency: amount.currency }).format(amount.minorUnits / 100);
  }
  return String(value ?? "—");
}
