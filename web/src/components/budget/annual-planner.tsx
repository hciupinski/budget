"use client";

import { Fragment, useEffect, useMemo, useState } from "react";
import { Input } from "@/components/ui/input";
import { MONTH_LABELS } from "@/lib/budget-types";
import { CalendarIcon, CopyIcon, RefreshIcon, SaveIcon, TrashIcon } from "@/components/budget/icons";
import {
  asSignedCurrency,
  sectionRowTone
} from "@/components/budget/budget-ui-utils";
import {
  resolveManagedSection,
  useSectionSettings,
  type ManagedSectionKind
} from "@/lib/section-settings";
import { useCurrencySetting } from "@/lib/currency-settings";
import {
  annualApiOneTimeRowKey,
  annualCustomOneTimeRowKey,
  createAnnualCustomDraft,
  PLANNER_CUSTOM_EVENT,
  refreshPlannerCustomizationFromApi,
  readAnnualCustomItems,
  readHiddenAnnualApiRows,
  readNameOverrides,
  readOneTimeAnnualRowKeys,
  saveAnnualCustomItems,
  saveHiddenAnnualApiRows,
  saveNameOverrides,
  saveOneTimeAnnualRowKeys,
  type AnnualCustomItem
} from "@/lib/planner-custom-items";
import { useAnnualPlannerState } from "@/components/budget/hooks/use-annual-planner-state";
import { groupRowsBySection, resolveCustomSection } from "@/components/budget/planner-row-utils";
import {
  AnnualMetricCard,
  AnnualPlannerSummaryPanel,
  AnnualPlannerTablePanel,
  AnnualSummaryLine
} from "@/components/budget/annual/annual-presentational";

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

const SECTION_COLUMN_WIDTH = 140;
const ITEM_COLUMN_WIDTH = 200;
const ACTION_COLUMN_WIDTH = 84;
const MONTH_COLUMN_WIDTH = 76;

function hasNonZero(value: number): boolean {
  return Math.abs(value) > 0.000001;
}

function oneTimeKeyForAnnualRow(row: AnnualDisplayRow, year: number): string | null {
  if (row.source === "api" && row.categoryId) {
    return annualApiOneTimeRowKey(year, row.categoryId);
  }

  if (row.source === "custom" && row.customId) {
    return annualCustomOneTimeRowKey(year, row.customId);
  }

  return null;
}

