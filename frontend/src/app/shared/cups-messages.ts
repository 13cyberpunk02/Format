/** Самые частые сообщения CUPS и принтеров - по-русски. Незнакомые остаются как есть. */
const CUPS_MESSAGES: Record<string, string> = {
  'media-empty': 'нет бумаги',
  'media-needed': 'нужна бумага',
  'media-jam': 'замятие бумаги',
  'media-low': 'бумага заканчивается',
  'cover-open': 'открыта крышка',
  'door-open': 'открыта дверца',
  'marker-supply-empty': 'закончились чернила',
  'marker-supply-low': 'заканчиваются чернила',
  'printer-stopped': 'принтер остановлен',
  'paused': 'принтер на паузе',
  'offline': 'принтер недоступен',
  'connecting-to-device': 'подключение к принтеру',
  'job-incoming': 'передаётся на принтер',
  'job-queued': 'ждёт своей очереди',
  'job-printing': 'печатается',
};

export function translateCupsMessage(message: string): string {
  return message
    .split(',')
    .map((part) => {
      // «media-empty-error», «Media Empty» → «media-empty»
      const key = part.trim().toLowerCase().replace(/\s+/g, '-').replace(/-(error|warning|report)$/, '');
      return CUPS_MESSAGES[key] ?? part.trim();
    })
    .join(', ');
}
