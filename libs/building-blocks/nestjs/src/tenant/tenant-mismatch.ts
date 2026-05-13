import { TenantMismatchException } from './tenant-mismatch.exception';

export interface TenantMatchResult {
  matches: boolean;
  expectedTenantId?: string;
  actualTenantId?: string;
}

export function tenantMatches(
  expectedTenantId: string | undefined,
  actualTenantId: string | undefined
): TenantMatchResult {
  return {
    matches: expectedTenantId !== undefined && expectedTenantId === actualTenantId,
    expectedTenantId,
    actualTenantId,
  };
}

export function assertTenantMatch(
  expectedTenantId: string | undefined,
  actualTenantId: string | undefined
): void {
  if (!tenantMatches(expectedTenantId, actualTenantId).matches) {
    throw new TenantMismatchException(expectedTenantId, actualTenantId);
  }
}

export function assertOptionalTenantMatch(
  trustedTenantId: string | undefined,
  candidateTenantId: string | undefined
): void {
  if (candidateTenantId === undefined) {
    return;
  }

  if (trustedTenantId !== candidateTenantId) {
    throw new TenantMismatchException(trustedTenantId, candidateTenantId);
  }
}
