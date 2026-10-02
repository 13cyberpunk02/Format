import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Drawing, UploadResult } from './models';
import {fileNameFrom} from '../../shared/files';

@Service()
export class DrawingsApi {
  private readonly http = inject(HttpClient);

  upload(file: File): Promise<UploadResult> {
    const body = new FormData();
    body.append('file', file, file.name);
    return firstValueFrom(this.http.post<UploadResult>('/api/drawings', body));
  }

  uploadPages(uploadId: string): Promise<Drawing[]> {
    return firstValueFrom(this.http.get<Drawing[]>(`/api/drawings/uploads/${uploadId}`));
  }

  file(drawingId: string): Promise<Blob> {
    return firstValueFrom(this.http.get(`/api/drawings/${drawingId}/file`, { responseType: 'blob' }));
  }

  /** Файл чертежа как данные в памяти - с токеном, через перехватчик. */
  async fetchFile(drawingId: string, download: boolean): Promise<{ blob: Blob; fileName: string }> {
    const response = await firstValueFrom(
      this.http.get(`/api/drawings/${drawingId}/file`, {
        params: { download },
        responseType: 'blob',
        observe: 'response',
      }),
    );

    return {
      blob: response.body!,
      fileName: fileNameFrom(response.headers.get('Content-Disposition')) ?? 'чертёж.pdf',
    };
  }
}
