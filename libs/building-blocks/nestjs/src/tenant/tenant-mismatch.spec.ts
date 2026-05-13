import { HttpStatus } from '@nestjs/common';

import { assertOptionalTenantMatch, assertTenantMatch, tenantMatches } from './tenant-mismatch';
import { TenantMismatchException } from './tenant-mismatch.exception';

describe('tenant mismatch helpers', () => {
  it('matches only when expected and actual tenant IDs are present and equal', () => {
    expect(tenantMatches('tenant-1', 'tenant-1').matches).toBe(true);
    expect(tenantMatches('tenant-1', 'tenant-2').matches).toBe(false);
    expect(tenantMatches(undefined, undefined).matches).toBe(false);
  });

  it('throws a 403 FORBIDDEN API exception on mismatch', () => {
    expect(() => assertTenantMatch('tenant-1', 'tenant-2')).toThrow(TenantMismatchException);

    try {
      assertTenantMatch('tenant-1', 'tenant-2');
    } catch (error) {
      expectTenantMismatch(error, 'tenant-1', 'tenant-2');
    }
  });

  it('allows missing optional candidate tenant IDs', () => {
    expect(() => assertOptionalTenantMatch('tenant-1', undefined)).not.toThrow();
  });

  it('allows matching optional candidate tenant IDs', () => {
    expect(() => assertOptionalTenantMatch('tenant-1', 'tenant-1')).not.toThrow();
  });

  it('throws a 403 FORBIDDEN API exception for optional candidate mismatches', () => {
    expect(() => assertOptionalTenantMatch('tenant-1', 'tenant-2')).toThrow(TenantMismatchException);

    try {
      assertOptionalTenantMatch('tenant-1', 'tenant-2');
    } catch (error) {
      expectTenantMismatch(error, 'tenant-1', 'tenant-2');
    }
  });
});

function expectTenantMismatch(
  error: unknown,
  expectedTenantId: string | undefined,
  actualTenantId: string | undefined
): void {
  expect(error).toBeInstanceOf(TenantMismatchException);
  expect((error as TenantMismatchException).getStatus()).toBe(HttpStatus.FORBIDDEN);
  expect((error as TenantMismatchException).code).toBe('FORBIDDEN');
  expect((error as TenantMismatchException).details).toEqual([
    {
      path: 'tenantId',
      expectedTenantId,
      actualTenantId,
    },
  ]);
}
