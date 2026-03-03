"use client";

import { useEffect, useMemo, useState } from "react";
import { Input } from "@/components/ui/input";
import {
  createSectionDraft,
  readSectionSettings,
  refreshSectionSettingsFromApi,
  resetSectionSettings,
  saveSectionSettings,
  type ManagedSection,
  type ManagedSectionKind
} from "@/lib/section-settings";
import {
  CURRENCY_OPTIONS,
  DEFAULT_CURRENCY,
  DEFAULT_THEME,
  readCurrencySetting,
  readThemeSetting,
  refreshCurrencySettingFromApi,
  refreshThemeSettingFromApi,
  saveCurrencySetting,
  saveThemeSetting,
  type CurrencyCode
} from "@/lib/currency-settings";
import { PlusIcon } from "@/components/budget/icons";

const KIND_OPTIONS: Array<{ value: ManagedSectionKind; label: string }> = [
  { value: "INCOME", label: "Income" },
  { value: "BUSINESS_EXPENSES", label: "Business Expenses" },
  { value: "PERSONAL_EXPENSES", label: "Personal Expenses" },
  { value: "SAVINGS", label: "Savings" },
  { value: "INVESTMENTS", label: "Investments" }
];

function sectionCardTone(kind: ManagedSectionKind): string {
  if (kind === "INCOME") {
    return "border-[#9bd7b2] bg-[#e8f4ee]";
  }

  if (kind === "BUSINESS_EXPENSES") {
    return "border-[#d9c2f4] bg-[#efebf7]";
  }

  if (kind === "PERSONAL_EXPENSES") {
    return "border-[#efcda8] bg-[#f5efe5]";
  }

  return "border-[#b8ccef] bg-[#e8eef7]";
}

