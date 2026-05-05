export interface ConfigValidationIssue {
  key: string;
  message: string;
}

export class ConfigValidationError extends Error {
  constructor(public readonly issues: ConfigValidationIssue[]) {
    super(
      `Configuration validation failed: ${issues
        .map((issue) => `${issue.key} ${issue.message}`)
        .join(', ')}`
    );
  }
}
