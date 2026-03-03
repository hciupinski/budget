"use client";

import { useEffect, useState } from "react";

export type ApiSection = "INCOME" | "COSTS" | "SAVINGS_INVESTMENTS";

export type ManagedSectionKind =
  | "INCOME"
  | "BUSINESS_EXPENSES"
  | "PERSONAL_EXPENSES"
  | "SAVINGS"
  | "INVESTMENTS";

export type ManagedSection = {
  id: string;
  name: string;
  kind: ManagedSectionKind;
  keywords: string[];
  order: number;
};

const STORAGE_KEY = "budget.section-settings.v1";
const UPDATE_EVENT = "budget-sections-updated";
const SECTIONS_API_PATH = "/api/budget/settings/sections";

const FALLBACK_BUSINESS_KEYWORDS = ["software", "office", "professional", "marketing", "accountant", "service"];
const FALLBACK_SAVINGS_KEYWORDS = ["emergency", "savings"];

export const DEFAULT_SECTIONS: ManagedSection[] = [
  {
    id: "sec-income",
    name: "Income",
    kind: "INCOME",
    keywords: ["income", "client", "consulting", "development"],
    order: 0
  },
  {
    id: "sec-business-costs",
    name: "Business Expenses",
    kind: "BUSINESS_EXPENSES",
    keywords: ["software", "office", "professional", "marketing", "accountant", "tools"],
    order: 1
  },
  {
    id: "sec-personal-costs",
    name: "Personal Expenses",
    kind: "PERSONAL_EXPENSES",
    keywords: ["housing", "food", "transportation", "healthcare", "utilities", "personal", "groceries"],
    order: 2
  },
  {
    id: "sec-savings",
    name: "Savings",
    kind: "SAVINGS",
    keywords: ["savings", "emergency"],
    order: 3
  },
  {
    id: "sec-investments",
    name: "Investments",
    kind: "INVESTMENTS",
    keywords: ["investment", "portfolio", "retirement", "401k", "stock"],
    order: 4
  }
];

function normalizeKeyword(keyword: string): string {
  return keyword.trim().toLowerCase();
}

function normalizeSections(input: ManagedSection[]): ManagedSection[] {
  const normalized = input
    .map((section, index) => ({
      ...section,
      id: section.id.trim() || `sec-${index}`,
      name: section.name.trim() || "Untitled Section",
      keywords: section.keywords.map(normalizeKeyword).filter(Boolean),
      order: Number.isFinite(section.order) ? section.order : index
    }))
    .sort((a, b) => a.order - b.order)
    .map((section, index) => ({
      ...section,
      order: index
    }));

  return normalized;
}

type SectionsApiResponse = {
  sections?: ManagedSection[];
};

function normalizeApiSections(input: unknown): ManagedSection[] {
  if (Array.isArray(input)) {
    return normalizeSections(input as ManagedSection[]);
  }

  if (input && typeof input === "object" && Array.isArray((input as SectionsApiResponse).sections)) {
    return normalizeSections((input as SectionsApiResponse).sections as ManagedSection[]);
  }

  return DEFAULT_SECTIONS;
}

async function pushSectionsToApi(next: ManagedSection[]): Promise<void> {
  try {
    await fetch(SECTIONS_API_PATH, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        sections: next
      })
    });
  } catch {
    // Keep local section edits even when API is temporarily unavailable.
  }
}

export function createSectionDraft(order: number): ManagedSection {
  return {
    id: `sec-${Date.now()}-${Math.round(Math.random() * 1000)}`,
    name: "New Section",
    kind: "PERSONAL_EXPENSES",
    keywords: [],
    order
  };
}

export function readSectionSettings(): ManagedSection[] {
  if (typeof window === "undefined") {
    return DEFAULT_SECTIONS;
  }

  const raw = window.localStorage.getItem(STORAGE_KEY);
  if (!raw) {
    return DEFAULT_SECTIONS;
  }

  try {
    const parsed = JSON.parse(raw) as ManagedSection[];
    if (!Array.isArray(parsed) || parsed.length === 0) {
      return DEFAULT_SECTIONS;
    }

    return normalizeSections(parsed);
  } catch {
    return DEFAULT_SECTIONS;
  }
}

export function saveSectionSettings(next: ManagedSection[]): ManagedSection[] {
  const normalized = normalizeSections(next);

  if (typeof window !== "undefined") {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(normalized));
    window.dispatchEvent(new Event(UPDATE_EVENT));
    void pushSectionsToApi(normalized);
  }

  return normalized;
}

export function resetSectionSettings(): ManagedSection[] {
  if (typeof window !== "undefined") {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(DEFAULT_SECTIONS));
    window.dispatchEvent(new Event(UPDATE_EVENT));
    void pushSectionsToApi(DEFAULT_SECTIONS);
  }

  return DEFAULT_SECTIONS;
}

export async function refreshSectionSettingsFromApi(): Promise<ManagedSection[] | null> {
  if (typeof window === "undefined") {
    return null;
  }

  try {
    const response = await fetch(SECTIONS_API_PATH, {
      method: "GET",
      cache: "no-store"
    });

    if (!response.ok) {
      return null;
    }

    const payload = (await response.json()) as unknown;
    const normalized = normalizeApiSections(payload);
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(normalized));
    window.dispatchEvent(new Event(UPDATE_EVENT));
    return normalized;
  } catch {
    return null;
  }
}

export function useSectionSettings(): ManagedSection[] {
  const [sections, setSections] = useState<ManagedSection[]>(DEFAULT_SECTIONS);

  useEffect(() => {
    setSections(readSectionSettings());
    void refreshSectionSettingsFromApi();

    function onUpdate() {
      setSections(readSectionSettings());
    }

    window.addEventListener(UPDATE_EVENT, onUpdate);
    window.addEventListener("storage", onUpdate);

    return () => {
      window.removeEventListener(UPDATE_EVENT, onUpdate);
      window.removeEventListener("storage", onUpdate);
    };
  }, []);

  return sections;
}

function includesAnyKeyword(value: string, keywords: string[]): boolean {
  const normalized = value.toLowerCase();
  return keywords.some((keyword) => normalized.includes(normalizeKeyword(keyword)));
}

function fallbackSectionKindFromApi(section: ApiSection, categoryName: string): ManagedSectionKind {
  if (section === "INCOME") {
    return "INCOME";
  }

  if (section === "SAVINGS_INVESTMENTS") {
    return includesAnyKeyword(categoryName, FALLBACK_SAVINGS_KEYWORDS) ? "SAVINGS" : "INVESTMENTS";
  }

  return includesAnyKeyword(categoryName, FALLBACK_BUSINESS_KEYWORDS) ? "BUSINESS_EXPENSES" : "PERSONAL_EXPENSES";
}

export function resolveManagedSection(
  apiSection: ApiSection,
  categoryName: string,
  sections: ManagedSection[]
): ManagedSection {
  const normalizedName = categoryName.toLowerCase();

  const byKeyword = sections.find((section) => includesAnyKeyword(normalizedName, section.keywords));
  if (byKeyword) {
    return byKeyword;
  }

  const fallbackKind = fallbackSectionKindFromApi(apiSection, categoryName);
  const byKind = sections.find((section) => section.kind === fallbackKind);
  if (byKind) {
    return byKind;
  }

  return {
    id: `fallback-${fallbackKind}`,
    name: fallbackKind.replaceAll("_", " "),
    kind: fallbackKind,
    keywords: [],
    order: sections.length + 1
  };
}
