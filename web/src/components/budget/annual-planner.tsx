"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { MONTH_LABELS, type AnnualPlanResponse } from "@/lib/budget-types";

function asCurrency(value: number): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 2
  }).format(value);
}

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

    const data = (await response.json()) as AnnualPlanResponse;
    setAnnualPlan(data);
    setLoading(false);
  }, []);

  useEffect(() => {
    void loadAnnualPlan(year);
  }, [loadAnnualPlan, year]);

  const categories = useMemo(() => annualPlan?.categories ?? [], [annualPlan]);

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

    const income = nextRows
      .filter((row) => row.section === "INCOME")
      .reduce((sum, row) => sum + row.total, 0);
    const costs = nextRows
      .filter((row) => row.section === "COSTS")
      .reduce((sum, row) => sum + row.total, 0);
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

    const nextPlan = (await response.json()) as AnnualPlanResponse;
    setAnnualPlan(nextPlan);
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

    const nextPlan = (await response.json()) as AnnualPlanResponse;
    setAnnualPlan(nextPlan);
    setSaving(false);
    setMessage(`Copied annual plan from ${year - 1}.`);
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Annual Planner</CardTitle>
        <CardDescription>Edit Jan-Dec planned values and yearly totals.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap items-center gap-2">
          <Input
            type="number"
            min={2000}
            max={2100}
            className="w-28"
            value={year}
            onChange={(event) => setYear(Number.parseInt(event.target.value, 10) || year)}
          />
          <Button type="button" variant="outline" onClick={() => void loadAnnualPlan(year)}>
            Reload
          </Button>
          <Button type="button" variant="secondary" onClick={copyFromPreviousYear} disabled={saving}>
            Copy {year - 1}
          </Button>
          <Button type="button" onClick={saveAnnualPlan} disabled={saving || loading || !annualPlan}>
            {saving ? "Saving..." : "Save Annual Plan"}
          </Button>
          {message ? <p className="text-sm text-muted-foreground">{message}</p> : null}
        </div>

        {loading ? <p className="text-sm text-muted-foreground">Loading annual plan...</p> : null}

        {!loading && annualPlan ? (
          <div className="space-y-3 overflow-x-auto pb-1">
            <table className="w-max min-w-full border-collapse text-sm">
              <thead>
                <tr>
                  <th className="min-w-[170px] border px-2 py-2 text-left">Category</th>
                  <th className="min-w-[210px] border px-2 py-2 text-left">Section</th>
                  {MONTH_LABELS.map((label) => (
                    <th key={label} className="w-[96px] min-w-[96px] border px-2 py-2 text-right whitespace-nowrap">
                      {label}
                    </th>
                  ))}
                  <th className="min-w-[145px] border px-2 py-2 text-right whitespace-nowrap">Total</th>
                </tr>
              </thead>
              <tbody>
                {categories.map((row, rowIndex) => (
                  <tr key={row.categoryId}>
                    <td className="border px-2 py-2 whitespace-nowrap">{row.categoryName}</td>
                    <td className="border px-2 py-2 whitespace-nowrap">{row.section}</td>
                    {row.months.map((monthValue, monthIndex) => (
                      <td
                        key={`${row.categoryId}-${monthIndex}`}
                        className="w-[96px] min-w-[96px] border px-1 py-1"
                      >
                        <Input
                          type="number"
                          step="0.01"
                          className="numeric-input h-8 min-w-[88px] px-2 text-right text-sm tabular-nums"
                          value={monthValue}
                          onChange={(event) => updateCell(rowIndex, monthIndex, event.target.value)}
                        />
                      </td>
                    ))}
                    <td className="min-w-[145px] border px-2 py-2 text-right font-medium tabular-nums whitespace-nowrap">
                      {asCurrency(row.total)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div className="grid gap-3 md:grid-cols-5">
              <SummaryCard label="Income" value={annualPlan.summary.income} />
              <SummaryCard label="Costs" value={annualPlan.summary.costs} />
              <SummaryCard label="Savings / Invest" value={annualPlan.summary.savingsInvestments} />
              <SummaryCard label="Remainder" value={annualPlan.summary.remainder} />
              <SummaryCard label="Grand Total" value={annualPlan.summary.grandTotal} />
            </div>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function SummaryCard({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-md border bg-background p-3">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="text-lg font-semibold">{asCurrency(value)}</p>
    </div>
  );
}
