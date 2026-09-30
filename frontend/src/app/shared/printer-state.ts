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
