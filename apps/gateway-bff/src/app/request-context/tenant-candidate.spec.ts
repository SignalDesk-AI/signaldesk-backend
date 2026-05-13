import { findClientProvidedTenantId, findClientProvidedTenantIds } from './tenant-candidate';

describe('findClientProvidedTenantIds', () => {
  it('collects every client-provided tenant ID from all supported locations', () => {
    expect(
      findClientProvidedTenantIds({
        headers: { 'x-tenant-id': 'tenant-header' },
        params: { tenantId: 'tenant-param' },
        query: { tenantId: 'tenant-query' },
        body: { tenantId: 'tenant-body' },
      })
    ).toEqual([
      { source: 'header', tenantId: 'tenant-header' },
      { source: 'params', tenantId: 'tenant-param' },
      { source: 'query', tenantId: 'tenant-query' },
      { source: 'body', tenantId: 'tenant-body' },
    ]);
  });

  it('treats missing and blank tenant IDs as absent', () => {
    expect(findClientProvidedTenantIds({ headers: { 'x-tenant-id': ' ' } })).toEqual([]);
    expect(findClientProvidedTenantIds({ headers: {} })).toEqual([]);
  });

  it('keeps the first candidate helper for existing call sites', () => {
    expect(
      findClientProvidedTenantId({
        headers: { 'x-tenant-id': 'tenant-header' },
        query: { tenantId: 'tenant-query' },
      })
    ).toBe('tenant-header');
  });
});
