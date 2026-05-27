<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getSystemInfo, type SystemInfo } from '../ryn'

const info = ref<SystemInfo | null>(null)
const error = ref('')
const loading = ref(true)

onMounted(async () => {
  try {
    info.value = await getSystemInfo()
  } catch (e: any) {
    error.value = e.message ?? 'Failed to fetch system info'
  } finally {
    loading.value = false
  }
})

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    info.value = await getSystemInfo()
  } catch (e: any) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="panel">
    <div class="panel-header">
      <h2>System Info</h2>
      <button class="btn-sm" @click="refresh" :disabled="loading">Refresh</button>
    </div>

    <div v-if="loading" class="status">Loading...</div>
    <div v-else-if="error" class="status error">{{ error }}</div>

    <div v-else-if="info" class="grid">
      <div class="card">
        <div class="card-label">Machine</div>
        <div class="card-value">{{ info.machineName }}</div>
      </div>
      <div class="card">
        <div class="card-label">OS</div>
        <div class="card-value small">{{ info.os }}</div>
      </div>
      <div class="card">
        <div class="card-label">Runtime</div>
        <div class="card-value small">{{ info.runtime }}</div>
      </div>
      <div class="card">
        <div class="card-label">Framework</div>
        <div class="card-value small">{{ info.framework }}</div>
      </div>
      <div class="card">
        <div class="card-label">CPU Cores</div>
        <div class="card-value">{{ info.cpuCount }}</div>
      </div>
      <div class="card">
        <div class="card-label">Memory (Working Set)</div>
        <div class="card-value">{{ info.memoryMb }} MB</div>
      </div>
    </div>

    <p class="hint">
      This data comes from C# via <code>[RynCommand]</code> &mdash;
      the Vue frontend calls <code>window.__ryn.invoke()</code> and
      gets typed JSON back from the .NET backend.
    </p>
  </div>
</template>

<style scoped>
.panel { max-width: 720px; }

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}

h2 { font-size: 1.4em; font-weight: 600; }

.btn-sm {
  padding: 6px 14px;
  border: 1px solid #2a2a4a;
  border-radius: 6px;
  background: #1a1a2e;
  color: #a78bfa;
  font-size: 13px;
  cursor: pointer;
}
.btn-sm:hover { background: #252545; }
.btn-sm:disabled { opacity: 0.5; cursor: default; }

.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 12px;
  margin-bottom: 20px;
}

.card {
  background: #111118;
  border: 1px solid #1e1e2e;
  border-radius: 10px;
  padding: 16px;
}

.card-label {
  font-size: 12px;
  color: #666;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 6px;
}

.card-value {
  font-size: 1.2em;
  font-weight: 600;
  color: #e0e0e0;
  word-break: break-all;
}

.card-value.small {
  font-size: 0.85em;
  font-weight: 400;
  color: #bbb;
  line-height: 1.4;
}

.status {
  padding: 40px;
  text-align: center;
  color: #888;
  font-size: 14px;
}

.status.error { color: #ef4444; }

.hint {
  color: #555;
  font-size: 13px;
  line-height: 1.6;
  margin-top: 16px;
}

code {
  background: #1a1a2e;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 12px;
  color: #a78bfa;
}
</style>
