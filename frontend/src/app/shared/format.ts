const numberFormat = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 });

/** 12 979 364 → «12,4 МБ» */
export function formatBytes(bytes: number): string {
  return bytes < 1024 * 1024
    ? `${numberFormat.format(bytes / 1024)} КБ`
    : `${numberFormat.format(bytes / 1024 / 1024)} МБ`;
}

/** Русские окончания: plural(3, 'лист', 'листа', 'листов') → «листа» */
export function plural(count: number, one: string, few: string, many: string): string {
  const mod10 = count % 10;
  const mod100 = count % 100;
  if (mod10 === 1 && mod100 !== 11) return one;
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return few;
  return many;
}

const metersFormat = new Intl.NumberFormat('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** 4321 → «4,32 м» */
export function formatMeters(millimeters: number): string {
  return `${metersFormat.format(millimeters / 1000)} м`;
}

/** 1001 → «ПЧ-1001» */
export function orderLabel(number: number): string {
  return `ПЧ-${number}`;
}

const dateTimeFormat = new Intl.DateTimeFormat('ru-RU', {
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
});

/** «29 сент., 14:05» - в часовом поясе пользователя */
export function formatDateTime(iso: string): string {
  return dateTimeFormat.format(new Date(iso));
}
