import {WebSocketClient} from './ws-client.js';
import {DataStore} from './data-store.js';
import {ChatPanel} from './chat-panel.js';
import {DataPanel} from './data-panel.js';
import {qs} from './utils.js';

const statusDot = qs('#status-indicator');
const statusText = qs('#status-text');

function updateStatus(connected) {
    statusDot.className = 'status-dot ' + (connected ? 'connected' : 'disconnected');
    statusText.textContent = connected ? '后端已连接' : '后端未连接';
}

const store = new DataStore();
const chatPanel = new ChatPanel(store);
const dataPanel = new DataPanel(store);

const client = new WebSocketClient();

client.on('status', (msg) => updateStatus(!!msg.backend_connected));

client.on('data_update', (msg) => store.update(msg.data));

client.connect();
