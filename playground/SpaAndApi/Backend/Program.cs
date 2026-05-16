var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello Todos!");

app.MapGet("/ping", () => Results.Ok());

var nextId = 0;
var newId = () => Interlocked.Increment(ref nextId);
var todos = new System.Collections.Concurrent.ConcurrentDictionary<int, Todo>();
var addTodo = (Todo todo) => todos.TryAdd(todo.Id, todo);
addTodo(new Todo(newId(), "Learn Aspire", false));
addTodo(new Todo(newId(), "Build awesome apps", false));
addTodo(new Todo(newId(), "Conquer the world", false));

app.MapGet("/todos", () => Results.Ok(todos.Values));

app.MapGet("/todos/{id}", (int id) =>
{
    return todos.TryGetValue(id, out var todo) ? Results.Ok(todo) : Results.NotFound();
});

app.MapPost("/todos", (NewTodo newTodo) =>
{
    var id = newId();
    var todo = new Todo(id, newTodo.Title, newTodo.IsCompleted);
    todos[id] = todo;
    return Results.Created($"/todos/{todo.Id}", todo);
});

app.MapPut("/todos/{id}", (int id, NewTodo updated) =>
{
    if (!todos.ContainsKey(id)) return Results.NotFound();
    var todo = new Todo(id, updated.Title, updated.IsCompleted);
    todos[id] = todo;
    return Results.Ok(todo);
});

app.MapDelete("/todos/{id}", (int id) =>
{
    return todos.TryRemove(id, out _) ? Results.NoContent() : Results.NotFound();
});

app.Run();

record NewTodo(string Title, bool IsCompleted);
record Todo(int Id, string Title, bool IsCompleted);