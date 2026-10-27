export type Severity = 'Low' | 'Medium' | 'High' | 'Critical';
export type AlertStatus = 'New' | 'Acknowledged' | 'Investigating' | 'Resolved' | 'FalsePositive';
export type IncidentStatus = 'Open' | 'Investigating' | 'Contained' | 'Resolved' | 'Closed';
export interface Entity { id: string; createdAt: string; version: number }
export interface User { id: string; email: string; roles: string[] }
export interface Page<T> { items: T[]; total: number; pageNumber: number; pageSize: number }
export interface SecurityEvent extends Entity {
  occurredAt: string; source: string; type: string; device: string; account: string; ipAddress: string;
  attributesJson: string; synthetic: boolean; scenarioRunId: string | null;
}
export interface Alert extends Entity {
  ruleId: string; ruleVersion: number; title: string; device: string; account: string; severity: Severity;
  status: AlertStatus; ownerId: string | null; feedback: string | null; detectedAt: string; synthetic: boolean;
  scenarioRunId: string | null;
}
export interface Incident extends Entity {
  title: string; summary: string; severity: Severity; status: IncidentStatus; ownerId: string | null; resolvedAt: string | null;
}
export interface Timeline extends Entity { actorId: string; kind: string; text: string }
export interface Asset extends Entity {
  name: string; kind: string; owner: string; account: string; ipAddress: string; lastSeenAt: string | null;
  synthetic: boolean; isolated: boolean; accountLocked: boolean;
}
export interface Rule extends Entity {
  name: string; description: string; kind: 'Threshold' | 'FailureThenSuccess' | 'PrivilegeMisuse' | 'TelemetryLoss';
  eventType: string; threshold: number; windowSeconds: number; severity: Severity; enabled: boolean;
}
export interface Playbook extends Entity { name: string; description: string; action: string; enabled: boolean; trigger: string; minimumSeverity: Severity }
export interface PlaybookRun extends Entity {
  playbookId: string; incidentId: string; assetId: string; requestedBy: string; actionSnapshot: string; playbookVersion: number; approvedBy: string | null;
  status: string; result: string | null; attempts: number;
}
export interface ScenarioRun extends Entity {
  scenario: string; status: string; cursor: number; totalEvents: number; device: string; account: string; error: string | null;
}
export interface Analytics {
  days: number; eventCount: number; alertCount: number; openAlerts: number; activeIncidents: number; assetCount: number;
  enabledRules: number; meanResolutionMinutes: number | null; syntheticPrecision: number | null;
  syntheticAlerts: number; labeledBenignAlerts: number;
  eventTypes: {name: string; count: number}[]; severities: {name: string; count: number}[]; trend: {day: string; count: number}[];
}
export interface Platform {
  postgres: boolean; redis: boolean; broker: {pending: number; consumers: number; deadLetters: number} | null; brokerError: string | null;
  worker: {ready: boolean; heartbeat: {lastSeenAt: string; state: string} | null};
  outboxPending: number; outboxRetries: number; processedEvents: number; oldestPending: string | null;
}
export interface Audit extends Entity { actorId: string; action: string; resource: string; detail: string; traceId: string }
