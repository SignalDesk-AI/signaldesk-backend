export interface AuthClaims {
  userId: string;
  tenantId: string;
  roles: string[];
  permissions: string[];
}

export interface AuthenticatedRequest {
  auth?: AuthClaims;
  headers: Record<string, string | string[] | undefined>;
  params?: Record<string, unknown>;
  query?: Record<string, unknown>;
  body?: unknown;
}
