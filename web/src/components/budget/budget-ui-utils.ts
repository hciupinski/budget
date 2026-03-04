import {
  type ApiSection,
  type ManagedSectionKind
} from "@/lib/section-settings";
import { MONTH_LABELS } from "@/lib/budget-types";
import { formatCurrency, formatSignedCurrency, readCurrencySetting } from "@/lib/currency-settings";

export function asCurrency(value: number): string {
  return formatCurrency(value, readCurrencySetting());
}

export function asSignedCurrency(value: number): string {
  return formatSignedCurrency(value, readCurrencySetting());
}

export function monthLongLabel(month: number): string {
  const label = MONTH_LABELS[month - 1];
  const map: Record<string, string> = {
    Jan: "January",
    Feb: "February",
    Mar: "March",
    Apr: "April",
    May: "May",
    Jun: "June",
    Jul: "July",
    Aug: "August",
    Sep: "September",
    Oct: "October",
    Nov: "November",
    Dec: "December"
  };

  return map[label] ?? label;
}

export function sectionRowTone(kind: ManagedSectionKind): string {
  if (kind === "INCOME") {
    return "bg-[#e8f4ee]";
  }

  if (kind === "BUSINESS_EXPENSES") {
    return "bg-[#efebf7]";
  }

  if (kind === "PERSONAL_EXPENSES") {
    return "bg-[#f5efe5]";
  }

  return "bg-[#e8eef7]";
}

export function differenceTone(section: ApiSection, difference: number): string {
  if (difference === 0) {
    return "text-[#6b7280]";
  }

  if (section === "INCOME") {
    return difference > 0 ? "text-[#11a34a]" : "text-[#e11d48]";
  }

  return difference < 0 ? "text-[#11a34a]" : "text-[#e11d48]";
}
