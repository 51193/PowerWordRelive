const chatMessages = document.getElementById('chat-messages');
const characterSelect = document.getElementById('character-select');
const msgCount = document.getElementById('msg-count');
const scrollBtn = document.getElementById('scroll-bottom');
const statusDot = document.getElementById('status-indicator');
const statusText = document.getElementById('status-text');

let ws = null;
let msgId = 0;
const pending = {};

let allMessages = [];
let uniqueSpeakers = new Set();
let selectedCharacter = '';
let loading = false;
let totalRefinements = 0;
let loadedCount = 0;

function connect() {
    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    ws = new WebSocket(`${protocol}//${location.host}/ws/frontend`);

    ws.onopen = () => {
    };

    ws.onmessage = (event) => {
        const msg = JSON.parse(event.data);
        if (msg.type === 'status') {
            updateStatus(msg.backend_connected);
            if (msg.backend_connected && allMessages.length === 0 && !loading) {
                startLoading();
            }
            return;
        }
        const cb = pending[msg.id];
        if (cb) {
            delete pending[msg.id];
            cb(msg);
        }
    };

    ws.onclose = () => {
        updateStatus(false);
        setTimeout(connect, 2000);
    };

    ws.onerror = () => ws.close();
}

function updateStatus(connected) {
    if (connected) {
        statusDot.className = 'status-dot connected';
        statusText.textContent = '后端已连接';
    } else {
        statusDot.className = 'status-dot disconnected';
        statusText.textContent = '后端未连接';
    }
}

function sendQuery(query, params, callback) {
    if (!ws || ws.readyState !== WebSocket.OPEN) return;
    if (!stateBackendConnected()) {
        statusText.textContent = '后端未连接，无法查询';
        return;
    }
    const id = String(++msgId);
    pending[id] = callback;
    ws.send(JSON.stringify({type: 'query', id, query, params}));
}

function stateBackendConnected() {
    return statusDot.classList.contains('connected');
}

function queryAsync(query, params) {
    return new Promise((resolve) => {
        if (!stateBackendConnected()) {
            resolve(null);
            return;
        }
        sendQuery(query, params, (msg) => {
            resolve(msg);
        });
    });
}

async function startLoading() {
    if (loading) return;
    loading = true;
    allMessages = [];
    uniqueSpeakers = new Set();
    chatMessages.innerHTML = '<div class="chat-placeholder">加载中...</div>';

    const first = await queryAsync('list_refinements', {limit: 100, offset: 0});
    if (!first || first.type === 'error') {
        chatMessages.innerHTML = '<div class="chat-placeholder">加载失败</div>';
        loading = false;
        return;
    }

    const items = first.data || [];
    totalRefinements = first.total || 0;
    processItems(items);
    renderMessages();

    let offset = 100;
    while (offset < totalRefinements && stateBackendConnected()) {
        const batch = await queryAsync('list_refinements', {limit: 10000, offset});
        if (!batch || batch.type === 'error') break;
        const batchItems = batch.data || [];
        if (batchItems.length === 0) break;
        processItems(batchItems);
        renderMessages();
        offset += batchItems.length;
    }

    loading = false;
    updateMsgCount();
}

function processItems(items) {
    for (const item of items) {
        const speaker = item.speaker || '';
        const content = item.content || '';

        const sceneMatch = content.match(/^\[场景\](.+)/);
        if (sceneMatch) {
            allMessages.push({
                speaker,
                content,
                isScene: true,
                sceneText: sceneMatch[1].trim()
            });
        } else {
            allMessages.push({
                speaker,
                content,
                isScene: false,
                sceneText: null
            });
        }
        if (speaker) uniqueSpeakers.add(speaker);
    }
    loadedCount = allMessages.length;
    updateSpeakerDropdown();
}

function updateSpeakerDropdown() {
    const currentValue = characterSelect.value;
    characterSelect.innerHTML = '<option value="">全部角色</option>';
    const sorted = [...uniqueSpeakers].sort();
    for (const s of sorted) {
        const opt = document.createElement('option');
        opt.value = s;
        opt.textContent = s;
        characterSelect.appendChild(opt);
    }
    characterSelect.value = currentValue;
}

function updateMsgCount() {
    msgCount.textContent = loadedCount >= totalRefinements
        ? `${loadedCount} 条`
        : `${loadedCount} / ${totalRefinements} 条`;
}

function renderMessages() {
    const wasAtBottom = isAtBottom();
    chatMessages.innerHTML = '';

    if (allMessages.length === 0) {
        chatMessages.innerHTML = '<div class="chat-placeholder">暂无消息</div>';
        return;
    }

    for (const msg of allMessages) {
        if (msg.isScene) {
            const el = document.createElement('div');
            el.className = 'scene-msg';
            el.textContent = msg.sceneText;
            chatMessages.appendChild(el);
        } else {
            const isLeft = selectedCharacter && msg.speaker === selectedCharacter;
            const row = document.createElement('div');
            row.className = 'msg-row' + (isLeft ? ' msg-left' : ' msg-right');

            const bubble = document.createElement('div');
            bubble.className = 'msg-bubble' + (isLeft ? ' bubble-green' : ' bubble-white');

            const name = document.createElement('div');
            name.className = 'msg-name';
            name.textContent = msg.speaker;

            const text = document.createElement('div');
            text.className = 'msg-text';
            text.textContent = msg.content;

            bubble.appendChild(name);
            bubble.appendChild(text);
            row.appendChild(bubble);
            chatMessages.appendChild(row);
        }
    }

    if (wasAtBottom) scrollToBottomInstant();
    updateMsgCount();
}

function isAtBottom() {
    const el = chatMessages;
    return el.scrollHeight - el.scrollTop - el.clientHeight < 60;
}

function scrollToBottom() {
    chatMessages.scrollTo({top: chatMessages.scrollHeight, behavior: 'smooth'});
}

function scrollToBottomInstant() {
    chatMessages.scrollTop = chatMessages.scrollHeight;
}

chatMessages.addEventListener('scroll', () => {
    scrollBtn.classList.toggle('visible', !isAtBottom() && allMessages.length > 0);
});

characterSelect.addEventListener('change', () => {
    selectedCharacter = characterSelect.value;
    renderMessages();
    scrollToBottom();
});

connect();
