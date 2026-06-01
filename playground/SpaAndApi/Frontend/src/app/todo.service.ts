import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface Todo {
  id: number;
  title: string;
  isCompleted: boolean;
}

export interface NewTodo {
  title: string;
  isCompleted: boolean;
}

@Injectable({ providedIn: 'root' })
export class TodoService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/todos';

  getAll(): Promise<Todo[]> {
    return firstValueFrom(this.http.get<Todo[]>(this.baseUrl));
  }

  create(todo: NewTodo): Promise<Todo> {
    return firstValueFrom(this.http.post<Todo>(this.baseUrl, todo));
  }

  update(id: number, todo: NewTodo): Promise<Todo> {
    return firstValueFrom(this.http.put<Todo>(`${this.baseUrl}/${id}`, todo));
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.baseUrl}/${id}`));
  }
}
