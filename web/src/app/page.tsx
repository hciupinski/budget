import { BudgetOverview } from "@/components/budget/budget-overview";
import { BudgetShell } from "@/components/budget/budget-shell";
import { requireOwnerSession } from "@/lib/require-owner-session";

export default async function HomePage() {
  await requireOwnerSession();

  return (
    <BudgetShell>
      <BudgetOverview />
    </BudgetShell>
  );
}
