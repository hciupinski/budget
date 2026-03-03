"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { type AnnualPlanResponse } from "@/lib/budget-types";
import {
  ArrowRightIcon,
  BusinessIcon,
  HomeIcon,
  InvestmentIcon,
  SavingsIcon
} from "@/components/budget/icons";
import {
  asCurrency,
  getSectionGroup,
  isSavingsCategory,
  monthLongLabel
} from "@/components/budget/budget-ui-utils";

const ACCOUNT_SNAPSHOT = [
  { label: "Business Account", value: 15420.5 },
  { label: "Personal Checking", value: 3240 },
  { label: "Emergency Fund", value: 12000 },
  { label: "Investment Portfolio", value: 28500 }
];

export function BudgetOverview() {
  const now = new Date();
  const year = now.getFullYear();
  const month = now.getMonth() + 1;
  const [annualPlan, setAnnualPlan] = useState<AnnualPlanResponse | null>(null);

  const loadAnnualPlan = useCallback(async (selectedYear: number) => {
    const response = await fetch(`/api/budget/annual/${selectedYear}`, {
      method: "GET",
      cache: "no-store"
    });

    if (!response.ok) {
      setAnnualPlan(null);
      return;
    }

    setAnnualPlan((await response.json()) as AnnualPlanResponse);
  }, []);

  useEffect(() => {
    void loadAnnualPlan(year);
  }, [loadAnnualPlan, year]);

  const monthMetrics = useMemo(() => {
    if (!annualPlan) {
      return {
        businessIncome: 8500,
        businessExpenses: 1235,
        transferToPersonal: 7265,
        personalExpenses: 3350,
        savings: 800,
        investments: 1500
      };
    }

    const rows = annualPlan.categories;
    const monthIndex = month - 1;

    const businessIncome = rows
      .filter((row) => row.section === "INCOME")
      .reduce((sum, row) => sum + (row.months[monthIndex] ?? 0), 0);

    const businessExpenses = rows
      .filter((row) => getSectionGroup(row.section, row.categoryName) === "BUSINESS_EXPENSES")
      .reduce((sum, row) => sum + (row.months[monthIndex] ?? 0), 0);

    const personalExpenses = rows
      .filter((row) => getSectionGroup(row.section, row.categoryName) === "PERSONAL_EXPENSES")
      .reduce((sum, row) => sum + (row.months[monthIndex] ?? 0), 0);

    const savings = rows
      .filter((row) => row.section === "SAVINGS_INVESTMENTS" && isSavingsCategory(row.categoryName))
      .reduce((sum, row) => sum + (row.months[monthIndex] ?? 0), 0);

    const investments = rows
      .filter((row) => row.section === "SAVINGS_INVESTMENTS" && !isSavingsCategory(row.categoryName))
      .reduce((sum, row) => sum + (row.months[monthIndex] ?? 0), 0);

    return {
      businessIncome,
      businessExpenses,
      transferToPersonal: businessIncome - businessExpenses,
      personalExpenses,
      savings,
      investments
    };
  }, [annualPlan, month]);

  return (
    <div className="space-y-8">
      <header>
        <h1 className="text-3xl md:text-4xl font-semibold tracking-[-0.02em] text-[#0f1321]">Budget Dashboard</h1>
        <p className="text-lg md:text-xl text-[#71768b]">
          {monthLongLabel(month)} {year} - Current Month Overview
        </p>
      </header>

      <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-6 md:p-8">
        <h2 className="text-2xl md:text-3xl font-medium text-[#171a24]">Monthly Money Flow</h2>
        <p className="mt-2 text-base md:text-lg text-[#73788d]">Track how your business income flows through to personal finances</p>

        <div className="mt-8 grid gap-3 xl:grid-cols-[1fr_auto_1fr_auto_1fr] xl:items-center">
          <FlowCard
            tone="blue"
            label="Business Income"
            value={monthMetrics.businessIncome}
            icon={<BusinessIcon size={22} />}
          />
          <ArrowRightIcon size={30} className="mx-auto hidden text-[#6d7184] xl:block" />
          <FlowCard
            tone="purple"
            label="Business Expenses"
            value={monthMetrics.businessExpenses}
            icon={<span className="text-[22px] font-semibold">$</span>}
          />
          <ArrowRightIcon size={30} className="mx-auto hidden text-[#6d7184] xl:block" />
          <FlowCard
            tone="green"
            label="To Personal"
            value={monthMetrics.transferToPersonal}
            icon={<HomeIcon size={22} />}
          />
        </div>

        <div className="my-8 border-t border-[#d9dce2]" />

        <h3 className="text-lg md:text-xl font-medium text-[#171a24]">Personal Distribution</h3>
        <div className="mt-4 grid gap-3 md:grid-cols-3">
          <DistributionCard
            tone="orange"
            label="Expenses"
            value={monthMetrics.personalExpenses}
            icon={<HomeIcon size={18} />}
          />
          <DistributionCard tone="teal" label="Savings" value={monthMetrics.savings} icon={<SavingsIcon size={18} />} />
          <DistributionCard
            tone="indigo"
            label="Investments"
            value={monthMetrics.investments}
            icon={<InvestmentIcon size={18} />}
          />
        </div>
      </section>

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {ACCOUNT_SNAPSHOT.map((account) => (
          <div key={account.label} className="rounded-[20px] border border-[#cfd3da] bg-[#f6f7f9] p-6">
            <p className="text-lg md:text-xl text-[#70768b]">{account.label}</p>
            <p className="mt-2 text-2xl md:text-3xl font-medium text-[#141824]">{asCurrency(account.value)}</p>
          </div>
        ))}
      </section>
    </div>
  );
}

