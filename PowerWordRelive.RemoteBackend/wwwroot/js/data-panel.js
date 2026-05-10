import {clear, div, placeholder, qs, qsa, setActive, span} from './utils.js';

const TAG_LABELS = {world: '世界观', character: '人物', item: '物品', event: '事件', null: '内部追踪'};

export class DataPanel {
    constructor(store) {
        this._store = store;
        this._currentTab = 'story_progress';
        this._taskStatus = 'in_progress';
        this._kwTag = 'world';

        this._initTabs();
        store.onChange(() => this._renderCurrent());
    }

    _initTabs() {
        qsa('#panel-tabs .tab').forEach(tab => {
            tab.addEventListener('click', () => this._switchTab(tab.dataset.tab));
        });

        qsa('#task-sub-tabs .sub-tab').forEach(tab => {
            tab.addEventListener('click', () => this._switchTaskStatus(tab.dataset.status));
        });

        qsa('#kw-sub-tabs .sub-tab').forEach(tab => {
            tab.addEventListener('click', () => this._switchKwTag(tab.dataset.tag));
        });
    }

    _switchTab(tab) {
        this._currentTab = tab;
        setActive('#panel-tabs', '.tab', el => el.dataset.tab === tab);

        qs('#sp-list').style.display = tab === 'story_progress' ? '' : 'none';
        qs('#task-container').style.display = tab === 'task' ? '' : 'none';
        qs('#kw-container').style.display = tab === 'keyword_notes' ? '' : 'none';
        qs('#con-list').style.display = tab === 'consistency' ? '' : 'none';

        this._renderCurrent();
    }

    _switchTaskStatus(status) {
        this._taskStatus = status;
        setActive('#task-sub-tabs', '.sub-tab', el => el.dataset.status === status);
        this._renderTasks();
    }

    _switchKwTag(tag) {
        this._kwTag = tag;
        setActive('#kw-sub-tabs', '.sub-tab', el => el.dataset.tag === tag);
        this._renderKeywordNotes();
    }

    _renderCurrent() {
        switch (this._currentTab) {
            case 'story_progress':
                this._renderStoryProgress();
                break;
            case 'task':
                this._renderTasks();
                break;
            case 'keyword_notes':
                this._renderKeywordNotes();
                break;
            case 'consistency':
                this._renderConsistency();
                break;
        }
    }

    _renderStoryProgress() {
        const container = qs('#sp-list');
        const items = this._store.storyProgress;
        clear(container);

        if (items.length === 0) {
            placeholder(container, '暂无故事进展');
            return;
        }

        items.forEach((item, i) => {
            const entry = div('sp-entry');
            entry.appendChild(span('sp-index', `${i + 1}. `));
            entry.appendChild(span('sp-text', item.content || ''));
            container.appendChild(entry);
        });
    }

    _renderTasks() {
        const container = qs('#task-list');
        const items = this._store.tasks[this._taskStatus] || [];
        clear(container);

        if (items.length === 0) {
            placeholder(container, '暂无任务');
            return;
        }

        items.forEach(item => {
            const entry = div('task-entry');
            entry.appendChild(div('task-summary', item.summary || ''));
            entry.appendChild(div('task-detail', item.detail || ''));
            container.appendChild(entry);
        });
    }

    _renderConsistency() {
        const container = qs('#con-list');
        this._renderConsistencyItems(container, this._store.consistency);
    }

    _renderKeywordNotes() {
        const container = qs('#kw-list');
        const items = this._store.consistency.filter(item => item.tag === this._kwTag);
        this._renderConsistencyItems(container, items);
    }

    _renderConsistencyItems(container, items) {
        clear(container);

        if (items.length === 0) {
            placeholder(container, '暂无条目');
            return;
        }

        items.forEach(item => {
            const entry = div('con-entry');

            const nameRow = div('con-name-row');
            nameRow.appendChild(span('con-name', item.name || ''));

            const tag = item.tag || 'null';
            nameRow.appendChild(span('con-tag', TAG_LABELS[tag] || tag));

            entry.appendChild(nameRow);
            entry.appendChild(div('con-detail', item.detail || ''));
            container.appendChild(entry);
        });
    }
}
