"use client";

import { Fragment, useEffect, useMemo, useState } from "react";
import { Input } from "@/components/ui/input";
import { MONTH_LABELS, type MonthlyWorkspaceResponse } from "@/lib/budget-types";
import { ChevronLeftIcon, ChevronRightIcon, SaveIcon, TrashIcon } from "@/components/budget/icons";
import {
  asSignedCurrency,
  differenceTone,
  sectionRowTone
} from "@/components/budget/budget-ui-utils";
import {
  resolveManagedSection,
  useSectionSettings,
  type ManagedSection,
  type ManagedSectionKind
} from "@/lib/section-settings";
import { useCurrencySetting } from "@/lib/currency-settings";
import {
  annualApiOneTimeRowKey,
  annualCustomOneTimeRowKey,
  createMonthlyCustomDraft,
  PLANNER_CUSTOM_EVENT,
  refreshPlannerCustomizationFromApi,
  readAnnualCustomItems,
  readHiddenMonthlyApiRows,
  readMonthlyCustomItems,
  readNameOverrides,
  readOneTimeAnnualRowKeys,
  saveHiddenMonthlyApiRows,
  saveMonthlyCustomItems,
  saveNameOverrides,
  type ActionStatus,
  type AnnualCustomItem,
  type MonthlyCustomItem
} from "@/lib/planner-custom-items";
import { useMonthlyWorkspaceState } from "@/components/budget/hooks/use-monthly-workspace-state";
import { groupRowsBySection, resolveCustomSection } from "@/components/budget/planner-row-utils";
import {
  MonthlyMetricCard,
  MonthlySummaryColumn,
  MonthlyWorkspaceSummaryPanel,
  MonthlyWorkspaceTablePanel
} from "@/components/budget/monthly/monthly-presentational";

type MonthlyDisplayRow = {
  rowId: string;
  source: "api" | "monthlyCustom";
  actionId?: string;
  categoryId?: string;
  monthlyCustomId?: string;
  name: string;
  section: "INCOME" | "COSTS" | "SAVINGS_INVESTMENTS";
  plannedAmount: number;
  actualAmount: number | null;
  status: ActionStatus;
  resolvedSection: {
    id: string;
    name: string;
    kind: ManagedSectionKind;
    order: number;
    sectionId: string;
  };
};

const STATUS_LABELS: Record<ActionStatus, string> = {
  PLANNED: "Unpaid",
  DONE: "Paid",
  PARTIAL: "Partial",
  SKIPPED: "Skipped"
};

function sectionFromKind(kind: ManagedSectionKind): "INCOME" | "COSTS" | "SAVINGS_INVESTMENTS" {
  if (kind === "INCOME") {
    return "INCOME";
  }

  if (kind === "BUSINESS_EXPENSES" || kind === "PERSONAL_EXPENSES") {
    return "COSTS";
  }

  return "SAVINGS_INVESTMENTS";
}

function rowMatchKey(name: string, sectionId: string, sectionKind: ManagedSectionKind): string {
  const normalizedName = name.trim().toLowerCase();
  const normalizedSectionId = sectionId.trim().toLowerCase();
  const fallbackSectionId = normalizedSectionId || sectionKind.toLowerCase();
  return `${fallbackSectionId}::${sectionKind}::${normalizedName}`;
}

