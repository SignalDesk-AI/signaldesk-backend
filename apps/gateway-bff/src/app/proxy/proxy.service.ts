import { Injectable, NotFoundException } from '@nestjs/common';

import { ProxyRequest, ProxyResponse, buildDownstreamUrl, buildForwardHeaders, buildProxyBody, readProxyResponse } from './proxy-request';
import { resolveRoute } from './gateway-route-map';

@Injectable()
export class ProxyService {
  async forward(request: ProxyRequest): Promise<ProxyResponse> {
    const route = resolveRoute(request.originalUrl ?? request.url);

    if (!route) {
      throw new NotFoundException('Gateway route is not mapped');
    }

    const response = await fetch(buildDownstreamUrl(route, request), {
      method: request.method,
      headers: buildForwardHeaders(request),
      body: buildProxyBody(request),
    });

    return readProxyResponse(response);
  }
}
