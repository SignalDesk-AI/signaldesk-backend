import { PATH_METADATA } from '@nestjs/common/constants';

import { ProxyController } from './proxy.controller';

describe('ProxyController', () => {
  it('registers routes without duplicating the global api prefix', () => {
    expect(Reflect.getMetadata(PATH_METADATA, ProxyController.prototype.forward)).toEqual([
      'auth',
      'auth/*path',
      'users',
      'users/*path',
      'workspace',
      'workspace/*path',
      'support',
      'support/*path',
      'knowledge',
      'knowledge/*path',
      'notifications',
      'notifications/*path',
      'search',
      'search/*path',
      'ai',
      'ai/*path',
      'campaigns',
      'campaigns/*path',
    ]);
  });
});