function mergeMonthlyCustomRowsFromAnnual(
  annualItems: AnnualCustomItem[],
  monthlyItems: MonthlyCustomItem[],
  year: number,
  month: number,
  sectionSettings: ManagedSection[],
  oneTimeAnnualRows: string[]
): { items: MonthlyCustomItem[]; added: number; updated: number } {
  const annualForYear = annualItems.filter((item) => item.year === year);
  if (annualForYear.length === 0) {
    return { items: monthlyItems, added: 0, updated: 0 };
  }

  const next = [...monthlyItems];
  const oneTimeSet = new Set(oneTimeAnnualRows);
  let added = 0;
  let updated = 0;

  for (const annualItem of annualForYear) {
    const resolvedAnnual = resolveCustomSection(
      {
        sectionId: annualItem.sectionId,
        sectionKind: annualItem.sectionKind
      },
      sectionSettings
    );
    const annualKey = rowMatchKey(annualItem.name, resolvedAnnual.sectionId, resolvedAnnual.kind);
    const annualPlanned = annualItem.months[month - 1] ?? 0;
    const oneTimeKey = annualCustomOneTimeRowKey(year, annualItem.id);
    const isOneTimeAnnual = oneTimeSet.has(oneTimeKey);

    const existingIndex = next.findIndex((monthlyItem) => {
      if (monthlyItem.year !== year || monthlyItem.month !== month) {
        return false;
      }

      if (monthlyItem.annualCustomItemId && monthlyItem.annualCustomItemId === annualItem.id) {
        return true;
      }

      const resolvedMonthly = resolveCustomSection(
        {
          sectionId: monthlyItem.sectionId,
          sectionKind: monthlyItem.sectionKind
        },
        sectionSettings
      );
      const monthlyKey = rowMatchKey(monthlyItem.name, resolvedMonthly.sectionId, resolvedMonthly.kind);
      return monthlyKey === annualKey;
    });

    if (isOneTimeAnnual && annualPlanned === 0) {
      if (existingIndex >= 0) {
        next.splice(existingIndex, 1);
        updated += 1;
      }
      continue;
    }

    if (existingIndex >= 0) {
      const existing = next[existingIndex];
      if (existing.plannedAmount !== annualPlanned || existing.annualCustomItemId !== annualItem.id) {
        next[existingIndex] = {
          ...existing,
          plannedAmount: annualPlanned,
          annualCustomItemId: annualItem.id
        };
        updated += 1;
      }
      continue;
    }

    const draft = createMonthlyCustomDraft(
      year,
      month,
      resolvedAnnual.sectionId || annualItem.sectionId,
      resolvedAnnual.kind
    );

    next.push({
      ...draft,
      name: annualItem.name,
      sectionId: resolvedAnnual.sectionId || annualItem.sectionId,
      sectionKind: resolvedAnnual.kind,
      plannedAmount: annualPlanned,
      annualCustomItemId: annualItem.id
    });
    added += 1;
  }

  return { items: next, added, updated };
}

