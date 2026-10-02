import { PrinterState } from '../core/api/models';

export const PRINTER_STATE_LABELS: Record<PrinterState, string> = {
  Idle: 'готов',
  Printing: 'печатает',
  Stopped: 'остановлен',
  NotAccepting: 'не принимает задания',
  Unavailable: 'нет связи',
};

export function isPrinterProblem(state: PrinterState): boolean {
  return state === 'Stopped' || state === 'NotAccepting' || state === 'Unavailable';
}

export const PRINTER_STATE_ICONS: Record<PrinterState, string> = {
  Idle: 'circle-check',
  Printing: 'print',
  Stopped: 'triangle-exclamation',
  NotAccepting: 'ban',
  Unavailable: 'link-slash',
};

/** Свиток — рулонный плоттер, принтер — МФУ. */
export function printerKindIcon(key: 'plotter' | 'office'): string {
  return key === 'plotter' ? 'scroll' : 'print';
}
