export class WebSocketClient {
    constructor() {
        this._ws = null;
        this._handlers = {};
    }

    on(type, handler) {
        if (!this._handlers[type]) this._handlers[type] = [];
        this._handlers[type].push(handler);
    }

    connect() {
        const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
        this._ws = new WebSocket(`${protocol}//${location.host}/ws/frontend`);

        this._ws.onmessage = (event) => {
            const msg = JSON.parse(event.data);
            const handlers = this._handlers[msg.type] || [];
            handlers.forEach(h => h(msg));
        };

        this._ws.onclose = () => {
            this._emit('status', {backend_connected: false});
            setTimeout(() => this.connect(), 2000);
        };

        this._ws.onerror = () => this._ws.close();
    }

    _emit(type, data) {
        const handlers = this._handlers[type] || [];
        handlers.forEach(h => h(data));
    }
}
