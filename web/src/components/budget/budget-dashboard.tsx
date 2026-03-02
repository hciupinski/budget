import { AnnualPlanner } from "@/components/budget/annual-planner";
import { MonthlyWorkspace } from "@/components/budget/monthly-workspace";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export function BudgetDashboard() {
  return (
    <main className="mx-auto flex min-h-screen max-w-[1400px] flex-col gap-6 px-6 py-8">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle>Budget Engine</CardTitle>
            <CardDescription>
              Epic 2 active: annual planning matrix and monthly execution workspace.
            </CardDescription>
          </div>
          <form action="/api/auth/logout" method="post">
            <Button type="submit" variant="outline">
              Sign out
            </Button>
          </form>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            Use Annual Planner to define monthly targets, then run Monthly Workspace to track PLANNED, DONE, PARTIAL,
            and SKIPPED actions.
          </p>
        </CardContent>
      </Card>

      <AnnualPlanner />
      <MonthlyWorkspace />
    </main>
  );
}
