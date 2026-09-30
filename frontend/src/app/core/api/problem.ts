import { HttpErrorResponse } from '@angular/common/http';

/** Ошибка в формате Problem Details - так отвечают все наши сервисы. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
}

/** Понятный текст ошибки для пользователя. */
export function problemMessage(error: unknown, fallback = 'Что-то пошло не так. Попробуйте ещё раз.'): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallback;
  }

  if (error.status === 0) {
    return 'Нет связи с сервером. Проверьте сеть и попробуйте ещё раз.';
  }

  if (error.status === 429) {
    return 'Слишком много попыток. Подождите минуту и попробуйте снова.';
  }

  const problem = error.error as ProblemDetails | null;
  if (problem?.title) {
    return problem.detail ? `${problem.title} ${problem.detail}` : problem.title;
  }

  if (error.status >= 500) {
    return 'Сервис временно недоступен. Попробуйте позже.';
  }

  return fallback;
}
