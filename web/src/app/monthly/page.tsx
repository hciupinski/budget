import { BudgetShell } from "@/components/budget/budget-shell";
import { MonthlyWorkspace } from "@/components/budget/monthly-workspace";
import { requireOwnerSession } from "@/lib/require-owner-session";

export default async function MonthlyWorkspacePage() {
  await requireOwnerSession();

  return (
    <BudgetShell>
      <MonthlyWorkspace />
    </BudgetShell>
  );
}
