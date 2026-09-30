/** Сохранить данные из памяти браузера как файл. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/** Имя файла из заголовка Content-Disposition. Сначала пробуем filename* - он умеет кириллицу. */
export function fileNameFrom(header: string | null): string | null {
  const encoded = header?.match(/filename\*=UTF-8''([^;]+)/i);
  if (encoded) return decodeURIComponent(encoded[1]);

  const plain = header?.match(/filename="?([^";]+)"?/i);
  return plain ? plain[1] : null;
}
