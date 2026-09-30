/** Без похожих символов: нет l/1/I, O/0 - пароль будут диктовать и переписывать с бумажки. */
const ALPHABET = 'abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789';

export function generatePassword(length = 12): string {
  const random = crypto.getRandomValues(new Uint32Array(length));
  return Array.from(random, (value) => ALPHABET[value % ALPHABET.length]).join('');
}
