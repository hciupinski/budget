import {
  resolveManagedSection,
  type ApiSection,
  type ManagedSection,
  type ManagedSectionKind
} from "@/lib/section-settings";
import { MONTH_LABELS, type AnnualPlanResponse, type MonthlyWorkspaceResponse } from "@/lib/budget-types";

export function asCurrency(value: number): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 2
  }).format(value);
}

export function asSignedCurrency(value: number): string {
  if (value > 0) {
    return `+${asCurrency(value)}`;
  }

  if (value < 0) {
    return `-${asCurrency(Math.abs(value))}`;
  }

  return asCurrency(0);
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

export function splitAnnualByBusinessAndPersonal(plan: AnnualPlanResponse, sections: ManagedSection[]) {
  const result = {
    businessCosts: 0,
    personalCosts: 0,
    savings: 0,
    investments: 0
  };

  for (const row of plan.categories) {
    const resolved = resolveManagedSection(row.section, row.categoryName, sections);

    if (resolved.kind === "BUSINESS_EXPENSES") {
      result.businessCosts += row.total;
      continue;
    }

    if (resolved.kind === "PERSONAL_EXPENSES") {
      result.personalCosts += row.total;
      continue;
    }

    if (resolved.kind === "SAVINGS") {
      result.savings += row.total;
      continue;
    }

    if (resolved.kind === "INVESTMENTS") {
      result.investments += row.total;
    }
  }

  return result;
}

export function splitMonthlyByBusinessAndPersonal(workspace: MonthlyWorkspaceResponse, sections: ManagedSection[]) {
  const result = {
    businessCostsPlanned: 0,
    businessCostsActual: 0,
    personalCostsPlanned: 0,
    personalCostsActual: 0,
    savingsPlanned: 0,
    savingsActual: 0,
    investmentsPlanned: 0,
    investmentsActual: 0
  };

  for (const action of workspace.actions) {
    const resolved = resolveManagedSection(action.section, action.categoryName, sections);
    const actual = action.actualAmount ?? 0;

    if (resolved.kind === "BUSINESS_EXPENSES") {
      result.businessCostsPlanned += action.plannedAmount;
      result.businessCostsActual += actual;
      continue;
    }

    if (resolved.kind === "PERSONAL_EXPENSES") {
      result.personalCostsPlanned += action.plannedAmount;
      result.personalCostsActual += actual;
      continue;
    }

    if (resolved.kind === "SAVINGS") {
      result.savingsPlanned += action.plannedAmount;
      result.savingsActual += actual;
      continue;
    }

    if (resolved.kind === "INVESTMENTS") {
      result.investmentsPlanned += action.plannedAmount;
      result.investmentsActual += actual;
    }
  }

  return result;
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
