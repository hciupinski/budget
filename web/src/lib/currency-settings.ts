"use client";

import { useEffect, useState } from "react";

export type CurrencyCode = "PLN" | "USD" | "EUR";

type CurrencyMeta = {
  locale: string;
  label: string;
};

export const DEFAULT_CURRENCY: CurrencyCode = "PLN";
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

const STORAGE_KEY = "budget.general.currency.v1";
const UPDATE_EVENT = "budget-currency-updated";
const GENERAL_API_PATH = "/api/budget/settings/general";

type GeneralSettingsApiResponse = {
  currency?: string;
};

function emitUpdate() {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event(UPDATE_EVENT));
  }
}

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

export function readCurrencySetting(): CurrencyCode {
  if (typeof window === "undefined") {
    return DEFAULT_CURRENCY;
  }

  return normalizeCurrency(window.localStorage.getItem(STORAGE_KEY));
}

async function pushCurrencyToApi(currency: CurrencyCode): Promise<void> {
  try {
    await fetch(GENERAL_API_PATH, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({
        currency
      })
    });
  } catch {
    // Keep local behavior if API is temporarily unavailable.
  }
}

export function saveCurrencySetting(next: CurrencyCode): CurrencyCode {
  const normalized = normalizeCurrency(next);

  if (typeof window !== "undefined") {
    window.localStorage.setItem(STORAGE_KEY, normalized);
    emitUpdate();
    void pushCurrencyToApi(normalized);
  }

  return normalized;
}

export async function refreshCurrencySettingFromApi(): Promise<CurrencyCode | null> {
  if (typeof window === "undefined") {
    return null;
  }

  try {
    const response = await fetch(GENERAL_API_PATH, {
      method: "GET",
      cache: "no-store"
    });

    if (!response.ok) {
      return null;
    }

    const payload = (await response.json()) as GeneralSettingsApiResponse;
    const normalized = normalizeCurrency(payload.currency);
    window.localStorage.setItem(STORAGE_KEY, normalized);
    emitUpdate();
    return normalized;
  } catch {
    return null;
  }
}

export function useCurrencySetting(): CurrencyCode {
  const [currency, setCurrency] = useState<CurrencyCode>(DEFAULT_CURRENCY);

  useEffect(() => {
    setCurrency(readCurrencySetting());
    void refreshCurrencySettingFromApi();

    function onUpdate() {
      setCurrency(readCurrencySetting());
    }

    window.addEventListener(UPDATE_EVENT, onUpdate);
    window.addEventListener("storage", onUpdate);

    return () => {
      window.removeEventListener(UPDATE_EVENT, onUpdate);
      window.removeEventListener("storage", onUpdate);
    };
  }, []);

  return currency;
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