export function MonthlyWorkspace() {
  const now = new Date();
  useCurrencySetting();
  const sectionSettings = useSectionSettings();
  const {
    year,
    setYear,
    month,
    setMonth,
    workspace,
    setWorkspace,
    loading,
    saving,
    message,
    loadWorkspace,
    saveAllActions
  } = useMonthlyWorkspaceState(now.getFullYear(), now.getMonth() + 1);
  const [annualCustomItems, setAnnualCustomItems] = useState<AnnualCustomItem[]>([]);
  const [monthlyCustomItems, setMonthlyCustomItems] = useState<MonthlyCustomItem[]>([]);
  const [nameOverrides, setNameOverrides] = useState<Record<string, string>>({});
  const [hiddenApiRows, setHiddenApiRows] = useState<string[]>([]);
  const [oneTimeAnnualRows, setOneTimeAnnualRows] = useState<string[]>([]);
  const [editingName, setEditingName] = useState<{ rowId: string; value: string } | null>(null);

  useEffect(() => {
    void loadWorkspace(year, month);
  }, [loadWorkspace, month, year]);

  useEffect(() => {
    function syncLocalRows() {
      setAnnualCustomItems(readAnnualCustomItems());
      setMonthlyCustomItems(readMonthlyCustomItems());
      setNameOverrides(readNameOverrides());
      setHiddenApiRows(readHiddenMonthlyApiRows());
      setOneTimeAnnualRows(readOneTimeAnnualRowKeys());
    }

    syncLocalRows();
    void refreshPlannerCustomizationFromApi();

    window.addEventListener(PLANNER_CUSTOM_EVENT, syncLocalRows);
    window.addEventListener("storage", syncLocalRows);

    return () => {
      window.removeEventListener(PLANNER_CUSTOM_EVENT, syncLocalRows);
      window.removeEventListener("storage", syncLocalRows);
    };
  }, []);

  useEffect(() => {
    const merged = mergeMonthlyCustomRowsFromAnnual(
      annualCustomItems,
      monthlyCustomItems,
      year,
      month,
      sectionSettings,
      oneTimeAnnualRows
    );

    if (merged.added === 0 && merged.updated === 0) {
      return;
    }

    const saved = saveMonthlyCustomItems(merged.items);
    setMonthlyCustomItems(saved);
  }, [annualCustomItems, month, monthlyCustomItems, oneTimeAnnualRows, sectionSettings, year]);

  const rowsWithMeta = useMemo(() => {
    const rows: MonthlyDisplayRow[] = [];
    const monthlyForCurrentPeriod = monthlyCustomItems.filter((item) => item.year === year && item.month === month);

    for (const row of workspace?.actions ?? []) {
      if (hiddenApiRows.includes(row.actionId)) {
        continue;
      }

      const displayName = nameOverrides[row.categoryId] ?? row.categoryName;
      const resolved = resolveManagedSection(row.section, displayName, sectionSettings);
      const apiOneTimeKey = annualApiOneTimeRowKey(year, row.categoryId);
      const isOneTimePersonal =
        resolved.kind === "PERSONAL_EXPENSES" &&
        oneTimeAnnualRows.includes(apiOneTimeKey);

      if (isOneTimePersonal && row.plannedAmount === 0) {
        continue;
      }

      rows.push({
        rowId: `api-${row.actionId}`,
        source: "api",
        actionId: row.actionId,
        categoryId: row.categoryId,
        name: displayName,
        section: row.section,
        plannedAmount: row.plannedAmount,
        actualAmount: row.actualAmount,
        status: row.status,
        resolvedSection: {
          id: resolved.id,
          name: resolved.name,
          kind: resolved.kind,
          order: resolved.order,
          sectionId: resolved.id
        }
      });
    }

    for (const item of monthlyForCurrentPeriod) {
      const resolved = resolveCustomSection(
        {
          sectionId: item.sectionId,
          sectionKind: item.sectionKind
        },
        sectionSettings
      );

      rows.push({
        rowId: `monthly-custom-${item.id}`,
        source: "monthlyCustom",
        monthlyCustomId: item.id,
        name: item.name,
        section: sectionFromKind(resolved.kind),
        plannedAmount: item.plannedAmount,
        actualAmount: item.actualAmount,
        status: item.status,
        resolvedSection: resolved
      });
    }

    return rows;
  }, [hiddenApiRows, month, monthlyCustomItems, nameOverrides, oneTimeAnnualRows, sectionSettings, workspace?.actions, year]);

  const groupedRows = useMemo(() => {
    return groupRowsBySection(rowsWithMeta, sectionSettings);
  }, [rowsWithMeta, sectionSettings]);

  function updateApiRow(actionId: string, change: Partial<MonthlyWorkspaceResponse["actions"][number]>) {
    setWorkspace((current) => {
      if (!current) {
        return current;
      }

      return {
        ...current,
        actions: current.actions.map((action) =>
          action.actionId === actionId
            ? {
                ...action,
                ...change
              }
            : action
        )
      };
    });
  }

  function updateMonthlyCustomRow(itemId: string, change: Partial<MonthlyCustomItem>) {
    const next = monthlyCustomItems.map((item) =>
      item.id === itemId
        ? {
            ...item,
            ...change
          }
        : item
    );

    setMonthlyCustomItems(saveMonthlyCustomItems(next));
  }

  function addMonthlyRow(sectionId: string, sectionKind: ManagedSectionKind) {
    const next = [...monthlyCustomItems, createMonthlyCustomDraft(year, month, sectionId, sectionKind)];
    setMonthlyCustomItems(saveMonthlyCustomItems(next));
  }

  function removeRow(row: MonthlyDisplayRow) {
    if (row.source === "api" && row.actionId) {
      const next = saveHiddenMonthlyApiRows([...hiddenApiRows, row.actionId]);
      setHiddenApiRows(next);
      return;
    }

    if (row.source === "monthlyCustom" && row.monthlyCustomId) {
      const next = monthlyCustomItems.filter((item) => item.id !== row.monthlyCustomId);
      setMonthlyCustomItems(saveMonthlyCustomItems(next));
      return;
    }
  }

  function commitNameEdit(row: MonthlyDisplayRow, value: string) {
    const nextName = value.trim() || "Unnamed Item";

    if (row.source === "api" && row.categoryId) {
      const next = saveNameOverrides({
        ...nameOverrides,
        [row.categoryId]: nextName
      });
      setNameOverrides(next);
      return;
    }

    if (row.source === "monthlyCustom" && row.monthlyCustomId) {
      const next = monthlyCustomItems.map((item) =>
        item.id === row.monthlyCustomId
          ? {
              ...item,
              name: nextName
            }
          : item
      );

      setMonthlyCustomItems(saveMonthlyCustomItems(next));
      return;
    }
  }

  async function onSaveAllActions() {
    if (!workspace) {
      return;
    }

    await saveAllActions(workspace.actions);
  }

  function shiftMonth(direction: -1 | 1) {
    const shifted = new Date(year, month - 1 + direction, 1);
    setYear(shifted.getFullYear());
    setMonth(shifted.getMonth() + 1);
  }

  const summary = useMemo(() => {
    let incomePlanned = 0;
    let incomeActual = 0;
    let businessCostsPlanned = 0;
    let businessCostsActual = 0;
    let personalCostsPlanned = 0;
    let personalCostsActual = 0;
    let savingsPlanned = 0;
    let savingsActual = 0;
    let investmentsPlanned = 0;
    let investmentsActual = 0;

    for (const row of rowsWithMeta) {
      const actual = row.actualAmount ?? 0;

      if (row.resolvedSection.kind === "INCOME") {
        incomePlanned += row.plannedAmount;
        incomeActual += actual;
      }

      if (row.resolvedSection.kind === "BUSINESS_EXPENSES") {
        businessCostsPlanned += row.plannedAmount;
        businessCostsActual += actual;
      }

      if (row.resolvedSection.kind === "PERSONAL_EXPENSES") {
        personalCostsPlanned += row.plannedAmount;
        personalCostsActual += actual;
      }

      if (row.resolvedSection.kind === "SAVINGS") {
        savingsPlanned += row.plannedAmount;
        savingsActual += actual;
      }

      if (row.resolvedSection.kind === "INVESTMENTS") {
        investmentsPlanned += row.plannedAmount;
        investmentsActual += actual;
      }
    }

    const savingsInvestPlanned = savingsPlanned + investmentsPlanned;
    const savingsInvestActual = savingsActual + investmentsActual;
    const transferPlanned = incomePlanned - businessCostsPlanned;
    const transferActual = incomeActual - businessCostsActual;
    const remainderPlanned = transferPlanned - personalCostsPlanned - savingsInvestPlanned;
    const remainderActual = transferActual - personalCostsActual - savingsInvestActual;

    return {
      incomePlanned,
      incomeActual,
      businessCostsPlanned,
      businessCostsActual,
      personalCostsPlanned,
      personalCostsActual,
      savingsInvestPlanned,
      savingsInvestActual,
      transferPlanned,
      transferActual,
      remainderPlanned,
      remainderActual
    };
  }, [rowsWithMeta]);

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <h1 className="ui-text-strong text-2xl font-semibold tracking-[-0.02em] md:text-3xl">Monthly Planning</h1>
          <p className="ui-text-muted text-base">Manage your monthly budget and track payments</p>
        </div>

        <div className="flex flex-wrap items-center gap-2 xl:justify-end">
          <div className="ui-btn-secondary ui-border inline-flex h-12 items-center rounded-2xl border p-1">
            <button
              type="button"
              className="ui-text ui-hover-soft grid h-10 w-10 place-items-center rounded-xl"
              onClick={() => shiftMonth(-1)}
            >
              <ChevronLeftIcon size={20} />
            </button>

            <select
              className="ui-control h-10 min-w-[170px] rounded-xl px-3 text-sm"
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
              className="ui-control ml-2 h-10 min-w-[110px] rounded-xl px-3 text-sm"
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
              className="ui-text ui-hover-soft ml-2 grid h-10 w-10 place-items-center rounded-xl"
              onClick={() => shiftMonth(1)}
            >
              <ChevronRightIcon size={20} />
            </button>
          </div>

          <button
            type="button"
            className="ui-btn-primary inline-flex h-12 items-center gap-2 rounded-2xl px-5 text-sm disabled:opacity-60 md:text-base"
            onClick={() => void onSaveAllActions()}
            disabled={!workspace || saving || loading}
          >
            <SaveIcon size={20} />
            {saving ? "Saving..." : "Save"}
          </button>
        </div>
      </header>

      {message ? (
        <p className="ui-text-muted text-sm md:text-base">
          {message.message}
          {message.details ? ` ${message.details}` : ""}
        </p>
      ) : null}
      {loading ? <p className="ui-text-muted text-sm md:text-base">Loading monthly plan...</p> : null}

      {workspace ? (
        <>
          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
            <MonthlyMetricCard label="INCOME" planned={summary.incomePlanned} actual={summary.incomeActual} valueTone="text-[#10a34a]" />
            <MonthlyMetricCard
              label="BUSINESS COSTS"
              planned={summary.businessCostsPlanned}
              actual={summary.businessCostsActual}
              valueTone="text-[#8f30ff]"
            />
            <MonthlyMetricCard
              label="PERSONAL COSTS"
              planned={summary.personalCostsPlanned}
              actual={summary.personalCostsActual}
              valueTone="text-[#f35b00]"
            />
            <MonthlyMetricCard
              label="SAVINGS / INVEST"
              planned={summary.savingsInvestPlanned}
              actual={summary.savingsInvestActual}
              valueTone="text-[#2563eb]"
            />
            <MonthlyMetricCard
              label="REMAINDER"
              planned={summary.remainderPlanned}
              actual={summary.remainderActual}
              valueTone="text-[#e11d48]"
              danger
            />
          </section>

          <MonthlyWorkspaceTablePanel>
          <section className="ui-border ui-surface overflow-hidden rounded-[22px] border">
            <div className="ui-border border-b p-5 md:p-6">
              <h2 className="ui-text-strong text-xl font-medium md:text-2xl">Budget Items for {MONTH_LABELS[month - 1]} {year}</h2>
              <p className="ui-text-muted mt-2 text-sm md:text-base">Manage budgeted amounts, track actual spending, and update payment status</p>
            </div>

            <div className="overflow-x-auto">
              <table className="w-max min-w-full border-collapse">
                <thead>
                  <tr className="bg-[#eceef2]">
                    <th className="border-b border-[#cdd2da] px-4 py-3 text-left text-sm font-semibold text-[#171b25] md:text-base">Section</th>
                    <th className="border-b border-[#cdd2da] px-4 py-3 text-left text-sm font-semibold text-[#171b25] md:text-base">Category</th>
                    <th className="border-b border-[#cdd2da] px-3 py-3 text-center text-sm font-semibold text-[#171b25] md:text-base">Budgeted</th>
                    <th className="border-b border-[#cdd2da] px-3 py-3 text-center text-sm font-semibold text-[#171b25] md:text-base">Actual</th>
                    <th className="border-b border-[#cdd2da] px-3 py-3 text-center text-sm font-semibold text-[#171b25] md:text-base">Difference</th>
                    <th className="border-b border-[#cdd2da] px-3 py-3 text-center text-sm font-semibold text-[#171b25] md:text-base">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {groupedRows.map((group) => (
                    <Fragment key={group.id}>
                      {group.rows.length === 0 ? (
                        <tr key={`${group.id}-empty`} className={sectionRowTone(group.kind)}>
                          <td className="border-b border-[#cad0d8] px-4 py-3 align-top text-sm text-[#1b1f2b] md:text-base">
                            {group.label}
                          </td>
                          <td className="border-b border-[#cad0d8] px-4 py-3" colSpan={5}>
                            <button
                              type="button"
                              onClick={() => addMonthlyRow(group.sectionId, group.kind)}
                              className="h-7 rounded-lg border border-[#c6cad2] bg-[#f8f9fb] px-3 text-xs text-[#30384b]"
                            >
                              + add item
                            </button>
                          </td>
                        </tr>
                      ) : (
                        <>
                          {group.rows.map((row, index) => {
                            const difference =
                              row.actualAmount === null ? null : row.actualAmount - row.plannedAmount;

                            return (
                              <tr key={row.rowId} className={sectionRowTone(group.kind)}>
                                {index === 0 ? (
                                  <td
                                    rowSpan={group.rows.length + 1}
                                    className="border-b border-[#cad0d8] px-4 py-3 align-top text-sm text-[#1b1f2b] md:text-base"
                                  >
                                    {group.label}
                                  </td>
                                ) : null}

                                <td className="border-b border-[#cad0d8] px-4 py-3 text-sm text-[#1b1f2b] md:text-base">
                                  <div className="flex items-center justify-between gap-2">
                                    {editingName?.rowId === row.rowId ? (
                                      <Input
                                        value={editingName.value}
                                        onChange={(event) =>
                                          setEditingName((current) =>
                                            current ? { ...current, value: event.target.value } : current
                                          )
                                        }
                                        autoFocus
                                        onBlur={() => {
                                          commitNameEdit(row, editingName.value);
                                          setEditingName(null);
                                        }}
                                        onKeyDown={(event) => {
                                          if (event.key === "Enter") {
                                            commitNameEdit(row, editingName.value);
                                            setEditingName(null);
                                          }

                                          if (event.key === "Escape") {
                                            setEditingName(null);
                                          }
                                        }}
                                        className="h-10 rounded-xl border-[#cfd3da] bg-[#f8f9fb]"
                                      />
                                    ) : (
                                      <button
                                        type="button"
                                        onClick={() => setEditingName({ rowId: row.rowId, value: row.name })}
                                        className="text-left underline decoration-dotted underline-offset-4"
                                      >
                                        {row.name}
                                      </button>
                                    )}

                                    <button
                                      type="button"
                                      onClick={() => removeRow(row)}
                                      aria-label={`Remove ${row.name}`}
                                      title="Remove row"
                                      className="grid h-7 w-7 place-items-center rounded-lg border border-[#f2a2b5] bg-[#fff1f4] text-[#be123c]"
                                    >
                                      <TrashIcon size={14} />
                                    </button>
                                  </div>
                                </td>

                                <td className="border-b border-[#cad0d8] px-3 py-3">
                                  {row.source === "monthlyCustom" && row.monthlyCustomId ? (
                                    <Input
                                      type="number"
                                      step="0.01"
                                      className="numeric-input h-10 min-w-[96px] rounded-xl border-0 bg-[#eff1f4] text-center text-sm font-medium text-[#1f2430] shadow-none md:text-base"
                                      value={row.plannedAmount}
                                      onChange={(event) => {
                                        const parsed = Number.parseFloat(event.target.value);
                                        updateMonthlyCustomRow(row.monthlyCustomId!, {
                                          plannedAmount: Number.isFinite(parsed) ? parsed : 0
                                        });
                                      }}
                                    />
                                  ) : (
                                    <div className="grid h-10 min-w-[96px] place-items-center rounded-xl bg-[#eff1f4] text-sm font-medium text-[#1f2430] md:text-base">
                                      {row.plannedAmount}
                                    </div>
                                  )}
                                </td>

                                <td className="border-b border-[#cad0d8] px-3 py-3">
                                  <Input
                                    type="number"
                                    step="0.01"
                                    className="numeric-input h-10 min-w-[96px] rounded-xl border-0 bg-[#eff1f4] text-center text-sm font-medium text-[#1f2430] shadow-none md:text-base"
                                    value={row.actualAmount ?? ""}
                                    onChange={(event) => {
                                      const parsed = Number.parseFloat(event.target.value);
                                      const nextActual = Number.isFinite(parsed) ? parsed : null;

                                      if (row.source === "api" && row.actionId) {
                                        updateApiRow(row.actionId, { actualAmount: nextActual });
                                        return;
                                      }

                                      if (row.source === "monthlyCustom" && row.monthlyCustomId) {
                                        updateMonthlyCustomRow(row.monthlyCustomId, { actualAmount: nextActual });
                                      }
                                    }}
                                  />
                                </td>

                                <td
                                  className={`border-b border-[#cad0d8] px-3 py-3 text-center text-sm md:text-base ${
                                    difference === null ? "text-[#6b7280]" : differenceTone(row.section, difference)
                                  }`}
                                >
                                  {difference === null ? "-" : difference === 0 ? "-" : asSignedCurrency(difference)}
                                </td>

                                <td className="border-b border-[#cad0d8] px-3 py-3">
                                  <select
                                    className="h-10 min-w-[140px] rounded-xl border-0 bg-[#eff1f4] px-3 text-sm text-[#1f2430] md:text-base"
                                    value={row.status}
                                    onChange={(event) => {
                                      const nextStatus = event.target.value as ActionStatus;

                                      if (row.source === "api" && row.actionId) {
                                        updateApiRow(row.actionId, {
                                          status: nextStatus
                                        });
                                        return;
                                      }

                                      if (row.source === "monthlyCustom" && row.monthlyCustomId) {
                                        updateMonthlyCustomRow(row.monthlyCustomId, {
                                          status: nextStatus
                                        });
                                      }
                                    }}
                                  >
                                    {Object.entries(STATUS_LABELS).map(([status, label]) => (
                                      <option key={`${row.rowId}-${status}`} value={status}>
                                        {label}
                                      </option>
                                    ))}
                                  </select>
                                </td>
                              </tr>
                            );
                          })}

                          <tr key={`${group.id}-add`} className={sectionRowTone(group.kind)}>
                            <td className="border-b border-[#cad0d8] px-4 py-3" colSpan={5}>
                              <button
                                type="button"
                                onClick={() => addMonthlyRow(group.sectionId, group.kind)}
                                className="h-7 rounded-lg border border-[#c6cad2] bg-[#f8f9fb] px-3 text-xs text-[#30384b]"
                              >
                                + add item
                              </button>
                            </td>
                          </tr>
                        </>
                      )}
                    </Fragment>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
          </MonthlyWorkspaceTablePanel>

          <MonthlyWorkspaceSummaryPanel>
          <section className="ui-border ui-surface rounded-[22px] border p-5 md:p-6">
            <h2 className="ui-text-strong text-xl font-medium md:text-2xl">Monthly Summary - {MONTH_LABELS[month - 1]} {year}</h2>

            <div className="mt-6 grid gap-8 xl:grid-cols-2">
              <MonthlySummaryColumn
                label="BUDGETED"
                income={summary.incomePlanned}
                business={summary.businessCostsPlanned}
                personal={summary.personalCostsPlanned}
                savings={summary.savingsInvestPlanned}
                remainder={summary.remainderPlanned}
              />
              <MonthlySummaryColumn
                label="ACTUAL"
                income={summary.incomeActual}
                business={summary.businessCostsActual}
                personal={summary.personalCostsActual}
                savings={summary.savingsInvestActual}
                remainder={summary.remainderActual}
              />
            </div>
          </section>
          </MonthlyWorkspaceSummaryPanel>
        </>
      ) : null}
    </div>
  );
}
