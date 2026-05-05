import { ConfigValidationError, ConfigValidationIssue } from './config-validation.error';

export function requireEnv(
  keys: readonly string[],
  source: NodeJS.ProcessEnv = process.env
): Record<string, string> {
  const issues: ConfigValidationIssue[] = [];
  const values: Record<string, string> = {};

  for (const key of keys) {
    const value = source[key];

    if (!value || value.trim().length === 0) {
      issues.push({ key, message: 'is required' });
      continue;
    }

    values[key] = value;
  }

  if (issues.length > 0) {
    throw new ConfigValidationError(issues);
  }

  return values;
}
