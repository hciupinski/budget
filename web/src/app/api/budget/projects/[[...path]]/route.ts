import { proxyBudgetApi } from "@/lib/api-proxy";

type RouteParams = { path?: string[] };

async function forward(
  request: Request,
  context: { params: Promise<RouteParams> }
): Promise<Response> {
  const { path = [] } = await context.params;
  const query = new URL(request.url).search;
  const targetPath = path.length > 0
    ? `/api/budget/projects/${path.join("/")}${query}`
    : `/api/budget/projects${query}`;

  const headers = new Headers();
  const contentType = request.headers.get("content-type");
  if (contentType) {
    headers.set("content-type", contentType);
  }

  const hasBody = request.method !== "GET" && request.method !== "HEAD";
  let body: BodyInit | undefined;

  if (hasBody) {
    if (contentType?.includes("multipart/form-data")) {
      body = await request.formData();
    } else if (contentType?.includes("application/json") || contentType?.startsWith("text/")) {
      body = await request.text();
    } else {
      body = await request.arrayBuffer();
    }
  }

  return proxyBudgetApi(targetPath, {
    method: request.method,
    headers,
    body
  });
}

export async function GET(request: Request, context: { params: Promise<RouteParams> }) {
  return forward(request, context);
}

export async function POST(request: Request, context: { params: Promise<RouteParams> }) {
  return forward(request, context);
}

export async function PATCH(request: Request, context: { params: Promise<RouteParams> }) {
  return forward(request, context);
}

export async function PUT(request: Request, context: { params: Promise<RouteParams> }) {
  return forward(request, context);
}

export async function DELETE(request: Request, context: { params: Promise<RouteParams> }) {
  return forward(request, context);
}
