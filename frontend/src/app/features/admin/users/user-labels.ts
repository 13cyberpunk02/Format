import { AccountStatus } from '../../../core/api/models';
import { UserRole } from '../../../core/auth/auth.models';

export const ROLE_LABELS: Record<UserRole, string> = {
  User: 'сотрудник',
  Admin: 'администратор',
};

export const ACCOUNT_STATUS_LABELS: Record<AccountStatus, string> = {
  PendingActivation: 'ожидает активации',
  Active: 'активен',
  Disabled: 'заблокирован',
};
