const chatMessages = document.getElementById('chat-messages');
const characterSelect = document.getElementById('character-select');
const msgCount = document.getElementById('msg-count');
const scrollBtn = document.getElementById('scroll-bottom');
const statusDot = document.getElementById('status-indicator');
const statusText = document.getElementById('status-text');
const spList = document.getElementById('sp-list');
const spPrev = document.getElementById('sp-prev');
const spNext = document.getElementById('sp-next');
const spPageInfo = document.getElementById('sp-page-info');
const spPagination = document.getElementById('sp-pagination');
const taskContainer = document.getElementById('task-container');
const taskList = document.getElementById('task-list');
const taskPrev = document.getElementById('task-prev');
const taskNext = document.getElementById('task-next');
const taskPageInfo = document.getElementById('task-page-info');
const taskPagination = document.getElementById('task-pagination');
const conList = document.getElementById('con-list');
const conPrev = document.getElementById('con-prev');
const conNext = document.getElementById('con-next');
const conPageInfo = document.getElementById('con-page-info');
const conPagination = document.getElementById('con-pagination');

let ws = null;
let msgId = 0;
const pending = {};

let allMessages = [];
let uniqueSpeakers = new Set();
let selectedCharacter = '';
let loading = false;
let totalRefinements = 0;
let loadedCount = 0;

let spOffset = 0;
let spTotal = 0;
const PAGE_SIZE = 20;

let currentPanelTab = 'story_progress';
let taskStatus = 'in_progress';
let taskOffset = 0;
let taskTotal = 0;
let conOffset = 0;
let conTotal = 0;