export function SettingsView() {
  const [activeTab, setActiveTab] = useState<"SECTIONS" | "GENERAL">("SECTIONS");
  const [draftSections, setDraftSections] = useState<ManagedSection[]>([]);
  const [draftCurrency, setDraftCurrency] = useState<CurrencyCode>(DEFAULT_CURRENCY);
  const [darkModeEnabled, setDarkModeEnabled] = useState<boolean>(DEFAULT_THEME === "DARK");
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    setDraftSections(readSectionSettings());
    setDraftCurrency(readCurrencySetting());
    setDarkModeEnabled(readThemeSetting() === "DARK");
    void refreshSectionSettingsFromApi().then((sections) => {
      if (sections) {
        setDraftSections(sections);
      }
    });
    void refreshCurrencySettingFromApi().then((currency) => {
      if (currency) {
        setDraftCurrency(currency);
      }
    });
    void refreshThemeSettingFromApi().then((theme) => {
      if (theme) {
        setDarkModeEnabled(theme === "DARK");
      }
    });
  }, []);

  function updateSection(sectionId: string, change: Partial<ManagedSection>) {
    setDraftSections((current) =>
      current.map((section) =>
        section.id === sectionId
          ? {
              ...section,
              ...change
            }
          : section
      )
    );
  }

  function removeSection(sectionId: string) {
    setDraftSections((current) => current.filter((section) => section.id !== sectionId));
  }

  function addSection() {
    setDraftSections((current) => [...current, createSectionDraft(current.length)]);
  }

  function moveSection(sectionId: string, direction: -1 | 1) {
    setDraftSections((current) => {
      const index = current.findIndex((section) => section.id === sectionId);
      if (index < 0) {
        return current;
      }

      const nextIndex = index + direction;
      if (nextIndex < 0 || nextIndex >= current.length) {
        return current;
      }

      const next = [...current];
      const [item] = next.splice(index, 1);
      next.splice(nextIndex, 0, item);

      return next.map((section, order) => ({
        ...section,
        order
      }));
    });
  }

  function onSave() {
    if (draftSections.length === 0) {
      setMessage("Add at least one section before saving.");
      return;
    }

    const saved = saveSectionSettings(draftSections);
    setDraftSections(saved);
    setMessage("Section settings saved. Annual and monthly views now use these sections.");
  }

  function onReset() {
    const defaults = resetSectionSettings();
    setDraftSections(defaults);
    setMessage("Section settings reset to defaults.");
  }

  const hasDuplicateNames = useMemo(() => {
    const names = draftSections.map((section) => section.name.trim().toLowerCase()).filter(Boolean);
    return new Set(names).size !== names.length;
  }, [draftSections]);

  return (
    <div className="space-y-6">
      <header>
        <h1 className="ui-text-strong text-2xl md:text-3xl font-semibold tracking-[-0.02em]">Settings</h1>
        <p className="ui-text-muted text-base">Manage budget configuration and section rules</p>
      </header>

      <section className="ui-border ui-surface rounded-[22px] border p-4">
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => setActiveTab("SECTIONS")}
            className={`h-11 rounded-2xl px-5 text-sm md:text-base ${
              activeTab === "SECTIONS"
                ? "ui-btn-primary"
                : "ui-btn-secondary ui-border border"
            }`}
          >
            Sections
          </button>

          <button
            type="button"
            onClick={() => setActiveTab("GENERAL")}
            className={`h-11 rounded-2xl px-5 text-sm md:text-base ${
              activeTab === "GENERAL"
                ? "ui-btn-primary"
                : "ui-btn-secondary ui-border border"
            }`}
          >
            General
          </button>
        </div>
      </section>

      {activeTab === "SECTIONS" ? (
        <section className="ui-border ui-surface rounded-[22px] border p-6 md:p-8">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <h2 className="ui-text-strong text-xl md:text-2xl font-medium">Section Management</h2>
              <p className="ui-text-muted mt-1 text-sm md:text-base">
                Add, edit, and remove sections. These rules are the source of truth for grouping in Annual and Monthly views.
              </p>
            </div>

            <button
              type="button"
              onClick={addSection}
              className="ui-btn-primary inline-flex h-11 items-center gap-2 rounded-2xl px-4 text-sm md:text-base"
            >
              <PlusIcon size={18} />
              Add Section
            </button>
          </div>

          <div className="mt-6 space-y-4">
            {draftSections.map((section, index) => (
              <article key={section.id} className={`rounded-[18px] border p-4 ${sectionCardTone(section.kind)}`}>
                <div className="grid gap-3 lg:grid-cols-[1.2fr_0.9fr_1.4fr_auto] lg:items-end">
                  <div>
                    <label className="ui-text-muted mb-1 block text-xs uppercase tracking-wide">Section Name</label>
                    <Input
                      value={section.name}
                      onChange={(event) => updateSection(section.id, { name: event.target.value })}
                      className="ui-control h-11 rounded-xl"
                    />
                  </div>

                  <div>
                    <label className="ui-text-muted mb-1 block text-xs uppercase tracking-wide">Type</label>
                    <select
                      value={section.kind}
                      onChange={(event) => updateSection(section.id, { kind: event.target.value as ManagedSectionKind })}
                      className="ui-control h-11 w-full rounded-xl border px-3 text-sm"
                    >
                      {KIND_OPTIONS.map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label className="ui-text-muted mb-1 block text-xs uppercase tracking-wide">Category Keywords</label>
                    <Input
                      value={section.keywords.join(", ")}
                      onChange={(event) =>
                        updateSection(section.id, {
                          keywords: event.target.value
                            .split(",")
                            .map((part) => part.trim())
                            .filter(Boolean)
                        })
                      }
                      placeholder="e.g. software, office, consulting"
                      className="ui-control h-11 rounded-xl"
                    />
                  </div>

                  <div className="flex flex-wrap gap-2 lg:justify-end">
                    <button
                      type="button"
                      onClick={() => moveSection(section.id, -1)}
                      disabled={index === 0}
                      className="ui-btn-secondary ui-border h-11 rounded-xl border px-3 text-sm disabled:opacity-40"
                    >
                      Up
                    </button>
                    <button
                      type="button"
                      onClick={() => moveSection(section.id, 1)}
                      disabled={index === draftSections.length - 1}
                      className="ui-btn-secondary ui-border h-11 rounded-xl border px-3 text-sm disabled:opacity-40"
                    >
                      Down
                    </button>
                    <button
                      type="button"
                      onClick={() => removeSection(section.id)}
                      className="h-11 rounded-xl border border-[#f2a2b5] bg-[#fff1f4] px-3 text-sm text-[#be123c]"
                    >
                      Delete
                    </button>
                  </div>
                </div>
              </article>
            ))}

            {draftSections.length === 0 ? (
              <p className="ui-text-muted text-sm md:text-base">No sections yet. Add your first section.</p>
            ) : null}
          </div>

          <div className="mt-6 flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={onSave}
              disabled={hasDuplicateNames}
              className="ui-btn-primary inline-flex h-11 items-center rounded-2xl px-5 text-sm md:text-base disabled:opacity-60"
            >
              Save Sections
            </button>
            <button
              type="button"
              onClick={onReset}
              className="ui-btn-secondary ui-border inline-flex h-11 items-center rounded-2xl border px-5 text-sm md:text-base"
            >
              Reset to Default
            </button>

            {hasDuplicateNames ? (
              <p className="text-sm text-[#b42318]">Section names must be unique.</p>
            ) : null}

            {message ? <p className="ui-text-muted text-sm">{message}</p> : null}
          </div>
        </section>
      ) : (
        <section className="ui-border ui-surface rounded-[22px] border p-6 md:p-8">
          <h2 className="ui-text-strong text-xl md:text-2xl font-medium">General</h2>
          <p className="ui-text-muted mt-2 text-sm md:text-base">Configure global display preferences.</p>

          <div className="ui-border ui-surface-soft mt-6 max-w-[420px] rounded-[18px] border p-4">
            <label className="ui-text-muted mb-2 block text-xs uppercase tracking-wide">Currency</label>
            <select
              value={draftCurrency}
              onChange={(event) => {
                const nextCurrency = event.target.value as CurrencyCode;
                const saved = saveCurrencySetting(nextCurrency);
                setDraftCurrency(saved);
                setMessage(`Currency updated to ${saved}.`);
              }}
              className="ui-control h-11 w-full rounded-xl border px-3 text-sm"
            >
              {CURRENCY_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
            <p className="ui-text-muted mt-2 text-sm">
              Changes how amounts are displayed across the whole UI. No recalculation is applied.
            </p>
          </div>

          <div className="ui-border ui-surface-soft mt-4 max-w-[420px] rounded-[18px] border p-4">
            <p className="ui-text-muted text-xs uppercase tracking-wide">Dark Mode</p>
            <div className="mt-2 flex items-center justify-between gap-3">
              <p className="ui-text text-sm">
                {darkModeEnabled ? "Enabled" : "Disabled"}
              </p>
              <button
                type="button"
                role="switch"
                aria-checked={darkModeEnabled}
                onClick={() => {
                  const next = !darkModeEnabled;
                  setDarkModeEnabled(next);
                  const saved = saveThemeSetting(next ? "DARK" : "LIGHT");
                  setMessage(`Theme updated to ${saved === "DARK" ? "Dark" : "Light"} mode.`);
                }}
                data-state={darkModeEnabled ? "on" : "off"}
                className="ui-switch-track relative h-8 w-14 rounded-full border transition-colors"
              >
                <span
                  className={`ui-switch-thumb absolute left-1 top-1 h-6 w-6 rounded-full transition-transform ${
                    darkModeEnabled ? "translate-x-6" : "translate-x-0"
                  }`}
                />
              </button>
            </div>
            <p className="ui-text-muted mt-2 text-sm">
              Applies dark appearance across the whole UI.
            </p>
          </div>

          <div className="mt-5 flex flex-wrap items-center gap-3">
            {message ? <p className="ui-text-muted text-sm">{message}</p> : null}
          </div>
        </section>
      )}
    </div>
  );
}
