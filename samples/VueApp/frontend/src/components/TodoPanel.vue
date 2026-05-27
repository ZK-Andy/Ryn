<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getTodos, addTodo, toggleTodo, removeTodo, type TodoItem } from '../ryn'

const todos = ref<TodoItem[]>([])
const doneCount = ref(0)
const newText = ref('')
const loading = ref(true)

async function sync(fn: () => Promise<{ items: TodoItem[], doneCount: number }>) {
  const result = await fn()
  todos.value = result.items
  doneCount.value = result.doneCount
}

onMounted(async () => {
  await sync(() => getTodos())
  loading.value = false
})

async function handleAdd() {
  const text = newText.value.trim()
  if (!text) return
  newText.value = ''
  await sync(() => addTodo(text))
}

async function handleToggle(id: number) {
  await sync(() => toggleTodo(id))
}

async function handleRemove(id: number) {
  await sync(() => removeTodo(id))
}
</script>

<template>
  <div class="panel">
    <h2>Todos</h2>
    <p class="subtitle">
      State lives in C# &mdash; Vue renders it. Every action round-trips through IPC.
    </p>

    <form class="add-form" @submit.prevent="handleAdd">
      <input
        v-model="newText"
        placeholder="What needs doing?"
        class="input"
        :disabled="loading"
      />
      <button type="submit" class="btn" :disabled="!newText.trim()">Add</button>
    </form>

    <div v-if="loading" class="status">Loading...</div>

    <TransitionGroup v-else name="list" tag="ul" class="todo-list">
      <li v-for="todo in todos" :key="todo.id" class="todo-item">
        <button
          class="check"
          :class="{ done: todo.done }"
          @click="handleToggle(todo.id)"
        >
          <span v-if="todo.done">&#10003;</span>
        </button>
        <span class="text" :class="{ done: todo.done }">{{ todo.text }}</span>
        <button class="remove" @click="handleRemove(todo.id)">&times;</button>
      </li>
    </TransitionGroup>

    <div v-if="!loading" class="footer">
      {{ doneCount }}/{{ todos.length }} done
    </div>
  </div>
</template>

<style scoped>
.panel { max-width: 540px; }

h2 { font-size: 1.4em; font-weight: 600; margin-bottom: 4px; }

.subtitle {
  color: #555;
  font-size: 13px;
  margin-bottom: 20px;
}

.add-form {
  display: flex;
  gap: 8px;
  margin-bottom: 16px;
}

.input {
  flex: 1;
  padding: 10px 14px;
  border: 1px solid #2a2a4a;
  border-radius: 8px;
  background: #111118;
  color: #e0e0e0;
  font-size: 14px;
  outline: none;
  transition: border-color 0.15s;
}

.input:focus { border-color: #7c3aed; }

.btn {
  padding: 10px 20px;
  border: none;
  border-radius: 8px;
  background: #7c3aed;
  color: #fff;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.15s;
}

.btn:hover { background: #6d28d9; }
.btn:disabled { opacity: 0.4; cursor: default; }

.todo-list {
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.todo-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  background: #111118;
  border: 1px solid #1e1e2e;
  border-radius: 8px;
  transition: all 0.2s;
}

.todo-item:hover { border-color: #2a2a4a; }

.check {
  width: 22px;
  height: 22px;
  border: 2px solid #3a3a5a;
  border-radius: 6px;
  background: transparent;
  color: #7c3aed;
  font-size: 14px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  transition: all 0.15s;
}

.check.done {
  background: #7c3aed22;
  border-color: #7c3aed;
}

.text {
  flex: 1;
  font-size: 14px;
  transition: all 0.15s;
}

.text.done {
  text-decoration: line-through;
  color: #555;
}

.remove {
  border: none;
  background: transparent;
  color: #555;
  font-size: 18px;
  cursor: pointer;
  padding: 0 4px;
  opacity: 0;
  transition: all 0.15s;
}

.todo-item:hover .remove { opacity: 1; }
.remove:hover { color: #ef4444; }

.footer {
  margin-top: 12px;
  text-align: right;
  color: #555;
  font-size: 13px;
}

.status {
  padding: 40px;
  text-align: center;
  color: #888;
}

.list-enter-active,
.list-leave-active {
  transition: all 0.2s ease;
}

.list-enter-from {
  opacity: 0;
  transform: translateY(-8px);
}

.list-leave-to {
  opacity: 0;
  transform: translateX(20px);
}
</style>
