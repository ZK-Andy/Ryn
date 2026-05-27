export interface SystemInfo {
  machineName: string
  os: string
  runtime: string
  framework: string
  cpuCount: number
  memoryMb: number
}

export interface TodoItem {
  id: number
  text: string
  done: boolean
}

export interface TodoList {
  items: TodoItem[]
  doneCount: number
}

function ryn() {
  return window.__ryn
}

export async function greet(name: string): Promise<string> {
  return (await ryn().invoke('app.greet', { name })) as string
}

export async function getSystemInfo(): Promise<SystemInfo> {
  return (await ryn().invoke('app.systemInfo', {})) as SystemInfo
}

export async function getTodos(): Promise<TodoList> {
  return (await ryn().invoke('app.todos', {})) as TodoList
}

export async function addTodo(text: string): Promise<TodoList> {
  return (await ryn().invoke('app.addTodo', { text })) as TodoList
}

export async function toggleTodo(id: number): Promise<TodoList> {
  return (await ryn().invoke('app.toggleTodo', { id })) as TodoList
}

export async function removeTodo(id: number): Promise<TodoList> {
  return (await ryn().invoke('app.removeTodo', { id })) as TodoList
}

export async function fibonacci(n: number): Promise<number> {
  return (await ryn().invoke('app.fibonacci', { n })) as number
}
