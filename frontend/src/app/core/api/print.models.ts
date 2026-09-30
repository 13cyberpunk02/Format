export interface PrintItemRequest {
  drawingId: string;
  copies: number;
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

export interface Sheet {
  index: number;
  kind: SheetKind;
  widthMm: number;
  lengthMm: number;
  placements: Placement[];
}

export interface OfficeJob {
  drawingId: string;
  fileName: string;
  pageNumber: number;
  format: string;
  copies: number;
}

export interface Preview {
  sheets: Sheet[];
  officeJobs: OfficeJob[];
  totalRollLengthMm: number;
}

export type OrderStatus = 'Queued' | 'Processing' | 'Printing' | 'Completed' | 'Failed' | 'Cancelled';
export type JobStatus = 'Pending' | 'Completed' | 'Failed' | 'Cancelled' | 'Replaced';

export interface OrderItem {
  drawingId: string;
  fileName: string;
  pageNumber: number;
  format: string;
  copies: number;
}

export interface OrderJob {
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
  startedAt: string | null;
  completedAt: string | null;
  error: string | null;
  items: OrderItem[];
  jobs: OrderJob[];
}
