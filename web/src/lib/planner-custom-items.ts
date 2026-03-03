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
const PLANNER_API_PATH = "/api/budget/settings/planner-customization";

export const PLANNER_CUSTOM_EVENT = "budget-planner-custom-updated";

let persistTimeout: number | null = null;
let persistInFlight = false;
let persistQueued = false;

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

type PlannerCustomizationPayload = {
  annualCustomItems: AnnualCustomItem[];
  monthlyCustomItems: MonthlyCustomItem[];
  nameOverrides: Record<string, string>;
  hiddenAnnualApiRows: string[];
  hiddenMonthlyApiRows: string[];
};

function readPlannerCustomizationPayload(): PlannerCustomizationPayload {
  return {
    annualCustomItems: readAnnualCustomItems(),
    monthlyCustomItems: readMonthlyCustomItems(),
    nameOverrides: readNameOverrides(),
    hiddenAnnualApiRows: readHiddenAnnualApiRows(),
    hiddenMonthlyApiRows: readHiddenMonthlyApiRows()
  };
}

function writePlannerCustomizationPayload(payload: PlannerCustomizationPayload) {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(ANNUAL_CUSTOM_KEY, JSON.stringify(normalizeAnnualCustom(payload.annualCustomItems)));
  window.localStorage.setItem(MONTHLY_CUSTOM_KEY, JSON.stringify(normalizeMonthlyCustom(payload.monthlyCustomItems)));
  window.localStorage.setItem(NAME_OVERRIDES_KEY, JSON.stringify(payload.nameOverrides));
  window.localStorage.setItem(HIDDEN_ANNUAL_API_ROWS_KEY, JSON.stringify(normalizeIdList(payload.hiddenAnnualApiRows)));
  window.localStorage.setItem(HIDDEN_MONTHLY_API_ROWS_KEY, JSON.stringify(normalizeIdList(payload.hiddenMonthlyApiRows)));
}

function schedulePlannerCustomizationPersist() {
  if (typeof window === "undefined") {
    return;
  }

  if (persistTimeout) {
    window.clearTimeout(persistTimeout);
  }

  persistTimeout = window.setTimeout(() => {
    void persistPlannerCustomizationNow();
  }, 150);
}

async function persistPlannerCustomizationNow(): Promise<void> {
  if (persistInFlight) {
    persistQueued = true;
    return;
  }

  persistInFlight = true;
  persistQueued = false;

  try {
    const payload = readPlannerCustomizationPayload();
    await fetch(PLANNER_API_PATH, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(payload)
    });
  } catch {
    // Keep local edits when API is unavailable.
  } finally {
    persistInFlight = false;

    if (persistQueued) {
      persistQueued = false;
      void persistPlannerCustomizationNow();
    }
  }
}

export async function refreshPlannerCustomizationFromApi(): Promise<void> {
  if (typeof window === "undefined") {
    return;
  }

  try {
    const response = await fetch(PLANNER_API_PATH, {
      method: "GET",
      cache: "no-store"
    });

    if (!response.ok) {
      return;
    }

    const payload = (await response.json()) as Partial<PlannerCustomizationPayload>;

    writePlannerCustomizationPayload({
      annualCustomItems: normalizeAnnualCustom(payload.annualCustomItems ?? []),
      monthlyCustomItems: normalizeMonthlyCustom(payload.monthlyCustomItems ?? []),
      nameOverrides: payload.nameOverrides ?? {},
      hiddenAnnualApiRows: normalizeIdList(payload.hiddenAnnualApiRows ?? []),
      hiddenMonthlyApiRows: normalizeIdList(payload.hiddenMonthlyApiRows ?? [])
    });

    emitUpdate();
  } catch {
    // Fallback to local state.
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
    schedulePlannerCustomizationPersist();
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
    schedulePlannerCustomizationPersist();
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
    schedulePlannerCustomizationPersist();
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
    schedulePlannerCustomizationPersist();
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
    schedulePlannerCustomizationPersist();
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