export function AnnualPlanner() {
  const now = new Date();
  useCurrencySetting();
  const sectionSettings = useSectionSettings();
  const {
    year,
    setYear,
    annualPlan,
    setAnnualPlan,
    loading,
    saving,
    message,
    loadAnnualPlanData,
    saveAnnualPlanData,
    copyFromPreviousYear: copyFromPreviousYearData
  } = useAnnualPlannerState(now.getFullYear());
  const [annualCustomItems, setAnnualCustomItems] = useState<AnnualCustomItem[]>([]);
  const [nameOverrides, setNameOverrides] = useState<Record<string, string>>({});
  const [hiddenApiRows, setHiddenApiRows] = useState<string[]>([]);
  const [oneTimeAnnualRows, setOneTimeAnnualRows] = useState<string[]>([]);
  const [editingName, setEditingName] = useState<{ rowId: string; value: string } | null>(null);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);

  useEffect(() => {
    void loadAnnualPlanData(year);
  }, [loadAnnualPlanData, year]);

  useEffect(() => {
    function syncLocalRows() {
      setAnnualCustomItems(readAnnualCustomItems());
      setNameOverrides(readNameOverrides());
      setHiddenApiRows(readHiddenAnnualApiRows());
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

  const oneTimeAnnualRowsSet = useMemo(() => new Set(oneTimeAnnualRows), [oneTimeAnnualRows]);

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
        resolvedSection: resolveCustomSection(
          {
            sectionId: item.sectionId,
            sectionKind: item.sectionKind
          },
          sectionSettings
        )
      });
    }

    return rows;
  }, [annualCustomItems, annualPlan?.categories, hiddenApiRows, nameOverrides, sectionSettings, year]);

  const groupedRows = useMemo(() => {
    return groupRowsBySection(rowsWithMeta, sectionSettings);
  }, [rowsWithMeta, sectionSettings]);
  const itemColumnLeft = SECTION_COLUMN_WIDTH;
  const actionColumnLeft = SECTION_COLUMN_WIDTH + ITEM_COLUMN_WIDTH;
  const tableMinWidth = SECTION_COLUMN_WIDTH + ITEM_COLUMN_WIDTH + ACTION_COLUMN_WIDTH + MONTH_COLUMN_WIDTH * MONTH_LABELS.length;

  function updateApiCell(categoryId: string, monthIndex: number, nextValue: number) {
    if (!annualPlan) {
      return;
    }

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

  function updateCustomCell(customId: string, monthIndex: number, nextValue: number) {
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

  function isOneTimeAnnualRow(row: AnnualDisplayRow): boolean {
    const key = oneTimeKeyForAnnualRow(row, year);
    return key ? oneTimeAnnualRowsSet.has(key) : false;
  }

  function toggleOneTimeAnnualRow(row: AnnualDisplayRow, checked: boolean) {
    if (row.resolvedSection.kind !== "PERSONAL_EXPENSES") {
      return;
    }

    const key = oneTimeKeyForAnnualRow(row, year);
    if (!key) {
      return;
    }

    if (checked) {
      const nonZeroMonths = row.months
        .map((monthValue, monthIndex) => ({ monthValue, monthIndex }))
        .filter((entry) => hasNonZero(entry.monthValue));

      if (nonZeroMonths.length > 1) {
        setValidationMessage(
          "Annual payment item can have value in only one month. Set all other months to 0 before enabling this option."
        );
        return;
      }
    }

    const next = checked
      ? Array.from(new Set([...oneTimeAnnualRows, key]))
      : oneTimeAnnualRows.filter((item) => item !== key);

    setOneTimeAnnualRows(saveOneTimeAnnualRowKeys(next));
  }

  function addRow(sectionId: string, sectionKind: ManagedSectionKind) {
    const next = [...annualCustomItems, createAnnualCustomDraft(year, sectionId, sectionKind)];
    setAnnualCustomItems(saveAnnualCustomItems(next));
  }

  function removeRow(row: AnnualDisplayRow) {
    const oneTimeKey = oneTimeKeyForAnnualRow(row, year);
    if (oneTimeKey && oneTimeAnnualRowsSet.has(oneTimeKey)) {
      const nextOneTimeRows = oneTimeAnnualRows.filter((item) => item !== oneTimeKey);
      setOneTimeAnnualRows(saveOneTimeAnnualRowKeys(nextOneTimeRows));
    }

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

    await saveAnnualPlanData(annualPlan, year);
  }

  async function copyFromPreviousYear() {
    await copyFromPreviousYearData(year);
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

  const monthlyRemainders = useMemo(() => {
    const incomeByMonth = Array<number>(MONTH_LABELS.length).fill(0);
    const allocatedByMonth = Array<number>(MONTH_LABELS.length).fill(0);

    for (const row of rowsWithMeta) {
      row.months.forEach((monthValue, monthIndex) => {
        if (row.resolvedSection.kind === "INCOME") {
          incomeByMonth[monthIndex] += monthValue;
          return;
        }

        allocatedByMonth[monthIndex] += monthValue;
      });
    }

    return incomeByMonth.map((incomeValue, monthIndex) => incomeValue - allocatedByMonth[monthIndex]);
  }, [rowsWithMeta]);

  return (
    <div className="space-y-6">
      <header className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <h1 className="ui-text-strong text-2xl font-semibold tracking-[-0.02em] md:text-3xl">Annual Budget Planning</h1>
          <p className="ui-text-muted text-base">Plan your budget across all months</p>
        </div>

        <div className="flex flex-wrap items-center gap-2 xl:justify-end">
          <select
            className="ui-control ui-border h-12 min-w-[132px] rounded-2xl border px-4 text-sm"
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
            className="ui-btn-secondary ui-border ui-hover-soft inline-flex h-12 items-center gap-2 rounded-2xl border px-4 text-sm md:text-base"
            onClick={() => void loadAnnualPlanData(year)}
          >
            <RefreshIcon size={20} />
            Reload
          </button>

          <button
            type="button"
            className="ui-btn-secondary ui-border ui-hover-soft inline-flex h-12 items-center gap-2 rounded-2xl border px-4 text-sm md:text-base"
            onClick={() => void copyFromPreviousYear()}
            disabled={saving}
          >
            <CopyIcon size={20} />
            Copy {year - 1}
          </button>

          <button
            type="button"
            className="ui-btn-primary inline-flex h-12 items-center gap-2 rounded-2xl px-5 text-sm disabled:opacity-60 md:text-base"
            onClick={() => void saveAnnualPlan()}
            disabled={saving || loading || !annualPlan}
          >
            <SaveIcon size={20} />
            {saving ? "Saving..." : "Save Annual Plan"}
          </button>
        </div>
      </header>

      {message ? (
        <p className="ui-text-muted text-sm md:text-base">
          {message.message}
          {message.details ? ` ${message.details}` : ""}
        </p>
      ) : null}
      {loading ? <p className="ui-text-muted text-sm md:text-base">Loading annual plan...</p> : null}

      {annualPlan ? (
        <>
          <AnnualPlannerTablePanel>
          <section className="ui-border ui-surface overflow-hidden rounded-[22px] border">
            <div className="max-w-full overflow-x-auto">
              <table className="w-full min-w-[1336px] table-fixed border-collapse" style={{ minWidth: tableMinWidth }}>
                <thead>
                  <tr className="bg-[#eceef2]">
                    <th
                      className="sticky left-0 z-40 border-b border-[#cdd2da] bg-[#eceef2] px-4 py-3 text-left text-sm font-semibold text-[#171b25] md:text-base"
                      style={{ width: SECTION_COLUMN_WIDTH, minWidth: SECTION_COLUMN_WIDTH }}
                    >
                      Section
                    </th>
                    <th
                      className="sticky z-40 border-b border-[#cdd2da] bg-[#eceef2] px-4 py-3 text-left text-sm font-semibold text-[#171b25] md:text-base"
                      style={{ left: itemColumnLeft, width: ITEM_COLUMN_WIDTH, minWidth: ITEM_COLUMN_WIDTH }}
                    >
                      Item
                    </th>
                    <th
                      className="sticky z-40 border-b border-[#cdd2da] bg-[#eceef2] px-2 py-3 text-center text-sm font-semibold text-[#171b25] md:text-base"
                      style={{ left: actionColumnLeft, width: ACTION_COLUMN_WIDTH, minWidth: ACTION_COLUMN_WIDTH }}
                    >
                      Action
                    </th>
                    {MONTH_LABELS.map((label) => (
                      <th
                        key={label}
                        className="border-b border-[#cdd2da] px-2 py-3 text-center text-sm font-semibold text-[#171b25] md:text-base"
                        style={{ width: MONTH_COLUMN_WIDTH, minWidth: MONTH_COLUMN_WIDTH }}
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
                          <td
                            className="sticky left-0 z-30 border-b border-[#cad0d8] bg-inherit px-4 py-3 align-top text-sm text-[#1b1f2b] md:text-base"
                            style={{ width: SECTION_COLUMN_WIDTH, minWidth: SECTION_COLUMN_WIDTH }}
                          >
                            {group.label}
                          </td>
                          <td
                            className="sticky z-20 border-b border-[#cad0d8] bg-inherit px-4 py-3"
                            style={{ left: itemColumnLeft, width: ITEM_COLUMN_WIDTH, minWidth: ITEM_COLUMN_WIDTH }}
                          >
                            <button
                              type="button"
                              onClick={() => addRow(group.sectionId, group.kind)}
                              className="h-7 rounded-lg border border-[#c6cad2] bg-[#f8f9fb] px-3 text-xs text-[#30384b]"
                            >
                              + add item
                            </button>
                          </td>
                          <td
                            className="sticky z-20 border-b border-[#cad0d8] bg-inherit px-2 py-3"
                            style={{ left: actionColumnLeft, width: ACTION_COLUMN_WIDTH, minWidth: ACTION_COLUMN_WIDTH }}
                          />
                          <td className="border-b border-[#cad0d8] px-2 py-3" colSpan={MONTH_LABELS.length} />
                        </tr>
                      ) : (
                        <>
                          {group.rows.map((row, index) => (
                            <tr key={row.rowId} className={sectionRowTone(group.kind)}>
                              {index === 0 ? (
                                <td
                                  rowSpan={group.rows.length + 1}
                                  className="sticky left-0 z-30 border-b border-[#cad0d8] bg-inherit px-4 py-3 align-top text-sm text-[#1b1f2b] md:text-base"
                                  style={{ width: SECTION_COLUMN_WIDTH, minWidth: SECTION_COLUMN_WIDTH }}
                                >
                                  {group.label}
                                </td>
                              ) : null}

                              <td
                                className="sticky z-20 border-b border-[#cad0d8] bg-inherit px-4 py-3 text-sm text-[#1b1f2b] md:text-base"
                                style={{ left: itemColumnLeft, width: ITEM_COLUMN_WIDTH, minWidth: ITEM_COLUMN_WIDTH }}
                              >
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
                                      className="line-clamp-2 text-left underline decoration-dotted underline-offset-4"
                                      title={row.name}
                                    >
                                      {row.name}
                                    </button>
                                  )}
                                </div>
                              </td>

                              <td
                                className="sticky z-20 border-b border-[#cad0d8] bg-inherit px-2 py-3 text-center"
                                style={{ left: actionColumnLeft, width: ACTION_COLUMN_WIDTH, minWidth: ACTION_COLUMN_WIDTH }}
                              >
                                <div className="mx-auto flex items-center justify-center gap-1">
                                  <button
                                    type="button"
                                    onClick={() => removeRow(row)}
                                    aria-label={`Remove ${row.name}`}
                                    title="Remove row"
                                    className="grid h-7 w-7 place-items-center rounded-lg border border-[#f2a2b5] bg-[#fff1f4] text-[#be123c]"
                                  >
                                    <TrashIcon size={14} />
                                  </button>

                                  {row.resolvedSection.kind === "PERSONAL_EXPENSES" ? (
                                    <button
                                      type="button"
                                      aria-pressed={isOneTimeAnnualRow(row)}
                                      onClick={() => toggleOneTimeAnnualRow(row, !isOneTimeAnnualRow(row))}
                                      aria-label={`Toggle annual payment mode for ${row.name}`}
                                      title="Annual payment (single month)"
                                      className={`grid h-7 w-7 place-items-center rounded-lg border ${
                                        isOneTimeAnnualRow(row)
                                          ? "border-[#9bd7b2] bg-[#e8f4ee] text-[#10a34a]"
                                          : "border-[#c6cad2] bg-[#f8f9fb] text-[#76809a]"
                                      }`}
                                    >
                                      <CalendarIcon size={14} />
                                    </button>
                                  ) : null}
                                </div>
                              </td>

                              {row.months.map((monthValue, monthIndex) => (
                                <td
                                  key={`${row.rowId}-${monthIndex}`}
                                  className="border-b border-[#cad0d8] px-1.5 py-2.5"
                                  style={{ width: MONTH_COLUMN_WIDTH, minWidth: MONTH_COLUMN_WIDTH }}
                                >
                                  <Input
                                    type="number"
                                    step="0.01"
                                    className="numeric-input h-9 min-w-0 rounded-xl border-0 bg-[#eff1f4] px-2 text-center text-sm font-medium text-[#1f2430] shadow-none md:text-base"
                                    value={monthValue}
                                    onChange={(event) => {
                                      const parsed = Number.parseFloat(event.target.value);
                                      const nextValue = Number.isFinite(parsed) ? parsed : 0;

                                      if (isOneTimeAnnualRow(row) && hasNonZero(nextValue)) {
                                        const conflictingMonthIndex = row.months.findIndex(
                                          (otherMonthValue, index) => index !== monthIndex && hasNonZero(otherMonthValue)
                                        );

                                        if (conflictingMonthIndex >= 0) {
                                          setValidationMessage(
                                            `Annual payment can be planned only in one month. Clear ${MONTH_LABELS[conflictingMonthIndex]} first.`
                                          );
                                          return;
                                        }
                                      }

                                      if (row.source === "api" && row.categoryId) {
                                        updateApiCell(row.categoryId, monthIndex, nextValue);
                                        return;
                                      }

                                      if (row.source === "custom" && row.customId) {
                                        updateCustomCell(row.customId, monthIndex, nextValue);
                                      }
                                    }}
                                  />
                                </td>
                              ))}
                            </tr>
                          ))}

                          <tr key={`${group.id}-add`} className={sectionRowTone(group.kind)}>
                            <td
                              className="sticky z-20 border-b border-[#cad0d8] bg-inherit px-4 py-3"
                              style={{ left: itemColumnLeft, width: ITEM_COLUMN_WIDTH, minWidth: ITEM_COLUMN_WIDTH }}
                            >
                              <button
                                type="button"
                                onClick={() => addRow(group.sectionId, group.kind)}
                                className="h-7 rounded-lg border border-[#c6cad2] bg-[#f8f9fb] px-3 text-xs text-[#30384b]"
                              >
                                + add item
                              </button>
                            </td>
                            <td
                              className="sticky z-20 border-b border-[#cad0d8] bg-inherit px-2 py-3"
                              style={{ left: actionColumnLeft, width: ACTION_COLUMN_WIDTH, minWidth: ACTION_COLUMN_WIDTH }}
                            />
                            <td className="border-b border-[#cad0d8] px-2 py-3" colSpan={MONTH_LABELS.length} />
                          </tr>
                        </>
                      )}
                    </Fragment>
                  ))}

                  <tr className="bg-[#eceef2]">
                    <td
                      className="sticky left-0 z-30 border-b border-[#cdd2da] bg-[#eceef2] px-4 py-3 text-sm font-semibold text-[#171b25] md:text-base"
                      style={{ width: SECTION_COLUMN_WIDTH, minWidth: SECTION_COLUMN_WIDTH }}
                    >
                      Summary
                    </td>
                    <td
                      className="sticky z-20 border-b border-[#cdd2da] bg-[#eceef2] px-4 py-3 text-sm font-semibold text-[#171b25] md:text-base"
                      style={{ left: itemColumnLeft, width: ITEM_COLUMN_WIDTH, minWidth: ITEM_COLUMN_WIDTH }}
                    >
                      Monthly Remainder
                    </td>
                    <td
                      className="sticky z-20 border-b border-[#cdd2da] bg-[#eceef2] px-2 py-3"
                      style={{ left: actionColumnLeft, width: ACTION_COLUMN_WIDTH, minWidth: ACTION_COLUMN_WIDTH }}
                    />
                    {monthlyRemainders.map((monthValue, monthIndex) => (
                      <td
                        key={`monthly-remainder-${monthIndex + 1}`}
                        className={`border-b border-[#cdd2da] px-2 py-3 text-center text-sm font-medium md:text-base ${
                          monthValue >= 0 ? "text-[#10a34a]" : "text-[#e11d48]"
                        }`}
                        style={{ width: MONTH_COLUMN_WIDTH, minWidth: MONTH_COLUMN_WIDTH }}
                      >
                        {asSignedCurrency(monthValue)}
                      </td>
                    ))}
                  </tr>
                </tbody>
              </table>
            </div>
          </section>
          </AnnualPlannerTablePanel>

          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
            <AnnualMetricCard label="INCOME" value={summary.income} valueTone="text-[#10a34a]" />
            <AnnualMetricCard label="BUSINESS COSTS" value={summary.businessCosts} valueTone="text-[#8f30ff]" />
            <AnnualMetricCard label="PERSONAL COSTS" value={summary.personalCosts} valueTone="text-[#f35b00]" />
            <AnnualMetricCard label="SAVINGS / INVEST" value={summary.savingsAndInvestments} valueTone="text-[#2563eb]" />
            <AnnualMetricCard label="REMAINDER" value={summary.remainder} valueTone="text-[#e11d48]" danger />
          </section>

          <AnnualPlannerSummaryPanel>
          <section className="ui-border ui-surface rounded-[22px] border p-5 md:p-6">
            <h2 className="ui-text-strong text-xl font-medium md:text-2xl">Annual Money Flow Summary</h2>

            <div className="mt-6 space-y-4 text-sm md:text-base">
              <AnnualSummaryLine label="Total Business Income" value={summary.income} valueTone="text-[#10a34a]" />
              <AnnualSummaryLine label="- Business Expenses" value={-summary.businessCosts} valueTone="text-[#8f30ff]" />

              <div className="border-t border-[#d7dbe2]" />

              <AnnualSummaryLine label="Transfer to Personal" value={summary.transferToPersonal} valueTone="text-[#10a34a]" />
              <AnnualSummaryLine label="- Personal Expenses" value={-summary.personalCosts} valueTone="text-[#f35b00]" />
              <AnnualSummaryLine
                label="- Savings"
                value={-summary.savingsAndInvestments}
                valueTone="text-[#2563eb]"
              />

              <div className="border-t border-[#d7dbe2]" />

              <AnnualSummaryLine label="Remainder (Unallocated)" value={summary.remainder} valueTone="text-[#e11d48]" />
            </div>
          </section>
          </AnnualPlannerSummaryPanel>
        </>
      ) : null}

      {validationMessage ? (
        <div className="fixed inset-0 z-[120] grid place-items-center bg-black/40 p-4">
          <div className="ui-border ui-surface w-full max-w-md rounded-2xl border p-5">
            <h3 className="ui-text-strong text-lg font-medium">Validation error</h3>
            <p className="ui-text mt-2 text-sm">{validationMessage}</p>
            <div className="mt-4 flex justify-end">
              <button
                type="button"
                onClick={() => setValidationMessage(null)}
                className="ui-btn-primary inline-flex h-10 items-center rounded-xl px-4 text-sm"
              >
                OK
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
