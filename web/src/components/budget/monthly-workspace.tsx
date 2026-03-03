"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Input } from "@/components/ui/input";
import { MONTH_LABELS, type MonthlyWorkspaceResponse } from "@/lib/budget-types";
import { ChevronLeftIcon, ChevronRightIcon, CopyIcon, SaveIcon } from "@/components/budget/icons";
import {
  asCurrency,
  asSignedCurrency,
  differenceTone,
  getSectionGroup,
  getSectionLabel,
  sectionRowTone,
  splitMonthlyByBusinessAndPersonal
} from "@/components/budget/budget-ui-utils";

type ActionStatus = "PLANNED" | "DONE" | "PARTIAL" | "SKIPPED";

const GROUP_ORDER = ["INCOME", "BUSINESS_EXPENSES", "PERSONAL_EXPENSES", "SAVINGS_INVESTMENTS"] as const;

const STATUS_LABELS: Record<ActionStatus, string> = {
  PLANNED: "Unpaid",
  DONE: "Paid",
  PARTIAL: "Partial",
  SKIPPED: "Skipped"
};

export function MonthlyWorkspace() {
  const now = new Date();
  const [year, setYear] = useState<number>(now.getFullYear());
  const [month, setMonth] = useState<number>(now.getMonth() + 1);
  const [workspace, setWorkspace] = useState<MonthlyWorkspaceResponse | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [saving, setSaving] = useState<boolean>(false);
  const [message, setMessage] = useState<string | null>(null);
  const [notes, setNotes] = useState<Record<string, string>>({});

  function redirectToLoginIfUnauthorized(statusCode: number): boolean {
    if (statusCode === 401) {
      window.location.assign("/login");
      return true;
    }

    return false;
  }

  const loadWorkspace = useCallback(async () => {
    setLoading(true);
    setMessage(null);

    const response = await fetch(`/api/budget/months/${year}/${month}?status=ALL`, {
      method: "GET",
      cache: "no-store"
    });

    if (!response.ok) {
      if (redirectToLoginIfUnauthorized(response.status)) {
        return;
      }

      setWorkspace(null);
      setMessage("Unable to load monthly planning data.");
      setLoading(false);
      return;
    }

    setWorkspace((await response.json()) as MonthlyWorkspaceResponse);
    setLoading(false);
  }, [month, year]);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const rowsWithMeta = useMemo(() => {
    const actions = workspace?.actions ?? [];

    return actions.map((row, rowIndex) => ({
      row,
      rowIndex,
      group: getSectionGroup(row.section, row.categoryName)
    }));
  }, [workspace]);

  const groupedRows = useMemo(
    () =>
      GROUP_ORDER.map((group) => ({
        group,
        label: getSectionLabel(group),
        rows: rowsWithMeta.filter((row) => row.group === group)
      })).filter((group) => group.rows.length > 0),
    [rowsWithMeta]
  );

  function updateDraft(
    actionId: string,
    change: Partial<MonthlyWorkspaceResponse["actions"][number]>
  ) {
    if (!workspace) {
      return;
    }

    setWorkspace({
      ...workspace,
      actions: workspace.actions.map((action) =>
        action.actionId === actionId
          ? {
              ...action,
              ...change
            }
          : action
      )
    });
  }

  async function saveAllActions() {
    if (!workspace) {
      return;
    }

    setSaving(true);
    setMessage(null);

    let completed = 0;

    for (const action of workspace.actions) {
      const response = await fetch(`/api/budget/months/${year}/${month}/actions/${action.actionId}`, {
        method: "PATCH",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          status: action.status,
          actualAmount: action.actualAmount
        })
      });

      if (!response.ok) {
        if (redirectToLoginIfUnauthorized(response.status)) {
          return;
        }

        setSaving(false);
        setMessage(`Failed to save ${action.categoryName}.`);
        return;
      }

      completed += 1;
    }

    setSaving(false);
    setMessage(`Saved ${completed} monthly items.`);
    await loadWorkspace();
  }

  async function generateFromAnnualPlan() {
    const response = await fetch(`/api/budget/months/${year}/${month}/generate`, {
      method: "POST"
    });

    if (!response.ok) {
      if (redirectToLoginIfUnauthorized(response.status)) {
        return;
      }

      setMessage("Failed to copy budget from annual plan.");
      return;
    }

    setMessage("Copied budget values from annual plan.");
    await loadWorkspace();
  }

  function shiftMonth(direction: -1 | 1) {
    const shifted = new Date(year, month - 1 + direction, 1);
    setYear(shifted.getFullYear());
    setMonth(shifted.getMonth() + 1);
  }

  const monthlySplit = useMemo(
    () =>
      workspace
        ? splitMonthlyByBusinessAndPersonal(workspace)
        : {
            businessCostsPlanned: 0,
            businessCostsActual: 0,
            personalCostsPlanned: 0,
            personalCostsActual: 0,
            savingsPlanned: 0,
            savingsActual: 0,
            investmentsPlanned: 0,
            investmentsActual: 0
          },
    [workspace]
  );

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <h1 className="text-3xl md:text-4xl font-semibold tracking-[-0.02em] text-[#0f1321]">Monthly Planning</h1>
          <p className="text-lg md:text-xl text-[#71768b]">Manage your monthly budget and track payments</p>
        </div>

        <div className="flex flex-wrap items-center gap-2 xl:justify-end">
          <div className="inline-flex h-12 items-center rounded-2xl border border-[#d1d5dd] bg-[#f3f4f6] p-1">
            <button
              type="button"
              className="grid h-10 w-10 place-items-center rounded-xl text-[#1a1e2a] hover:bg-[#e4e7ed]"
              onClick={() => shiftMonth(-1)}
            >
              <ChevronLeftIcon size={20} />
            </button>

            <select
              className="h-10 min-w-[170px] rounded-xl bg-[#e8eaee] px-3 text-lg text-[#1c202c]"
              value={month}
              onChange={(event) => setMonth(Number.parseInt(event.target.value, 10))}
            >
              {MONTH_LABELS.map((label, index) => (
                <option key={label} value={index + 1}>
                  {label}
                </option>
              ))}
            </select>

            <select
              className="ml-2 h-10 min-w-[110px] rounded-xl bg-[#e8eaee] px-3 text-lg text-[#1c202c]"
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
              className="ml-2 grid h-10 w-10 place-items-center rounded-xl text-[#1a1e2a] hover:bg-[#e4e7ed]"
              onClick={() => shiftMonth(1)}
            >
              <ChevronRightIcon size={20} />
            </button>
          </div>

          <button
            type="button"
            className="inline-flex h-12 items-center gap-2 rounded-2xl border border-[#d1d5dd] bg-[#f3f4f6] px-4 text-sm md:text-base text-[#171b27] hover:bg-[#e9ebf0]"
            onClick={() => void generateFromAnnualPlan()}
          >
            <CopyIcon size={20} />
            Copy from Annual
          </button>

          <button
            type="button"
            className="inline-flex h-12 items-center gap-2 rounded-2xl bg-[#040426] px-5 text-sm md:text-base text-white hover:opacity-95 disabled:opacity-60"
            onClick={() => void saveAllActions()}
            disabled={!workspace || saving || loading}
          >
            <SaveIcon size={20} />
            {saving ? "Saving..." : "Save"}
          </button>
        </div>
      </header>

      {message ? <p className="text-sm md:text-base text-[#686e84]">{message}</p> : null}
      {loading ? <p className="text-sm md:text-base text-[#686e84]">Loading monthly plan...</p> : null}

      {workspace ? (
        <>
          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
            <MonthlyMetricCard
              label="INCOME"
              planned={workspace.summary.incomePlanned}
              actual={workspace.summary.incomeActual}
              valueTone="text-[#10a34a]"
            />
            <MonthlyMetricCard
              label="BUSINESS COSTS"
              planned={monthlySplit.businessCostsPlanned}
              actual={monthlySplit.businessCostsActual}
              valueTone="text-[#8f30ff]"
            />
            <MonthlyMetricCard
              label="PERSONAL COSTS"
              planned={monthlySplit.personalCostsPlanned}
              actual={monthlySplit.personalCostsActual}
              valueTone="text-[#f35b00]"
            />
            <MonthlyMetricCard
              label="SAVINGS / INVEST"
              planned={workspace.summary.savingsPlanned}
              actual={workspace.summary.savingsActual}
              valueTone="text-[#2563eb]"
            />
            <MonthlyMetricCard
              label="REMAINDER"
              planned={workspace.summary.remainderPlanned}
              actual={workspace.summary.remainderActual}
              valueTone="text-[#e11d48]"
              danger
            />
          </section>

          <section className="overflow-hidden rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9]">
            <div className="border-b border-[#d5d9e0] p-6 md:p-8">
              <h2 className="text-2xl md:text-3xl font-medium text-[#171a24]">
                Budget Items for {MONTH_LABELS[month - 1]} {year}
              </h2>
              <p className="mt-2 text-base md:text-lg text-[#73788d]">
                Manage budgeted amounts, track actual spending, and update payment status
              </p>
            </div>

            <div className="overflow-x-auto">
              <table className="w-max min-w-full border-collapse">
                <thead>
                  <tr className="bg-[#eceef2]">
                    <th className="border-b border-[#cdd2da] px-4 py-4 text-left text-sm md:text-base font-semibold text-[#171b25]">Section</th>
                    <th className="border-b border-[#cdd2da] px-4 py-4 text-left text-sm md:text-base font-semibold text-[#171b25]">Category</th>
                    <th className="border-b border-[#cdd2da] px-3 py-4 text-center text-sm md:text-base font-semibold text-[#171b25]">Budgeted</th>
                    <th className="border-b border-[#cdd2da] px-3 py-4 text-center text-sm md:text-base font-semibold text-[#171b25]">Actual</th>
                    <th className="border-b border-[#cdd2da] px-3 py-4 text-center text-sm md:text-base font-semibold text-[#171b25]">Difference</th>
                    <th className="border-b border-[#cdd2da] px-3 py-4 text-center text-sm md:text-base font-semibold text-[#171b25]">Status</th>
                    <th className="border-b border-[#cdd2da] px-3 py-4 text-left text-sm md:text-base font-semibold text-[#171b25]">Notes</th>
                  </tr>
                </thead>
                <tbody>
                  {groupedRows.map((group) => (
                    group.rows.map(({ row }, index) => {
                      const actual = row.actualAmount ?? 0;
                      const difference = actual - row.plannedAmount;

                      return (
                        <tr key={row.actionId} className={sectionRowTone(group.group)}>
                          {index === 0 ? (
                            <td
                              rowSpan={group.rows.length}
                              className="border-b border-[#cad0d8] px-4 py-4 align-top text-lg md:text-xl text-[#1b1f2b]"
                            >
                              {group.label}
                            </td>
                          ) : null}

                          <td className="border-b border-[#cad0d8] px-4 py-4 text-lg md:text-xl text-[#1b1f2b]">{row.categoryName}</td>

                          <td className="border-b border-[#cad0d8] px-3 py-3">
                            <div className="grid h-11 min-w-[108px] place-items-center rounded-xl bg-[#eff1f4] text-sm md:text-base font-medium text-[#1f2430]">
                              {row.plannedAmount}
                            </div>
                          </td>

                          <td className="border-b border-[#cad0d8] px-3 py-3">
                            <Input
                              type="number"
                              step="0.01"
                              className="numeric-input h-11 min-w-[108px] rounded-xl border-0 bg-[#eff1f4] text-center text-sm md:text-base font-medium text-[#1f2430] shadow-none"
                              value={row.actualAmount ?? ""}
                              onChange={(event) => {
                                const parsed = Number.parseFloat(event.target.value);
                                updateDraft(row.actionId, {
                                  actualAmount: Number.isFinite(parsed) ? parsed : null
                                });
                              }}
                            />
                          </td>

                          <td className={`border-b border-[#cad0d8] px-3 py-3 text-center text-base md:text-lg ${differenceTone(row.section, difference)}`}>
                            {difference === 0 ? "-" : asSignedCurrency(difference)}
                          </td>

                          <td className="border-b border-[#cad0d8] px-3 py-3">
                            <select
                              className="h-11 min-w-[160px] rounded-xl border-0 bg-[#eff1f4] px-3 text-sm md:text-base text-[#1f2430]"
                              value={row.status}
                              onChange={(event) =>
                                updateDraft(row.actionId, {
                                  status: event.target.value as ActionStatus
                                })
                              }
                            >
                              {Object.entries(STATUS_LABELS).map(([status, label]) => (
                                <option key={`${row.actionId}-${status}`} value={status}>
                                  {label}
                                </option>
                              ))}
                            </select>
                          </td>

                          <td className="border-b border-[#cad0d8] px-3 py-3">
                            <Input
                              value={notes[row.actionId] ?? ""}
                              onChange={(event) =>
                                setNotes((current) => ({
                                  ...current,
                                  [row.actionId]: event.target.value
                                }))
                              }
                              placeholder="Add notes..."
                              className="h-11 min-w-[180px] rounded-xl border-0 bg-[#eff1f4] text-sm md:text-base text-[#6d7287] shadow-none"
                            />
                          </td>
                        </tr>
                      );
                    })
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-6 md:p-8">
            <h2 className="text-2xl md:text-3xl font-medium text-[#171a24]">Monthly Summary - {MONTH_LABELS[month - 1]} {year}</h2>

            <div className="mt-6 grid gap-8 xl:grid-cols-2">
              <SummaryColumn
                label="BUDGETED"
                income={workspace.summary.incomePlanned}
                business={monthlySplit.businessCostsPlanned}
                personal={monthlySplit.personalCostsPlanned}
                savings={workspace.summary.savingsPlanned}
                remainder={workspace.summary.remainderPlanned}
              />
              <SummaryColumn
                label="ACTUAL"
                income={workspace.summary.incomeActual}
                business={monthlySplit.businessCostsActual}
                personal={monthlySplit.personalCostsActual}
                savings={workspace.summary.savingsActual}
                remainder={workspace.summary.remainderActual}
              />
            </div>
          </section>
        </>
      ) : null}
    </div>
  );
}

function MonthlyMetricCard({
  label,
  planned,
  actual,
  valueTone,
  danger = false
}: {
  label: string;
  planned: number;
  actual: number;
  valueTone: string;
  danger?: boolean;
}) {
  return (
    <div
      className={`rounded-[20px] border bg-[#f6f7f9] p-6 ${danger ? "border-[#f43f5e]" : "border-[#cfd3da]"}`}
    >
      <p className="text-sm md:text-base tracking-wide text-[#72778b]">{label}</p>
      <p className={`mt-2 text-3xl md:text-4xl font-medium ${valueTone}`}>{asCurrency(planned)}</p>
      <p className="mt-1 text-sm md:text-base text-[#72778b]">
        Actual: <span className={valueTone}>{asCurrency(actual)}</span>
      </p>
    </div>
  );
}

function SummaryColumn({
  label,
  income,
  business,
  personal,
  savings,
  remainder
}: {
  label: string;
  income: number;
  business: number;
  personal: number;
  savings: number;
  remainder: number;
}) {
  const transfer = income - business;

  return (
    <div className="space-y-4">
      <p className="text-sm md:text-base tracking-wide text-[#72778b]">{label}</p>

      <SummaryLine label="Total Business Income" value={income} valueTone="text-[#10a34a]" />
      <SummaryLine label="- Business Expenses" value={-business} valueTone="text-[#8f30ff]" />

      <div className="border-t border-[#d7dbe2]" />

      <SummaryLine label="Transfer to Personal" value={transfer} valueTone="text-[#10a34a]" />
      <SummaryLine label="- Personal Expenses" value={-personal} valueTone="text-[#f35b00]" />
      <SummaryLine label="- Savings" value={-savings} valueTone="text-[#2563eb]" />

      <div className="border-t border-[#d7dbe2]" />

      <SummaryLine label="Remainder" value={remainder} valueTone={remainder >= 0 ? "text-[#10a34a]" : "text-[#e11d48]"} />
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
    <div className="flex items-center justify-between gap-4 text-lg md:text-xl">
      <p className="text-[#6f7489]">{label}</p>
      <p className={valueTone}>{asSignedCurrency(value)}</p>
    </div>
  );
}
