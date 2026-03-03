"use client";

import { useEffect, useState } from "react";

export type CurrencyCode = "PLN" | "USD" | "EUR";
export type ThemeMode = "LIGHT" | "DARK";

type CurrencyMeta = {
  locale: string;
  label: string;
};

type GeneralSettingsApiResponse = {
  currency?: string;
  theme?: string;
};

export const DEFAULT_CURRENCY: CurrencyCode = "PLN";
export const DEFAULT_THEME: ThemeMode = "LIGHT";

export const CURRENCY_OPTIONS: ReadonlyArray<{ value: CurrencyCode; label: string }> = [
  { value: "PLN", label: "Polish Zloty (PLN)" },
  { value: "USD", label: "US Dollar (USD)" },
  { value: "EUR", label: "Euro (EUR)" }
];

const CURRENCY_META: Record<CurrencyCode, CurrencyMeta> = {
  PLN: {
    locale: "pl-PL",
    label: "PLN"
  },
  USD: {
    locale: "en-US",
    label: "USD"
  },
  EUR: {
    locale: "de-DE",
    label: "EUR"
  }
};

const CURRENCY_STORAGE_KEY = "budget.general.currency.v1";
const THEME_STORAGE_KEY = "budget.general.theme.v1";
const CURRENCY_UPDATE_EVENT = "budget-currency-updated";
const THEME_UPDATE_EVENT = "budget-theme-updated";
const GENERAL_API_PATH = "/api/budget/settings/general";

function normalizeCurrency(value: string | null | undefined): CurrencyCode {
  if (!value) {
    return DEFAULT_CURRENCY;
  }

  const normalized = value.trim().toUpperCase();
  if (normalized === "PLN" || normalized === "USD" || normalized === "EUR") {
    return normalized;
  }

  return DEFAULT_CURRENCY;
}

function normalizeTheme(value: string | null | undefined): ThemeMode {
  if (!value) {
    return DEFAULT_THEME;
  }

  const normalized = value.trim().toUpperCase();
  if (normalized === "LIGHT" || normalized === "DARK") {
    return normalized;
  }

  return DEFAULT_THEME;
}

function emitCurrencyUpdate() {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(CURRENCY_UPDATE_EVENT));
  }
}

function emitThemeUpdate() {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(THEME_UPDATE_EVENT));
  }
}

function applyThemeToDocument(theme: ThemeMode) {
  if (typeof document === "undefined") {
    return;
  }

  document.documentElement.setAttribute("data-theme", theme === "DARK" ? "dark" : "light");
}

async function fetchGeneralSettings(): Promise<GeneralSettingsApiResponse | null> {
  try {
    const response = await fetch(GENERAL_API_PATH, {
      method: "GET",
      cache: "no-store"
    });

    if (!response.ok) {
      return null;
    }

    return (await response.json()) as GeneralSettingsApiResponse;
  } catch {
    return null;
  }
}

export function readCurrencySetting(): CurrencyCode {
  if (typeof window === "undefined") {
    return DEFAULT_CURRENCY;
  }

  return normalizeCurrency(window.localStorage.getItem(CURRENCY_STORAGE_KEY));
}

export function readThemeSetting(): ThemeMode {
  if (typeof window === "undefined") {
    return DEFAULT_THEME;
  }

  return normalizeTheme(window.localStorage.getItem(THEME_STORAGE_KEY));
}

async function pushGeneralSettingsToApi(change: Partial<{ currency: CurrencyCode; theme: ThemeMode }>): Promise<void> {
  try {
    await fetch(GENERAL_API_PATH, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(change)
    });
  } catch {
    // Keep local behavior if API is temporarily unavailable.
  }
}

export function saveCurrencySetting(next: CurrencyCode): CurrencyCode {
  const normalized = normalizeCurrency(next);

  if (typeof window !== "undefined") {
    window.localStorage.setItem(CURRENCY_STORAGE_KEY, normalized);
    emitCurrencyUpdate();
    void pushGeneralSettingsToApi({ currency: normalized });
  }

  return normalized;
}

export function saveThemeSetting(next: ThemeMode): ThemeMode {
  const normalized = normalizeTheme(next);

  if (typeof window !== "undefined") {
    window.localStorage.setItem(THEME_STORAGE_KEY, normalized);
    applyThemeToDocument(normalized);
    emitThemeUpdate();
    void pushGeneralSettingsToApi({ theme: normalized });
  }

  return normalized;
}

export async function refreshCurrencySettingFromApi(): Promise<CurrencyCode | null> {
  if (typeof window === "undefined") {
    return null;
  }

  const payload = await fetchGeneralSettings();
  if (!payload) {
    return null;
  }

  const normalized = normalizeCurrency(payload.currency);
  window.localStorage.setItem(CURRENCY_STORAGE_KEY, normalized);
  emitCurrencyUpdate();
  return normalized;
}

export async function refreshThemeSettingFromApi(): Promise<ThemeMode | null> {
  if (typeof window === "undefined") {
    return null;
  }

  const payload = await fetchGeneralSettings();
  if (!payload) {
    return null;
  }

  const normalized = normalizeTheme(payload.theme);
  window.localStorage.setItem(THEME_STORAGE_KEY, normalized);
  applyThemeToDocument(normalized);
  emitThemeUpdate();
  return normalized;
}

export function useCurrencySetting(): CurrencyCode {
  const [currency, setCurrency] = useState<CurrencyCode>(DEFAULT_CURRENCY);

  useEffect(() => {
    setCurrency(readCurrencySetting());
    void refreshCurrencySettingFromApi();

    function onUpdate() {
      setCurrency(readCurrencySetting());
    }

    window.addEventListener(CURRENCY_UPDATE_EVENT, onUpdate);
    window.addEventListener("storage", onUpdate);

    return () => {
      window.removeEventListener(CURRENCY_UPDATE_EVENT, onUpdate);
      window.removeEventListener("storage", onUpdate);
    };
  }, []);

  return currency;
}

export function useThemeSetting(): ThemeMode {
  const [theme, setTheme] = useState<ThemeMode>(DEFAULT_THEME);

  useEffect(() => {
    const current = readThemeSetting();
    setTheme(current);
    applyThemeToDocument(current);
    void refreshThemeSettingFromApi();

    function onUpdate() {
      const next = readThemeSetting();
      setTheme(next);
      applyThemeToDocument(next);
    }

    window.addEventListener(THEME_UPDATE_EVENT, onUpdate);
    window.addEventListener("storage", onUpdate);

    return () => {
      window.removeEventListener(THEME_UPDATE_EVENT, onUpdate);
      window.removeEventListener("storage", onUpdate);
    };
  }, []);

  return theme;
}

export function currencyLabel(currency: CurrencyCode): string {
  return CURRENCY_META[currency].label;
}

export function formatCurrency(value: number, currency: CurrencyCode): string {
  const { locale } = CURRENCY_META[currency];

  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency,
    maximumFractionDigits: 2
  }).format(value);
}

export function formatSignedCurrency(value: number, currency: CurrencyCode): string {
  if (value > 0) {
    return `+${formatCurrency(value, currency)}`;
  }

  if (value < 0) {
    return `-${formatCurrency(Math.abs(value), currency)}`;
  }

  return formatCurrency(0, currency);
}
