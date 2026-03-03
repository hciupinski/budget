"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Input } from "@/components/ui/input";
import { MONTH_LABELS, type AnnualPlanResponse } from "@/lib/budget-types";
import { CopyIcon, RefreshIcon, SaveIcon } from "@/components/budget/icons";
import {
  asCurrency,
  asSignedCurrency,
  getSectionGroup,
  getSectionLabel,
  sectionRowTone,
  splitAnnualByBusinessAndPersonal
} from "@/components/budget/budget-ui-utils";

const GROUP_ORDER = ["INCOME", "BUSINESS_EXPENSES", "PERSONAL_EXPENSES", "SAVINGS_INVESTMENTS"] as const;

export function AnnualPlanner() {
  const now = new Date();
  const [year, setYear] = useState<number>(now.getFullYear());
  const [annualPlan, setAnnualPlan] = useState<AnnualPlanResponse | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [saving, setSaving] = useState<boolean>(false);
  const [message, setMessage] = useState<string | null>(null);

  function redirectToLoginIfUnauthorized(statusCode: number): boolean {
    if (statusCode === 401) {
      window.location.assign("/login");
      return true;
    }

    return false;
  }

  const loadAnnualPlan = useCallback(async (selectedYear: number) => {
    setLoading(true);
    setMessage(null);

    const response = await fetch(`/api/budget/annual/${selectedYear}`, {
      method: "GET",
      cache: "no-store"
    });

    if (!response.ok) {
      if (redirectToLoginIfUnauthorized(response.status)) {
        return;
      }

      setAnnualPlan(null);
      setMessage("Unable to load annual plan.");
      setLoading(false);
      return;
    }

    setAnnualPlan((await response.json()) as AnnualPlanResponse);
    setLoading(false);
  }, []);

  useEffect(() => {
    void loadAnnualPlan(year);
  }, [loadAnnualPlan, year]);

  const rowsWithMeta = useMemo(() => {
    const rows = annualPlan?.categories ?? [];

    return rows.map((row, rowIndex) => ({
      row,
      rowIndex,
      group: getSectionGroup(row.section, row.categoryName)
    }));
  }, [annualPlan]);

  const groupedRows = useMemo(
    () =>
      GROUP_ORDER.map((group) => ({
        group,
        label: getSectionLabel(group),
        rows: rowsWithMeta.filter((row) => row.group === group)
      })).filter((group) => group.rows.length > 0),
    [rowsWithMeta]
  );

  function updateCell(rowIndex: number, monthIndex: number, value: string) {
    if (!annualPlan) {
      return;
    }

    const parsed = Number.parseFloat(value);
    const nextValue = Number.isFinite(parsed) ? parsed : 0;

    const nextRows = annualPlan.categories.map((row, currentRow) => {
      if (currentRow !== rowIndex) {
        return row;
      }

      const nextMonths = row.months.map((monthValue, currentMonth) =>
        currentMonth === monthIndex ? nextValue : monthValue
      );

      const total = nextMonths.reduce((sum, current) => sum + current, 0);

      return {
        ...row,
        months: nextMonths,
        total
      };
    });

    const income = nextRows.filter((row) => row.section === "INCOME").reduce((sum, row) => sum + row.total, 0);
    const costs = nextRows.filter((row) => row.section === "COSTS").reduce((sum, row) => sum + row.total, 0);
    const savingsInvestments = nextRows
      .filter((row) => row.section === "SAVINGS_INVESTMENTS")
      .reduce((sum, row) => sum + row.total, 0);

    setAnnualPlan({
      ...annualPlan,
      categories: nextRows,
      summary: {
        income,
        costs,
        savingsInvestments,
        remainder: income - costs - savingsInvestments,
        grandTotal: income + costs + savingsInvestments
      }
    });
  }

  async function saveAnnualPlan() {
    if (!annualPlan) {
      return;
    }

    setSaving(true);
    setMessage(null);

    const payload = {
      cells: annualPlan.categories.flatMap((row) =>
        row.months.map((plannedAmount, index) => ({
          categoryId: row.categoryId,
          month: index + 1,
          plannedAmount
        }))
      )
    };

    const response = await fetch(`/api/budget/annual/${year}`, {
      method: "PUT",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      if (redirectToLoginIfUnauthorized(response.status)) {
        return;
      }

      setSaving(false);
      setMessage("Save failed.");
      return;
    }

    setAnnualPlan((await response.json()) as AnnualPlanResponse);
    setSaving(false);
    setMessage("Annual plan saved.");
  }

  async function copyFromPreviousYear() {
    setSaving(true);
    setMessage(null);

    const response = await fetch(`/api/budget/annual/${year}/copy-from/${year - 1}`, {
      method: "POST"
    });

    if (!response.ok) {
      if (redirectToLoginIfUnauthorized(response.status)) {
        return;
      }

      setSaving(false);
      setMessage(`Cannot copy from ${year - 1}.`);
      return;
    }

    setAnnualPlan((await response.json()) as AnnualPlanResponse);
    setSaving(false);
    setMessage(`Copied annual plan from ${year - 1}.`);
  }

  const splitTotals = useMemo(
    () =>
      annualPlan
        ? splitAnnualByBusinessAndPersonal(annualPlan)
        : {
            businessCosts: 0,
            personalCosts: 0,
            savings: 0,
            investments: 0
          },
    [annualPlan]
  );

  const savingsAndInvestments = splitTotals.savings + splitTotals.investments;
  const transferToPersonal = (annualPlan?.summary.income ?? 0) - splitTotals.businessCosts;

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <h1 className="text-3xl md:text-4xl font-semibold tracking-[-0.02em] text-[#0f1321]">Annual Budget Planning</h1>
          <p className="text-lg md:text-xl text-[#71768b]">Plan your budget across all months</p>
        </div>

        <div className="flex flex-wrap items-center gap-2 xl:justify-end">
          <select
            className="h-12 min-w-[132px] rounded-2xl border border-[#d1d5dd] bg-[#e9eaed] px-4 text-lg text-[#202532]"
            value={year}
            onChange={(event) => setYear(Number.parseInt(event.target.value, 10))}
          >
            {Array.from({ length: 9 }).map((_, index) => {
              const optionYear = now.getFullYear() - 3 + index;
              return (
                <option key={optionYear} value={optionYear}>
                  {optionYear}
                </option>
              );
            })}
          </select>

          <button
            type="button"
            className="inline-flex h-12 items-center gap-2 rounded-2xl border border-[#d1d5dd] bg-[#f3f4f6] px-4 text-sm md:text-base text-[#171b27] hover:bg-[#e9ebf0]"
            onClick={() => void loadAnnualPlan(year)}
          >
            <RefreshIcon size={20} />
            Reload
          </button>

          <button
            type="button"
            className="inline-flex h-12 items-center gap-2 rounded-2xl border border-[#d1d5dd] bg-[#f3f4f6] px-4 text-sm md:text-base text-[#171b27] hover:bg-[#e9ebf0]"
            onClick={() => void copyFromPreviousYear()}
            disabled={saving}
          >
            <CopyIcon size={20} />
            Copy {year - 1}
          </button>

          <button
            type="button"
            className="inline-flex h-12 items-center gap-2 rounded-2xl bg-[#040426] px-5 text-sm md:text-base text-white hover:opacity-95 disabled:opacity-60"
            onClick={() => void saveAnnualPlan()}
            disabled={saving || loading || !annualPlan}
          >
            <SaveIcon size={20} />
            {saving ? "Saving..." : "Save Annual Plan"}
          </button>
        </div>
      </header>

      {message ? <p className="text-sm md:text-base text-[#686e84]">{message}</p> : null}
      {loading ? <p className="text-sm md:text-base text-[#686e84]">Loading annual plan...</p> : null}

      {annualPlan ? (
        <>
          <section className="overflow-hidden rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9]">
            <div className="overflow-x-auto">
              <table className="w-max min-w-full border-collapse">
                <thead>
                  <tr className="bg-[#eceef2]">
                    <th className="border-b border-[#cdd2da] px-4 py-4 text-left text-sm md:text-base font-semibold text-[#171b25]">Section</th>
                    <th className="border-b border-[#cdd2da] px-4 py-4 text-left text-sm md:text-base font-semibold text-[#171b25]">Item</th>
                    {MONTH_LABELS.map((label) => (
                      <th
                        key={label}
                        className="w-[122px] border-b border-[#cdd2da] px-3 py-4 text-center text-sm md:text-base font-semibold text-[#171b25]"
                      >
                        {label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {groupedRows.map((group) => (
                    group.rows.map(({ row, rowIndex }, index) => (
                      <tr key={row.categoryId} className={sectionRowTone(group.group)}>
                        {index === 0 ? (
                          <td
                            rowSpan={group.rows.length}
                            className="border-b border-[#cad0d8] px-4 py-4 align-top text-lg md:text-xl text-[#1b1f2b]"
                          >
                            {group.label}
                          </td>
                        ) : null}

                        <td className="border-b border-[#cad0d8] px-4 py-4 text-lg md:text-xl text-[#1b1f2b]">{row.categoryName}</td>

                        {row.months.map((monthValue, monthIndex) => (
                          <td key={`${row.categoryId}-${monthIndex}`} className="border-b border-[#cad0d8] px-2 py-3">
                            <Input
                              type="number"
                              step="0.01"
                              className="numeric-input h-11 min-w-[108px] rounded-xl border-0 bg-[#eff1f4] text-center text-sm md:text-base font-medium text-[#1f2430] shadow-none"
                              value={monthValue}
                              onChange={(event) => updateCell(rowIndex, monthIndex, event.target.value)}
                            />
                          </td>
                        ))}
                      </tr>
                    ))
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
            <MetricCard label="INCOME" value={annualPlan.summary.income} valueTone="text-[#10a34a]" />
            <MetricCard label="BUSINESS COSTS" value={splitTotals.businessCosts} valueTone="text-[#8f30ff]" />
            <MetricCard label="PERSONAL COSTS" value={splitTotals.personalCosts} valueTone="text-[#f35b00]" />
            <MetricCard label="SAVINGS / INVEST" value={savingsAndInvestments} valueTone="text-[#2563eb]" />
            <MetricCard
              label="REMAINDER"
              value={annualPlan.summary.remainder}
              valueTone="text-[#e11d48]"
              danger
            />
          </section>

          <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-6 md:p-8">
            <h2 className="text-2xl md:text-3xl font-medium text-[#171a24]">Annual Money Flow Summary</h2>

            <div className="mt-6 space-y-4 text-lg md:text-xl">
              <SummaryLine label="Total Business Income" value={annualPlan.summary.income} valueTone="text-[#10a34a]" />
              <SummaryLine
                label="- Business Expenses"
                value={-splitTotals.businessCosts}
                valueTone="text-[#8f30ff]"
              />

              <div className="border-t border-[#d7dbe2]" />

              <SummaryLine label="Transfer to Personal" value={transferToPersonal} valueTone="text-[#10a34a]" />
              <SummaryLine
                label="- Personal Expenses"
                value={-splitTotals.personalCosts}
                valueTone="text-[#f35b00]"
              />
              <SummaryLine
                label="- Savings"
                value={-savingsAndInvestments}
                valueTone="text-[#2563eb]"
              />

              <div className="border-t border-[#d7dbe2]" />

              <SummaryLine label="Remainder (Unallocated)" value={annualPlan.summary.remainder} valueTone="text-[#e11d48]" />
            </div>
          </section>
        </>
      ) : null}
    </div>
  );
}

function MetricCard({
  label,
  value,
  valueTone,
  danger = false
}: {
  label: string;
  value: number;
  valueTone: string;
  danger?: boolean;
}) {
  return (
    <div
      className={`rounded-[20px] border bg-[#f6f7f9] p-6 ${danger ? "border-[#f43f5e]" : "border-[#cfd3da]"}`}
    >
      <p className="text-sm md:text-base tracking-wide text-[#72778b]">{label}</p>
      <p className={`mt-2 text-3xl md:text-4xl font-medium ${valueTone}`}>{asCurrency(value)}</p>
    </div>
  );
}

function SummaryLine({
  label,
  value,
  valueTone
}: {
  label: string;
  value: number;
  valueTone: string;
}) {
  return (
    <div className="flex items-center justify-between gap-4">
      <p className="text-[#6f7489]">{label}</p>
      <p className={valueTone}>{asSignedCurrency(value)}</p>
    </div>
  );
}
