import { Socket } from 'node:net';

export type DependencyStatus = 'ok' | 'fail';

export interface DependencyCheckResult {
  name: string;
  status: DependencyStatus;
  target: string;
  required: boolean;
  checkedAt: string;
  error?: string;
}

export interface ReadinessResponse {
  status: 'ok' | 'degraded';
  service: string;
  checkedAt: string;
  dependencies: DependencyCheckResult[];
}

export function readinessPayload(
  service: string,
  dependencies: DependencyCheckResult[]
): ReadinessResponse {
  const ready = dependencies.every(
    (dependency) => dependency.status === 'ok' || !dependency.required
  );

  return {
    status: ready ? 'ok' : 'degraded',
    service,
    checkedAt: new Date().toISOString(),
    dependencies,
  };
}

export function configuredCheck(
  name: string,
  value: string | undefined,
  target = 'environment'
): DependencyCheckResult {
  return {
    name,
    status: value && value.trim().length > 0 ? 'ok' : 'fail',
    target,
    required: true,
    checkedAt: new Date().toISOString(),
    error: value && value.trim().length > 0 ? undefined : 'Missing configuration value',
  };
}

export async function tcpCheckFromUrl(
  name: string,
  url: string | undefined,
  defaultUrl: string,
  defaultPort: number,
  timeoutMs = 1500
): Promise<DependencyCheckResult> {
  const targetUrl = url && url.trim().length > 0 ? url : defaultUrl;
  const parsed = new URL(targetUrl);
  const host = parsed.hostname;
  const port = parsed.port ? Number(parsed.port) : defaultPort;

  return tcpCheck(name, host, port, true, timeoutMs);
}

export async function tcpCheck(
  name: string,
  host: string,
  port: number,
  required = true,
  timeoutMs = 1500
): Promise<DependencyCheckResult> {
  const target = `${host}:${port}`;

  return new Promise((resolve) => {
    const socket = new Socket();
    let settled = false;

    const finish = (status: DependencyStatus, error?: string) => {
      if (settled) {
        return;
      }

      settled = true;
      socket.destroy();
      resolve({
        name,
        status,
        target,
        required,
        checkedAt: new Date().toISOString(),
        error,
      });
    };

    socket.setTimeout(timeoutMs);
    socket.once('connect', () => finish('ok'));
    socket.once('timeout', () => finish('fail', `Timed out after ${timeoutMs}ms`));
    socket.once('error', (error) => finish('fail', error.message));
    socket.connect(port, host);
  });
}

export async function httpCheck(
  name: string,
  url: string,
  required = true,
  timeoutMs = 2000
): Promise<DependencyCheckResult> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);

  try {
    const response = await fetch(url, { signal: controller.signal });

    return {
      name,
      status: response.ok ? 'ok' : 'fail',
      target: url,
      required,
      checkedAt: new Date().toISOString(),
      error: response.ok ? undefined : `HTTP ${response.status}`,
    };
  } catch (error) {
    return {
      name,
      status: 'fail',
      target: url,
      required,
      checkedAt: new Date().toISOString(),
      error: error instanceof Error ? error.message : 'Unknown error',
    };
  } finally {
    clearTimeout(timeout);
  }
}