function connect() {
    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    ws = new WebSocket(`${protocol}//${location.host}/ws/frontend`);

    ws.onopen = () => {
    };

    ws.onmessage = (event) => {
        const msg = JSON.parse(event.data);
        if (msg.type === 'status') {
            updateStatus(msg.backend_connected);
            if (msg.backend_connected) {
                if (allMessages.length === 0 && !loading) startLoading();
                if (spTotal === 0) loadStoryProgress(0);
                if (taskTotal === 0) loadTasks('in_progress', 0);
                if (conTotal === 0) loadConsistency(0);
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
        sendQuery(query, params, (msg) => resolve(msg));
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
            allMessages.push({speaker, content, isScene: true, sceneText: sceneMatch[1].trim()});
        } else {
            allMessages.push({speaker, content, isScene: false, sceneText: null});
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

async function loadStoryProgress(offset) {
    const batch = await queryAsync('list_story_progress', {limit: PAGE_SIZE, offset});
    if (!batch || batch.type === 'error') {
        spList.innerHTML = '<div class="chat-placeholder">加载失败</div>';
        return;
    }

    spOffset = offset;
    spTotal = batch.total || 0;
    const items = batch.data || [];

    spList.innerHTML = '';
    if (items.length === 0) {
        spList.innerHTML = '<div class="chat-placeholder">暂无故事进展</div>';
        spPagination.style.display = 'none';
        return;
    }

    for (let i = 0; i < items.length; i++) {
        const entry = document.createElement('div');
        entry.className = 'sp-entry';
        const idx = document.createElement('span');
        idx.className = 'sp-index';
        idx.textContent = (offset + i + 1) + '. ';
        const txt = document.createElement('span');
        txt.className = 'sp-text';
        txt.textContent = items[i].content || '';
        entry.appendChild(idx);
        entry.appendChild(txt);
        spList.appendChild(entry);
    }

    const totalPages = Math.ceil(spTotal / PAGE_SIZE) || 1;
    const currentPage = Math.floor(spOffset / PAGE_SIZE) + 1;
    spPageInfo.textContent = `第 ${currentPage}/${totalPages} 页`;
    spPrev.disabled = spOffset <= 0;
    spNext.disabled = spOffset + PAGE_SIZE >= spTotal;
    spPagination.style.display = 'flex';
}

function changeSpPage(direction) {
    const newOffset = spOffset + direction * PAGE_SIZE;
    if (newOffset < 0 || newOffset >= spTotal) return;
    loadStoryProgress(newOffset);
}

function switchPanelTab(tab) {
    currentPanelTab = tab;
    document.querySelectorAll('#panel-tabs .tab').forEach(t => t.classList.remove('active'));
    document.querySelectorAll('#panel-tabs .tab').forEach(t => {
        if (t.textContent.includes('故事进展') && tab === 'story_progress') t.classList.add('active');
        if (t.textContent.includes('任务') && tab === 'task') t.classList.add('active');
        if (t.textContent.includes('一致性表格') && tab === 'consistency') t.classList.add('active');
    });

    spList.style.display = tab === 'story_progress' ? '' : 'none';
    spPagination.style.display = (tab === 'story_progress' && spTotal > 0) ? 'flex' : 'none';
    taskContainer.style.display = tab === 'task' ? '' : 'none';
    conList.style.display = tab === 'consistency' ? '' : 'none';
    conPagination.style.display = (tab === 'consistency' && conTotal > 0) ? 'flex' : 'none';
}

function switchTaskTab(status) {
    taskStatus = status;
    document.querySelectorAll('#task-sub-tabs .sub-tab').forEach(t => t.classList.remove('active'));
    const labels = {in_progress: '进行中', complete: '已完成', fail: '已失败', discard: '已放弃'};
    document.querySelectorAll('#task-sub-tabs .sub-tab').forEach(t => {
        if (t.textContent === labels[status]) t.classList.add('active');
    });
    loadTasks(status, 0);
}

async function loadTasks(status, offset) {
    const batch = await queryAsync('list_tasks', {status, limit: PAGE_SIZE, offset});
    if (!batch || batch.type === 'error') {
        taskList.innerHTML = '<div class="chat-placeholder">加载失败</div>';
        return;
    }

    taskStatus = status;
    taskOffset = offset;
    taskTotal = batch.total || 0;
    const items = batch.data || [];

    taskList.innerHTML = '';
    if (items.length === 0) {
        taskList.innerHTML = '<div class="chat-placeholder">暂无任务</div>';
        taskPagination.style.display = 'none';
        return;
    }

    for (const item of items) {
        const entry = document.createElement('div');
        entry.className = 'task-entry';

        const summary = document.createElement('div');
        summary.className = 'task-summary';
        summary.textContent = item.summary || '';

        const detail = document.createElement('div');
        detail.className = 'task-detail';
        detail.textContent = item.detail || '';

        entry.appendChild(summary);
        entry.appendChild(detail);
        taskList.appendChild(entry);
    }

    const totalPages = Math.ceil(taskTotal / PAGE_SIZE) || 1;
    const currentPage = Math.floor(taskOffset / PAGE_SIZE) + 1;
    taskPageInfo.textContent = `第 ${currentPage}/${totalPages} 页`;
    taskPrev.disabled = taskOffset <= 0;
    taskNext.disabled = taskOffset + PAGE_SIZE >= taskTotal;
    taskPagination.style.display = 'flex';
}

function changeTaskPage(direction) {
    const newOffset = taskOffset + direction * PAGE_SIZE;
    if (newOffset < 0 || newOffset >= taskTotal) return;
    loadTasks(taskStatus, newOffset);
}

async function loadConsistency(offset) {
    const batch = await queryAsync('list_consistency', {limit: PAGE_SIZE, offset});
    if (!batch || batch.type === 'error') {
        conList.innerHTML = '<div class="chat-placeholder">加载失败</div>';
        return;
    }

    conOffset = offset;
    conTotal = batch.total || 0;
    const items = batch.data || [];

    conList.innerHTML = '';
    if (items.length === 0) {
        conList.innerHTML = '<div class="chat-placeholder">暂无条目</div>';
        conPagination.style.display = 'none';
        return;
    }

    for (const item of items) {
        const entry = document.createElement('div');
        entry.className = 'con-entry';

        const name = document.createElement('div');
        name.className = 'con-name';
        name.textContent = item.name || '';

        const detail = document.createElement('div');
        detail.className = 'con-detail';
        detail.textContent = item.detail || '';

        entry.appendChild(name);
        entry.appendChild(detail);
        conList.appendChild(entry);
    }

    const totalPages = Math.ceil(conTotal / PAGE_SIZE) || 1;
    const currentPage = Math.floor(conOffset / PAGE_SIZE) + 1;
    conPageInfo.textContent = `第 ${currentPage}/${totalPages} 页`;
    conPrev.disabled = conOffset <= 0;
    conNext.disabled = conOffset + PAGE_SIZE >= conTotal;
    conPagination.style.display = 'flex';
}

function changeConPage(direction) {
    const newOffset = conOffset + direction * PAGE_SIZE;
    if (newOffset < 0 || newOffset >= conTotal) return;
    loadConsistency(newOffset);
}

connect();
