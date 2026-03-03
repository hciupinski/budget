"use client";

import { Fragment, useCallback, useEffect, useMemo, useState } from "react";
import { Input } from "@/components/ui/input";
import { MONTH_LABELS, type MonthlyWorkspaceResponse } from "@/lib/budget-types";
import { ChevronLeftIcon, ChevronRightIcon, CopyIcon, SaveIcon, TrashIcon } from "@/components/budget/icons";
import {
  asCurrency,
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
import {
  createMonthlyCustomDraft,
  PLANNER_CUSTOM_EVENT,
  refreshPlannerCustomizationFromApi,
  readAnnualCustomItems,
  readHiddenMonthlyApiRows,
  readMonthlyCustomItems,
  readNameOverrides,
  saveAnnualCustomItems,
  saveHiddenMonthlyApiRows,
  saveMonthlyCustomItems,
  saveNameOverrides,
  type ActionStatus,
  type AnnualCustomItem,
  type MonthlyCustomItem
} from "@/lib/planner-custom-items";

type MonthlyDisplayRow = {
  rowId: string;
  source: "api" | "annualCustom" | "monthlyCustom";
  actionId?: string;
  categoryId?: string;
  annualCustomId?: string;
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

function fallbackLabelFromKind(kind: ManagedSectionKind): string {
  if (kind === "INCOME") {
    return "Income";
  }

  if (kind === "BUSINESS_EXPENSES") {
    return "Business Expenses";
  }

  if (kind === "PERSONAL_EXPENSES") {
    return "Personal Expenses";
  }

  if (kind === "SAVINGS") {
    return "Savings";
  }

  return "Investments";
}

function resolveCustomSection(
  input: { sectionId: string; sectionKind: ManagedSectionKind },
  sections: ManagedSection[]
) {
  const byId = sections.find((section) => section.id === input.sectionId);
  if (byId) {
    return {
      id: byId.id,
      name: byId.name,
      kind: byId.kind,
      order: byId.order,
      sectionId: byId.id
    };
  }

  const byKind = sections.find((section) => section.kind === input.sectionKind);
  if (byKind) {
    return {
      id: byKind.id,
      name: byKind.name,
      kind: byKind.kind,
      order: byKind.order,
      sectionId: byKind.id
    };
  }

  return {
    id: `fallback-${input.sectionKind}`,
    name: fallbackLabelFromKind(input.sectionKind),
    kind: input.sectionKind,
    order: 999,
    sectionId: ""
  };
}

function sectionFromKind(kind: ManagedSectionKind): "INCOME" | "COSTS" | "SAVINGS_INVESTMENTS" {
  if (kind === "INCOME") {
    return "INCOME";
  }

  if (kind === "BUSINESS_EXPENSES" || kind === "PERSONAL_EXPENSES") {
    return "COSTS";
  }

  return "SAVINGS_INVESTMENTS";
}

export function MonthlyWorkspace() {
  const now = new Date();
  const sectionSettings = useSectionSettings();
  const [year, setYear] = useState<number>(now.getFullYear());
  const [month, setMonth] = useState<number>(now.getMonth() + 1);
  const [workspace, setWorkspace] = useState<MonthlyWorkspaceResponse | null>(null);
  const [annualCustomItems, setAnnualCustomItems] = useState<AnnualCustomItem[]>([]);
  const [monthlyCustomItems, setMonthlyCustomItems] = useState<MonthlyCustomItem[]>([]);
  const [nameOverrides, setNameOverrides] = useState<Record<string, string>>({});
  const [hiddenApiRows, setHiddenApiRows] = useState<string[]>([]);
  const [editingName, setEditingName] = useState<{ rowId: string; value: string } | null>(null);
  const [notes, setNotes] = useState<Record<string, string>>({});
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

  useEffect(() => {
    function syncLocalRows() {
      setAnnualCustomItems(readAnnualCustomItems());
      setMonthlyCustomItems(readMonthlyCustomItems());
      setNameOverrides(readNameOverrides());
      setHiddenApiRows(readHiddenMonthlyApiRows());
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

  const rowsWithMeta = useMemo(() => {
    const rows: MonthlyDisplayRow[] = [];

    for (const row of workspace?.actions ?? []) {
      if (hiddenApiRows.includes(row.actionId)) {
        continue;
      }

      const displayName = nameOverrides[row.categoryId] ?? row.categoryName;
      const resolved = resolveManagedSection(row.section, displayName, sectionSettings);

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

    for (const item of annualCustomItems.filter((item) => item.year === year)) {
      const resolved = resolveCustomSection(item, sectionSettings);

      rows.push({
        rowId: `annual-custom-${item.id}`,
        source: "annualCustom",
        annualCustomId: item.id,
        name: item.name,
        section: sectionFromKind(resolved.kind),
        plannedAmount: item.months[month - 1] ?? 0,
        actualAmount: null,
        status: "PLANNED",
        resolvedSection: resolved
      });
    }

    for (const item of monthlyCustomItems.filter((item) => item.year === year && item.month === month)) {
      const resolved = resolveCustomSection(item, sectionSettings);

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
  }, [annualCustomItems, hiddenApiRows, month, monthlyCustomItems, nameOverrides, sectionSettings, workspace?.actions, year]);

  const groupedRows = useMemo(() => {
    const groupedMap = new Map<
      string,
      {
        id: string;
        label: string;
        kind: ManagedSectionKind;
        order: number;
        sectionId: string;
        rows: MonthlyDisplayRow[];
      }
    >();

    for (const section of [...sectionSettings].sort((a, b) => a.order - b.order)) {
      groupedMap.set(section.id, {
        id: section.id,
        label: section.name,
        kind: section.kind,
        order: section.order,
        sectionId: section.id,
        rows: []
      });
    }

    for (const row of rowsWithMeta) {
      const existing = groupedMap.get(row.resolvedSection.id);

      if (existing) {
        existing.rows.push(row);
        continue;
      }

      groupedMap.set(row.resolvedSection.id, {
        id: row.resolvedSection.id,
        label: row.resolvedSection.name,
        kind: row.resolvedSection.kind,
        order: row.resolvedSection.order,
        sectionId: row.resolvedSection.sectionId,
        rows: [row]
      });
    }

    return Array.from(groupedMap.values()).sort((a, b) => a.order - b.order);
  }, [rowsWithMeta, sectionSettings]);

  function updateApiRow(actionId: string, change: Partial<MonthlyWorkspaceResponse["actions"][number]>) {
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

    if (row.source === "annualCustom" && row.annualCustomId) {
      const next = annualCustomItems.filter((item) => item.id !== row.annualCustomId);
      setAnnualCustomItems(saveAnnualCustomItems(next));
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

    if (row.source === "annualCustom" && row.annualCustomId) {
      const next = annualCustomItems.map((item) =>
        item.id === row.annualCustomId
          ? {
              ...item,
              name: nextName
            }
          : item
      );

      setAnnualCustomItems(saveAnnualCustomItems(next));
    }
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
          <h1 className="text-2xl font-semibold tracking-[-0.02em] text-[#0f1321] md:text-3xl">Monthly Planning</h1>
          <p className="text-base text-[#71768b]">Manage your monthly budget and track payments</p>
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
              className="h-10 min-w-[170px] rounded-xl bg-[#e8eaee] px-3 text-sm text-[#1c202c]"
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
              className="ml-2 h-10 min-w-[110px] rounded-xl bg-[#e8eaee] px-3 text-sm text-[#1c202c]"
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
            className="inline-flex h-12 items-center gap-2 rounded-2xl border border-[#d1d5dd] bg-[#f3f4f6] px-4 text-sm text-[#171b27] hover:bg-[#e9ebf0] md:text-base"
            onClick={() => void generateFromAnnualPlan()}
          >
            <CopyIcon size={20} />
            Copy from Annual
          </button>

          <button
            type="button"
            className="inline-flex h-12 items-center gap-2 rounded-2xl bg-[#040426] px-5 text-sm text-white hover:opacity-95 disabled:opacity-60 md:text-base"
            onClick={() => void saveAllActions()}
            disabled={!workspace || saving || loading}
          >
            <SaveIcon size={20} />
            {saving ? "Saving..." : "Save"}
          </button>
        </div>
      </header>

      {message ? <p className="text-sm text-[#686e84] md:text-base">{message}</p> : null}
      {loading ? <p className="text-sm text-[#686e84] md:text-base">Loading monthly plan...</p> : null}

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

          <section className="overflow-hidden rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9]">
            <div className="border-b border-[#d5d9e0] p-5 md:p-6">
              <h2 className="text-xl font-medium text-[#171a24] md:text-2xl">Budget Items for {MONTH_LABELS[month - 1]} {year}</h2>
              <p className="mt-2 text-sm text-[#73788d] md:text-base">Manage budgeted amounts, track actual spending, and update payment status</p>
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
                    <th className="border-b border-[#cdd2da] px-3 py-3 text-left text-sm font-semibold text-[#171b25] md:text-base">Notes</th>
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
                          <td className="border-b border-[#cad0d8] px-4 py-3" colSpan={6}>
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
                                  {row.source === "annualCustom" ? (
                                    <div className="grid h-10 min-w-[96px] place-items-center rounded-xl bg-[#eff1f4] text-sm text-[#76809a] md:text-base">
                                      -
                                    </div>
                                  ) : (
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
                                  )}
                                </td>

                                <td
                                  className={`border-b border-[#cad0d8] px-3 py-3 text-center text-sm md:text-base ${
                                    difference === null ? "text-[#6b7280]" : differenceTone(row.section, difference)
                                  }`}
                                >
                                  {difference === null ? "-" : difference === 0 ? "-" : asSignedCurrency(difference)}
                                </td>

                                <td className="border-b border-[#cad0d8] px-3 py-3">
                                  {row.source === "annualCustom" ? (
                                    <div className="grid h-10 min-w-[140px] place-items-center rounded-xl bg-[#eff1f4] text-sm text-[#76809a] md:text-base">
                                      From annual
                                    </div>
                                  ) : (
                                    <select
                                      className="h-10 min-w-[140px] rounded-xl border-0 bg-[#eff1f4] px-3 text-sm text-[#1f2430] md:text-base"
                                      value={row.status}
                                      onChange={(event) => {
                                        if (row.source === "api" && row.actionId) {
                                          updateApiRow(row.actionId, {
                                            status: event.target.value as ActionStatus
                                          });
                                          return;
                                        }

                                        if (row.source === "monthlyCustom" && row.monthlyCustomId) {
                                          updateMonthlyCustomRow(row.monthlyCustomId, {
                                            status: event.target.value as ActionStatus
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
                                  )}
                                </td>

                                <td className="border-b border-[#cad0d8] px-3 py-3">
                                  <Input
                                    value={notes[row.rowId] ?? ""}
                                    onChange={(event) =>
                                      setNotes((current) => ({
                                        ...current,
                                        [row.rowId]: event.target.value
                                      }))
                                    }
                                    placeholder="Add notes..."
                                    className="h-10 min-w-[160px] rounded-xl border-0 bg-[#eff1f4] text-sm text-[#6d7287] shadow-none md:text-base"
                                  />
                                </td>
                              </tr>
                            );
                          })}

                          <tr key={`${group.id}-add`} className={sectionRowTone(group.kind)}>
                            <td className="border-b border-[#cad0d8] px-4 py-3" colSpan={6}>
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

          <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-5 md:p-6">
            <h2 className="text-xl font-medium text-[#171a24] md:text-2xl">Monthly Summary - {MONTH_LABELS[month - 1]} {year}</h2>

            <div className="mt-6 grid gap-8 xl:grid-cols-2">
              <SummaryColumn
                label="BUDGETED"
                income={summary.incomePlanned}
                business={summary.businessCostsPlanned}
                personal={summary.personalCostsPlanned}
                savings={summary.savingsInvestPlanned}
                remainder={summary.remainderPlanned}
              />
              <SummaryColumn
                label="ACTUAL"
                income={summary.incomeActual}
                business={summary.businessCostsActual}
                personal={summary.personalCostsActual}
                savings={summary.savingsInvestActual}
                remainder={summary.remainderActual}
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
    <div className={`rounded-[20px] border bg-[#f6f7f9] p-4 ${danger ? "border-[#f43f5e]" : "border-[#cfd3da]"}`}>
      <p className="text-sm tracking-wide text-[#72778b] md:text-base">{label}</p>
      <p className={`mt-1 text-2xl font-medium md:text-3xl ${valueTone}`}>{asCurrency(planned)}</p>
      <p className="mt-1 text-sm text-[#72778b] md:text-base">
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
      <p className="text-sm tracking-wide text-[#72778b] md:text-base">{label}</p>

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
    <div className="flex items-center justify-between gap-4 text-sm md:text-base">
      <p className="text-[#6f7489]">{label}</p>
      <p className={valueTone}>{asSignedCurrency(value)}</p>
    </div>
  );
}
