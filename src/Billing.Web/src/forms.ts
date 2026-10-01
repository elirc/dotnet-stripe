import type { Entity } from "./api";
export type Values = Record<string, string | boolean>;
export type Field = { key: string; label: string; type?: "text" | "email" | "password" | "number" | "checkbox" | "datetime-local";
  options?: string[]; relation?: string; optional?: boolean; hint?: string };
const currency: Field = { key: "currency", label: "Currency", options: ["USD", "EUR", "GBP"] };
export const fields: Record<string, Field[]> = {
  customers: [{ key: "name", label: "Customer name" }, { key: "email", label: "Customer email", type: "email" }, currency,
    { key: "isArchived", label: "Archive customer", type: "checkbox", hint: "Existing billing history is retained. Archived customers cannot start subscriptions." }],
  plans: [{ key: "name", label: "Plan name" }, { key: "code", label: "Plan code", hint: "Lowercase letters, digits and hyphens; start with a letter." },
    { key: "cycle", label: "Billing cycle", options: ["Monthly", "Annual"] }, { key: "pricingModel", label: "Pricing model", options: ["Flat", "Metered", "Hybrid"] }, currency,
    { key: "basePrice", label: "Base price", type: "number" }, { key: "includedUnits", label: "Included units", type: "number" },
    { key: "unitPrice", label: "Price per extra unit", type: "number" }, { key: "metric", label: "Usage metric" }, { key: "isActive", label: "Active plan", type: "checkbox" }],
  coupons: [{ key: "name", label: "Coupon name" }, { key: "code", label: "Coupon code" },
    { key: "percentOff", label: "Percentage discount", type: "number", optional: true, hint: "Fill in either percentage or fixed amount, never both." },
    { key: "amountOff", label: "Fixed discount", type: "number", optional: true }, currency,
    { key: "expiresAt", label: "Expires at (local time)", type: "datetime-local", optional: true }, { key: "isActive", label: "Active coupon", type: "checkbox" }],
  subscriptions: [{ key: "customerId", label: "Customer", relation: "customers" }, { key: "planId", label: "Plan", relation: "plans" },
    { key: "couponId", label: "Coupon", relation: "coupons", optional: true }],
  subscriptionEdit: [{ key: "cancelAtPeriodEnd", label: "Cancel at period end", type: "checkbox", hint: "The final completed period is invoiced. Clear this before the period ends to keep the subscription active." }],
  "usage-events": [{ key: "subscriptionId", label: "Subscription", relation: "subscriptions" }, { key: "quantity", label: "Quantity", type: "number" },
    { key: "metric", label: "Usage metric" }, { key: "occurredAt", label: "Occurred at (local time)", type: "datetime-local", hint: "Must be inside the current period and no later than the simulator clock." }],
  members: [{ key: "name", label: "Member name" }, { key: "email", label: "Member email", type: "email" },
    { key: "password", label: "Temporary workshop password", type: "password", hint: "12–128 characters. This local lab does not send invitations or offer password recovery." },
    { key: "role", label: "Role", options: ["Operator", "Viewer"] }],
  memberEdit: [{ key: "role", label: "Role", options: ["Operator", "Viewer"] }]
};
export function localTime(iso: string): string {
  const value = new Date(iso);
  return new Date(value.getTime() - value.getTimezoneOffset() * 60000).toISOString().slice(0, 23);
}
export function initial(kind: string, row: Entity | null, clock: string): Values {
  const result: Values = {};
  for (const field of fields[kind] ?? []) {
    const raw = row?.[field.key];
    if (field.type === "checkbox") result[field.key] = raw === undefined ? field.key === "isActive" : Boolean(raw);
    else if (["basePrice", "unitPrice", "amountOff"].includes(field.key) && raw && typeof raw === "object" && "minorUnits" in raw)
      result[field.key] = String(Number(raw.minorUnits) / 100);
    else if (field.key === "currency" && row?.basePrice && typeof row.basePrice === "object" && "currency" in row.basePrice)
      result[field.key] = String(row.basePrice.currency);
    else if (field.key === "currency" && row?.amountOff && typeof row.amountOff === "object" && "currency" in row.amountOff)
      result[field.key] = String(row.amountOff.currency);
    else if (field.type === "datetime-local") result[field.key] = raw ? localTime(String(raw)) : field.optional ? "" : localTime(clock);
    else result[field.key] = raw == null ? field.options?.[0] ?? (field.type === "number" ? field.optional ? "" : "0" : field.key === "metric" ? "api_calls" : "") : String(raw);
  }
  return result;
}
export function payload(kind: string, values: Values): Record<string, unknown> {
  return Object.fromEntries((fields[kind] ?? []).map(field => {
    const value = values[field.key];
    return [field.key, field.optional && value === "" ? null : field.type === "number" ? Number(value) :
      field.type === "datetime-local" && value ? new Date(String(value)).toISOString() : value];
  }));
}