function FlowCard({
  tone,
  label,
  value,
  icon
}: {
  tone: "blue" | "purple" | "green";
  label: string;
  value: number;
  icon: ReactNode;
}) {
  const classes: Record<typeof tone, string> = {
    blue: "border-[#aad0f6] bg-[#e8f1fb]",
    purple: "border-[#d8bdf5] bg-[#f1ebf8]",
    green: "border-[#98e2bd] bg-[#e5f4eb]"
  };

  return (
    <div className={`rounded-[18px] border p-5 ${classes[tone]}`}>
      <div className="flex items-center gap-3">
        <div className="grid h-12 w-12 place-items-center rounded-2xl bg-[#2d7af5] text-white">{icon}</div>
        <div>
          <p className="text-base md:text-lg text-[#6d7287]">{label}</p>
          <p className="text-3xl md:text-4xl font-medium text-[#121621]">{asCurrency(value)}</p>
        </div>
      </div>
    </div>
  );
}

function DistributionCard({
  tone,
  label,
  value,
  icon
}: {
  tone: "orange" | "teal" | "indigo";
  label: string;
  value: number;
  icon: ReactNode;
}) {
  const classes: Record<typeof tone, string> = {
    orange: "border-[#f3c18c] bg-[#f5efe6]",
    teal: "border-[#82e8dc] bg-[#e2f0f0]",
    indigo: "border-[#b6c2f7] bg-[#e9edf8]"
  };

  const iconBg: Record<typeof tone, string> = {
    orange: "bg-[#f97316]",
    teal: "bg-[#0ea5a2]",
    indigo: "bg-[#4f46e5]"
  };

  return (
    <div className={`rounded-[18px] border p-4 ${classes[tone]}`}>
      <div className="flex items-center gap-2 text-[#6d7287]">
        <span className={`grid h-6 w-6 place-items-center rounded-md text-white ${iconBg[tone]}`}>{icon}</span>
        <span className="text-base md:text-lg">{label}</span>
      </div>
      <p className="mt-2 text-2xl md:text-3xl font-medium text-[#141824]">{asCurrency(value)}</p>
    </div>
  );
}
