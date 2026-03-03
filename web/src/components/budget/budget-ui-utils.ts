import { MONTH_LABELS, type AnnualPlanResponse, type MonthlyWorkspaceResponse } from "@/lib/budget-types";

export type SectionGroup = "INCOME" | "BUSINESS_EXPENSES" | "PERSONAL_EXPENSES" | "SAVINGS_INVESTMENTS";

const BUSINESS_COST_KEYWORDS = ["software", "office", "professional", "marketing", "accountant", "service"];
const SAVINGS_KEYWORDS = ["emergency", "savings"];

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

export function isBusinessCostCategory(name: string): boolean {
  const lower = name.toLowerCase();
  return BUSINESS_COST_KEYWORDS.some((keyword) => lower.includes(keyword));
}

export function isSavingsCategory(name: string): boolean {
  const lower = name.toLowerCase();
  return SAVINGS_KEYWORDS.some((keyword) => lower.includes(keyword));
}

export function getSectionGroup(section: "INCOME" | "COSTS" | "SAVINGS_INVESTMENTS", categoryName: string): SectionGroup {
  if (section === "INCOME") {
    return "INCOME";
  }

  if (section === "SAVINGS_INVESTMENTS") {
    return "SAVINGS_INVESTMENTS";
  }

  return isBusinessCostCategory(categoryName) ? "BUSINESS_EXPENSES" : "PERSONAL_EXPENSES";
}

export function getSectionLabel(group: SectionGroup): string {
  if (group === "INCOME") {
    return "Income";
  }

  if (group === "BUSINESS_EXPENSES") {
    return "Business Expenses";
  }

  if (group === "PERSONAL_EXPENSES") {
    return "Personal Expenses";
  }

  return "Savings & Investments";
}

export function sectionRowTone(group: SectionGroup): string {
  if (group === "INCOME") {
    return "bg-[#e8f4ee]";
  }

  if (group === "BUSINESS_EXPENSES") {
    return "bg-[#efebf7]";
  }

  if (group === "PERSONAL_EXPENSES") {
    return "bg-[#f5efe5]";
  }

  return "bg-[#e8eef7]";
}

export function splitAnnualByBusinessAndPersonal(plan: AnnualPlanResponse) {
  const businessCosts = plan.categories
    .filter((row) => row.section === "COSTS" && isBusinessCostCategory(row.categoryName))
    .reduce((sum, row) => sum + row.total, 0);

  const personalCosts = plan.categories
    .filter((row) => row.section === "COSTS" && !isBusinessCostCategory(row.categoryName))
    .reduce((sum, row) => sum + row.total, 0);

  const savings = plan.categories
    .filter((row) => row.section === "SAVINGS_INVESTMENTS" && isSavingsCategory(row.categoryName))
    .reduce((sum, row) => sum + row.total, 0);

  const investments = plan.categories
    .filter((row) => row.section === "SAVINGS_INVESTMENTS" && !isSavingsCategory(row.categoryName))
    .reduce((sum, row) => sum + row.total, 0);

  return {
    businessCosts,
    personalCosts,
    savings,
    investments
  };
}

export function splitMonthlyByBusinessAndPersonal(workspace: MonthlyWorkspaceResponse) {
  const businessCostsPlanned = workspace.actions
    .filter((action) => action.section === "COSTS" && isBusinessCostCategory(action.categoryName))
    .reduce((sum, action) => sum + action.plannedAmount, 0);

  const businessCostsActual = workspace.actions
    .filter((action) => action.section === "COSTS" && isBusinessCostCategory(action.categoryName))
    .reduce((sum, action) => sum + (action.actualAmount ?? 0), 0);

  const personalCostsPlanned = workspace.actions
    .filter((action) => action.section === "COSTS" && !isBusinessCostCategory(action.categoryName))
    .reduce((sum, action) => sum + action.plannedAmount, 0);

  const personalCostsActual = workspace.actions
    .filter((action) => action.section === "COSTS" && !isBusinessCostCategory(action.categoryName))
    .reduce((sum, action) => sum + (action.actualAmount ?? 0), 0);

  const savingsPlanned = workspace.actions
    .filter((action) => action.section === "SAVINGS_INVESTMENTS" && isSavingsCategory(action.categoryName))
    .reduce((sum, action) => sum + action.plannedAmount, 0);

  const savingsActual = workspace.actions
    .filter((action) => action.section === "SAVINGS_INVESTMENTS" && isSavingsCategory(action.categoryName))
    .reduce((sum, action) => sum + (action.actualAmount ?? 0), 0);

  const investmentsPlanned = workspace.actions
    .filter((action) => action.section === "SAVINGS_INVESTMENTS" && !isSavingsCategory(action.categoryName))
    .reduce((sum, action) => sum + action.plannedAmount, 0);

  const investmentsActual = workspace.actions
    .filter((action) => action.section === "SAVINGS_INVESTMENTS" && !isSavingsCategory(action.categoryName))
    .reduce((sum, action) => sum + (action.actualAmount ?? 0), 0);

  return {
    businessCostsPlanned,
    businessCostsActual,
    personalCostsPlanned,
    personalCostsActual,
    savingsPlanned,
    savingsActual,
    investmentsPlanned,
    investmentsActual
  };
}

export function differenceTone(section: "INCOME" | "COSTS" | "SAVINGS_INVESTMENTS", difference: number): string {
  if (difference === 0) {
    return "text-[#6b7280]";
  }

  if (section === "INCOME") {
    return difference > 0 ? "text-[#11a34a]" : "text-[#e11d48]";
  }

  return difference < 0 ? "text-[#11a34a]" : "text-[#e11d48]";
}
