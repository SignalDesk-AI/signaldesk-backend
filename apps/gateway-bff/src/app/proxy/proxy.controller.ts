import { All, Controller, Req, Res } from '@nestjs/common';
import { Response } from 'express';

import { ProxyRequest } from './proxy-request';
import { ProxyService } from './proxy.service';

@Controller()
export class ProxyController {
  constructor(private readonly proxyService: ProxyService) {}

  @All([
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
  ])
  async forward(@Req() request: ProxyRequest, @Res() response: Response): Promise<void> {
    const downstream = await this.proxyService.forward(request);

    for (const [name, value] of Object.entries(downstream.headers)) {
      if (name !== 'transfer-encoding') {
        response.setHeader(name, value);
      }
    }

    response.status(downstream.statusCode).send(downstream.body);
  }
}
