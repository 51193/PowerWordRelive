import {clear, div, hashColor, isAtBottom, placeholder, qs, scrollToBottom} from './utils.js';

export class ChatPanel {
    constructor(store) {
        this._store = store;
        this._container = qs('#chat-messages');
        this._select = qs('#character-select');
        this._count = qs('#msg-count');
        this._scrollBtn = qs('#scroll-bottom');
        this._allMessages = [];
        this._selectedCharacter = '';

        this._select.addEventListener('change', () => {
            this._selectedCharacter = this._select.value;
            this._render();
        });

        this._container.addEventListener('scroll', () => {
            this._scrollBtn.classList.toggle('visible', !isAtBottom(this._container) && this._allMessages.length > 0);
        });

        this._scrollBtn.addEventListener('click', () => scrollToBottom(this._container));

        store.onChange(() => this._onDataUpdate());
    }

    _onDataUpdate() {
        const refs = this._store.refinements;
        this._allMessages = [];
        const speakers = new Set();

        for (const item of refs) {
            const speaker = item.speaker || '';
            const content = item.content || '';
            if (speaker === '[场景]') {
                this._allMessages.push({isScene: true, sceneText: content});
            } else {
                this._allMessages.push({speaker, content, isScene: false});
                if (speaker) speakers.add(speaker);
            }
        }

        this._updateDropdown(speakers);
        this._render();
    }

    _updateDropdown(speakers) {
        const current = this._select.value;
        this._select.innerHTML = '<option value="">全部角色</option>';
        [...speakers].sort().forEach(s => {
            const opt = document.createElement('option');
            opt.value = s;
            opt.textContent = s;
            this._select.appendChild(opt);
        });
        this._select.value = current;
    }

    _render() {
        const wasAtBottom = isAtBottom(this._container);
        clear(this._container);

        if (this._allMessages.length === 0) {
            placeholder(this._container, '暂无消息');
            this._count.textContent = '0 条';
            return;
        }

        for (const msg of this._allMessages) {
            if (msg.isScene) {
                const wrapper = div('scene-wrapper');
                wrapper.appendChild(div('scene-msg', msg.sceneText));
                this._container.appendChild(wrapper);
            } else {
                this._renderBubble(msg);
            }
        }

        if (wasAtBottom) scrollToBottom(this._container, true);
        this._count.textContent = `${this._allMessages.length} 条`;
    }

    _renderBubble(msg) {
        const isLeft = this._selectedCharacter && msg.speaker === this._selectedCharacter;
        const row = div('msg-row' + (isLeft ? ' msg-left' : ' msg-right'));

        const avatar = div('msg-avatar');
        avatar.textContent = msg.speaker.charAt(0);
        avatar.style.backgroundColor = hashColor(msg.speaker);

        const body = div('msg-body');
        body.appendChild(div('msg-name', msg.speaker));
        const bubble = div('msg-bubble' + (isLeft ? ' bubble-green' : ' bubble-white'));
        bubble.appendChild(div('msg-text', msg.content));
        body.appendChild(bubble);

        if (isLeft) {
            row.appendChild(avatar);
            row.appendChild(body);
        } else {
            row.appendChild(body);
            row.appendChild(avatar);
        }

        this._container.appendChild(row);
    }
}
