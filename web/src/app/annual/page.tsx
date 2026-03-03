import { AnnualPlanner } from "@/components/budget/annual-planner";
import { BudgetShell } from "@/components/budget/budget-shell";
import { requireOwnerSession } from "@/lib/require-owner-session";

export default async function AnnualPlannerPage() {
  await requireOwnerSession();

  return (
    <BudgetShell>
      <AnnualPlanner />
    </BudgetShell>
  );
}
