import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TodoService, Todo } from './todo.service';

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  template: `
    <h1>Todos</h1>

    <form (ngSubmit)="addTodo()" class="add-form">
      <input
        [(ngModel)]="newTitle"
        name="newTitle"
        placeholder="New todo…"
        required
      />
      <button type="submit" [disabled]="!newTitle().trim()">Add</button>
    </form>

    @if (todos().length === 0) {
      <p class="empty">No todos yet.</p>
    }

    <ul>
      @for (todo of todos(); track todo.id) {
        <li [class.done]="todo.isCompleted">
          @if (editingId() === todo.id) {
            <input
              [(ngModel)]="editTitle"
              name="editTitle"
              class="edit-input"
            />
            <button (click)="saveEdit(todo)">Save</button>
            <button (click)="cancelEdit()">Cancel</button>
          } @else {
            <input
              type="checkbox"
              [checked]="todo.isCompleted"
              (change)="toggleComplete(todo)"
            />
            <span (dblclick)="startEdit(todo)">{{ todo.title }}</span>
            <button class="delete" aria-label="Delete todo" (click)="deleteTodo(todo.id)">✕</button>
          }
        </li>
      }
    </ul>
  `,
  styles: `
    :host {
      display: block;
      max-width: 480px;
      margin: 40px auto;
      font-family: 'Arial', sans-serif;
    }
    h1 { color: orange; margin-bottom: 20px; }
    .add-form { display: flex; gap: 8px; margin-bottom: 16px; }
    .add-form input { flex: 1; padding: 6px 10px; font-size: 1rem; }
    button { padding: 6px 12px; cursor: pointer; }
    ul { list-style: none; padding: 0; }
    li {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 4px;
      border-bottom: 1px solid #eee;
    }
    li span { flex: 1; cursor: pointer; }
    li.done span { text-decoration: line-through; color: #aaa; }
    .edit-input { flex: 1; padding: 4px 8px; font-size: 1rem; }
    .delete { margin-left: auto; color: #c00; background: none; border: none; font-size: 1rem; }
    .empty { color: #999; }
  `
})
export class App implements OnInit {
  private readonly todoService = inject(TodoService);

  todos = signal<Todo[]>([]);
  newTitle = signal('');
  editingId = signal<number | null>(null);
  editTitle = '';

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  private async load(): Promise<void> {
    this.todos.set(await this.todoService.getAll());
  }

  async addTodo(): Promise<void> {
    const title = this.newTitle().trim();
    if (!title) return;
    const todo = await this.todoService.create({ title, isCompleted: false });
    this.todos.update(list => [...list, todo]);
    this.newTitle.set('');
  }

  async toggleComplete(todo: Todo): Promise<void> {
    const updated = await this.todoService.update(todo.id, { title: todo.title, isCompleted: !todo.isCompleted });
    this.todos.update(list => list.map(t => t.id === updated.id ? updated : t));
  }

  startEdit(todo: Todo): void {
    this.editingId.set(todo.id);
    this.editTitle = todo.title;
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  async saveEdit(todo: Todo): Promise<void> {
    const title = this.editTitle.trim();
    if (!title) return;
    const updated = await this.todoService.update(todo.id, { title, isCompleted: todo.isCompleted });
    this.todos.update(list => list.map(t => t.id === updated.id ? updated : t));
    this.editingId.set(null);
  }

  async deleteTodo(id: number): Promise<void> {
    await this.todoService.delete(id);
    this.todos.update(list => list.filter(t => t.id !== id));
  }
}
