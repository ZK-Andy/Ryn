<script setup lang="ts">
import { ref } from 'vue'
import { greet, fibonacci } from '../ryn'

const name = ref('World')
const greeting = ref('')

const fibN = ref(10)
const fibResult = ref<number | null>(null)
const fibTime = ref(0)

const rawCommand = ref('app.greet')
const rawArgs = ref('{ "name": "Ryn" }')
const rawResult = ref('')

async function handleGreet() {
  greeting.value = await greet(name.value)
}

async function handleFib() {
  const start = performance.now()
  fibResult.value = await fibonacci(fibN.value)
  fibTime.value = Math.round((performance.now() - start) * 100) / 100
}

async function handleRaw() {
  try {
    const args = JSON.parse(rawArgs.value)
    const result = await window.__ryn.invoke(rawCommand.value, args)
    rawResult.value = JSON.stringify(result, null, 2)
  } catch (e: any) {
    rawResult.value = `Error: ${e.message}`
  }
}
</script>

<template>
  <div class="panel">
    <h2>IPC Playground</h2>
    <p class="subtitle">Call C# methods directly from Vue and see the results.</p>

    <div class="sections">
      <section class="card">
        <h3>Greet</h3>
        <div class="row">
          <input v-model="name" class="input" placeholder="Name" @keyup.enter="handleGreet" />
          <button class="btn" @click="handleGreet">Send</button>
        </div>
        <div v-if="greeting" class="result">{{ greeting }}</div>
      </section>

      <section class="card">
        <h3>Fibonacci</h3>
        <p class="desc">Computed in C# on the backend. Try large numbers.</p>
        <div class="row">
          <input
            v-model.number="fibN"
            type="number"
            class="input narrow"
            min="0"
            max="92"
            @keyup.enter="handleFib"
          />
          <button class="btn" @click="handleFib">Compute</button>
        </div>
        <div v-if="fibResult !== null" class="result">
          fib({{ fibN }}) = <strong>{{ fibResult.toLocaleString() }}</strong>
          <span class="time">{{ fibTime }}ms round-trip</span>
        </div>
      </section>

      <section class="card">
        <h3>Raw IPC</h3>
        <p class="desc">Call any registered command with raw JSON args.</p>
        <div class="row">
          <input v-model="rawCommand" class="input" placeholder="command" />
        </div>
        <textarea v-model="rawArgs" class="textarea" rows="3" placeholder='{ "key": "value" }' />
        <button class="btn full" @click="handleRaw">Invoke</button>
        <pre v-if="rawResult" class="result pre">{{ rawResult }}</pre>
      </section>
    </div>
  </div>
</template>

<style scoped>
.panel { max-width: 540px; }

h2 { font-size: 1.4em; font-weight: 600; margin-bottom: 4px; }
h3 { font-size: 1.05em; font-weight: 600; color: #a78bfa; margin-bottom: 8px; }

.subtitle {
  color: #555;
  font-size: 13px;
  margin-bottom: 20px;
}

.desc {
  color: #555;
  font-size: 12px;
  margin-bottom: 10px;
}

.sections {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.card {
  background: #111118;
  border: 1px solid #1e1e2e;
  border-radius: 10px;
  padding: 18px;
}

.row {
  display: flex;
  gap: 8px;
  margin-bottom: 8px;
}

.input {
  flex: 1;
  padding: 8px 12px;
  border: 1px solid #2a2a4a;
  border-radius: 6px;
  background: #0a0a0f;
  color: #e0e0e0;
  font-size: 14px;
  outline: none;
}

.input:focus { border-color: #7c3aed; }
.input.narrow { max-width: 100px; }

.textarea {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid #2a2a4a;
  border-radius: 6px;
  background: #0a0a0f;
  color: #e0e0e0;
  font-family: monospace;
  font-size: 13px;
  outline: none;
  resize: vertical;
  margin-bottom: 8px;
}

.textarea:focus { border-color: #7c3aed; }

.btn {
  padding: 8px 16px;
  border: none;
  border-radius: 6px;
  background: #7c3aed;
  color: #fff;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  white-space: nowrap;
}

.btn:hover { background: #6d28d9; }
.btn.full { width: 100%; }

.result {
  margin-top: 8px;
  padding: 10px 12px;
  background: #0a0a0f;
  border: 1px solid #1e1e2e;
  border-radius: 6px;
  font-size: 14px;
  color: #ccc;
  line-height: 1.5;
}

.result.pre {
  font-family: monospace;
  font-size: 12px;
  white-space: pre-wrap;
  word-break: break-all;
}

.time {
  float: right;
  font-size: 12px;
  color: #555;
}
</style>
