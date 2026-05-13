import { UnauthorizedException } from '@nestjs/common';

import { JwtPlaceholderVerifier } from './jwt-placeholder.verifier';

describe('JwtPlaceholderVerifier', () => {
  const verifier = new JwtPlaceholderVerifier();

  it('extracts gateway auth claims from a bearer token payload', () => {
    const claims = verifier.verifyAuthorizationHeader(
      `Bearer ${jwt({ sub: 'user-1', tenantId: 'tenant-1', roles: ['admin'], permissions: ['tickets:read'] })}`
    );

    expect(claims).toEqual({
      userId: 'user-1',
      tenantId: 'tenant-1',
      roles: ['admin'],
      permissions: ['tickets:read'],
    });
  });

  it('fails closed when the token is missing or malformed', () => {
    expect(() => verifier.verifyAuthorizationHeader(undefined)).toThrow(UnauthorizedException);
    expect(() => verifier.verifyAuthorizationHeader('Bearer not-a-jwt')).toThrow(UnauthorizedException);
  });

  it('fails closed when required claims are missing', () => {
    expect(() => verifier.verifyAuthorizationHeader(`Bearer ${jwt({ sub: 'user-1' })}`)).toThrow(
      UnauthorizedException
    );
  });
});

function jwt(payload: object): string {
  return [base64Url({ alg: 'none', typ: 'JWT' }), base64Url(payload), 'signature'].join('.');
}

function base64Url(value: object): string {
  return Buffer.from(JSON.stringify(value))
    .toString('base64')
    .replace(/=/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_');
}
