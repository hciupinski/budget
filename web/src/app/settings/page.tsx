import { BudgetShell } from "@/components/budget/budget-shell";
import { SettingsView } from "@/components/budget/settings-view";
import { requireOwnerSession } from "@/lib/require-owner-session";

export default async function SettingsPage() {
  await requireOwnerSession();

  return (
    <BudgetShell>
      <SettingsView />
    </BudgetShell>
  );
}
