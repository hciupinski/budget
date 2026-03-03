"use client";

import { Fragment, useCallback, useEffect, useMemo, useState } from "react";
import { Input } from "@/components/ui/input";
import { MONTH_LABELS, type AnnualPlanResponse } from "@/lib/budget-types";
import { CopyIcon, RefreshIcon, SaveIcon, TrashIcon } from "@/components/budget/icons";
import {
  asCurrency,
  asSignedCurrency,
  sectionRowTone
} from "@/components/budget/budget-ui-utils";
import {
  resolveManagedSection,
  useSectionSettings,
  type ManagedSection,
  type ManagedSectionKind
} from "@/lib/section-settings";
import {
  createAnnualCustomDraft,
  PLANNER_CUSTOM_EVENT,
  readAnnualCustomItems,
  readHiddenAnnualApiRows,
  readNameOverrides,
  saveAnnualCustomItems,
  saveHiddenAnnualApiRows,
  saveNameOverrides,
  type AnnualCustomItem
} from "@/lib/planner-custom-items";

type AnnualDisplayRow = {
  rowId: string;
  source: "api" | "custom";
  categoryId?: string;
  customId?: string;
  name: string;
  months: number[];
  resolvedSection: {
    id: string;
    name: string;
    kind: ManagedSectionKind;
    order: number;
    sectionId: string;
  };
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

function resolveCustomSection(item: AnnualCustomItem, sections: ManagedSection[]) {
  const byId = sections.find((section) => section.id === item.sectionId);
  if (byId) {
    return {
      id: byId.id,
      name: byId.name,
      kind: byId.kind,
      order: byId.order,
      sectionId: byId.id
    };
  }

  const byKind = sections.find((section) => section.kind === item.sectionKind);
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
    id: `fallback-${item.sectionKind}`,
    name: fallbackLabelFromKind(item.sectionKind),
    kind: item.sectionKind,
    order: 999,
    sectionId: ""
  };
}

