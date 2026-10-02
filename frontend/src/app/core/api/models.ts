import {UserRole} from '../auth/auth.models';

/** Чертёж - как его описывает сервис хранения. */
export interface Drawing {
  id: string;
  uploadId: string;
  fileName: string;
  pageNumber: number;
  pageCount: number;
  format: string;
  widthMm: number;
  heightMm: number;
  sizeBytes: number;
  uploadedById: string;
  uploadedByName: string;
  uploadedAt: string;
}

export interface RejectedPage {
  pageNumber: number;
  reason: string;
}

export interface UploadResult {
  uploadId: string;
  drawings: Drawing[];
  rejectedPages: RejectedPage[];
}

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface FormatInfo {
  name: string;
  shortSide: number;
  longSide: number;
  printer: 'plotter' | 'office';
  isPrintable: boolean;
  reason: string | null;
}

export type SheetKind = 'Single' | 'Rotated' | 'DuplicatePair' | 'Nested';

export interface Placement {
  drawingId: string;
  fileName: string;
  pageNumber: number;
  format: string;
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface PreviewSheet {
  index: number;
  kind: SheetKind;
  widthMm: number;
  lengthMm: number;
  placements: Placement[];
}

export interface PreviewOfficeJob {
  drawingId: string;
  fileName: string;
  pageNumber: number;
  format: string;
  copies: number;
}

export interface PrintPreview {
  sheets: PreviewSheet[];
  officeJobs: PreviewOfficeJob[];
  totalRollLengthMm: number;
}

export interface PrintItemRequest {
  drawingId: string;
  copies: number;
}

export type OrderStatus = 'Queued' | 'Processing' | 'Printing' | 'Completed' | 'Failed' | 'Cancelled';

/** Заказ - пока только то, что нужно после создания. Полностью опишем на следующем шаге. */
export type JobStatus = 'Pending' | 'Completed' | 'Failed' | 'Cancelled' | 'Replaced';

export interface OrderItem {
  drawingId: string;
  fileName: string;
  pageNumber: number;
  format: string;
  copies: number;
}

export interface PrintJob {
  cupsJobId: number;
  printer: 'plotter' | 'office';
  description: string;
  copies: number;
  status: JobStatus;
  createdAt: string;
  completedAt: string | null;
  stateMessage: string | null;
  error: string | null;
}

export interface Order {
  id: string;
  number: number;
  title: string;
  status: OrderStatus;
  createdAt: string;
  createdById: string;
  createdByName: string;
  createdByDepartment: string;
  totalRollLengthMm: number;
  sheetCount: number;
  officeJobCount: number;
  punch: boolean;
  startedAt: string | null;
  completedAt: string | null;
  error: string | null;
  items: OrderItem[];
  jobs: PrintJob[];
}

export type PrinterState = 'Idle' | 'Printing' | 'Stopped' | 'NotAccepting' | 'Unavailable';

export interface PrinterStatus {
  key: 'plotter' | 'office';
  name: string;
  state: PrinterState;
  message: string | null;
  queuedJobs: number;
}

export interface PrintStatus {
  printers: PrinterStatus[];
  queue: { waiting: number; printing: number };
  pickup: { location: string; hours: string } | null;
  punchAvailable: boolean;
}

export interface PrintSummary {
  active: number;
  completedSince: number;
  rollMmSince: number;
}

export type AccountStatus = 'PendingActivation' | 'Active' | 'Disabled';

export interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  department: string;
  role: UserRole;
  status: AccountStatus;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface AdminQueue {
  active: Order[];
  failed: Order[];
}
