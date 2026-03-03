"use client";

import type { ManagedSectionKind } from "@/lib/section-settings";

export type ActionStatus = "PLANNED" | "DONE" | "PARTIAL" | "SKIPPED";

export type AnnualCustomItem = {
  id: string;
  year: number;
  sectionId: string;
  sectionKind: ManagedSectionKind;
  name: string;
  months: number[];
};

export type MonthlyCustomItem = {
  id: string;
  year: number;
  month: number;
  sectionId: string;
  sectionKind: ManagedSectionKind;
  name: string;
  plannedAmount: number;
  actualAmount: number | null;
  status: ActionStatus;
};

const ANNUAL_CUSTOM_KEY = "budget.annual-custom-items.v1";
const MONTHLY_CUSTOM_KEY = "budget.monthly-custom-items.v1";
const NAME_OVERRIDES_KEY = "budget.item-name-overrides.v1";
const HIDDEN_ANNUAL_API_ROWS_KEY = "budget.hidden-annual-api-rows.v1";
const HIDDEN_MONTHLY_API_ROWS_KEY = "budget.hidden-monthly-api-rows.v1";

export const PLANNER_CUSTOM_EVENT = "budget-planner-custom-updated";

function emitUpdate() {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(PLANNER_CUSTOM_EVENT));
  }
}

function parseJson<T>(raw: string | null, fallback: T): T {
  if (!raw) {
    return fallback;
  }

  try {
    return JSON.parse(raw) as T;
  } catch {
    return fallback;
  }
}

function normalizeAnnualCustom(items: AnnualCustomItem[]): AnnualCustomItem[] {
  return items
    .filter((item) => Number.isInteger(item.year) && item.year > 1900)
    .map((item) => ({
      ...item,
      name: item.name?.trim() || "New Item",
      months: Array.from({ length: 12 }).map((_, index) => Number(item.months?.[index] ?? 0))
    }));
}

function normalizeMonthlyCustom(items: MonthlyCustomItem[]): MonthlyCustomItem[] {
  return items
    .filter((item) => Number.isInteger(item.year) && item.year > 1900 && item.month >= 1 && item.month <= 12)
    .map((item) => ({
      ...item,
      name: item.name?.trim() || "New Item",
      plannedAmount: Number(item.plannedAmount ?? 0),
      actualAmount: item.actualAmount === null ? null : Number(item.actualAmount ?? 0),
      status: item.status ?? "PLANNED"
    }));
}

export function readAnnualCustomItems(): AnnualCustomItem[] {
  if (typeof window === "undefined") {
    return [];
  }

  const parsed = parseJson<AnnualCustomItem[]>(window.localStorage.getItem(ANNUAL_CUSTOM_KEY), []);
  return normalizeAnnualCustom(parsed);
}

export function saveAnnualCustomItems(items: AnnualCustomItem[]): AnnualCustomItem[] {
  const normalized = normalizeAnnualCustom(items);

  if (typeof window !== "undefined") {
    window.localStorage.setItem(ANNUAL_CUSTOM_KEY, JSON.stringify(normalized));
    emitUpdate();
  }

  return normalized;
}

export function readMonthlyCustomItems(): MonthlyCustomItem[] {
  if (typeof window === "undefined") {
    return [];
  }

  const parsed = parseJson<MonthlyCustomItem[]>(window.localStorage.getItem(MONTHLY_CUSTOM_KEY), []);
  return normalizeMonthlyCustom(parsed);
}

export function saveMonthlyCustomItems(items: MonthlyCustomItem[]): MonthlyCustomItem[] {
  const normalized = normalizeMonthlyCustom(items);

  if (typeof window !== "undefined") {
    window.localStorage.setItem(MONTHLY_CUSTOM_KEY, JSON.stringify(normalized));
    emitUpdate();
  }

  return normalized;
}

export function readNameOverrides(): Record<string, string> {
  if (typeof window === "undefined") {
    return {};
  }

  const parsed = parseJson<Record<string, string>>(window.localStorage.getItem(NAME_OVERRIDES_KEY), {});
  const normalized: Record<string, string> = {};

  for (const [key, value] of Object.entries(parsed)) {
    if (key && value.trim()) {
      normalized[key] = value.trim();
    }
  }

  return normalized;
}

export function saveNameOverrides(next: Record<string, string>): Record<string, string> {
  const normalized: Record<string, string> = {};

  for (const [key, value] of Object.entries(next)) {
    if (key && value.trim()) {
      normalized[key] = value.trim();
    }
  }

  if (typeof window !== "undefined") {
    window.localStorage.setItem(NAME_OVERRIDES_KEY, JSON.stringify(normalized));
    emitUpdate();
  }

  return normalized;
}

function normalizeIdList(values: string[]): string[] {
  return Array.from(new Set(values.map((value) => value.trim()).filter(Boolean)));
}

export function readHiddenAnnualApiRows(): string[] {
  if (typeof window === "undefined") {
    return [];
  }

  const parsed = parseJson<string[]>(window.localStorage.getItem(HIDDEN_ANNUAL_API_ROWS_KEY), []);
  return normalizeIdList(parsed);
}

export function saveHiddenAnnualApiRows(values: string[]): string[] {
  const normalized = normalizeIdList(values);

  if (typeof window !== "undefined") {
    window.localStorage.setItem(HIDDEN_ANNUAL_API_ROWS_KEY, JSON.stringify(normalized));
    emitUpdate();
  }

  return normalized;
}

export function readHiddenMonthlyApiRows(): string[] {
  if (typeof window === "undefined") {
    return [];
  }

  const parsed = parseJson<string[]>(window.localStorage.getItem(HIDDEN_MONTHLY_API_ROWS_KEY), []);
  return normalizeIdList(parsed);
}

export function saveHiddenMonthlyApiRows(values: string[]): string[] {
  const normalized = normalizeIdList(values);

  if (typeof window !== "undefined") {
    window.localStorage.setItem(HIDDEN_MONTHLY_API_ROWS_KEY, JSON.stringify(normalized));
    emitUpdate();
  }

  return normalized;
}

export function createAnnualCustomDraft(
  year: number,
  sectionId: string,
  sectionKind: ManagedSectionKind
): AnnualCustomItem {
  return {
    id: `annual-${Date.now()}-${Math.round(Math.random() * 1000)}`,
    year,
    sectionId,
    sectionKind,
    name: "New Item",
    months: Array(12).fill(0)
  };
}

export function createMonthlyCustomDraft(
  year: number,
  month: number,
  sectionId: string,
  sectionKind: ManagedSectionKind
): MonthlyCustomItem {
  return {
    id: `monthly-${Date.now()}-${Math.round(Math.random() * 1000)}`,
    year,
    month,
    sectionId,
    sectionKind,
    name: "New Item",
    plannedAmount: 0,
    actualAmount: null,
    status: "PLANNED"
  };
}