export function AnnualPlanner() {
  const now = new Date();
  const sectionSettings = useSectionSettings();
  const [year, setYear] = useState<number>(now.getFullYear());
  const [annualPlan, setAnnualPlan] = useState<AnnualPlanResponse | null>(null);
  const [annualCustomItems, setAnnualCustomItems] = useState<AnnualCustomItem[]>([]);
  const [nameOverrides, setNameOverrides] = useState<Record<string, string>>({});
  const [hiddenApiRows, setHiddenApiRows] = useState<string[]>([]);
  const [editingName, setEditingName] = useState<{ rowId: string; value: string } | null>(null);
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

  useEffect(() => {
    function syncLocalRows() {
      setAnnualCustomItems(readAnnualCustomItems());
      setNameOverrides(readNameOverrides());
      setHiddenApiRows(readHiddenAnnualApiRows());
    }

    syncLocalRows();

    window.addEventListener(PLANNER_CUSTOM_EVENT, syncLocalRows);
    window.addEventListener("storage", syncLocalRows);

    return () => {
      window.removeEventListener(PLANNER_CUSTOM_EVENT, syncLocalRows);
      window.removeEventListener("storage", syncLocalRows);
    };
  }, []);

  const rowsWithMeta = useMemo(() => {
    const rows: AnnualDisplayRow[] = [];

    for (const row of annualPlan?.categories ?? []) {
      if (hiddenApiRows.includes(row.categoryId)) {
        continue;
      }

      const displayName = nameOverrides[row.categoryId] ?? row.categoryName;
      const resolved = resolveManagedSection(row.section, displayName, sectionSettings);

      rows.push({
        rowId: `api-${row.categoryId}`,
        source: "api",
        categoryId: row.categoryId,
        name: displayName,
        months: row.months,
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
      rows.push({
        rowId: `custom-${item.id}`,
        source: "custom",
        customId: item.id,
        name: item.name,
        months: item.months,
        resolvedSection: resolveCustomSection(item, sectionSettings)
      });
    }

    return rows;
  }, [annualCustomItems, annualPlan?.categories, hiddenApiRows, nameOverrides, sectionSettings, year]);

  const groupedRows = useMemo(() => {
    const groupedMap = new Map<
      string,
      {
        id: string;
        label: string;
        kind: ManagedSectionKind;
        order: number;
        sectionId: string;
        rows: AnnualDisplayRow[];
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

  function updateApiCell(categoryId: string, monthIndex: number, value: string) {
    if (!annualPlan) {
      return;
    }

    const parsed = Number.parseFloat(value);
    const nextValue = Number.isFinite(parsed) ? parsed : 0;

    const nextRows = annualPlan.categories.map((row) => {
      if (row.categoryId !== categoryId) {
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

  function updateCustomCell(customId: string, monthIndex: number, value: string) {
    const parsed = Number.parseFloat(value);
    const nextValue = Number.isFinite(parsed) ? parsed : 0;

    const next = annualCustomItems.map((item) => {
      if (item.id !== customId) {
        return item;
      }

      const months = item.months.map((monthValue, currentMonth) =>
        currentMonth === monthIndex ? nextValue : monthValue
      );

      return {
        ...item,
        months
      };
    });

    setAnnualCustomItems(saveAnnualCustomItems(next));
  }

  function addRow(sectionId: string, sectionKind: ManagedSectionKind) {
    const next = [...annualCustomItems, createAnnualCustomDraft(year, sectionId, sectionKind)];
    setAnnualCustomItems(saveAnnualCustomItems(next));
  }

  function removeRow(row: AnnualDisplayRow) {
    if (row.source === "api" && row.categoryId) {
      const next = saveHiddenAnnualApiRows([...hiddenApiRows, row.categoryId]);
      setHiddenApiRows(next);
      return;
    }

    if (row.source === "custom" && row.customId) {
      const next = annualCustomItems.filter((item) => item.id !== row.customId);
      setAnnualCustomItems(saveAnnualCustomItems(next));
    }
  }

  function commitNameEdit(row: AnnualDisplayRow, value: string) {
    const nextName = value.trim() || "Unnamed Item";

    if (row.source === "api" && row.categoryId) {
      const next = saveNameOverrides({
        ...nameOverrides,
        [row.categoryId]: nextName
      });
      setNameOverrides(next);
      return;
    }

    if (row.source === "custom" && row.customId) {
      const next = annualCustomItems.map((item) =>
        item.id === row.customId
          ? {
              ...item,
              name: nextName
            }
          : item
      );

      setAnnualCustomItems(saveAnnualCustomItems(next));
    }
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
    setMessage("Annual plan saved. Custom rows are stored locally in UI settings.");
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

  const summary = useMemo(() => {
    let income = 0;
    let businessCosts = 0;
    let personalCosts = 0;
    let savings = 0;
    let investments = 0;

    for (const row of rowsWithMeta) {
      const total = row.months.reduce((sum, monthValue) => sum + monthValue, 0);

      if (row.resolvedSection.kind === "INCOME") {
        income += total;
      }

      if (row.resolvedSection.kind === "BUSINESS_EXPENSES") {
        businessCosts += total;
      }

      if (row.resolvedSection.kind === "PERSONAL_EXPENSES") {
        personalCosts += total;
      }

      if (row.resolvedSection.kind === "SAVINGS") {
        savings += total;
      }

      if (row.resolvedSection.kind === "INVESTMENTS") {
        investments += total;
      }
    }

    const savingsAndInvestments = savings + investments;
    const transferToPersonal = income - businessCosts;
    const remainder = transferToPersonal - personalCosts - savingsAndInvestments;

    return {
      income,
      businessCosts,
      personalCosts,
      savings,
      investments,
      savingsAndInvestments,
      transferToPersonal,
      remainder
    };
  }, [rowsWithMeta]);

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-[-0.02em] text-[#0f1321] md:text-3xl">Annual Budget Planning</h1>
          <p className="text-base text-[#71768b]">Plan your budget across all months</p>
        </div>

        <div className="flex flex-wrap items-center gap-2 xl:justify-end">
          <select
            className="h-12 min-w-[132px] rounded-2xl border border-[#d1d5dd] bg-[#e9eaed] px-4 text-sm text-[#202532]"
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
            className="inline-flex h-12 items-center gap-2 rounded-2xl border border-[#d1d5dd] bg-[#f3f4f6] px-4 text-sm text-[#171b27] hover:bg-[#e9ebf0] md:text-base"
            onClick={() => void loadAnnualPlan(year)}
          >
            <RefreshIcon size={20} />
            Reload
          </button>

          <button
            type="button"
            className="inline-flex h-12 items-center gap-2 rounded-2xl border border-[#d1d5dd] bg-[#f3f4f6] px-4 text-sm text-[#171b27] hover:bg-[#e9ebf0] md:text-base"
            onClick={() => void copyFromPreviousYear()}
            disabled={saving}
          >
            <CopyIcon size={20} />
            Copy {year - 1}
          </button>

          <button
            type="button"
            className="inline-flex h-12 items-center gap-2 rounded-2xl bg-[#040426] px-5 text-sm text-white hover:opacity-95 disabled:opacity-60 md:text-base"
            onClick={() => void saveAnnualPlan()}
            disabled={saving || loading || !annualPlan}
          >
            <SaveIcon size={20} />
            {saving ? "Saving..." : "Save Annual Plan"}
          </button>
        </div>
      </header>

      {message ? <p className="text-sm text-[#686e84] md:text-base">{message}</p> : null}
      {loading ? <p className="text-sm text-[#686e84] md:text-base">Loading annual plan...</p> : null}

      {annualPlan ? (
        <>
          <section className="overflow-hidden rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9]">
            <div className="max-w-full overflow-x-auto">
              <table className="w-max min-w-full border-collapse">
                <thead>
                  <tr className="bg-[#eceef2]">
                    <th className="sticky left-0 z-40 min-w-[170px] border-b border-[#cdd2da] bg-[#eceef2] px-4 py-3 text-left text-sm font-semibold text-[#171b25] md:text-base">
                      Section
                    </th>
                    <th className="sticky left-[170px] z-40 min-w-[250px] border-b border-[#cdd2da] bg-[#eceef2] px-4 py-3 text-left text-sm font-semibold text-[#171b25] md:text-base">
                      Item
                    </th>
                    <th className="sticky left-[420px] z-40 w-[74px] min-w-[74px] border-b border-[#cdd2da] bg-[#eceef2] px-2 py-3 text-center text-sm font-semibold text-[#171b25] md:text-base">
                      Action
                    </th>
                    {MONTH_LABELS.map((label) => (
                      <th
                        key={label}
                        className="w-[108px] min-w-[108px] border-b border-[#cdd2da] px-2 py-3 text-center text-sm font-semibold text-[#171b25] md:text-base"
                      >
                        {label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {groupedRows.map((group) => (
                    <Fragment key={group.id}>
                      {group.rows.length === 0 ? (
                        <tr key={`${group.id}-empty`} className={sectionRowTone(group.kind)}>
                          <td className="sticky left-0 z-30 min-w-[170px] border-b border-[#cad0d8] bg-inherit px-4 py-3 align-top text-sm text-[#1b1f2b] md:text-base">
                            {group.label}
                          </td>
                          <td className="sticky left-[170px] z-20 min-w-[250px] border-b border-[#cad0d8] bg-inherit px-4 py-3">
                            <button
                              type="button"
                              onClick={() => addRow(group.sectionId, group.kind)}
                              className="h-7 rounded-lg border border-[#c6cad2] bg-[#f8f9fb] px-3 text-xs text-[#30384b]"
                            >
                              + add item
                            </button>
                          </td>
                          <td className="sticky left-[420px] z-20 w-[74px] min-w-[74px] border-b border-[#cad0d8] bg-inherit px-2 py-3" />
                          <td className="border-b border-[#cad0d8] px-2 py-3" colSpan={MONTH_LABELS.length} />
                        </tr>
                      ) : (
                        <>
                          {group.rows.map((row, index) => (
                            <tr key={row.rowId} className={sectionRowTone(group.kind)}>
                              {index === 0 ? (
                                <td
                                  rowSpan={group.rows.length + 1}
                                  className="sticky left-0 z-30 min-w-[170px] border-b border-[#cad0d8] bg-inherit px-4 py-3 align-top text-sm text-[#1b1f2b] md:text-base"
                                >
                                  {group.label}
                                </td>
                              ) : null}

                              <td className="sticky left-[170px] z-20 min-w-[250px] border-b border-[#cad0d8] bg-inherit px-4 py-3 text-sm text-[#1b1f2b] md:text-base">
                                <div className="flex items-center gap-2">
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
                                </div>
                              </td>

                              <td className="sticky left-[420px] z-20 w-[74px] min-w-[74px] border-b border-[#cad0d8] bg-inherit px-2 py-3 text-center">
                                <button
                                  type="button"
                                  onClick={() => removeRow(row)}
                                  aria-label={`Remove ${row.name}`}
                                  title="Remove row"
                                  className="mx-auto grid h-7 w-7 place-items-center rounded-lg border border-[#f2a2b5] bg-[#fff1f4] text-[#be123c]"
                                >
                                  <TrashIcon size={14} />
                                </button>
                              </td>

                              {row.months.map((monthValue, monthIndex) => (
                                <td key={`${row.rowId}-${monthIndex}`} className="border-b border-[#cad0d8] px-2 py-3">
                                  <Input
                                    type="number"
                                    step="0.01"
                                    className="numeric-input h-10 min-w-[96px] rounded-xl border-0 bg-[#eff1f4] text-center text-sm font-medium text-[#1f2430] shadow-none md:text-base"
                                    value={monthValue}
                                    onChange={(event) => {
                                      if (row.source === "api" && row.categoryId) {
                                        updateApiCell(row.categoryId, monthIndex, event.target.value);
                                        return;
                                      }

                                      if (row.source === "custom" && row.customId) {
                                        updateCustomCell(row.customId, monthIndex, event.target.value);
                                      }
                                    }}
                                  />
                                </td>
                              ))}
                            </tr>
                          ))}

                          <tr key={`${group.id}-add`} className={sectionRowTone(group.kind)}>
                            <td className="sticky left-[170px] z-20 min-w-[250px] border-b border-[#cad0d8] bg-inherit px-4 py-3">
                              <button
                                type="button"
                                onClick={() => addRow(group.sectionId, group.kind)}
                                className="h-7 rounded-lg border border-[#c6cad2] bg-[#f8f9fb] px-3 text-xs text-[#30384b]"
                              >
                                + add item
                              </button>
                            </td>
                            <td className="sticky left-[420px] z-20 w-[74px] min-w-[74px] border-b border-[#cad0d8] bg-inherit px-2 py-3" />
                            <td className="border-b border-[#cad0d8] px-2 py-3" colSpan={MONTH_LABELS.length} />
                          </tr>
                        </>
                      )}
                    </Fragment>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
            <MetricCard label="INCOME" value={summary.income} valueTone="text-[#10a34a]" />
            <MetricCard label="BUSINESS COSTS" value={summary.businessCosts} valueTone="text-[#8f30ff]" />
            <MetricCard label="PERSONAL COSTS" value={summary.personalCosts} valueTone="text-[#f35b00]" />
            <MetricCard label="SAVINGS / INVEST" value={summary.savingsAndInvestments} valueTone="text-[#2563eb]" />
            <MetricCard label="REMAINDER" value={summary.remainder} valueTone="text-[#e11d48]" danger />
          </section>

          <section className="rounded-[22px] border border-[#cfd3da] bg-[#f6f7f9] p-5 md:p-6">
            <h2 className="text-xl font-medium text-[#171a24] md:text-2xl">Annual Money Flow Summary</h2>

            <div className="mt-6 space-y-4 text-sm md:text-base">
              <SummaryLine label="Total Business Income" value={summary.income} valueTone="text-[#10a34a]" />
              <SummaryLine label="- Business Expenses" value={-summary.businessCosts} valueTone="text-[#8f30ff]" />

              <div className="border-t border-[#d7dbe2]" />

              <SummaryLine label="Transfer to Personal" value={summary.transferToPersonal} valueTone="text-[#10a34a]" />
              <SummaryLine label="- Personal Expenses" value={-summary.personalCosts} valueTone="text-[#f35b00]" />
              <SummaryLine
                label="- Savings"
                value={-summary.savingsAndInvestments}
                valueTone="text-[#2563eb]"
              />

              <div className="border-t border-[#d7dbe2]" />

              <SummaryLine label="Remainder (Unallocated)" value={summary.remainder} valueTone="text-[#e11d48]" />
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
    <div className={`rounded-[20px] border bg-[#f6f7f9] p-4 ${danger ? "border-[#f43f5e]" : "border-[#cfd3da]"}`}>
      <p className="text-sm tracking-wide text-[#72778b] md:text-base">{label}</p>
      <p className={`mt-1 text-2xl font-medium md:text-3xl ${valueTone}`}>{asCurrency(value)}</p>
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
